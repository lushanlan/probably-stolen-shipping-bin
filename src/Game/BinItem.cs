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
            var window = DirectoryUtils.CreateInventoryWindow(Rules.BaseCapacitySize, Rules.BaseCapacitySize, true);

            GameGridInventory grid = window.Item2 as GameGridInventory;
            if (grid == null)
            {
                throw new InvalidOperationException("ShippingBin: grid container was not created.");
            }

            GameItem bin = ItemDirectory.CreateEmptyItem(ModIds.ItemIdentifier);
            bin.SetContentWindow(window.Item1);
            bin.SetSpriteAndShape(ModIds.BinAtlas, ModIds.BinSprite);
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

            ApplySize(bin);
            return bin;
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
            GameGridInventory grid = GridOf(bin);
            if (grid == null)
            {
                return;
            }
            int size = Rules.CapacitySizeFor(GetPoints(bin));
            grid.SetShape(size, size);
            grid.Validate();
        }

        private static void OnLoaded(GameItem bin)
        {
            // 读档时这里已在子物品挂回之后被调用（IL 已证实），扩容是安全的。
            ApplySize(bin);
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
            if (Rules.IsMaxedOut(GetPoints(bin)))
            {
                return false;
            }
            return GeneralHelper.IsItemEffectivelyOwned(material)
                && GeneralHelper.IsItemEffectivelyOwned(bin);
        }

        internal static void DoUpgrade(GameItem material, GameItem bin)
        {
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

            ApplySize(bin);

            if (outcome.NewSize > outcome.OldSize)
            {
                Feedback(Loc.Get("upgrade_ok", outcome.NewSize));
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
