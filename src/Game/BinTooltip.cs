// 出货箱的动态 tooltip：追加“升级进度”一行（原版 OnCreateTooltipLate 钩子）。
//
// 原生机制（IL 已核证）：
//   GameItemElement.GetTooltipBasic() 依次调用 GeneralHelper.CreateGeneralItemTooltip、
//   各种 *Helper.CreateTooltip、ModInternal.CreateTooltipEarly/Late（= ModHook 事件），
//   最后才追加 item.shortDescription 与 flavorText。
//   tooltip 每次悬停都会重新构建，所以这里直接读物品当前 tag/网格即可“材料升级后立即更新”，
//   不需要缓存，也不需要改 shortDescription（原有说明保持不动）。
//
// 不误导原则：网格读不到时显示“当前容量暂不可读取”，绝不把按点数推算的档位尺寸
// 当成实际容量；pending 目标尺寸单独列出，不会被说成已生效。
using System;
using ShippingBin.Core;

namespace ShippingBin
{
    internal static class BinTooltip
    {
        // 与 ModHook.OnCreateTooltipLate 的 Action<RichTextBuilder, GameItem> 签名一致。
        internal static void Append(RichTextBuilder builder, GameItem item)
        {
            if (builder == null || item == null || !BinItem.IsBin(item))
            {
                return;
            }
            try
            {
                int points = BinItem.GetPoints(item);

                int actualWidth;
                int actualHeight;
                if (!BinItem.TryGetBinShapeSize(item, out actualWidth, out actualHeight))
                {
                    // 读不到就是读不到：传 0，让状态层标记 ActualKnown=false，
                    // 由本地化文案显示“当前容量暂不可读取”，而不是推算一个尺寸。
                    actualWidth = 0;
                    actualHeight = 0;
                }

                int pendingWidth = 0;
                int pendingHeight = 0;
                BinItem.GetPendingSize(item, out pendingWidth, out pendingHeight);

                UpgradeStatus status = UpgradeStatusLogic.Compute(
                    points, actualWidth, actualHeight, pendingWidth, pendingHeight);

                string key = UpgradeStatusLogic.TooltipKey(status);
                string line;
                switch (status.Kind)
                {
                    case UpgradeStatusKind.Pending:
                        // 必须同时给出“待扩容目标”和“当前实际容量”（或“暂不可读取”）。
                        line = Loc.Get(key,
                            status.Points,
                            status.MaxPoints,
                            status.PendingWidth,
                            status.PendingHeight,
                            status.ActualWidth,
                            status.ActualHeight);
                        break;
                    case UpgradeStatusKind.MaxLevel:
                        line = Loc.Get(key,
                            status.Points,
                            status.MaxPoints,
                            status.ActualWidth,
                            status.ActualHeight);
                        break;
                    default:
                        line = Loc.Get(key,
                            status.Points,
                            status.NextThreshold,
                            status.ActualWidth,
                            status.ActualHeight);
                        break;
                }
                if (!string.IsNullOrEmpty(line))
                {
                    builder.AddLine(line);
                }
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("bin tooltip append failed: " + ex);
            }
        }
    }
}
