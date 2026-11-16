using System;
using ShippingBin.Core;

namespace ShippingBin
{
    // 出货箱物品本身：工厂、容量、升级点数、原生"物品当目标"升级交互。
    //
    // 关键约束（均来自 IL 核证）：
    //  * 存档以"工厂重建同构空箱 + 按节点序号挂回子物品"的方式恢复，
    //    因此工厂里必须一次性建好 contentWindow / GameGridInventory 结构，
    //    运行期不得更换 contentWindow（否则读档会静默丢货）。
    //  * 不使用 ContainerHelper.InitContainerItemIncludeContainer：它的加入谓词
    //    要求 IsItemOwned(added)，会永久拒收赃物，与"赃物默认允许"冲突。
    //  * 升级只扩容量，不把材料 unitValue 累加到箱子价值（用户明确禁止）。
    internal static class BinItem
    {
        // ---- 工厂 ----------------------------------------------------------
        internal static GameItem Create()
        {
            // 返回 ValueTuple<PixelWindow, GameInventory>（IL 已证实），不要写成 Tuple。
            //
            // 工厂必须直接建出“能容纳旧存档任何合法坐标”的最大内容区（32x30）
            // ——这是读档安全的关键：SaveManager.DecodeNodesInternal 先用工厂重建物品，
            // 再按保存的节点序号把子物品 UncheckedAccept 挂回（不校验形状、不调用
            // onLoaded），最后才触发 onLoaded。若工厂只建初始 10x10，旧档 12x12 里
            // x/y >= 10 的物品在挂回瞬间就越界、之后也“看不见”。因此这里建 32x30，
            // 新建箱子随后由 PrepareFresh 收窄到 10x10；旧档则由 OnLoaded 保守处理。
            var window = DirectoryUtils.CreateInventoryWindow(Rules.MaxCapacityWidth, Rules.MaxCapacityHeight, true);

            GameGridInventory grid = window.Item2 as GameGridInventory;
            if (grid == null)
            {
                throw new InvalidOperationException("ShippingBin: grid container was not created.");
            }

            GameItem bin = ItemDirectory.CreateEmptyItem(ModIds.ItemIdentifier);
            bin.SetContentWindow(window.Item1);

            // 贴图：优先使用本 mod 自带 Textures/Items/<key>.png（由用户出图），
            // 找不到时回退原版容器贴图，保证不影响可玩性。
            if (!TryApplyModSprite(bin))
            {
                bin.SetSpriteAndShape(ModIds.BinAtlas, ModIds.BinSprite);
            }

            bin.name = Loc.Get("item_name");
            bin.shortDescription = Loc.Get("item_description");
            bin.SetValue(0);

            // 容器语义：只加 CONTAINER_TAG，不加 IMPORTANT_TAG（IMPORTANT 是原版
            // 机器/剧情物的通用标记，加上会被我们自己的保护规则拦掉）。
            bin.EnableTag(ModIds.TagContainer, false);

            bin.mayInventoryAddItemFunc = MayAcceptItem;
            bin.mayItemTargetThisFunc = CanUpgrade;
            bin.canItemTargetThisFunc = CanUpgrade;
            bin.onItemTargetThisFunc = DoUpgrade;
            bin.onLoaded = OnLoaded;

            // 新建箱子从初始档（原版“储藏区”10x10）开始；工厂建的 32x30 只是
            // 读档挂回的安全画布，不能留给新箱子当初始容量。
            PrepareFresh(bin);
            return bin;
        }

        // 新建箱子：收窄到初始档并写入“实际生效形状”记录（读档时据此恢复）。
        internal static void PrepareFresh(GameItem bin)
        {
            GameGridInventory grid = GridOf(bin);
            if (grid == null)
            {
                return;
            }
            SetShape(grid, bin, Rules.CapacityWidthFor(0), Rules.CapacityHeightFor(0));
            ClearPendingSize(bin);
        }

        // 使用本 mod 的 Textures/Items/<BinSpriteKey>.png（若已随包提供）。
        // 原版 ModLoader.LoadTexturesFromFolder 会把 Textures/Items 下的图片导入为
        // pixel-art sprite（文件名=key、点过滤、无 mipmap），SetSpriteAndShapeFromMod
        // 再按非透明像素推导占格形状（每格 UISettings.gridSize=16 像素，IL 已证实）。
        // 这里先确认 sprite 确实存在，避免原版在缺图时打印 warning 并回退 unknown。
        private static bool TryApplyModSprite(GameItem bin)
        {
            return TryApplyModSprite(bin, ModIds.BinSpriteKey);
        }

        private static bool TryApplyModSprite(GameItem bin, string spriteKey)
        {
            try
            {
                string modId = ShippingBinMod.ModId != null ? ShippingBinMod.ModId : ModIds.Namespace;
                System.Collections.Generic.List<LoadedMod> mods = ModLoader.Mods;
                if (mods == null)
                {
                    return false;
                }
                for (int i = 0; i < mods.Count; i++)
                {
                    LoadedMod mod = mods[i];
                    if (mod == null || mod.Manifest == null || mod.Manifest.ID != modId)
                    {
                        continue;
                    }
                    if (mod.Sprites == null || !mod.Sprites.ContainsKey(spriteKey))
                    {
                        return false;
                    }
                    bin.SetSpriteAndShapeFromMod(modId, spriteKey);
                    return true;
                }
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("mod sprite lookup failed, using vanilla sprite: " + ex);
            }
            return false;
        }

        // 读档后恢复贴图与“物品自身占格”。
        // 存档只序列化 spriteAtlasPath / spritePath，不保存 useSpriteFromMod
        // （SaveItemNode 字段已核证），所以必须重新走 SetSpriteAndShapeFromMod，
        // 否则读档后 ResolveSpriteByName 会走原版图集、显示 unknown。
        // 占格按“存档里保存的是哪个 key”决定：
        //   * v2  = 64x64 = 4x4 格（新箱子）；
        //   * legacy = 32x32 = 2x2 格（第 5 轮构建写进过存档的 key）。
        // 旧存档保持 2x2，避免把 4x4 贴图套到旧 2x2 物品上与周围物品重叠。
        private static void RestoreSprite(GameItem bin)
        {
            try
            {
                string modId = ShippingBinMod.ModId != null ? ShippingBinMod.ModId : ModIds.Namespace;
                bool fromMod = !string.IsNullOrEmpty(bin.spriteAtlasPath) && bin.spriteAtlasPath == modId;
                if (fromMod)
                {
                    string key = bin.spritePath == ModIds.LegacyBinSpriteKey
                        ? ModIds.LegacyBinSpriteKey
                        : ModIds.BinSpriteKey;
                    if (TryApplyModSprite(bin, key))
                    {
                        return;
                    }
                }
                // 无贴图信息或 mod 资源缺失：回退原版 2x2 容器贴图（不会与旧存档冲突）。
                bin.SetSpriteAndShape(ModIds.BinAtlas, ModIds.BinSprite);
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("bin load sprite restore failed: " + ex);
            }
        }

        // GameItem.IsDestroyed() 是抽象成员，真正实现只在 GameItemElement 上（IL 已证实）。
        internal static bool IsDestroyed(GameItem item)
        {
            if (item == null)
            {
                return true;
            }
            GameItemElement element = item as GameItemElement;
            return element != null && element.IsDestroyed();
        }

        // ---- 基础判定 ------------------------------------------------------
        internal static bool IsBin(GameItem item)
        {
            return item != null && item.identifier == ModIds.ItemIdentifier;
        }

        internal static GameGridInventory GridOf(GameItem bin)
        {
            if (bin == null || bin.contentWindow == null)
            {
                return null;
            }
            return bin.contentWindow.child as GameGridInventory;
        }

        // 允许把任何非空物品放进箱子：任务物/赃物/违禁品都只是"存放"，
        // 是否出售由夜间结算按规则决定；玩家可以先放进去再在睡前取回。
        private static bool MayAcceptItem(GameItem item, GameInventory inventory)
        {
            return item != null && inventory != null;
        }

        // ---- 升级点数（每箱独立，随物品存档） -------------------------------
        internal static int GetPoints(GameItem bin)
        {
            if (bin == null)
            {
                return 0;
            }
            TagState state = bin.GetTagReadonly(ModIds.TagUpgradePoints);
            return state == null ? 0 : state.GetInt();
        }

        internal static void SetPoints(GameItem bin, int points)
        {
            if (bin == null)
            {
                return;
            }
            int clamped = points < 0 ? 0 : (points > Rules.MaxUpgradePoints ? Rules.MaxUpgradePoints : points);
            bin.ModifyTag(ModIds.TagUpgradePoints, delegate(TagState s) { s.SetInt(clamped); }, false);
        }

        // 通用整型 tag 读写（调试赠送标记等需要独立于升级点数的标记）。
        internal static int GetIntTag(GameItem item, string tag)
        {
            if (item == null)
            {
                return 0;
            }
            TagState state = item.GetTagReadonly(tag);
            return state == null ? 0 : state.GetInt();
        }

        internal static void SetIntTag(GameItem item, string tag, int value)
        {
            if (item == null)
            {
                return;
            }
            item.ModifyTag(tag, delegate(TagState s) { s.SetInt(value); }, false);
        }

        internal static void ApplySize(GameItem bin)
        {
            ApplySize(bin, false);
        }

        // allowRelocation：升级路径传 true —— 形状"变宽变矮"时允许把箱内物品
        // 全部取出、按新形状重排后放回（失败完整回滚）；读档路径传 false，
        // 避免载入时静默改变玩家摆放，只做无风险的"真超集"扩容。
        internal static bool ApplySize(GameItem bin, bool allowRelocation)
        {
            GameGridInventory grid = GridOf(bin);
            if (grid == null)
            {
                return false;
            }
            int width = Rules.CapacityWidthFor(GetPoints(bin));
            int height = Rules.CapacityHeightFor(GetPoints(bin));

            int currentWidth;
            int currentHeight;
            if (!TryGetShapeSize(grid, out currentWidth, out currentHeight))
            {
                currentWidth = 0;
                currentHeight = 0;
            }

            ShapeAction action = ShapeMigration.Decide(currentWidth, currentHeight, width, height);
            if (action == ShapeAction.None)
            {
                return true;
            }
            if (action == ShapeAction.KeepCurrent)
            {
                return false;
            }
            if (action == ShapeAction.ApplyDirect)
            {
                SetShape(grid, bin, width, height);
                return true;
            }
            if (!allowRelocation)
            {
                return false;
            }
            return RelocateIntoShape(grid, bin, width, height, currentWidth, currentHeight);
        }

        private static void SetShape(GameGridInventory grid, GameItem bin, int width, int height)
        {
            grid.SetShape(width, height);
            grid.Validate();
            SetIntTag(bin, ModIds.TagShapeApplied, ShapeMigration.EncodeShape(width, height));
        }

        // 形状互有增减时的安全迁移：取出全部物品 -> 设新形状 -> 逐件放回。
        // 任一件放不回原位/新位则完整回滚到原形状并原样放回，绝不丢货。
        private static bool RelocateIntoShape(GameGridInventory grid, GameItem bin, int width, int height,
            int currentWidth, int currentHeight)
        {
            System.Collections.Generic.List<GameItem> items = new System.Collections.Generic.List<GameItem>();
            try
            {
                for (int i = 0; i < grid.childItems.Count; i++)
                {
                    items.Add(grid.childItems[i]);
                }
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("shape relocation aborted while listing items: " + ex.Message);
                return false;
            }

            System.Collections.Generic.List<GameItem> detached = new System.Collections.Generic.List<GameItem>();
            for (int i = 0; i < items.Count; i++)
            {
                GameItem item = items[i];
                bool ok = false;
                try
                {
                    ok = grid.Expel(item);
                }
                catch (Exception ex)
                {
                    ShippingBinMod.WriteLog("shape relocation detach failed: " + ex.Message);
                }
                if (!ok)
                {
                    // 已经取出的先放回原形状，原形状未变，放回是安全的。
                    for (int k = detached.Count - 1; k >= 0; k--)
                    {
                        TryAccept(grid, detached[k]);
                    }
                    return false;
                }
                detached.Add(item);
            }

            int placed = 0;
            try
            {
                SetShape(grid, bin, width, height);
                for (int i = 0; i < items.Count; i++)
                {
                    if (!TryAccept(grid, items[i]))
                    {
                        break;
                    }
                    placed++;
                }
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("shape relocation failed while growing: " + ex.Message);
                placed = -1;
            }

            if (placed == items.Count)
            {
                return true;
            }

            // 回滚：撤回已放回的新位置，恢复原形状，再把全部物品按原顺序放回。
            ShippingBinMod.WriteLog("shape relocation could not fit all items, rolling back to "
                + currentWidth + "x" + currentHeight);
            for (int i = 0; i < items.Count; i++)
            {
                try
                {
                    if (grid.childItems.Contains(items[i]))
                    {
                        grid.Expel(items[i]);
                    }
                }
                catch (Exception)
                {
                }
            }
            try
            {
                grid.SetShape(currentWidth, currentHeight);
                grid.Validate();
                SetIntTag(bin, ModIds.TagShapeApplied, ShapeMigration.EncodeShape(currentWidth, currentHeight));
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("shape relocation rollback of shape failed: " + ex.Message);
            }
            for (int i = 0; i < items.Count; i++)
            {
                TryAccept(grid, items[i]);
            }
            return false;
        }

        private static bool TryAccept(GameGridInventory grid, GameItem item)
        {
            if (item == null)
            {
                return true;
            }
            try
            {
                return GraphUtils.TryAcceptAll(grid, item, -1) > 0;
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("re-accept failed: " + ex.Message);
                return false;
            }
        }

        // 当前占格尺寸：GameGridInventory.Validate() 会把 widthPixels/heightPixels
        // 设为 inventoryShape.globalWidth/Height * UISettings.gridSize（IL 已证实），
        // GridShape 类型本身无法被 csc 引用（CS0570），因此用像素尺寸反推格子数，
        // 避免对“计算出来的类型”做反射（会被扫描器标记 reflection-external）。
        private static bool TryGetShapeSize(GameGridInventory grid, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (grid == null)
            {
                return false;
            }
            try
            {
                int gridSize = UISettings.current != null ? UISettings.current.gridSize : 0;
                int widthPixels = grid.widthPixels;
                int heightPixels = grid.heightPixels;
                if (gridSize <= 0 || widthPixels <= 0 || heightPixels <= 0)
                {
                    return false;
                }
                width = widthPixels / gridSize;
                height = heightPixels / gridSize;
                return width > 0 && height > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---- 待扩容标记（点数已到档、但箱内布局暂时放不下更大/不同形状） ----
        // 记为物品 tag（随存档持久化），不需要再消耗材料，开箱/睡觉等原生时机重试。
        internal static bool HasPendingSize(GameItem bin)
        {
            return GetIntTag(bin, ModIds.TagPendingShape) > 0;
        }

        internal static void SetPendingSize(GameItem bin, int width, int height)
        {
            int encoded = ShapeMigration.EncodeShape(width, height);
            if (encoded <= 0)
            {
                ClearPendingSize(bin);
                return;
            }
            SetIntTag(bin, ModIds.TagPendingShape, encoded);
        }

        internal static void ClearPendingSize(GameItem bin)
        {
            if (GetIntTag(bin, ModIds.TagPendingShape) != 0)
            {
                SetIntTag(bin, ModIds.TagPendingShape, 0);
            }
        }

        // 重试待扩容：直接做“取出全部 -> 设目标形状 -> 逐件放回”，失败完整回滚。
        // 成功才清除标记；失败保持原状并提示，玩家腾出空间后可再次触发。
        internal static bool TryApplyPendingSize(GameItem bin, bool notify)
        {
            if (bin == null)
            {
                return false;
            }
            int encoded = GetIntTag(bin, ModIds.TagPendingShape);
            if (encoded <= 0)
            {
                return true;
            }
            int width;
            int height;
            if (!ShapeMigration.TryDecodeShape(encoded, out width, out height))
            {
                ClearPendingSize(bin);
                return true;
            }
            GameGridInventory grid = GridOf(bin);
            if (grid == null)
            {
                return false;
            }
            int currentWidth;
            int currentHeight;
            if (!TryGetShapeSize(grid, out currentWidth, out currentHeight))
            {
                currentWidth = 0;
                currentHeight = 0;
            }
            if (currentWidth == width && currentHeight == height)
            {
                ClearPendingSize(bin);
                return true;
            }
            bool ok = RelocateIntoShape(grid, bin, width, height, currentWidth, currentHeight);
            if (ok)
            {
                ClearPendingSize(bin);
                if (notify)
                {
                    Feedback(Loc.Get("upgrade_pending_recovered", width, height));
                }
                return true;
            }
            if (notify)
            {
                Feedback(Loc.Get("upgrade_pending_space"));
            }
            return false;
        }

        private static void OnLoaded(GameItem bin)
        {
            // 读档时这里在子物品已按保存坐标挂回之后才被调用（IL 已证实），
            // 所以任何“缩小/换形状”都必须先确认安全，否则会越界丢物。
            try
            {
                GameGridInventory grid = GridOf(bin);
                if (grid == null)
                {
                    return;
                }

                // 先恢复贴图/自身占格（读档后 useSpriteFromMod 丢失，必须重设）。
                RestoreSprite(bin);

                int targetWidth = Rules.CapacityWidthFor(GetPoints(bin));
                int targetHeight = Rules.CapacityHeightFor(GetPoints(bin));

                int currentWidth;
                int currentHeight;
                if (!TryGetShapeSize(grid, out currentWidth, out currentHeight))
                {
                    currentWidth = 0;
                    currentHeight = 0;
                }

                // 新版本存档：恢复“上次实际应用的形状”（可能大于点数目标，表示待扩容）。
                int savedWidth;
                int savedHeight;
                if (ShapeMigration.TryDecodeShape(GetIntTag(bin, ModIds.TagShapeApplied), out savedWidth, out savedHeight))
                {
                    SetShape(grid, bin, savedWidth, savedHeight);
                    if (savedWidth == targetWidth && savedHeight == targetHeight)
                    {
                        ClearPendingSize(bin);
                    }
                    else
                    {
                        SetPendingSize(bin, targetWidth, targetHeight);
                    }
                    return;
                }

                // 旧档（没有形状记录）：工厂已建 32x30，保存坐标全部在界内，且此时
                // 子物品已经挂回。绝不直接缩小；需要迁移的写入待扩容，交给开箱/睡觉。
                LoadShapeAction action = ShapeMigration.DecideOnLoad(currentWidth, currentHeight,
                    targetWidth, targetHeight, false);
                if (action == LoadShapeAction.ApplyTarget)
                {
                    SetShape(grid, bin, targetWidth, targetHeight);
                    ClearPendingSize(bin);
                    return;
                }
                if (action == LoadShapeAction.None)
                {
                    ClearPendingSize(bin);
                    return;
                }
                SetIntTag(bin, ModIds.TagShapeApplied, ShapeMigration.EncodeShape(currentWidth, currentHeight));
                SetPendingSize(bin, targetWidth, targetHeight);
                Feedback(Loc.Get("upgrade_pending_load"));
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("bin load shape restore failed: " + ex);
            }
        }

        // ---- 原生"物品当目标"升级交互 -------------------------------------
        // 与原版瓶子打印机同形：材料拖到箱子上 -> 谓词允许 -> 回调消耗材料并加点。
        private static int MaterialPointsOf(GameItem item)
        {
            return item == null ? 0 : Rules.MaterialPoints(item.identifier);
        }

        internal static bool CanUpgrade(GameItem material, GameItem bin)
        {
            if (material == null || bin == null)
            {
                return false;
            }
            if (!IsBin(bin) || material == bin)
            {
                return false;
            }
            if (MaterialPointsOf(material) <= 0)
            {
                return false;
            }
            // 点数已满但仍有待扩容时仍然允许交互 —— 否则迁移失败后玩家永远无法重试。
            if (Rules.IsMaxedOut(GetPoints(bin)) && !HasPendingSize(bin))
            {
                return false;
            }
            return GeneralHelper.IsItemEffectivelyOwned(material)
                && GeneralHelper.IsItemEffectivelyOwned(bin);
        }

        internal static void DoUpgrade(GameItem material, GameItem bin)
        {
            if (material == null || bin == null || !IsBin(bin) || material == bin)
            {
                return;
            }

            // 上一次因箱内布局放不下而推迟的形状迁移：先做一次“不消耗材料”的重试。
            // 这样即使点数已经到 300、CanUpgrade 按满级拒绝，玩家拖材料也能触发恢复。
            if (HasPendingSize(bin))
            {
                if (!TryApplyPendingSize(bin, true))
                {
                    return;
                }
                if (Rules.IsMaxedOut(GetPoints(bin)))
                {
                    Feedback(Loc.Get("upgrade_max"));
                    return;
                }
            }

            if (!CanUpgrade(material, bin))
            {
                return;
            }

            int oldPoints = GetPoints(bin);
            int gained = MaterialPointsOf(material);
            UpgradeOutcome outcome = Rules.ApplyUpgradePoints(oldPoints, gained);
            if (!outcome.Accepted)
            {
                Feedback(Loc.Get("upgrade_max"));
                return;
            }

            SetPoints(bin, outcome.NewPoints);

            // 复用原版材料消耗（Tinkerer 20% 免耗时仍会加点，语义完全一致）。
            StartingPerkContent.ConsumeUpgradeMaterial(material);

            bool resized = ApplySize(bin, true);

            if (outcome.NewSize > outcome.OldSize)
            {
                if (resized)
                {
                    ClearPendingSize(bin);
                    Feedback(Loc.Get("upgrade_ok", outcome.NewWidth, outcome.NewHeight));
                }
                else
                {
                    // 点数已保留（材料已消耗），但当前布局放不下目标形状。
                    // 记录待扩容，开箱/睡觉/下次拖材料都会自动重试，不需要再消耗材料。
                    SetPendingSize(bin, outcome.NewWidth, outcome.NewHeight);
                    Feedback(Loc.Get("upgrade_pending_space"));
                }
            }
            if (outcome.Overflow > 0)
            {
                // 最后一次跨档后会继续吸收到满级上限，多出的点数明确告知玩家。
                Feedback(Loc.Get("upgrade_overflow", outcome.Overflow, Rules.MaxUpgradePoints));
            }
        }

        // ---- 玩家反馈 ------------------------------------------------------
        internal static string GetDisplayNameSafe(GameItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }
            try
            {
                return item.GetDisplayName(false);
            }
            catch (Exception)
            {
                return item.identifier == null ? string.Empty : item.identifier;
            }
        }

        // 原版没有通用 toast API（已核证），用夜间日志承载中文提示；
        // 同时写 mod 日志，便于验收时核对。
        internal static void Feedback(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            ShippingBinMod.WriteLog(text);
            try
            {
                if (PlayerStore.Instance != null)
                {
                    PlayerStore.Instance.AddNightLog(text, "#8fd3ff");
                }
            }
            catch (Exception)
            {
                // 反馈失败不影响升级/结算本身。
            }
        }
    }
}
