using System;
using System.Collections.Generic;

namespace ShippingBin.Core
{
    /// <summary>
    /// 提交结果。必须如实反映"钱、货、存档标记"三者的真实状态，
    /// 不允许"回滚失败却报告已回滚"或"付款后出错却报告成功"。
    /// </summary>
    public sealed class CommitResult
    {
        public bool Success;

        /// <summary>仅在"付款开始之前"失败，且所有尝试放回都已验证成功时为 true。</summary>
        public bool RolledBack;

        /// <summary>是否已进入不可逆的付款阶段。一旦为 true，就不再有"整单回滚"。</summary>
        public bool PaymentStarted;

        /// <summary>本次实际加进玩家现金的总额（只统计确实付款成功的条目）。</summary>
        public long PaidAmount;

        /// <summary>本次实际按已付款处理并销毁的件数。</summary>
        public int SoldCount;

        public int RetainedCount;

        public string FailureReasonKey = "";
        public string FailureItemName = "";

        /// <summary>失败所在阶段：revalidate / detach / verify / pay / destroy / crime / mislabel / settle / rollback。</summary>
        public string FailurePhase = "";

        /// <summary>全部失败记录（阶段:物品:信息），供日志逐条输出。</summary>
        public List<string> Failures = new List<string>();

        /// <summary>回滚时未能确认放回原箱的物品名。</summary>
        public List<string> RollbackFailures = new List<string>();

        public int SettledDay;
        public bool Settled;

        /// <summary>本次确实付款成功的条目（顺序与计划一致）。记账单只按这些条目生成。</summary>
        public List<SaleEntry> PaidEntries = new List<SaleEntry>();

        /// <summary>
        /// 记账义务是否已全部落地（涉案/假标签）。为 false 时不得写 day 标记，
        /// 下一次进入游戏要补提交。
        /// </summary>
        public bool ObligationsDone = true;

        public static CommitResult Failed(string reasonKey, string itemName)
        {
            CommitResult r = new CommitResult();
            r.Success = false;
            r.FailureReasonKey = reasonKey;
            r.FailureItemName = itemName;
            return r;
        }

        public void Fail(string phase, string itemName, string message)
        {
            FailurePhase = phase;
            if (string.IsNullOrEmpty(FailureReasonKey))
            {
                FailureReasonKey = "log_commit_" + phase + "_failed";
            }
            if (string.IsNullOrEmpty(FailureItemName) && !string.IsNullOrEmpty(itemName))
            {
                FailureItemName = itemName;
            }
            Failures.Add(phase + ":" + (itemName == null ? "" : itemName) + ":" + (message == null ? "" : message));
        }
    }

    /// <summary>
    /// 提交阶段对游戏世界的全部副作用入口。核心层只按这个契约编排顺序，
    /// 不引用任何游戏类型，因此整个事务流程可以在游戏外做故障注入测试。
    ///
    /// 适配层契约：
    ///  * Detach 只把物品从箱子里取下、不销毁，失败时允许已部分生效。
    ///  * VerifyDetached 必须直接检查"物品是否已不在原箱"，不能只看有没有抛异常。
    ///  * ReturnToBin 幂等：物品已在箱内时视为成功。
    ///  * VerifyReturned 必须直接检查"物品确实回到原箱"，用于核实回滚是否真的成功。
    ///  * Pay 必须原子：要么把金额完整加上，要么在加法之前抛异常（不得半加）。
    ///  * Destroy 允许抛异常；抛异常时条目仍按"已付款、已售出"记账，并在结果里报告。
    /// </summary>
    public interface ICommitWorld
    {
        /// <summary>提交前复核：物品仍在原箱、仍是玩家物品、仍未被锁、价值与计划一致。</summary>
        bool Revalidate(SaleEntry entry);

        /// <summary>把整棵待售根从箱子里取下（含后代），不销毁。</summary>
        bool Detach(SaleEntry entry);

        /// <summary>确认已完全离开箱子且引用可继续操作。</summary>
        bool VerifyDetached(SaleEntry entry);

        /// <summary>把物品放回原箱（回滚用）。必须幂等。</summary>
        void ReturnToBin(SaleEntry entry);

        /// <summary>核实物品确实已回到原箱（回滚验收用）。</summary>
        bool VerifyReturned(SaleEntry entry);

        /// <summary>按条目付款。金额已在计划阶段算定，这里只做原子加法。</summary>
        void Pay(SaleEntry entry);

        /// <summary>销毁已售出的整棵物品树。</summary>
        void Destroy(SaleEntry entry);

        /// <summary>
        /// 结算涉案（全晚按类型合并后一次提交）。每条 CrimeLine 带原版犯罪类型与
        /// 已算定的金额，适配层逐条调用原版 CommitCrime，不自行推导类型或金额。
        /// </summary>
        void ApplyCrime(IList<CrimeLine> crimes);

        /// <summary>扣假标签声望惩罚（一次，全晚合计）。</summary>
        void ApplyMislabel(int penalty);

        /// <summary>把当天记为“已结算”。付款成功后必须尽力写入，写失败要如实上报。</summary>
        void MarkSettled(int day);
    }

    /// <summary>
    /// 结算后的记账义务（原版涉案 + 假标签声望）。
    ///
    /// 契约：
    ///  - 只接受"确实已付款"的条目，金额/涉案/假标签必须按该子集重新计算，不按整单；
    ///  - 实现方必须先写盘再提交，逐条提交、逐条落盘；
    ///  - 返回 true 表示没有任何遗留义务；false 表示仍有待提交项，调用方不得写 day 标记；
    ///  - 抛异常等价于 false，且调用方会记录失败阶段。
    /// </summary>
    public interface IObligationLedger
    {
        bool PersistAndApply(IList<SaleEntry> paid, NightPlan plan, int day);
    }

    public sealed class Committer
    {
        /// <summary>
        /// 事务编排：
        ///  1) 复核全部 → 2) 全部取下（记录"本次尝试"，含失败的那一件）→
        ///  3) 全部确认离开 → 4) 逐条付款（不可逆点）→ 5) 销毁 →
        ///  6) 涉案 → 7) 假标声望 → 8) 最后写 day 标记。
        ///
        /// 付款前失败：把"所有尝试取下过的条目"（包括失败的那一件，可能已部分生效）
        /// 逆序放回原箱，并逐条 VerifyReturned 验收；只有全部验收通过才把
        /// RolledBack 置 true。付款后失败：不再整单回滚，而是按已付款/未付款拆分：
        /// 已付款的照常销毁（避免"钱+货"双得），未付款的放回原箱，并如实上报。
        /// </summary>
        public static CommitResult Run(NightPlan plan, ICommitWorld world, int day)
        {
            return Run(plan, world, day, null);
        }

        /// <summary>
        /// ledger 非 null 时，涉案/假标签不再直接提交，而是交给记账单：
        /// 先落盘"已付款子集应记的账"，再逐条提交、逐条落盘；只有全部落地才写 day 标记。
        /// </summary>
        public static CommitResult Run(NightPlan plan, ICommitWorld world, int day, IObligationLedger ledger)
        {
            CommitResult result = new CommitResult();
            if (plan == null || world == null)
            {
                CommitResult bad = CommitResult.Failed("log_commit_invalid_plan", "");
                bad.Fail("plan", "", "plan or world is null");
                return bad;
            }
            result.RetainedCount = plan.Retained.Count;

            if (plan.Sales.Count == 0)
            {
                try
                {
                    world.MarkSettled(day);
                    result.Success = true;
                    result.Settled = true;
                    result.SettledDay = day;
                }
                catch (Exception ex)
                {
                    result.Fail("settle", "", ex.Message);
                }
                return result;
            }

            // 1. 复核：此时还没有任何副作用。
            for (int i = 0; i < plan.Sales.Count; i++)
            {
                SaleEntry entry = plan.Sales[i];
                bool ok;
                string message = "";
                try
                {
                    ok = world.Revalidate(entry);
                }
                catch (Exception ex)
                {
                    ok = false;
                    message = ex.Message;
                }
                if (!ok)
                {
                    result.Fail("revalidate", entry.DisplayName, message);
                    return result;
                }
            }

            // 2. 取下：先把"本次尝试的条目"加入列表，再执行；失败项也可能已部分脱离，
            //    因此回滚列表必须包含它。
            List<SaleEntry> attempted = new List<SaleEntry>();
            for (int i = 0; i < plan.Sales.Count; i++)
            {
                SaleEntry entry = plan.Sales[i];
                bool ok;
                string message = "";
                attempted.Add(entry);
                try
                {
                    ok = world.Detach(entry);
                }
                catch (Exception ex)
                {
                    ok = false;
                    message = ex.Message;
                }
                if (!ok)
                {
                    result.Fail("detach", entry.DisplayName, message);
                    Rollback(attempted, world, result);
                    result.RolledBack = result.RollbackFailures.Count == 0;
                    return result;
                }
            }

            // 3. 确认全部已离开箱子。
            for (int i = 0; i < plan.Sales.Count; i++)
            {
                SaleEntry entry = plan.Sales[i];
                bool ok;
                string message = "";
                try
                {
                    ok = world.VerifyDetached(entry);
                }
                catch (Exception ex)
                {
                    ok = false;
                    message = ex.Message;
                }
                if (!ok)
                {
                    result.Fail("verify", entry.DisplayName, message);
                    Rollback(attempted, world, result);
                    result.RolledBack = result.RollbackFailures.Count == 0;
                    return result;
                }
            }

            // 4. 付款：不可逆点。Pay 契约是原子加法。
            result.PaymentStarted = true;
            int paidCount = 0;
            for (int i = 0; i < plan.Sales.Count; i++)
            {
                SaleEntry entry = plan.Sales[i];
                try
                {
                    world.Pay(entry);
                    result.PaidAmount += entry.Amount;
                    result.PaidEntries.Add(entry);
                    paidCount++;
                }
                catch (Exception ex)
                {
                    result.Fail("pay", entry.DisplayName, ex.Message);
                    // 已付款条目必须销毁（钱已到手，物品不能留在世界里）；
                    // 未付款条目交还箱内，下一晚还能再卖。
                    // 注意：已付款的那部分仍要记账，不能因为整单失败就漏掉。
                    DestroyPaidAndReturnUnpaid(plan, world, ledger, result, paidCount, day);
                    return result;
                }
            }

            // 5. 销毁。销毁失败不影响"已付款"的记账，但必须如实上报并重试一次。
            for (int i = 0; i < plan.Sales.Count; i++)
            {
                SaleEntry entry = plan.Sales[i];
                string message = "";
                bool destroyed;
                try
                {
                    world.Destroy(entry);
                    destroyed = true;
                }
                catch (Exception ex)
                {
                    destroyed = false;
                    message = ex.Message;
                }
                if (!destroyed)
                {
                    // 重试一次：付款已完成，物品留在世界等于"钱+货"双得。
                    try
                    {
                        world.Destroy(entry);
                        destroyed = true;
                    }
                    catch (Exception ex2)
                    {
                        message = message + " / retry: " + ex2.Message;
                    }
                }
                result.SoldCount++;
                if (!destroyed)
                {
                    result.Fail("destroy", entry.DisplayName, message);
                }
            }

            // 6. 涉案记账：ledger 非空时交给持久记账单（只按已付款子集生成，
            //    先落盘再逐条提交，失败留待下次进游戏补提交）；ledger 为 null 时
            //    退回直接提交路径（用于纯规则测试与兼容）。
            if (ledger != null)
            {
                ApplyObligations(ledger, plan, result, result.PaidEntries, day);
            }
            else
            {
                if (plan.TotalCrime > 0)
                {
                    try
                    {
                        world.ApplyCrime(plan.Crimes);
                    }
                    catch (Exception ex)
                    {
                        result.Fail("crime", "", ex.Message);
                    }
                }
                if (plan.MislabelPenalty > 0)
                {
                    try
                    {
                        world.ApplyMislabel(plan.MislabelPenalty);
                    }
                    catch (Exception ex)
                    {
                        result.Fail("mislabel", "", ex.Message);
                    }
                }
            }

            // 7. 最后写 day 标记。记账义务没落地时绝对不能写，否则本夜涉案/假标签
            //    会永久丢失；留到下次进游戏由记账单补提交。
            if (!result.ObligationsDone)
            {
                return result;
            }

            //    失败时钱与货都已处理完，但当天未记结算；
            //    此时箱内已无可卖条目，重复结算不会重复付款。
            try
            {
                world.MarkSettled(day);
                result.Settled = true;
                result.SettledDay = day;
            }
            catch (Exception ex)
            {
                result.Fail("settle", "", ex.Message);
            }

            result.Success = result.Failures.Count == 0;
            return result;
        }

        /// <summary>付款中途失败：已付款的销毁、未付款的放回原箱，并如实记录。</summary>
        private static void DestroyPaidAndReturnUnpaid(NightPlan plan, ICommitWorld world, IObligationLedger ledger, CommitResult result, int paidCount, int day)
        {
            for (int i = 0; i < paidCount; i++)
            {
                SaleEntry entry = plan.Sales[i];
                string message = "";
                bool destroyed;
                try
                {
                    world.Destroy(entry);
                    destroyed = true;
                }
                catch (Exception ex)
                {
                    destroyed = false;
                    message = ex.Message;
                }
                result.SoldCount++;
                if (!destroyed)
                {
                    try
                    {
                        world.Destroy(entry);
                        destroyed = true;
                    }
                    catch (Exception ex2)
                    {
                        message = message + " / retry: " + ex2.Message;
                    }
                }
                if (!destroyed)
                {
                    result.Fail("destroy", entry.DisplayName, message);
                }
            }

            List<SaleEntry> unpaid = new List<SaleEntry>();
            for (int i = paidCount; i < plan.Sales.Count; i++)
            {
                unpaid.Add(plan.Sales[i]);
            }
            Rollback(unpaid, world, result);

            // 已付款的那部分仍然产生记账义务，必须按该子集记账；
            // 未落地时 result.ObligationsDone=false，调用方不会写 day 标记。
            if (ledger != null)
            {
                ApplyObligations(ledger, plan, result, result.PaidEntries, day);
            }
        }

        /// <summary>提交记账义务；抛异常等价于"未全部落地"。</summary>
        private static void ApplyObligations(IObligationLedger ledger, NightPlan plan, CommitResult result, IList<SaleEntry> paid, int day)
        {
            bool done;
            string message = "";
            try
            {
                done = ledger.PersistAndApply(paid, plan, day);
            }
            catch (Exception ex)
            {
                done = false;
                message = ex.Message;
            }
            result.ObligationsDone = done;
            if (!done)
            {
                result.Fail("ledger", "", message);
            }
        }

        /// <summary>
        /// 逆序放回原箱，并逐条核实。任何一条抛异常或核实失败都记入 RollbackFailures，
        /// 不会被静默吞掉，也不会让调用方误以为回滚成功。
        /// </summary>
        private static void Rollback(List<SaleEntry> entries, ICommitWorld world, CommitResult result)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                SaleEntry entry = entries[i];
                string message = "";
                bool returned;
                try
                {
                    world.ReturnToBin(entry);
                    returned = true;
                }
                catch (Exception ex)
                {
                    returned = false;
                    message = ex.Message;
                }

                if (returned)
                {
                    try
                    {
                        returned = world.VerifyReturned(entry);
                        if (!returned) message = "verify returned false";
                    }
                    catch (Exception ex)
                    {
                        returned = false;
                        message = "verify: " + ex.Message;
                    }
                }

                if (!returned)
                {
                    result.RollbackFailures.Add(entry.DisplayName);
                    result.Fail("rollback", entry.DisplayName, message);
                }
            }
        }
    }
}
