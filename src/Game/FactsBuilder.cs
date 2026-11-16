using System;
using System.Collections.Generic;
using HarmonyLib;
using ShippingBin.Core;

namespace ShippingBin
{
    // 把游戏对象翻译成纯规则层可消费的 ItemFacts 树。
    //
    // 原则：
    //  * 只读取原版运行时标记/类型，不按物品名字猜任务物（剧情物例外：
    //    原版 GeneralHelper.importantStoryItemIds 就是按 identifier 的两条数据表）。
    //  * 整树估值直接复用原版 GetCurrentValue（含 IGNORE_CHILD_VALUE、锁后跳过、
    //    客户特征继承、液体容器价、商店事件），结算侧不再手工逐子累加。
    //  * 涉案构成按原版 SecData 的算法逐节点给出（GetNegociatedValue 为基数），
    //    由规则层决定倍率与减免，避免与销售回调重复犯罪。
    internal static class FactsBuilder
    {
        internal static ItemFacts Build(GameItem root)
        {
            if (root == null)
            {
                return null;
            }

            ItemFacts rootFacts = BuildNode(root);
            rootFacts.VanillaTreeValue = NativeTreeValue(root);
            // 假贴标按"销售单位"归属：原版 IsMislabelled 自身会检查整棵子树，
            // 所以只在待售根上判一次，避免容器与子物品重复累计（用户规格 5）。
            rootFacts.IsMislabelled = SafeMislabelled(root);

            HashSet<object> visited = new HashSet<object>();
            visited.Add(root);
            CollectChildren(root, rootFacts, visited);
            return rootFacts;
        }

        private static void CollectChildren(GraphNodeStorage node, ItemFacts parentFacts, HashSet<object> visited)
        {
            List<GraphNodeStorage> children = node.children;
            if (children == null)
            {
                return;
            }

            for (int i = 0; i < children.Count; i++)
            {
                GraphNodeStorage childNode = children[i];
                if (childNode == null || !visited.Add(childNode))
                {
                    continue;
                }

                GameItem childItem = childNode as GameItem;
                if (childItem != null)
                {
                    ItemFacts facts = BuildNode(childItem);
                    parentFacts.Children.Add(facts);
                    CollectChildren(childItem, facts, visited);
                }
                else
                {
                    // contentWindow / GameGridInventory 等中间节点对规则层透明，
                    // 其下的物品仍属于同一个父物品。
                    CollectChildren(childNode, parentFacts, visited);
                }
            }
        }

        internal static ItemFacts BuildNode(GameItem item)
        {
            ItemFacts f = new ItemFacts();
            f.Key = item;
            f.Identifier = item.identifier;
            f.DisplayName = SafeDisplayName(item);

            f.Owned = GeneralHelper.IsItemEffectivelyOwned(item);
            f.BehindLock = item.IsTag(ModIds.TagBehindLock) || item.IsTag(ModIds.TagLocked) || SafeCurrentlyLocked(item);
            f.IsShippingBin = BinItem.IsBin(item);
            f.IsAutomationHopper = IsAutomationHopper(item);
            f.IsQuestItem = IsQuestRelated(item);
            f.IsContainer = item.IsTag(ModIds.TagContainer) || item.IsGameItemType("CONTAINER");
            f.IsProtectedItem = IsProtected(item);
            f.IsStolen = item.IsTag(ModIds.TagStolen);
            f.StolenPercent = f.IsStolen ? SafePercent(item) : 0;
            f.IsContraband = ContrabandHelper.IsContraband(item);
            f.IgnoreChildValue = item.IsTag(ModIds.TagIgnoreChildValue);
            f.SelfValue = NativeSelfValue(item);

            f.CrimeContributions = BuildCrimeContributions(item, f.IsStolen, f.StolenPercent, f.IsContraband);
            return f;
        }

        private static HashSet<string> storyIds;

        // 原版唯一的“剧情/任务物”数据表：GeneralHelper.importantStoryItemIds
        // （private static HashSet<string>，IL 已证实）。运行时直接读这张表，
        // 而不是按物品名字猜；读不到时才退回 ModIds 里的同一份数据快照。
        internal static HashSet<string> VanillaStoryIds()
        {
            if (storyIds != null)
            {
                return storyIds;
            }

            HashSet<string> set = new HashSet<string>();
            try
            {
                System.Reflection.FieldInfo field = AccessTools.Field(typeof(GeneralHelper), "importantStoryItemIds");
                if (field != null)
                {
                    HashSet<string> live = field.GetValue(null) as HashSet<string>;
                    if (live != null)
                    {
                        foreach (string id in live)
                        {
                            if (!string.IsNullOrEmpty(id))
                            {
                                set.Add(id);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("story id table read failed: " + ex.Message);
            }

            if (set.Count == 0)
            {
                string[] fallback = ModIds.VanillaStoryItems;
                for (int i = 0; i < fallback.Length; i++)
                {
                    set.Add(fallback[i]);
                }
            }

            storyIds = set;
            return set;
        }

        private static bool IsVanillaStoryItem(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }
            return VanillaStoryIds().Contains(identifier);
        }

        // 保护规则（对应"任务/非玩家物品保护，IMPORTANT_TAG 不能一刀切"）。
        //
        // 原版能静态证实的"不可转售/任务相关"信号只有这些（见 docs/il/quest-protection.md）：
        //  1) GeneralHelper.importantStoryItemIds —— 剧情物数据表，运行时只有两条
        //     （luxury_supply_crate / self_refilling_neuroactive_perfume），
        //     由 GeneralHelper.LoadImportantStoryItemTag 映射成 IMPORTANT_TAG；
        //  2) 当前激活的委托单 StoreRequest.CanThisItemFillRequest(item) —— 原版自己
        //     用来判断"这件物品能否交任务"的谓词，命中即不得出售（运行时标记，不猜名字）；
        //  3) NOT_FOR_RESALE —— StoreClient.IsClientRefusingItem 的第一条判断，原版显式禁售；
        //  4) 危险废物 GeneralHelper.IsHazardousWaste —— 原版客户无条件拒收；
        //  5) 锁：LockHelper.IsCurrentlyLocked + ITEM_BEHIND_LOCK_TAG/LOCKED_TAG。
        //
        // 明确不做的两件事（用户 m01170 要求，有 IL 证据）：
        //  * 不把 IMPORTANT_TAG 当保护条件：112 处工厂都会写它（机器/家具/容器/文件/工具），
        //    PlayerStore 里只有 IsEligibleForRepo / GetRandomItem / tooltip 三处读取，
        //    没有"不能卖给客户"的判断；
        //  * 不按 MACHINE/DOCUMENT 类型禁售：原版 PlayerStore.SellItem 没有任何类型检查，
        //    MACHINE/DOCUMENT 只出现在"债务抵偿 Repo"的 IsEligibleForRepo 里，而 Repo 的
        //    唯一调用者是高利贷催收（StoreClientList.CreateLoanSharkCollect），不是销售通道。
        //    把装配设备一刀切拒售属于用户未授权的规则取舍。
        private static bool IsProtected(GameItem item)
        {
            if (item.IsTag(ModIds.TagNotForResale) || item.IsTag(ModIds.TagModuleStuck))
            {
                return true;
            }
            return SafeHazardousWaste(item);
        }

        // 自动化料斗保护（用户 m02433）：进料斗/出料斗是原版自动化传送链的两端，
        // 卖掉任意一端都会破坏玩家已有的自动化。IL 证据（docs/il/quest-protection.md §8）：
        // HopperHelper.CreateIntake() 的 identifier = "hopper_in"，打 HOPPER_INTAKE_TAG；
        // HopperHelper.CreateOutlet() 的 identifier = "hopper_out"，打 HOPPER_OUTLET_TAG；
        // HopperHelper.IsIntake/IsOutlet 仅按这两个 tag 判断。
        // 判定同时接受 tag 与 identifier，避免 tag 被其它模组剥离时漏保护。
        private static bool IsAutomationHopper(GameItem item)
        {
            if (item == null)
            {
                return false;
            }
            if (item.IsTag(ModIds.TagHopperIntake) || item.IsTag(ModIds.TagHopperOutlet))
            {
                return true;
            }
            string id = item.identifier;
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }
            string[] ids = ModIds.AutomationHopperIds;
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>剧情物或"当前激活委托单可交付物"。</summary>
        private static bool IsQuestRelated(GameItem item)
        {
            if (IsVanillaStoryItem(item.identifier))
            {
                return true;
            }
            try
            {
                PlayerStore store = PlayerStore.Instance;
                if (store == null || store.storeRequests == null)
                {
                    return false;
                }
                for (int i = 0; i < store.storeRequests.Count; i++)
                {
                    StoreRequest request = store.storeRequests[i];
                    if (request != null && request.CanThisItemFillRequest(item))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("store request check failed: " + ex.Message);
            }
            return false;
        }

        private static bool SafeCurrentlyLocked(GameItem item)
        {
            try
            {
                return LockHelper.IsCurrentlyLocked(item);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool SafeHazardousWaste(GameItem item)
        {
            try
            {
                return GeneralHelper.IsHazardousWaste(item);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string SafeDisplayName(GameItem item)
        {
            try
            {
                return item.GetDisplayName(false);
            }
            catch (Exception)
            {
                return item.identifier ?? "item";
            }
        }

        private static bool SafeMislabelled(GameItem item)
        {
            try
            {
                return VendingMachineHelper.IsMislabelled(item);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static int SafePercent(GameItem item)
        {
            TagState state = item.GetTagReadonly(ModIds.TagStolenPercent);
            if (state == null)
            {
                return 0;
            }
            int pct = state.GetInt();
            if (pct < 0) return 0;
            if (pct > 100) return 100;
            return pct;
        }

        // 单节点原版估值（不含子物品），用于规则层 legacy 递归路径；
        // 正常情况下根节点使用 VanillaTreeValue，逐子求和不会发生。
        private static long NativeSelfValue(GameItem item)
        {
            try
            {
                long value = item.GetCurrentValue(false, false, true, false, 0);
                long count = item.unitCount > 0 ? item.unitCount : 1;
                return checked(value * count);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        // 整树原版估值 → 容器按"一件物品"成交，数量只对根乘一次。
        private static long NativeTreeValue(GameItem root)
        {
            try
            {
                long value = root.GetCurrentValue(false, true, true, false, 0);
                long count = root.unitCount > 0 ? root.unitCount : 1;
                return checked(value * count);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static List<CrimeLine> BuildCrimeContributions(GameItem item, bool stolen, int percent, bool contraband)
        {
            List<CrimeLine> lines = new List<CrimeLine>();
            if (!stolen && !contraband)
            {
                return lines;
            }

            long rawNegociated = 0;
            try
            {
                rawNegociated = item.GetNegociatedValue();
            }
            catch (Exception)
            {
                rawNegociated = 0;
            }

            if (stolen && percent > 0 && rawNegociated > 0 && rawNegociated <= int.MaxValue)
            {
                int basis = RNG.MultiplyAndRound((int)rawNegociated, percent / 100.0, false);
                if (basis > 0)
                {
                    CrimeLine line = new CrimeLine();
                    line.Type = ModIds.CrimeFencing;
                    line.Basis = basis;
                    line.Kind = CrimeKind.Stolen;
                    lines.Add(line);
                }
            }

            if (contraband && rawNegociated > 0 && rawNegociated <= int.MaxValue)
            {
                CrimeLine line = new CrimeLine();
                line.Type = ContrabandType(item);
                line.Basis = rawNegociated;
                line.Kind = CrimeKind.Contraband;
                lines.Add(line);
            }
            return lines;
        }

        private static string ContrabandType(GameItem item)
        {
            if (item.IsGameItemType("SUBSTANCE"))
            {
                return ModIds.CrimeDrug;
            }
            if (item.IsGameItemType("WEAPON") || item.IsGameItemType("MELEE_WEAPON"))
            {
                return ModIds.CrimeWeapon;
            }
            if (item.IsGameItemType("ACCESS_CARD"))
            {
                return ModIds.CrimeCard;
            }
            return ModIds.CrimeContraband;
        }
    }
}
