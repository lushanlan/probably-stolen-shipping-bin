using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShippingBin.Core
{
    /// <summary>记账行状态。InFlight 表示"已调用原版记账接口，但尚未确认写入"。</summary>
    public enum LedgerState
    {
        Pending = 0,
        InFlight = 1,
        Applied = 2
    }

    /// <summary>一行待提交的原版涉案（赃物/违禁品），金额已由规则层算定。</summary>
    public sealed class LedgerLine
    {
        public string Type = string.Empty;
        public CrimeKind Kind;
        public long Basis;
        public long Amount;
        public LedgerState State;

        public LedgerLine()
        {
        }

        public LedgerLine(string type, CrimeKind kind, long basis, long amount)
        {
            Type = type;
            Kind = kind;
            Basis = basis;
            Amount = amount;
            State = LedgerState.Pending;
        }

        public CrimeLine ToCrimeLine()
        {
            CrimeLine line = new CrimeLine(Type, Basis, Kind);
            line.Amount = Amount;
            return line;
        }

        public LedgerLine Clone()
        {
            LedgerLine copy = new LedgerLine(Type, Kind, Basis, Amount);
            copy.State = State;
            return copy;
        }
    }

    /// <summary>
    /// 结算后的持久记账单。
    ///
    /// 用途：付款一旦发生就不可回滚，而涉案/假标签的提交可能失败。把"已付款项对应的
    /// 记账义务"先写盘、再逐条提交并逐条落盘，就能在下次进入游戏时把没提交完的
    /// 义务补上，同时避免重复提交。
    ///
    /// 幂等策略（有证据边界，见 docs/acceptance.md）：
    ///  - 每次调用原版接口前先落盘 InFlight，调用成功后落盘 Applied；
    ///  - 恢复时把遗留的 InFlight 视为"已生效"（至多一次），宁可少记一次也不重复惩罚；
    ///  - 每行的提交与状态落盘是一对，崩溃窗口最多只有一个 InFlight 行。
    /// </summary>
    public sealed class SettlementLedger
    {
        public const int CurrentVersion = 1;
        public const string Header = "SB-LEDGER";
        public const string DataKey = "pending_obligations";

        public int Version = CurrentVersion;
        public int Day;
        public int SoldCount;
        public long PaidAmount;
        public long MislabelledAmount;
        public long MislabelPenalty;
        public LedgerState MislabelState;
        public List<LedgerLine> Lines = new List<LedgerLine>();

        public bool HasObligations
        {
            get { return Lines.Count > 0 || MislabelPenalty > 0; }
        }

        public int PendingCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Lines.Count; i++)
                {
                    if (Lines[i].State != LedgerState.Applied) count++;
                }
                if (MislabelPenalty > 0 && MislabelState != LedgerState.Applied) count++;
                return count;
            }
        }

        public bool AllApplied
        {
            get { return PendingCount == 0; }
        }

        /// <summary>
        /// 只按"确实已付款"的条目生成记账单，金额/涉案/假标签都不按原整单计算。
        /// 付款中途失败时，未付款项不会产生任何记账义务。
        /// </summary>
        public static SettlementLedger FromPaid(IList<SaleEntry> paid, int day)
        {
            SettlementLedger ledger = new SettlementLedger();
            ledger.Day = day;
            long mislabelled = 0;
            bool hasMislabelled = false;
            if (paid != null)
            {
                for (int i = 0; i < paid.Count; i++)
                {
                    SaleEntry entry = paid[i];
                    if (entry == null) continue;
                    ledger.SoldCount++;
                    ledger.PaidAmount = SafeAdd(ledger.PaidAmount, entry.Amount);
                    if (entry.Mislabelled)
                    {
                        hasMislabelled = true;
                        mislabelled = SafeAdd(mislabelled, entry.Amount);
                    }
                    if (entry.Crimes != null)
                    {
                        for (int c = 0; c < entry.Crimes.Count; c++)
                        {
                            CrimeLine crime = entry.Crimes[c];
                            if (crime == null) continue;
                            if (crime.Amount <= 0) continue;
                            ledger.Lines.Add(new LedgerLine(crime.Type, crime.Kind, crime.Basis, crime.Amount));
                        }
                    }
                }
            }
            ledger.MislabelledAmount = mislabelled;
            ledger.MislabelPenalty = hasMislabelled ? Rules.MislabelPenalty(mislabelled, true) : 0;
            return ledger;
        }

        /// <summary>只包含尚未提交的涉案行（保持原顺序）。</summary>
        public List<CrimeLine> RemainingCrimes()
        {
            List<CrimeLine> result = new List<CrimeLine>();
            for (int i = 0; i < Lines.Count; i++)
            {
                if (Lines[i].State != LedgerState.Applied) result.Add(Lines[i].ToCrimeLine());
            }
            return result;
        }

        /// <summary>恢复时的至多一次语义：遗留 InFlight 一律当作已生效。</summary>
        public bool ResolveInFlight()
        {
            bool resolved = false;
            for (int i = 0; i < Lines.Count; i++)
            {
                if (Lines[i].State == LedgerState.InFlight)
                {
                    Lines[i].State = LedgerState.Applied;
                    resolved = true;
                }
            }
            if (MislabelState == LedgerState.InFlight)
            {
                MislabelState = LedgerState.Applied;
                resolved = true;
            }
            return resolved;
        }

        public string Encode()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Header).Append(' ').Append(CurrentVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("D|")
              .Append(Day.ToString(CultureInfo.InvariantCulture)).Append('|')
              .Append(SoldCount.ToString(CultureInfo.InvariantCulture)).Append('|')
              .Append(PaidAmount.ToString(CultureInfo.InvariantCulture)).Append('|')
              .Append(MislabelledAmount.ToString(CultureInfo.InvariantCulture)).Append('|')
              .Append(MislabelPenalty.ToString(CultureInfo.InvariantCulture)).Append('|')
              .Append(((int)MislabelState).ToString(CultureInfo.InvariantCulture)).Append('\n');
            for (int i = 0; i < Lines.Count; i++)
            {
                LedgerLine line = Lines[i];
                if (!IsSafeToken(line.Type)) continue;
                sb.Append("L|")
                  .Append(line.Type).Append('|')
                  .Append(((int)line.Kind).ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append(line.Basis.ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append(line.Amount.ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append(((int)line.State).ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>解析失败一律返回 false 且不产生半张单子（调用方应保留旧数据并告警）。</summary>
        public static bool TryDecode(string text, out SettlementLedger ledger)
        {
            ledger = null;
            if (string.IsNullOrEmpty(text)) return false;
            string[] rows = text.Replace("\r", "").Split('\n');
            if (rows.Length < 2) return false;
            string[] header = rows[0].Split(' ');
            if (header.Length != 2 || header[0] != Header) return false;
            int version;
            if (!int.TryParse(header[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out version)) return false;
            if (version != CurrentVersion) return false;

            string[] summary = rows[1].Split('|');
            if (summary.Length != 7 || summary[0] != "D") return false;

            SettlementLedger parsed = new SettlementLedger();
            parsed.Version = version;
            int mislabelState;
            if (!TryInt(summary[1], out parsed.Day)) return false;
            if (!TryInt(summary[2], out parsed.SoldCount)) return false;
            if (!TryLong(summary[3], out parsed.PaidAmount)) return false;
            if (!TryLong(summary[4], out parsed.MislabelledAmount)) return false;
            if (!TryLong(summary[5], out parsed.MislabelPenalty)) return false;
            if (!TryInt(summary[6], out mislabelState)) return false;
            if (mislabelState < 0 || mislabelState > 2) return false;
            parsed.MislabelState = (LedgerState)mislabelState;

            for (int i = 2; i < rows.Length; i++)
            {
                string row = rows[i];
                if (row.Length == 0) continue;
                string[] parts = row.Split('|');
                if (parts.Length != 6 || parts[0] != "L") return false;
                if (!IsSafeToken(parts[1])) return false;
                int kind;
                int state;
                long basis;
                long amount;
                if (!TryInt(parts[2], out kind)) return false;
                if (!TryLong(parts[3], out basis)) return false;
                if (!TryLong(parts[4], out amount)) return false;
                if (!TryInt(parts[5], out state)) return false;
                if (kind != (int)CrimeKind.Stolen && kind != (int)CrimeKind.Contraband) return false;
                if (state < 0 || state > 2) return false;
                if (amount <= 0) return false;
                LedgerLine line = new LedgerLine(parts[1], (CrimeKind)kind, basis, amount);
                line.State = (LedgerState)state;
                parsed.Lines.Add(line);
            }
            ledger = parsed;
            return true;
        }

        private static bool IsSafeToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                if (c == '|' || c == '\n' || c == '\r') return false;
            }
            return true;
        }

        private static bool TryInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryLong(string text, out long value)
        {
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static long SafeAdd(long a, long b)
        {
            if (b > 0 && a > long.MaxValue - b) return long.MaxValue;
            if (b < 0 && a < long.MinValue - b) return long.MinValue;
            return a + b;
        }
    }
}
