using System;
using HarmonyLib;
using ShippingBin.Core;

namespace ShippingBin
{
    // 仅在王尔德恩惠（Wild Network）正常购买出货箱，无免费首箱。
    //
    // 依据（IL 已证实）：
    //  * WildNetworkUI.OnUseClicked 是 private static：先校验 IsReady/余额，
    //    恩惠优先、现金补差，RunStats.OnWildUpgradeBought(cashPaid)，再 Unlock()。
    //    全程没有 try/catch，也没有放置失败的回滚。
    //  * NetworkUpgrade.Unlock() 先把 state 置 2，再调用我们注册的 onUnlock 回调。
    //  * PlayerStore.IsPropertyPaid 决定 Advanced 页是否可见。
    //
    // 因此：
    //  * 用 Harmony prefix 在扣款前预检背包空间；空间不足直接阻止购买（不扣钱）。
    //  * prefix 同时快照（现金/恩惠/两个统计），onUnlock 放置失败时做绝对值回滚，
    //    并把升级状态退回"可购买"、冷却清零。
    internal static class Purchase
    {
        private static bool snapshotValid;
        private static int oldCash;
        private static int oldFavor;
        private static int oldSpent;
        private static int oldBought;

        internal static void Register(string modId)
        {
            ModHelper.AddNetworkUpgrade(
                modId,
                ModIds.UpgradeId,
                Rules.PurchasePrice,
                Loc.Get("purchase_subtitle"),
                delegate { return Loc.Get("purchase_title"); },
                delegate { return Loc.Get("purchase_description"); },
                delegate { OnUnlocked(); },
                "Advanced",
                string.Empty,
                null,
                Rules.PurchaseCooldownDays,
                null,
                delegate { return Loc.Get("purchase_already_bought"); },
                false);
        }

        internal static void Patch(Harmony harmony)
        {
            // 目标方法全部用字面 typeof + 字面方法名，避免运行期反射扫描告警。
            System.Reflection.MethodInfo newClick = AccessTools.Method(typeof(WildNetworkUI), "OnUseClicked");
            if (newClick != null)
            {
                harmony.Patch(newClick, prefix: new HarmonyMethod(typeof(Purchase), "PrefixNew"));
            }
            else
            {
                ShippingBinMod.WriteLog("purchase patch target missing: WildNetworkUI.OnUseClicked");
            }

            System.Reflection.MethodInfo oldClick = AccessTools.Method(typeof(WildUIManager), "OnUnlockClicked");
            if (oldClick != null)
            {
                harmony.Patch(oldClick, prefix: new HarmonyMethod(typeof(Purchase), "PrefixOld"));
            }
            else
            {
                ShippingBinMod.WriteLog("purchase patch target missing: WildUIManager.OnUnlockClicked");
            }

            System.Reflection.MethodInfo hidden = AccessTools.Method(typeof(NetworkUpgrade), "IsHiddenForRun");
            if (hidden != null)
            {
                harmony.Patch(hidden, postfix: new HarmonyMethod(typeof(Purchase), "PostfixHidden"));
            }
            else
            {
                ShippingBinMod.WriteLog("purchase patch target missing: NetworkUpgrade.IsHiddenForRun");
            }
        }

        // WildNetworkUI.OnUseClicked 是静态方法，selectedId 也是静态字段。
        private static bool PrefixNew()
        {
            string id = (string)AccessTools.Field(typeof(WildNetworkUI), "selectedId").GetValue(null);
            return CheckBeforePurchase(id);
        }

        // 旧版 WildUIManager.OnUnlockClicked 是实例方法，字段为实例字段。
        private static bool PrefixOld(WildUIManager __instance)
        {
            string id = (string)AccessTools.Field(typeof(WildUIManager), "currentSelectedId").GetValue(__instance);
            return CheckBeforePurchase(id);
        }

        private static bool CheckBeforePurchase(string id)
        {
            if (id != ModIds.UpgradeId)
            {
                return true;
            }
            if (PlayerStore.Instance == null)
            {
                return true;
            }

            snapshotValid = false;
            GameItem probe = null;
            try
            {
                probe = ModHelper.SpawnItem(ModIds.ItemIdentifier);
                if (probe == null)
                {
                    return true;
                }
                bool room = BinWorld.HasRoomFor(probe);
                if (!room)
                {
                    BinItem.Feedback(Loc.Get("purchase_no_space"));
                    return false;
                }

                oldCash = PlayerStore.Instance.playerCash;
                oldFavor = PlayerStore.Instance.wildFavor;
                RunStats stats = RunStats.Current;
                oldSpent = stats == null ? 0 : stats.wildNetworkSpent;
                oldBought = stats == null ? 0 : stats.wildUpgradesBought;
                snapshotValid = true;
                return true;
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("purchase precheck exception: " + ex);
                return true;
            }
            finally
            {
                if (probe != null)
                {
                    try
                    {
                        probe.Destroy();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private static void OnUnlocked()
        {
            GameItem bin = null;
            bool placed = false;
            try
            {
                bin = ModHelper.SpawnItem(ModIds.ItemIdentifier);
                if (bin != null)
                {
                    ModHelper.SetItemOwnerShip(bin, true);
                    placed = BinWorld.TryPlaceIntoPlayerStorage(bin);
                }
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("purchase placement exception: " + ex);
                placed = false;
            }

            if (placed)
            {
                snapshotValid = false;
                BinItem.Feedback(Loc.Get("purchase_ok"));
                return;
            }

            if (bin != null && bin.parentInventory == null)
            {
                try
                {
                    bin.Destroy();
                }
                catch (Exception ex)
                {
                    ShippingBinMod.WriteLog("purchase cleanup exception: " + ex);
                }
            }

            Rollback();
            BinItem.Feedback(Loc.Get("purchase_failed"));
        }

        // 绝对值回滚：不依赖"恩惠优先/现金补差"的拆分细节，直接把三项余额
        // 与两个统计字段恢复到扣款前的值，再把升级退回可购买状态。
        private static void Rollback()
        {
            try
            {
                if (PlayerStore.Instance != null && snapshotValid)
                {
                    PlayerStore.Instance.playerCash = oldCash;
                    PlayerStore.Instance.wildFavor = oldFavor;
                    RunStats stats = RunStats.Current;
                    if (stats != null)
                    {
                        stats.wildNetworkSpent = oldSpent;
                        stats.wildUpgradesBought = oldBought;
                    }
                }
                NetworkUpgrade upgrade = ModHelper.GetNetworkUpgrade(ModIds.UpgradeId);
                if (upgrade != null)
                {
                    upgrade.state = 1;
                    upgrade.cooldownCurrent = 0;
                }
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("purchase rollback exception: " + ex);
            }
            snapshotValid = false;
        }

        // 默认"买店后才显示"由 tab=Advanced + 原版 IsHiddenForRun 保证；
        // 提前解锁开关只影响我们自己的入口。
        private static void PostfixHidden(string id, ref bool __result)
        {
            if (id != ModIds.UpgradeId)
            {
                return;
            }
            bool early = false;
            if (ShippingBinMod.SettingsRef != null && ShippingBinMod.SettingsRef.EarlyUnlock != null)
            {
                early = ShippingBinMod.SettingsRef.EarlyUnlock.Value;
            }
            bool propertyPaid = PlayerStore.Instance != null && PlayerStore.Instance.IsPropertyPaid;
            __result = !early && !propertyPaid;
        }
    }
}
