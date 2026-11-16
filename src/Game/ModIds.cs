namespace ShippingBin
{
    // ---------------------------------------------------------------------
    // 全部 ID / Tag / 配置键 / 存档键的唯一出处。
    // 全部使用独立的 "lushanlan.shipping_bin" 命名空间，避免与其他 mod 冲突。
    // ---------------------------------------------------------------------
    internal static class ModIds
    {
        // manifest 的 <ID>（Init 时以 manifest.ID 覆盖，防止用户改 ID 后失效）
        internal const string Namespace = "lushanlan.shipping_bin";

        // 物品与购买项
        internal const string ItemIdentifier = "lushanlan.shipping_bin.container";
        internal const string UpgradeId = "lushanlan.shipping_bin.purchase";

        // 每箱独立的升级点数 tag（随物品存档；默认 0 不写盘）
        internal const string TagUpgradePoints = "lushanlan.shipping_bin.upgrade_points";

        // 实际生效的网格形状（宽 * 1000 + 高），用于跨版本审计与"旧档不缩容"判断。
        // 注意：只写非 0 值；形状本身仍由 contentWindow 里的 GameGridInventory 承载。
        internal const string TagShapeApplied = "lushanlan.shipping_bin.shape_applied";

        // 待扩容标记：记录"点数已达某档，但当前箱内物品布局放不下目标形状"的目标尺寸
        // （编码同 TagShapeApplied）。在打开箱子/睡觉等安全时机自动重试，成功即清除；
        // 不需要玩家再拖一次材料，也不会因为点数已到 300 而被 CanUpgrade 永久拒绝。
        internal const string TagPendingShape = "lushanlan.shipping_bin.pending_shape";

        // 调试赠送专属标记：只有本模组"赠送"出来的箱子带这个 tag，
        // 用来区分正常购买的箱子，避免把买来的箱子误认为已赠送。
        internal const string TagDebugGrantToken = "lushanlan.shipping_bin.debug_grant_token";

        // 每存档 modData 键
        internal const string DataLastSettlementDay = "last_settlement_day";
        internal const string DataPendingDebugGrant = "debug_grant_pending";
        internal const string DataDebugGrantDone = "debug_grant_done";
        // 待提交记账单（涉案/假标签）；键值必须与 SettlementLedger.DataKey 一致
        internal const string DataPendingObligations = "pending_obligations";

        // 配置键前缀
        internal const string ConfigPrefix = "lushanlan.shipping_bin.";

        // 本地化键（物品/购买项）
        internal const string LocItemName = "item_name";
        internal const string LocItemDescription = "item_description";
        internal const string LocPurchaseTitle = "purchase_title";
        internal const string LocPurchaseDescription = "purchase_description";
        internal const string LocPurchaseSubtitle = "purchase_subtitle";
        internal const string LocPurchaseAlreadyBought = "purchase_already_bought";

        // 原生剧情物：原版 GeneralHelper.importantStoryItemIds 目前恰好只有这两个
        // （IL 已证实）。它们是原版运行时数据表，不是按名字猜任务物。
        internal static readonly string[] VanillaStoryItems = new string[]
        {
            "luxury_supply_crate",
            "self_refilling_neuroactive_perfume"
        };

        // 原版犯罪类型（SecData 常量值；IL 已证实）
        internal const string CrimeFencing = "FENCING";
        internal const string CrimeDrug = "DRUG_TRAFFICKING";
        internal const string CrimeWeapon = "WEAPON_TRAFFICKING";
        internal const string CrimeCard = "CARD_TRAFFICKING";
        internal const string CrimeContraband = "CONTRABAND_TRAFFICKING";

        // 原版相关 tag（只读使用，不新增私有语义）
        internal const string TagImportant = "IMPORTANT_TAG";
        internal const string TagContainer = "CONTAINER_TAG";
        internal const string TagBehindLock = "ITEM_BEHIND_LOCK_TAG";
        internal const string TagLocked = "LOCKED_TAG";
        internal const string TagNotForResale = "NOT_FOR_RESALE";
        internal const string TagStolen = "STOLEN_TAG";
        internal const string TagStolenPercent = "STOLEN_VALUE_INT";
        internal const string TagContraband = "CONTRABAND_ITEM_TAG";
        internal const string TagIgnoreChildValue = "IGNORE_CHILD_VALUE";
        internal const string TagModuleStuck = "MODULE_STUCK_TAG";

        // 图集与贴图：默认使用原版已存在的容器贴图（DLL 字符串证据），
        // 避免本 mod 不带 Textures 资源时显示 unknown 占位。
        internal const string BinAtlas = "Items/items_container";
        internal const string BinSprite = "eleanor_box";

        // 自定义贴图约定（美术由用户提供，本项目不生成资源）：
        //   文件：packaging\Textures\Items\<key>.png
        //   打包后：<mod>/Textures/Items/<key>.png
        // 物品工厂会先尝试 ModHelper 侧 sprite（SetSpriteAndShapeFromMod），
        // 找不到时回退到上面的原版贴图。
        //
        // 版本策略（关键兼容点）：
        //  * v2：64x64 = 4x4 格，新的箱子本体占格（用户 m02060 要求）。
        //  * legacy：32x32 = 2x2 格。第 5 轮构建曾经把 "shipping_bin" 这个 key
        //    写进过存档（spriteAtlasPath=modId, spritePath="shipping_bin"），
        //    读档时若把 4x4 贴图套回旧 2x2 物品，会在背包里与周围物品重叠；
        //    因此旧 key 必须保留为 2x2，读档时按保存的 key 恢复对应贴图。
        internal const string BinSpriteKey = "shipping_bin_v2";
        internal const string LegacyBinSpriteKey = "shipping_bin";
    }
}
