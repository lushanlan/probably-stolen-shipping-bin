// 升级进度展示状态（纯规则层，不引用任何游戏类型）。
// 只负责“该显示哪一行文案 + 需要哪些数字”，具体本地化在 src\Game\BinTooltip.cs。
using System;

namespace ShippingBin.Core
{
    /// <summary>tooltip 里“升级进度”这一行的三种形态。</summary>
    public enum UpgradeStatusKind
    {
        /// <summary>未满级、没有待扩容：显示累计点数、下一档阈值、当前实际容量。</summary>
        Progress = 0,

        /// <summary>已满级、没有待扩容：显示已满级与累计点数、当前实际容量。</summary>
        MaxLevel = 1,

        /// <summary>有待扩容（pending_shape）：必须同时说明“待扩容目标”和“当前实际容量”，不能只报目标。</summary>
        Pending = 2
    }

    /// <summary>tooltip 展示所需的全部数字；不包含任何本地化字符串。</summary>
    public struct UpgradeStatus
    {
        public UpgradeStatusKind Kind;
        public int Points;
        public int MaxPoints;
        /// <summary>下一档累计阈值；满级时 0。</summary>
        public int NextThreshold;
        /// <summary>实际容量是否可读取。false 时不得显示任何尺寸，改用“暂不可读取”文案。</summary>
        public bool ActualKnown;
        /// <summary>当前“实际已应用”的容量（来自容器网格，不是 pending 目标）。</summary>
        public int ActualWidth;
        public int ActualHeight;
        /// <summary>待扩容目标；无 pending 时为 0。</summary>
        public int PendingWidth;
        public int PendingHeight;

        public bool HasPending
        {
            get { return PendingWidth > 0 && PendingHeight > 0; }
        }
    }

    public static class UpgradeStatusLogic
    {
        /// <summary>
        /// 计算展示状态。points 允许超出上限（历史数据/异常），统一按满级处理。
        /// actual 尺寸非法（&lt;=0）时视为“不可读取”（ActualKnown=false）：不得用档位尺寸冒充实际容量。
        /// pending 只有宽高都 &gt;0 才算有效。
        /// </summary>
        public static UpgradeStatus Compute(int points, int actualWidth, int actualHeight,
                                           int pendingWidth, int pendingHeight)
        {
            if (points < 0) points = 0;

            UpgradeStatus status = new UpgradeStatus();
            status.Points = points;
            status.MaxPoints = Rules.MaxUpgradePoints;
            status.ActualKnown = actualWidth > 0 && actualHeight > 0;
            status.ActualWidth = status.ActualKnown ? actualWidth : 0;
            status.ActualHeight = status.ActualKnown ? actualHeight : 0;
            status.PendingWidth = pendingWidth > 0 ? pendingWidth : 0;
            status.PendingHeight = pendingHeight > 0 ? pendingHeight : 0;

            if (status.HasPending)
            {
                status.Kind = UpgradeStatusKind.Pending;
                status.NextThreshold = Rules.NextThresholdFor(points);
            }
            else if (points >= Rules.MaxUpgradePoints)
            {
                status.Kind = UpgradeStatusKind.MaxLevel;
                status.NextThreshold = 0;
            }
            else
            {
                status.Kind = UpgradeStatusKind.Progress;
                status.NextThreshold = Rules.NextThresholdFor(points);
            }
            return status;
        }

        /// <summary>
        /// 该状态应使用的本地化键（纯逻辑，便于单测；未知实际容量时用 *_unknown 变体）。
        /// </summary>
        public static string TooltipKey(UpgradeStatus status)
        {
            switch (status.Kind)
            {
                case UpgradeStatusKind.Pending:
                    return status.ActualKnown
                        ? "tooltip_upgrade_pending"
                        : "tooltip_upgrade_pending_unknown";
                case UpgradeStatusKind.MaxLevel:
                    return status.ActualKnown
                        ? "tooltip_upgrade_max"
                        : "tooltip_upgrade_max_unknown";
                default:
                    return status.ActualKnown
                        ? "tooltip_upgrade_progress"
                        : "tooltip_upgrade_progress_unknown";
            }
        }
    }
}
