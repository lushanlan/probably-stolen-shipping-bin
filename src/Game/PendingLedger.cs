using System;
using System.Collections.Generic;
using ShippingBin.Core;

namespace ShippingBin
{
    /// <summary>
    /// 记账义务的持久化与提交。
    ///
    /// 顺序（每一步都先落盘再执行）：
    ///   1) 按"已付款子集"生成记账单并写入 modData（随存档保存）；
    ///   2) 逐条：状态改 InFlight → 落盘 → 调原版接口 → 状态改 Applied → 落盘；
    ///   3) 全部落地后清空记账单。
    ///
    /// 恢复：任何遗留 InFlight 行视为"已生效"（至多一次，宁可少记不重复惩罚），
    /// 其余 Pending 行继续提交。未全部落地时不清空，也不允许写 day 标记。
    ///
    /// 证据边界：原版 `SecData.CommitCrime` 只改内存列表（见 docs/il/crime-protection.md），
    /// 本身没有幂等键；`StoreReputation.ModReputation` 同理。因此幂等只能靠
    /// "先落盘意图、后落盘结果"这对状态转移实现，崩溃窗口最多一行。
    /// </summary>
    internal static class PendingLedger
    {
        private const string Color = "#ffb36b";

        internal static bool HasPending()
        {
            SettlementLedger ledger = Load();
            return ledger != null && ledger.HasObligations;
        }

        internal static SettlementLedger Load()
        {
            string text;
            try
            {
                text = ModHelper.GetModData(ModIds.Namespace, ModIds.DataPendingObligations, string.Empty);
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("读取待提交记账单失败: " + ex.Message);
                return null;
            }
            if (string.IsNullOrEmpty(text)) return null;
            SettlementLedger ledger;
            if (!SettlementLedger.TryDecode(text, out ledger) || ledger == null)
            {
                ShippingBinMod.WriteLog("待提交记账单无法解析，已保留原始数据以便人工检查");
                return null;
            }
            return ledger;
        }

        /// <summary>纯规则层出入口：给 Committer 用，只按已付款条目生成并提交。</summary>
        internal sealed class Sink : IObligationLedger
        {
            private readonly PlayerStore store;

            internal Sink(PlayerStore store)
            {
                this.store = store;
            }

            public bool PersistAndApply(IList<SaleEntry> paid, NightPlan plan, int day)
            {
                SettlementLedger ledger = SettlementLedger.FromPaid(paid, day);
                if (!ledger.HasObligations) return true;
                if (!Save(ledger)) return false;
                bool done = ApplyPending(store, ledger);
                // 全部落地后立刻清空，避免下次读档把"已完成的记账单"当成遗留义务重复报告。
                if (done) Clear();
                return done;
            }
        }

        /// <summary>进入游戏 / 睡觉时补提交；返回 true 表示没有遗留义务。</summary>
        internal static bool Recover(PlayerStore store)
        {
            if (store == null) return true;
            SettlementLedger ledger = Load();
            if (ledger == null) return true;
            if (!ledger.HasObligations)
            {
                Clear();
                return true;
            }
            if (ledger.AllApplied)
            {
                // 上次已经全部提交完，只是没来得及清除；静默清掉，不重复提示。
                Clear();
                return true;
            }
            if (ledger.ResolveInFlight())
            {
                ShippingBinMod.WriteLog("记账单有未确认的行，按至多一次规则视为已生效");
                Save(ledger);
            }
            bool done = ApplyPending(store, ledger);
            if (done)
            {
                Clear();
                if (PlayerStore.Instance != null)
                {
                    PlayerStore.Instance.AddNightLog(
                        Loc.Get("log_ledger_recovered", ledger.Day, ledger.SoldCount, ledger.PaidAmount), Color);
                }
            }
            else
            {
                Save(ledger);
                if (PlayerStore.Instance != null)
                {
                    PlayerStore.Instance.AddNightLog(Loc.Get("log_ledger_pending_retry", ledger.Day), Color);
                }
            }
            return done;
        }

        private static bool ApplyPending(PlayerStore store, SettlementLedger ledger)
        {
            for (int i = 0; i < ledger.Lines.Count; i++)
            {
                LedgerLine line = ledger.Lines[i];
                if (line == null || line.State == LedgerState.Applied) continue;
                if (line.State == LedgerState.Pending)
                {
                    line.State = LedgerState.InFlight;
                    if (!Save(ledger)) return false;
                }
                try
                {
                    ApplyCrimeLine(store, line);
                }
                catch (Exception ex)
                {
                    ShippingBinMod.WriteLog("提交涉案失败(" + line.Type + "): " + ex.Message);
                    Save(ledger);
                    return false;
                }
                line.State = LedgerState.Applied;
                if (!Save(ledger)) return false;
            }

            if (ledger.MislabelPenalty > 0 && ledger.MislabelState != LedgerState.Applied)
            {
                if (ledger.MislabelState == LedgerState.Pending)
                {
                    ledger.MislabelState = LedgerState.InFlight;
                    if (!Save(ledger)) return false;
                }
                try
                {
                    ApplyMislabel(store, ledger);
                }
                catch (Exception ex)
                {
                    ShippingBinMod.WriteLog("提交假标签声望失败: " + ex.Message);
                    Save(ledger);
                    return false;
                }
                ledger.MislabelState = LedgerState.Applied;
                if (!Save(ledger)) return false;
            }
            return ledger.AllApplied;
        }

        /// <summary>
        /// 逐条提交涉案：类型与金额都来自原版口径（FENCING/DRUG/… 与 GetNegociatedValue），
        /// 本方法只负责"提交一次 + 事后可验证的最小检查"。
        /// </summary>
        private static void ApplyCrimeLine(PlayerStore store, LedgerLine line)
        {
            if (store == null || store.secData == null) throw new InvalidOperationException("secData is null");
            SecData secData = store.secData;
            int amount = line.Amount > int.MaxValue ? int.MaxValue : (int)line.Amount;
            bool firstOfType = secData.GetCrimeIndex(line.Type) < 0;
            secData.CommitCrime(line.Type, amount);
            if (firstOfType && secData.GetCrimeIndex(line.Type) < 0)
            {
                // 原版 CommitCrime 对未知 crimeID 会新建条目；条目仍未出现说明调用没生效，
                // 抛出去让记账单保持 Pending/InFlight，下次再补。
                throw new InvalidOperationException("crime type was not recorded: " + line.Type);
            }
        }

        private static void ApplyMislabel(PlayerStore store, SettlementLedger ledger)
        {
            int penalty = ledger.MislabelPenalty > int.MaxValue ? int.MaxValue : (int)ledger.MislabelPenalty;
            int before = StoreReputation.GetBMReputation();
            StoreReputation.ModReputation(StoreReputation.FACTION_BLACK_MARKET, -penalty, false);
            int after = StoreReputation.GetBMReputation();
            if (penalty > 0 && after >= before)
            {
                ShippingBinMod.WriteLog("假标签声望未见下降（可能已到下限）: before=" + before + " after=" + after);
            }
        }

        private static bool Save(SettlementLedger ledger)
        {
            try
            {
                ModHelper.SetModData(ModIds.Namespace, ModIds.DataPendingObligations, ledger.Encode());
                return true;
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("写入待提交记账单失败: " + ex.Message);
                return false;
            }
        }

        private static void Clear()
        {
            try
            {
                ModHelper.RemoveModData(ModIds.Namespace, ModIds.DataPendingObligations);
            }
            catch (Exception ex)
            {
                ShippingBinMod.WriteLog("清除待提交记账单失败: " + ex.Message);
            }
        }
    }
}
