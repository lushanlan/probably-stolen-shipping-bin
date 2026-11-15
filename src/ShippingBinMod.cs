using System;
namespace ShippingBin
{
    public sealed class ShippingBinMod : IMod
    {
        internal const string ItemId = "shipping_bin_container";
        internal static ConfigEntry<float> SaleMultiplier, StolenMultiplier;
        internal static ConfigEntry<bool> AllowStolen, AllowContraband, HalfAt100, ExemptAt200, EarlyUnlock, DebugGrant;
        public void Init(ModManifest manifest)
        {
            var config = ModHelper.GetConfig(manifest.ID);
            SaleMultiplier = config.Define("sale_multiplier", 0.8f, "出货", () => "出售倍率", 0f, 10f, () => "按物品当前显示估值计算。", false);
            StolenMultiplier = config.Define("stolen_multiplier", 2f, "出货", () => "赃物涉案倍率", 0f, 10f, () => "以原版基础涉案金额为基准。", false);
            AllowStolen = config.Define("allow_stolen", true, "出货", () => "允许赃物", null, false);
            AllowContraband = config.Define("allow_contraband", true, "出货", () => "允许违禁品", null, false);
            HalfAt100 = config.Define("half_at_100", true, "出货", () => "黑市声望100减半", null, false);
            ExemptAt200 = config.Define("exempt_at_200", true, "出货", () => "黑市声望200豁免", null, false);
            EarlyUnlock = config.Define("early_unlock", false, "获取", () => "买店前开放购买", null, false);
            DebugGrant = config.Define("debug_grant", false, "调试", () => "新局赠送出货箱", null, false);
        }
        public void OnEnable() { ModHook.OnModItemDirectoryInit += RegisterItems; }
        public void OnDisable() { ModHook.OnModItemDirectoryInit -= RegisterItems; }
        private void RegisterItems(ModItemDirectory directory) { directory.Add(ItemId, CreateBin); }
        private GameItem CreateBin()
        {
            var window = DirectoryUtils.CreateInventoryWindow(6, 6, true);
            GameItem item = ItemDirectory.CreateEmptyItem(ItemId);
            item.SetContentWindow(window.Item1);
            item.SetSpriteAndShape("Items/items_container", "eleanor_box");
            item.name = "出货箱";
            item.shortDescription = "睡觉时出售箱内货物。";
            item.unitValue = 0;
            ContainerHelper.InitBackpackItem(window.Item2, item);
            return item;
        }
    }
}
