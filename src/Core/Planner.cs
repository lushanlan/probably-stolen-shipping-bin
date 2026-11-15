using System;
using System.Collections.Generic;

namespace ShippingBin.Core
{
    // ---------------------------------------------------------------------
    // 夜间结算计划：先把所有箱子的“待售根 + 保留项 + 金额 + 涉案”一次算清，
    // 再交给 Committer 提交。计划阶段绝不修改世界状态，因此任何一箱算不出来
    // 都不会产生半完成的交易。
    //
    // 关键规则（用户定案）：
    //   * 多箱顺序无关：全部金额都用同一个“当晚起始黑市声望”快照计算。
    //   * 同一待售根只算一次（跨箱去重、同箱重复列出也只算一次）。
    //   * 任一后代被拒绝 -> 整个待售根保留，并给出具体阻塞物品与中文键。
    //   * 容器作为一个整体出售：估值走原版整树估值（GetCurrentValue），
    //     不手工累加后代，避免重复计算市场修正/IGNORE_CHILD_VALUE/锁后物品。
    //   * 赃物涉案只按“最上层赃物节点”计一次；违禁品单独走自己的类型与倍率。
    //   * 假贴标只按“待售根”归属，全晚合计后最后扣一次。
    // ---------------------------------------------------------------------

    public sealed class BinSnapshot
    {
        public ItemFacts Bin;
        public List<ItemFacts> Contents = new List<ItemFacts>();
    }

    public sealed class SaleEntry
    {
        public object Bin;
        public ItemFacts Root;
        public object Key;
        public string DisplayName = string.Empty;
        public long NativeValue;
        public long Amount;
        public long StolenBasis;
        public long ContrabandBasis;
        public long CrimeAmount;
        public bool Mislabelled;
        public List<CrimeLine> Crimes = new List<CrimeLine>();
    }

    public sealed class RetainedEntry
    {
        public object Bin;
        public ItemFacts Root;
        public RejectReason Reason;
    }

    public sealed class NightPlan
    {
        public int ReputationSnapshot;
        public Settings Settings;
        public List<SaleEntry> Sales = new List<SaleEntry>();
        public List<RetainedEntry> Retained = new List<RetainedEntry>();
        public long TotalAmount;
        public long TotalCrime;
        public long MislabelledAmount;
        public int MislabelPenalty;
        public int MislabelledCount;
        public List<CrimeLine> Crimes = new List<CrimeLine>();
        public bool AmountOverflow;
        public bool CrimeOverflow;
    }

    public static class Planner
    {
        public static NightPlan Build(IList<BinSnapshot> bins, Settings settings, int reputationSnapshot)
        {
            NightPlan plan = new NightPlan();
            plan.ReputationSnapshot = reputationSnapshot;
            plan.Settings = settings == null ? new Settings() : settings;
            if (bins == null) return plan;

            List<BinSnapshot> seenBins = new List<BinSnapshot>();
            List<ItemFacts> seenRoots = new List<ItemFacts>();

            for (int b = 0; b < bins.Count; b++)
            {
                BinSnapshot snapshot = bins[b];
                if (snapshot == null || ContainsRef(seenBins, snapshot)) continue;
                seenBins.Add(snapshot);
                if (snapshot.Contents == null) continue;

                for (int i = 0; i < snapshot.Contents.Count; i++)
                {
                    ItemFacts root = snapshot.Contents[i];
                    if (HasRef(seenRoots, root)) continue;
                    if (root != null) seenRoots.Add(root);

                    RejectReason reason = Protection.Evaluate(root, plan.Settings);
                    if (reason != null)
                    {
                        plan.Retained.Add(Retained(root, snapshot.Bin, reason));
                        continue;
                    }

                    long nativeValue = TotalValue(root);
                    if (nativeValue < 0)
                    {
                        plan.Retained.Add(Retained(root, snapshot.Bin, new RejectReason(RejectCode.ValueOverflow, root)));
                        continue;
                    }
                    if (nativeValue <= 0)
                    {
                        plan.Retained.Add(Retained(root, snapshot.Bin, new RejectReason(RejectCode.NoValue, root)));
                        continue;
                    }

                    SaleEntry sale = new SaleEntry();
                    sale.Bin = snapshot.Bin;
                    sale.Root = root;
                    sale.Key = root.Key;
                    sale.DisplayName = root.DisplayName == null ? string.Empty : root.DisplayName;
                    sale.NativeValue = nativeValue;
                    sale.Amount = Rules.SaleAmount(nativeValue, plan.Settings.SaleMultiplier);
                    sale.Mislabelled = root.IsMislabelled;

                    List<ItemFacts> visited = new List<ItemFacts>();
                    CollectCrimes(root, plan.Settings, reputationSnapshot, visited, sale.Crimes, false, sale);

                    if (plan.TotalAmount > long.MaxValue - sale.Amount)
                    {
                        plan.AmountOverflow = true;
                    }
                    else
                    {
                        plan.TotalAmount += sale.Amount;
                    }

                    if (sale.Mislabelled)
                    {
                        plan.MislabelledCount++;
                        if (plan.MislabelledAmount > long.MaxValue - sale.Amount) plan.AmountOverflow = true;
                        else plan.MislabelledAmount += sale.Amount;
                    }

                    for (int c = 0; c < sale.Crimes.Count; c++)
                    {
                        MergeCrime(plan.Crimes, plan.Settings, reputationSnapshot, sale.Crimes[c], plan);
                    }

                    plan.Sales.Add(sale);
                }
            }

            plan.MislabelPenalty = Rules.MislabelPenalty(plan.MislabelledAmount, plan.MislabelledCount > 0);
            return plan;
        }

        private static RetainedEntry Retained(ItemFacts root, object bin, RejectReason reason)
        {
            RetainedEntry entry = new RetainedEntry();
            entry.Bin = bin;
            entry.Root = root;
            entry.Reason = reason;
            return entry;
        }

        /// <summary>
        /// 待售根的原版整树估值。适配层提供了 VanillaTreeValue 时直接用（原版
        /// GetCurrentValue 已经正确处理 IGNORE_CHILD_VALUE、锁后后代、价格修正、
        /// 液体容器价与客户市场修正），否则按纯数据递归求和，供规则测试使用。
        /// </summary>
        public static long TotalValue(ItemFacts root)
        {
            List<ItemFacts> visited = new List<ItemFacts>();
            return TotalValue(root, visited);
        }

        private static long TotalValue(ItemFacts node, List<ItemFacts> visited)
        {
            if (node == null) return 0;
            if (ContainsRef(visited, node)) return 0;
            visited.Add(node);

            if (node.VanillaTreeValue >= 0) return node.VanillaTreeValue;
            if (node.SelfValue < 0) return -1;

            long total = node.SelfValue;
            if (node.IgnoreChildValue || node.Children == null) return total;

            for (int i = 0; i < node.Children.Count; i++)
            {
                long child = TotalValue(node.Children[i], visited);
                if (child < 0) return -1;
                if (total > long.MaxValue - child) return -1;
                total += child;
            }
            return total;
        }

        /// <summary>
        /// 收集涉案构成：优先用适配层给出的原版 CrimeContributions；否则按纯数据推导。
        /// 赃物只在最上层赃物节点计一次（后代的赃物标记不再重复计罪）；
        /// 违禁品与赃物是两套类型，同一件物品可以同时产生两种罪。
        /// </summary>
        private static void CollectCrimes(ItemFacts node, Settings settings, int reputation, List<ItemFacts> visited, List<CrimeLine> into, bool stolenAlreadyCounted, SaleEntry sale)
        {
            if (node == null) return;
            if (ContainsRef(visited, node)) return;
            visited.Add(node);

            if (node.CrimeContributions != null && node.CrimeContributions.Count > 0)
            {
                for (int i = 0; i < node.CrimeContributions.Count; i++)
                {
                    CrimeLine source = node.CrimeContributions[i];
                    if (source == null || source.Basis <= 0) continue;
                    if (source.Kind == CrimeKind.Stolen && stolenAlreadyCounted) continue;
                    if (source.Kind == CrimeKind.Stolen) stolenAlreadyCounted = true;

                    CrimeLine priced = new CrimeLine(source.Type, source.Basis, source.Kind);
                    priced.Amount = Rules.CrimeAmount(source.Basis, reputation, settings.HalfCrimeAt100, settings.ExemptCrimeAt200, Rules.MultiplierFor(source.Kind, settings));
                    AddCrimeLine(into, priced);
                    if (source.Kind == CrimeKind.Stolen) sale.StolenBasis += priced.Basis;
                    else if (source.Kind == CrimeKind.Contraband) sale.ContrabandBasis += priced.Basis;
                    sale.CrimeAmount += priced.Amount;
                }
            }
            else
            {
                if (!stolenAlreadyCounted && node.IsStolen && node.StolenPercent > 0)
                {
                    long basis = Rules.StolenBasis(TotalValue(node), node.StolenPercent);
                    if (basis > 0)
                    {
                        CrimeLine priced = new CrimeLine("FENCING", basis, CrimeKind.Stolen);
                        priced.Amount = Rules.CrimeAmount(basis, reputation, settings.HalfCrimeAt100, settings.ExemptCrimeAt200, Rules.MultiplierFor(CrimeKind.Stolen, settings));
                        AddCrimeLine(into, priced);
                        sale.StolenBasis += basis;
                        sale.CrimeAmount += priced.Amount;
                        stolenAlreadyCounted = true;
                    }
                }
                if (node.IsContraband)
                {
                    long basis = TotalValue(node);
                    if (basis > 0)
                    {
                        CrimeLine priced = new CrimeLine("CONTRABAND_TRAFFICKING", basis, CrimeKind.Contraband);
                        priced.Amount = Rules.CrimeAmount(basis, reputation, settings.HalfCrimeAt100, settings.ExemptCrimeAt200, Rules.MultiplierFor(CrimeKind.Contraband, settings));
                        AddCrimeLine(into, priced);
                        sale.ContrabandBasis += basis;
                        sale.CrimeAmount += priced.Amount;
                    }
                }
            }

            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    CollectCrimes(node.Children[i], settings, reputation, visited, into, stolenAlreadyCounted, sale);
                }
            }
        }

        private static void AddCrimeLine(List<CrimeLine> lines, CrimeLine line)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Type == line.Type)
                {
                    lines[i].Basis += line.Basis;
                    lines[i].Amount += line.Amount;
                    return;
                }
            }
            lines.Add(line);
        }

        private static void MergeCrime(List<CrimeLine> night, Settings settings, int reputation, CrimeLine line, NightPlan plan)
        {
            for (int i = 0; i < night.Count; i++)
            {
                if (night[i].Type == line.Type)
                {
                    if (night[i].Basis > long.MaxValue - line.Basis) plan.CrimeOverflow = true;
                    else night[i].Basis += line.Basis;
                    if (night[i].Amount > long.MaxValue - line.Amount) plan.CrimeOverflow = true;
                    else night[i].Amount += line.Amount;
                    if (plan.TotalCrime > long.MaxValue - line.Amount) plan.CrimeOverflow = true;
                    else plan.TotalCrime += line.Amount;
                    return;
                }
            }
            CrimeLine copy = line.Clone();
            night.Add(copy);
            if (plan.TotalCrime > long.MaxValue - copy.Amount) plan.CrimeOverflow = true;
            else plan.TotalCrime += copy.Amount;
        }

        private static bool ContainsRef<T>(List<T> list, T item) where T : class
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (object.ReferenceEquals(list[i], item)) return true;
            }
            return false;
        }

        private static bool HasRef(List<ItemFacts> list, ItemFacts item)
        {
            return ContainsRef(list, item);
        }
    }
}
