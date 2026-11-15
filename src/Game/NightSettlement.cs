using System;
using System.Collections.Generic;
using ShippingBin.Core;

namespace ShippingBin
{
    // 夜间统一结算。
    //
    // 时序依据（IL 已证实）：
    //   EndNight(): FireOnGoingSleepEarly -> 服务扣款 -> ... -> ClearFrontInv ->
    //               FireOnGoingSleepLate -> BeginDay(): StartDay(dayCounter+1,
    //               NetworkUpgrade.OnDayStart) -> ... -> SaveGame()
    // 因此结算必须挂在 OnGoingSleepEarly：此时当夜所有货物都能被快照，
    // 全局黑市声望尚未被其他系统改动，且结果会随后被 SaveGame 写入存档。
    internal static class NightSettlement
    {
        private static bool running;

        internal static void Run(ModSettings settings)
        {
            if (running)
            {
                return;
            }
            PlayerStore store = PlayerStore.Instance;
            if (store == null || StoreStation.Instance == null)
            {
                return;
            }

            // 先补提交上一夜没落地完的记账义务（涉案/假标签）。若仍有遗留，
            // 本夜不结算：宁可晚一天卖，也不能覆盖未提交的记账单。
            if (!PendingLedger.Recover(store))
            {
                BinItem.Feedback(Loc.Get("log_ledger_pending_retry", StoreStation.Instance.dayCounter));
                return;
            }

            int day = StoreStation.Instance.dayCounter;
            int lastDay = ModHelper.GetModDataInt(ModIds.Namespace, ModIds.DataLastSettlementDay, -1);
            if (lastDay == day)
            {
                return;
            }

            running = true;
            try
            {
                BinWorld.RescueFrontCounterBins();
                BinWorld.WarnUnsavedLocations();

                List<GameItem> bins = BinWorld.FindAllBins();
                List<GameItem> ownedBins = new List<GameItem>();
                for (int i = 0; i < bins.Count; i++)
                {
                    if (GeneralHelper.IsItemEffectivelyOwned(bins[i]))
                    {
                        ownedBins.Add(bins[i]);
                    }
                }
                if (ownedBins.Count == 0)
                {
                    return;
                }

                Core.Settings snapshot = settings.Snapshot();
                int reputationSnapshot = StoreReputation.GetBMReputation();
                List<BinSnapshot> snapshots = BuildSnapshots(ownedBins);
                NightPlan plan = Planner.Build(snapshots, snapshot, reputationSnapshot);

                if (plan.AmountOverflow || plan.CrimeOverflow)
                {
                    BinItem.Feedback(Loc.Get("log_overflow_retry"));
                    ShippingBinMod.WriteLog("settlement skipped: numeric overflow");
                    return;
                }

                GameCommitWorld world = new GameCommitWorld(ownedBins, store, day);
                // 记账单只按"确实已付款"的条目生成，并按行持久化提交；
                // 没落地时不会写 day 标记，下一次睡觉/读档补提交。
                CommitResult result = Committer.Run(plan, world, day, new PendingLedger.Sink(store));

                if (!result.Success)
                {
                    WriteFailureReport(plan, result);
                    return;
                }

                WriteNightReport(plan, result);
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("settlement exception: " + ex);
                BinItem.Feedback(Loc.Get("log_commit_failed"));
            }
            finally
            {
                running = false;
            }
        }

        private static List<BinSnapshot> BuildSnapshots(List<GameItem> bins)
        {
            List<BinSnapshot> snapshots = new List<BinSnapshot>();
            HashSet<object> seenContents = new HashSet<object>();

            for (int b = 0; b < bins.Count; b++)
            {
                GameItem bin = bins[b];
                GameGridInventory grid = BinItem.GridOf(bin);
                if (grid == null)
                {
                    continue;
                }

                BinSnapshot snapshot = new BinSnapshot();
                snapshot.Bin = FactsBuilder.BuildNode(bin);

                List<GameItem> contents = new List<GameItem>(grid.childItems);
                for (int i = 0; i < contents.Count; i++)
                {
                    GameItem item = contents[i];
                    if (item == null || !seenContents.Add(item))
                    {
                        continue;
                    }
                    ItemFacts facts = FactsBuilder.Build(item);
                    if (facts != null)
                    {
                        snapshot.Contents.Add(facts);
                    }
                }
                snapshots.Add(snapshot);
            }
            return snapshots;
        }

        // 失败时如实报告：钱/货/存档标记各自处在什么状态，回滚是否真的完成。
        private static void WriteFailureReport(NightPlan plan, CommitResult result)
        {
            PlayerStore store = PlayerStore.Instance;
            string reason = string.IsNullOrEmpty(result.FailureReasonKey)
                ? Loc.Get("log_commit_failed")
                : Loc.Get(result.FailureReasonKey);
            store.AddNightLog(reason, "#ff9d9d");

            if (!string.IsNullOrEmpty(result.FailureItemName))
            {
                store.AddNightLog(Loc.Get("log_commit_item", result.FailureItemName), "#ff9d9d");
            }
            if (result.PaymentStarted)
            {
                store.AddNightLog(
                    Loc.Get("log_commit_partial", result.SoldCount, result.PaidAmount),
                    "#f0c86a");
            }
            if (result.RolledBack)
            {
                store.AddNightLog(Loc.Get("log_commit_rolled_back", plan.Sales.Count), "#9fe08f");
            }
            if (result.RollbackFailures.Count > 0)
            {
                string names = string.Empty;
                for (int i = 0; i < result.RollbackFailures.Count && i < 3; i++)
                {
                    if (names.Length > 0)
                    {
                        names += Loc.Get("log_list_separator");
                    }
                    names += result.RollbackFailures[i];
                }
                store.AddNightLog(
                    Loc.Get("log_commit_rollback_incomplete", result.RollbackFailures.Count, names),
                    "#ff6b6b");
            }
            if (!result.Settled)
            {
                store.AddNightLog(Loc.Get("log_commit_not_settled"), "#f0c86a");
            }

            for (int i = 0; i < result.Failures.Count; i++)
            {
                ShippingBinMod.WriteLog("settlement failure " + result.Failures[i]);
            }
        }

        private static void WriteNightReport(NightPlan plan, CommitResult result)
        {
            PlayerStore store = PlayerStore.Instance;

            store.AddNightLog(
                Loc.Get("log_sold", result.SoldCount, result.PaidAmount),
                "#9fe08f");

            if (plan.Retained.Count > 0)
            {
                store.AddNightLog(
                    Loc.Get("log_retained", plan.Retained.Count),
                    "#f0c86a");

                int shown = 0;
                for (int i = 0; i < plan.Retained.Count && shown < 3; i++)
                {
                    RetainedEntry entry = plan.Retained[i];
                    string name = entry.Root == null || entry.Root.DisplayName == null
                        ? string.Empty
                        : entry.Root.DisplayName;
                    string reason = entry.Reason == null ? string.Empty : Loc.Get(entry.Reason.ReasonKey);
                    store.AddNightLog(Loc.Get("log_retained_reason", name, reason), "#f0c86a");
                    shown++;
                }
            }

            if (plan.TotalCrime > 0)
            {
                string parts = BuildCrimeText(plan.Crimes);
                store.AddNightLog(Loc.Get("log_crime", parts), "#ff9d9d");
            }

            if (plan.MislabelPenalty > 0)
            {
                store.AddNightLog(
                    Loc.Get("log_mislabel", plan.MislabelPenalty, plan.MislabelledAmount),
                    "#ff9d9d");
            }
        }

        private static string BuildCrimeText(List<CrimeLine> crimes)
        {
            string parts = string.Empty;
            for (int i = 0; i < crimes.Count; i++)
            {
                CrimeLine line = crimes[i];
                string type = Loc.Get("crime_" + line.Type.ToLowerInvariant());
                if (parts.Length > 0)
                {
                    parts += Loc.Get("log_list_separator");
                }
                parts += type + " " + line.Amount;
            }
            return parts;
        }

        // 把规则层事务映射到真实游戏副作用。
        private sealed class GameCommitWorld : ICommitWorld
        {
            private readonly List<GameItem> bins;
            private readonly PlayerStore store;
            private readonly int day;

            internal GameCommitWorld(List<GameItem> bins, PlayerStore store, int day)
            {
                this.bins = bins;
                this.store = store;
                this.day = day;
            }

            private GameItem BinOf(SaleEntry entry)
            {
                ItemFacts binFacts = entry.Bin as ItemFacts;
                if (binFacts == null)
                {
                    return null;
                }
                return binFacts.Key as GameItem;
            }

            private GameItem RootOf(SaleEntry entry)
            {
                return entry.Root == null ? null : entry.Root.Key as GameItem;
            }

            public bool Revalidate(SaleEntry entry)
            {
                GameItem root = RootOf(entry);
                GameItem bin = BinOf(entry);
                if (root == null || bin == null || BinItem.IsDestroyed(root))
                {
                    return false;
                }
                GameGridInventory grid = BinItem.GridOf(bin);
                if (grid == null || !object.ReferenceEquals(root.parentInventory, grid))
                {
                    return false;
                }
                if (!GeneralHelper.IsItemEffectivelyOwned(root))
                {
                    return false;
                }
                return grid.childItems != null && grid.childItems.Contains(root);
            }

            public bool Detach(SaleEntry entry)
            {
                GameItem root = RootOf(entry);
                GameItem bin = BinOf(entry);
                if (root == null || bin == null)
                {
                    return false;
                }
                GameGridInventory grid = BinItem.GridOf(bin);
                if (grid == null)
                {
                    return false;
                }
                // 不吞异常：向上抛出，让核心层把"本次尝试"（可能已部分生效）纳入回滚并记录原因。
                return grid.Expel(root);
            }

            public bool VerifyDetached(SaleEntry entry)
            {
                GameItem root = RootOf(entry);
                return root != null && root.parentInventory == null && !BinItem.IsDestroyed(root);
            }

            public void ReturnToBin(SaleEntry entry)
            {
                GameItem root = RootOf(entry);
                GameItem bin = BinOf(entry);
                if (root == null || bin == null)
                {
                    throw new InvalidOperationException("return target missing");
                }
                GameGridInventory grid = BinItem.GridOf(bin);
                if (grid == null)
                {
                    throw new InvalidOperationException("bin grid missing");
                }
                if (object.ReferenceEquals(root.parentInventory, grid))
                {
                    return;
                }
                if (root.parentInventory != null)
                {
                    throw new InvalidOperationException("item still attached to another inventory");
                }
                grid.UncheckedAccept(root);
                if (!object.ReferenceEquals(root.parentInventory, grid))
                {
                    throw new InvalidOperationException("UncheckedAccept did not attach item");
                }
            }

            // 回滚验收：直接检查物品是否真的回到原箱，而不是相信 UncheckedAccept 没抛异常。
            public bool VerifyReturned(SaleEntry entry)
            {
                GameItem root = RootOf(entry);
                GameItem bin = BinOf(entry);
                if (root == null || bin == null || BinItem.IsDestroyed(root))
                {
                    return false;
                }
                GameGridInventory grid = BinItem.GridOf(bin);
                if (grid == null || !object.ReferenceEquals(root.parentInventory, grid))
                {
                    return false;
                }
                return grid.childItems != null && grid.childItems.Contains(root);
            }

            public void Pay(SaleEntry entry)
            {
                // ModCash 是单字段加法，符合"原子付款"契约。
                store.ModCash((int)entry.Amount, false);
            }

            public void Destroy(SaleEntry entry)
            {
                GameItem root = RootOf(entry);
                if (root == null)
                {
                    throw new InvalidOperationException("destroy target missing");
                }
                // 不吞异常：销毁失败意味着"钱已经付了但物品还在"，必须让核心层重试并报告。
                root.Destroy();
                if (!BinItem.IsDestroyed(root))
                {
                    throw new InvalidOperationException("destroy did not destroy item");
                }
            }

            public void ApplyCrime(IList<CrimeLine> crimes)
            {
                if (crimes == null || store.secData == null)
                {
                    throw new InvalidOperationException("crime data unavailable");
                }
                string failures = string.Empty;
                int applied = 0;
                for (int i = 0; i < crimes.Count; i++)
                {
                    CrimeLine line = crimes[i];
                    if (line == null || line.Amount <= 0)
                    {
                        continue;
                    }
                    long amount = line.Amount > int.MaxValue ? int.MaxValue : line.Amount;
                    try
                    {
                        store.secData.CommitCrime(line.Type, (int)amount);
                        applied++;
                    }
                    catch (Exception ex)
                    {
                        if (failures.Length > 0)
                        {
                            failures += " / ";
                        }
                        failures += line.Type + ": " + ex.Message;
                    }
                }
                if (failures.Length > 0)
                {
                    throw new InvalidOperationException(
                        "crime partially applied (" + applied + "): " + failures);
                }
            }

            public void ApplyMislabel(int penalty)
            {
                if (penalty <= 0)
                {
                    return;
                }
                StoreReputation.ModReputation(StoreReputation.FACTION_BLACK_MARKET, -penalty, false);
            }

            public void MarkSettled(int settledDay)
            {
                ModHelper.SetModDataInt(ModIds.Namespace, ModIds.DataLastSettlementDay, settledDay);
            }
        }
    }
}
