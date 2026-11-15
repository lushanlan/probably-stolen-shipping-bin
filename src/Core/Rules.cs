using System;

namespace ShippingBin.Core
{
    // ---------------------------------------------------------------------
    // 数值与阈值集中说明
    //
    // 本文件是全部可调数值的唯一出处。游戏侧代码不得再出现裸数字。
    // 数值来源标注：
    //   [用户定案]  = 用户明确指定的数值/规则，不可随意改动。
    //   [开发提案]  = 开发阶段选取、缺少原版平衡证据的默认值，集中在此说明，
    //                 便于验收时调整，不影响规则结构。
    // ---------------------------------------------------------------------
    public static class Rules
    {
        // === 出售倍率 =====================================================
        // [用户定案] 默认 0.8；[开发提案] 允许范围 0.0 - 5.0。
        // 取值范围理由：0 表示货物仍会被清空但不产生收入（可用于纯清理用途）；
        // 上限 5.0 是开发选择的“合理范围”上界 —— 原版估值已经包含事件、
        // 天赋与商店加价修正，超过 5 倍的放大既会让单晚收入明显脱离正常
        // 交易量级，也会让赃物涉案金额（另一套独立倍率）显得毫无意义。
        // 设置界面需在描述文本中显示 [MinSaleMultiplier, MaxSaleMultiplier]。
        public const double DefaultSaleMultiplier = 0.8;
        public const double MinSaleMultiplier = 0.0;
        public const double MaxSaleMultiplier = 5.0;

        // === 赃物涉案倍率 =================================================
        // [用户定案] 默认 2，以“原版基础涉案金额”为基准；不乘出售倍率。
        // 原版 STOLEN_VALUE_INT 本身是百分比（原版算法的一部分），不是涉案金额。
        // [开发提案] 允许范围 0.0 - 10.0。
        public const double DefaultStolenMultiplier = 2.0;
        public const double MinStolenMultiplier = 0.0;
        public const double MaxStolenMultiplier = 10.0;

        // === 违禁品涉案 ===================================================
        // [用户定案] 违禁品不得无依据套用赃物公式：固定按原版基数 1:1 计，
        // 不提供面向用户的违禁品倍率设置（用户规格未要求该项）。
        // 赃物/违禁品仍是两个独立开关（用户规格 3）。
        /// <summary>违禁品涉案基数与原版 1:1（不乘任何倍率）。</summary>
        public const double ContrabandCrimeFactor = 1.0;

        // === 黑市声望门槛 =================================================
        // [用户定案] 固定门槛、不开放数值设置；两个开关默认开启。
        // 全免优先：先判 200 全免，再判 100 减半。
        // 关闭“200 全免”时，200 仍然落在 >=100 区间，因此仍可减半。
        public const int HalfReputationThreshold = 100;
        public const int ExemptReputationThreshold = 200;

        // === 假贴标处罚 ===================================================
        // [用户定案] 全晚假贴标货物“实际出售金额”的 1%，全晚统一合计、
        // 四舍五入（半数远离零）；当晚有假贴标销售时至少 1、最多 20。
        // 只扣黑市声望，不引入原版售货机的 FRAUD / ENDANGERMENT 后果。
        public const int MislabelPercent = 1;
        public const int MislabelPenaltyMin = 1;
        public const int MislabelPenaltyMax = 20;

        // === 购买 =========================================================
        // [开发提案] 价格 500，购买货币为 WildFavor（王尔德的恩惠）。
        // [用户定案] 每个游戏日最多购买一次，睡觉进入次日重置。
        public const int PurchasePrice = 500;
        public const int PurchaseCooldownDays = 1;

        // === 容量成长 =====================================================
        // [开发提案] 初始 6x6；累计 5 点 -> 8x8；15 点 -> 10x10；30 点 -> 12x12。
        // [用户定案] 满级容量必须“明显大于原版大型容器”。
        // 本机原版大型容器证据：BackpackLarge 8x9=72 格、BackpackLargeMilitary 7x11=77 格；
        // 满级 12x12=144 格，约为原版最大背包的 1.9 倍，满足“明显大于”。
        public const int BaseCapacitySize = 6;
        public const int MaxCapacitySize = 12;
        public static readonly int[] CapacityThresholds = new int[] { 0, 5, 15, 30 };
        public static readonly int[] CapacitySizes = new int[] { 6, 8, 10, 12 };
        public const int MaxUpgradePoints = 30;

        // [用户定案] 可替代升级点数：垃圾 1、废金属 2、金属锭 5。
        public const int JunkPoints = 1;
        public const int ScrapMetalPoints = 2;
        public const int MetalIngotPoints = 5;

        public const string MaterialJunk = "junk";
        public const string MaterialScrapMetal = "scrap_metal";
        public const string MaterialMetalIngot = "metal_ingot";

        // === 纯算术 =======================================================

        /// <summary>四舍五入，.5 一律远离零（用户为假贴标指定，全模组统一使用）。</summary>
        public static long RoundHalfAwayFromZero(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            return (long)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        /// <summary>出售金额 = 原版当前状态估值 * 倍率，四舍五入。非正值返回 0。</summary>
        public static long SaleAmount(long nativeValue, double multiplier)
        {
            if (nativeValue <= 0 || multiplier <= 0.0) return 0;
            return RoundHalfAwayFromZero(nativeValue * multiplier);
        }

        /// <summary>赃物涉案系数。全免优先；关闭全免时 200 仍可减半。</summary>
        public static double StolenFactor(int reputation, bool halfEnabled, bool exemptEnabled, double multiplier)
        {
            if (multiplier < 0.0) multiplier = 0.0;
            if (exemptEnabled && reputation >= ExemptReputationThreshold) return 0.0;
            if (halfEnabled && reputation >= HalfReputationThreshold) return multiplier * 0.5;
            return multiplier;
        }

        /// <summary>单个待售根的涉案金额 = 原版基础 * 系数，四舍五入。</summary>
        public static long CrimeAmount(long nativeBasis, int reputation, bool halfEnabled, bool exemptEnabled, double multiplier)
        {
            if (nativeBasis <= 0) return 0;
            double factor = StolenFactor(reputation, halfEnabled, exemptEnabled, multiplier);
            if (factor <= 0.0) return 0;
            return RoundHalfAwayFromZero(nativeBasis * factor);
        }

        /// <summary>
        /// 原版基础涉案金额的百分比部分：value * percent / 100。
        /// percent 来自原版 STOLEN_VALUE_INT（本机已确认为百分比）。
        /// 适配层仍应优先复用原版取整函数；此函数用于无游戏依赖的规则验证。
        /// </summary>
        public static long StolenBasis(long value, int percent)
        {
            if (value <= 0 || percent <= 0) return 0;
            return RoundHalfAwayFromZero(value * (percent / 100.0));
        }

        /// <summary>全晚假贴标处罚：合计金额的 1%，至少 1、最多 20；无假贴标销售为 0。</summary>
        public static int MislabelPenalty(long mislabelledProceeds, bool hasMislabelledSale)
        {
            if (!hasMislabelledSale) return 0;
            long penalty = RoundHalfAwayFromZero(mislabelledProceeds * (MislabelPercent / 100.0));
            if (penalty < MislabelPenaltyMin) penalty = MislabelPenaltyMin;
            if (penalty > MislabelPenaltyMax) penalty = MislabelPenaltyMax;
            return (int)penalty;
        }

        /// <summary>
        /// 按涉案类别取倍率。赃物倍率可配置（用户规格 4）；违禁品不套用赃物公式，
        /// 固定 1:1 使用原版基数（用户规格 4 的"违禁品不能无依据套赃物公式"），
        /// 因此这里不提供面向用户的违禁品倍率设置。
        /// </summary>
        public static double MultiplierFor(CrimeKind kind, Settings settings)
        {
            if (kind == CrimeKind.Stolen)
                return settings == null ? DefaultStolenMultiplier : settings.StolenMultiplier;
            return ContrabandCrimeFactor;
        }

        // === 容量与升级 ===================================================

        public static int CapacitySizeFor(int points)
        {
            if (points < 0) points = 0;
            int size = CapacitySizes[0];
            for (int i = 0; i < CapacityThresholds.Length; i++)
            {
                if (points >= CapacityThresholds[i]) size = CapacitySizes[i];
            }
            return size;
        }

        public static int CapacityLevelFor(int points)
        {
            if (points < 0) points = 0;
            int level = 0;
            for (int i = 0; i < CapacityThresholds.Length; i++)
            {
                if (points >= CapacityThresholds[i]) level = i;
            }
            return level;
        }

        public static bool IsMaxedOut(int points)
        {
            return points >= MaxUpgradePoints;
        }

        /// <summary>材料对应的升级点数；非升级材料返回 0。</summary>
        public static int MaterialPoints(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return 0;
            switch (identifier)
            {
                case MaterialJunk: return JunkPoints;
                case MaterialScrapMetal: return ScrapMetalPoints;
                case MaterialMetalIngot: return MetalIngotPoints;
                default: return 0;
            }
        }

        /// <summary>
        /// 应用一次材料投入。满级前允许最后一次跨档并吸收到上限，多余点数作为溢出返回，
        /// 由界面给中文提示；已满级时直接拒绝且不消耗材料。
        /// </summary>
        public static UpgradeOutcome ApplyUpgradePoints(int currentPoints, int materialPoints)
        {
            UpgradeOutcome outcome = new UpgradeOutcome();
            if (currentPoints < 0) currentPoints = 0;
            outcome.OldPoints = currentPoints;
            outcome.OldSize = CapacitySizeFor(currentPoints);
            outcome.NewPoints = currentPoints;
            outcome.NewSize = outcome.OldSize;
            outcome.AtMaxBefore = IsMaxedOut(currentPoints);
            outcome.Accepted = false;
            outcome.Overflow = 0;
            if (materialPoints <= 0 || outcome.AtMaxBefore) return outcome;

            outcome.Accepted = true;
            int raw = currentPoints + materialPoints;
            if (raw > MaxUpgradePoints)
            {
                outcome.NewPoints = MaxUpgradePoints;
                outcome.Overflow = raw - MaxUpgradePoints;
            }
            else
            {
                outcome.NewPoints = raw;
            }
            outcome.NewSize = CapacitySizeFor(outcome.NewPoints);
            return outcome;
        }

        // === 购买 =========================================================

        /// <summary>lastPurchaseDay 使用 -1 表示从未购买；同一天不可重复购买。</summary>
        public static bool CanPurchaseToday(int today, int lastPurchaseDay)
        {
            return lastPurchaseDay != today;
        }

        /// <summary>买店前解锁开关只影响本模组自有入口，不改变原版买店限制。</summary>
        public static bool ShouldShowPurchase(bool propertyPaid, bool earlyUnlockEnabled)
        {
            return propertyPaid || earlyUnlockEnabled;
        }
    }

    public sealed class UpgradeOutcome
    {
        public bool Accepted;
        public bool AtMaxBefore;
        public int OldPoints;
        public int NewPoints;
        public int OldSize;
        public int NewSize;
        public int Overflow;
    }

    /// <summary>纯设置快照：由游戏侧 ConfigEntry 读取后一次性拷贝，规则层不持有游戏对象。</summary>
    public sealed class Settings
    {
        public double SaleMultiplier = Core.Rules.DefaultSaleMultiplier;
        public double StolenMultiplier = Core.Rules.DefaultStolenMultiplier;

        public bool AllowStolen = true;
        public bool AllowContraband = true;
        public bool HalfCrimeAt100 = true;
        public bool ExemptCrimeAt200 = true;
        public bool EarlyUnlock = false;
        public bool DebugGrant = false;

        public Settings Clone()
        {
            Settings copy = new Settings();
            copy.SaleMultiplier = SaleMultiplier;
            copy.StolenMultiplier = StolenMultiplier;

            copy.AllowStolen = AllowStolen;
            copy.AllowContraband = AllowContraband;
            copy.HalfCrimeAt100 = HalfCrimeAt100;
            copy.ExemptCrimeAt200 = ExemptCrimeAt200;
            copy.EarlyUnlock = EarlyUnlock;
            copy.DebugGrant = DebugGrant;
            return copy;
        }
    }
}
