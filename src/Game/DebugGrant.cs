using System;
using System.Collections.Generic;
using ShippingBin.Core;

namespace ShippingBin
{
    // 调试模式赠送：默认关闭；开启后每个新开局赠送一个，且只赠送一次。
    //
    // 语义要求（用户规格）：
    //  * 不依赖 dev.txt，不受买店限制，也不改变正常购买限制。
    //  * 普通读档不重复赠送：pending 标记只在"新开局"时置位。
    //  * 空间不足时持久保留 pending 并在之后时机重试，绝不丢失、也绝不重复。
    //  * "本局已赠送"只认两种证据：done 标记，或世界里存在带赠送 token 的箱子。
    //    正常购买的箱子没有 token，因此不会把买来的箱子误判成已赠送。
    internal static class DebugGrant
    {
        internal static void OnNewGame(ModSettings settings)
        {
            if (!IsEnabled(settings))
            {
                return;
            }
            if (IsDone())
            {
                return;
            }
            if (!IsPending())
            {
                ModHelper.SetModDataBool(ModIds.Namespace, ModIds.DataPendingDebugGrant, true);
            }
            TryGrant(settings);
        }

        internal static void TryGrant(ModSettings settings)
        {
            if (settings == null || settings.DebugGrant == null)
            {
                return;
            }
            if (!settings.DebugGrant.Value)
            {
                // 开关关闭：保留 pending，等重新打开后再补领。
                return;
            }

            bool pending = IsPending();
            bool done = IsDone();
            bool hasTokenBin = BinWorld.FindDebugGrantBins().Count > 0;

            GrantAction action = GrantPolicy.Decide(true, done, pending, hasTokenBin);
            if (action == GrantAction.None)
            {
                return;
            }
            if (action == GrantAction.MarkDone)
            {
                MarkDone();
                return;
            }

            GameItem bin = null;
            try
            {
                bin = BinItem.Create();
                BinItem.SetIntTag(bin, ModIds.TagDebugGrantToken, 1);
                ModHelper.SetItemOwnerShip(bin, true);
                if (BinWorld.TryPlaceIntoPlayerStorage(bin))
                {
                    MarkDone();
                    BinItem.Feedback(Loc.Get("debug_grant_ok"));
                    return;
                }
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("debug grant exception: " + ex);
            }

            // 放置失败：销毁这个临时实例，保留 pending，等待下一次时机补领。
            if (bin != null && bin.parentInventory == null && !BinItem.IsDestroyed(bin))
            {
                try
                {
                    bin.Destroy();
                }
                catch (Exception ex)
                {
                    ShippingBinMod.WriteLog("debug grant cleanup exception: " + ex);
                }
            }
            BinItem.Feedback(Loc.Get("debug_grant_pending"));
        }

        private static bool IsEnabled(ModSettings settings)
        {
            return settings != null && settings.DebugGrant != null && settings.DebugGrant.Value;
        }

        private static bool IsPending()
        {
            return ModHelper.GetModDataBool(ModIds.Namespace, ModIds.DataPendingDebugGrant, false);
        }

        private static bool IsDone()
        {
            return ModHelper.GetModDataBool(ModIds.Namespace, ModIds.DataDebugGrantDone, false);
        }

        private static void MarkDone()
        {
            ModHelper.SetModDataBool(ModIds.Namespace, ModIds.DataDebugGrantDone, true);
            ModHelper.SetModDataBool(ModIds.Namespace, ModIds.DataPendingDebugGrant, false);
        }
    }
}
