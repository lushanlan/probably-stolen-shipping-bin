using System;
using System.Reflection;
using HarmonyLib;
using ShippingBin.Core;

namespace ShippingBin
{
    // 原版“材料拖到目标上”的判定链（IL 已证实，证据见 docs/il/capacity-and-config.md）：
    //   ItemMouseDragHandler.EndDrag
    //     IL_02fe: currentItem.MayTarget(lastItem)   IL_0311: currentItem.CanTarget(lastItem)
    //     IL_0329: ItemBehaviourManager.Target(source,target)   （只遍历目标身上的 ItemTargetBehaviour 组件）
    //     IL_033a/IL_034d: 再判一次 MayTarget/CanTarget
    //     IL_0360: GameItem.Target(target)
    //
    // GameItem.MayTarget/CanTarget 把源与目标两侧的四组委托做“第一个非 null 生效”的合并：
    //   source.mayItemTargetItemFunc / source.mayThisTargetItemFunc /
    //   target.mayItemTargetItemFunc / target.mayItemTargetThisFunc
    // 金属锭在 MaterialDirectory.MetalIngot() 里设置 source.mayThisTargetItemFunc = CanIngotTarget，
    // 后者是写死白名单（vending_machine_core / CAGE_TAG / 水箱 / RAS），对我们的出货箱返回显式 false，
    // 且“非 null 优先”使这个 false 压过目标侧（我们箱子）设置的 mayItemTargetThisFunc = CanUpgrade。
    // 结果：拖拽门在 GameItem.Target 之前就被否决，挂在箱子上的 onItemTargetThisFunc 永远不触发。
    //
    // 修复只补“门”：仍然走原版拖拽手势 -> GameItem.Target 的完整调用链。
    //
    // GameItem.Target 的副作用审计（IL 已证实，逐条核对过）：
    //   source.onItemTargetItemFunc        —— 金属锭未设置
    //   target.onItemTargetItemFunc        —— 我们的箱子未设置
    //   source.onThisTargetItemFunc        —— 金属锭 = MaterialDirectory.OnIngotTarget，
    //                                          白名单外全部直接 return，不会消耗材料
    //   target.onItemTargetThisFunc        —— 我们的箱子 = BinItem.DoUpgrade，只在这里消耗 1 份材料
    //   四个 slot 版本回调                    —— 金属锭只设置 may/canSelectSlotItemFunc，未设置 on* 版本
    //   最后播放 source.soundUse + 刷新库存 UI
    // 因此 EndDrag 的两次 MayTarget/CanTarget 不会导致重复升级（本补丁只改布尔结果），
    // GameItem.Target 也只调用一次 DoUpgrade，材料由 StartingPerkContent.ConsumeUpgradeMaterial
    // 在原版“每次 1 件、Tinkerer 20% 免耗但仍加点”的语义下消耗一次，不存在多耗或作用到别的物品。
    internal static class UpgradeDragPatch
    {
        private static bool logged;

        internal static void Patch(Harmony harmony)
        {
            MethodInfo mayTarget = AccessTools.Method(typeof(GameItem), "MayTarget");
            MethodInfo canTarget = AccessTools.Method(typeof(GameItem), "CanTarget");
            MethodInfo mayPostfix = AccessTools.Method(typeof(UpgradeDragPatch), "MayTargetPostfix");
            MethodInfo canPostfix = AccessTools.Method(typeof(UpgradeDragPatch), "CanTargetPostfix");

            if (mayTarget != null && mayPostfix != null)
            {
                harmony.Patch(mayTarget, null, new HarmonyMethod(mayPostfix));
            }
            if (canTarget != null && canPostfix != null)
            {
                harmony.Patch(canTarget, null, new HarmonyMethod(canPostfix));
            }
        }

        // 用 __0 而不是参数名，避免上游方法签名改参数名时补丁静默失效。
        private static void MayTargetPostfix(GameItem __instance, GameItem __0, ref bool __result)
        {
            ForceIfUpgrade(__instance, __0, ref __result, "MayTarget");
        }

        private static void CanTargetPostfix(GameItem __instance, GameItem __0, ref bool __result)
        {
            ForceIfUpgrade(__instance, __0, ref __result, "CanTarget");
        }

        private static void ForceIfUpgrade(GameItem source, GameItem target, ref bool result, string phase)
        {
            if (result || source == null || target == null)
            {
                return;
            }

            try
            {
                // 只对三种升级材料 + 本模组出货箱放行；其余组合保持原版判定。
                if (Rules.MaterialPoints(source.identifier) <= 0)
                {
                    return;
                }
                if (!BinItem.IsBin(target))
                {
                    return;
                }
                if (!BinItem.CanUpgrade(source, target))
                {
                    return;
                }

                result = true;
                if (!logged)
                {
                    logged = true;
                    ShippingBinMod.WriteLog(
                        "upgrade drag gate overridden (" + phase + "): "
                        + source.identifier + " -> " + target.identifier);
                }
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("upgrade drag patch failed: " + ex);
            }
        }
    }
}
