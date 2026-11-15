using ShippingBin.Core;

namespace ShippingBin
{
    // 所有配置项集中在这里声明与读取，其余文件只拿 Settings 快照。
    internal sealed class ModSettings
    {
        internal ConfigEntry<float> SaleMultiplier;
        internal ConfigEntry<float> StolenMultiplier;
        internal ConfigEntry<bool> AllowStolen;
        internal ConfigEntry<bool> AllowContraband;
        internal ConfigEntry<bool> HalfCrimeAt100;
        internal ConfigEntry<bool> ExemptCrimeAt200;
        internal ConfigEntry<bool> EarlyUnlock;
        internal ConfigEntry<bool> DebugGrant;

        internal void DefineAll(string modId)
        {
            ModConfig config = ModHelper.GetConfig(modId);

            SaleMultiplier = config.Define(
                ModIds.ConfigPrefix + "sale_multiplier",
                (float)Rules.DefaultSaleMultiplier,
                "出货箱",
                delegate { return Loc.Get("cfg_sale_multiplier"); },
                (float)Rules.MinSaleMultiplier,
                (float)Rules.MaxSaleMultiplier,
                delegate { return Loc.Get("cfg_sale_multiplier_tip"); },
                false);

            StolenMultiplier = config.Define(
                ModIds.ConfigPrefix + "stolen_multiplier",
                (float)Rules.DefaultStolenMultiplier,
                "涉案",
                delegate { return Loc.Get("cfg_stolen_multiplier"); },
                (float)Rules.MinStolenMultiplier,
                (float)Rules.MaxStolenMultiplier,
                delegate { return Loc.Get("cfg_stolen_multiplier_tip"); },
                false);


            AllowStolen = config.Define(
                ModIds.ConfigPrefix + "allow_stolen",
                true,
                "出货箱",
                delegate { return Loc.Get("cfg_allow_stolen"); },
                delegate { return Loc.Get("cfg_allow_stolen_tip"); },
                false);

            AllowContraband = config.Define(
                ModIds.ConfigPrefix + "allow_contraband",
                true,
                "出货箱",
                delegate { return Loc.Get("cfg_allow_contraband"); },
                delegate { return Loc.Get("cfg_allow_contraband_tip"); },
                false);

            HalfCrimeAt100 = config.Define(
                ModIds.ConfigPrefix + "half_at_100",
                true,
                "涉案",
                delegate { return Loc.Get("cfg_half_at_100"); },
                delegate { return Loc.Get("cfg_half_at_100_tip"); },
                false);

            ExemptCrimeAt200 = config.Define(
                ModIds.ConfigPrefix + "exempt_at_200",
                true,
                "涉案",
                delegate { return Loc.Get("cfg_exempt_at_200"); },
                delegate { return Loc.Get("cfg_exempt_at_200_tip"); },
                false);

            EarlyUnlock = config.Define(
                ModIds.ConfigPrefix + "early_unlock",
                false,
                "购买",
                delegate { return Loc.Get("cfg_early_unlock"); },
                delegate { return Loc.Get("cfg_early_unlock_tip"); },
                false);

            DebugGrant = config.Define(
                ModIds.ConfigPrefix + "debug_grant",
                false,
                "调试",
                delegate { return Loc.Get("cfg_debug_grant"); },
                delegate { return Loc.Get("cfg_debug_grant_tip"); },
                false);
        }

        internal Settings Snapshot()
        {
            Settings s = new Settings();
            s.SaleMultiplier = SaleMultiplier != null ? SaleMultiplier.Value : Rules.DefaultSaleMultiplier;
            s.StolenMultiplier = StolenMultiplier != null ? StolenMultiplier.Value : Rules.DefaultStolenMultiplier;
            s.AllowStolen = AllowStolen == null || AllowStolen.Value;
            s.AllowContraband = AllowContraband == null || AllowContraband.Value;
            s.HalfCrimeAt100 = HalfCrimeAt100 == null || HalfCrimeAt100.Value;
            s.ExemptCrimeAt200 = ExemptCrimeAt200 == null || ExemptCrimeAt200.Value;
            s.EarlyUnlock = EarlyUnlock != null && EarlyUnlock.Value;
            s.DebugGrant = DebugGrant != null && DebugGrant.Value;

            // 配置层兜底：即使配置文件被手工改成越界值，规则层也只接收合法范围。
            s.SaleMultiplier = Clamp(s.SaleMultiplier, Rules.MinSaleMultiplier, Rules.MaxSaleMultiplier);
            s.StolenMultiplier = Clamp(s.StolenMultiplier, Rules.MinStolenMultiplier, Rules.MaxStolenMultiplier);
            return s;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (double.IsNaN(value)) return min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
