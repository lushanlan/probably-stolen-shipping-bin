using System;
using System.Reflection;
using HarmonyLib;

namespace ShippingBin
{
    // 打开容器时重试“待扩容”的形状迁移。
    //
    // 时机依据（IL 已证实）：
    //  * 读档时 PlayerStore.LoadGame -> DecodeSaveItem 先按工厂结构建箱、挂回全部子物品，
    //    最后才调用 onLoaded；此时若直接缩小形状会让越界物品“看不见”。
    //  * 因此旧档/待扩容的箱子保留较大形状，推迟到玩家真正打开箱子时，
    //    在物品全部就位、界面尚未展示的空档做一次安全重排（失败完整回滚）。
    // ItemMouseDoubleClickHandler.OpenContentAction(GameItem, Vector2) 是 public 实例方法，
    // 原版点击/双击以及 OverlayHandler.ShowInventory 都走它。
    internal static class BinOpenHook
    {
        internal static void Patch(Harmony harmony)
        {
            if (harmony == null)
            {
                return;
            }
            try
            {
                MethodInfo target = AccessTools.Method(typeof(ItemMouseDoubleClickHandler), "OpenContentAction");
                MethodInfo prefix = AccessTools.Method(typeof(BinOpenHook), "PrefixOpen");
                if (target == null || prefix == null)
                {
                    ShippingBinMod.WriteLog("bin open hook target not found.");
                    return;
                }
                harmony.Patch(target, new HarmonyMethod(prefix));
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("bin open hook patch failed: " + ex);
            }
        }

        // 打开箱子前尝试把“点数已到档但布局放不下”的尺寸迁移落地。
        // 失败不影响打开（箱子继续以较大形状可用），只提示玩家腾出空间。
        private static void PrefixOpen(GameItem newItem)
        {
            try
            {
                if (newItem == null || !BinItem.IsBin(newItem) || !BinItem.HasPendingSize(newItem))
                {
                    return;
                }
                BinItem.TryApplyPendingSize(newItem, true);
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("bin pending size retry on open failed: " + ex);
            }
        }
    }
}
