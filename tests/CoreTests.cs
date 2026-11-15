using System;
using System.Collections.Generic;
using ShippingBin.Core;

/// <summary>
/// 纯规则层测试。不加载游戏、不引用任何游戏类型，可在游戏外用 csc 直接跑。
/// 覆盖：数值/阈值矩阵、保护判定、多箱顺序独立、嵌套去重、嵌套出货箱、
/// 赃物基础金额、假贴标归属、升级余点与溢出、事务提交与故障回滚。
/// </summary>
public static class CoreTests
{
    private static int passed;
    private static int failed;

    public static int Main(string[] args)
    {
        Run("Rules", TestRules);
        Run("Protection", TestProtection);
        Run("Planner", TestPlanner);
        Run("Commit", TestCommit);
        Run("Ledger", TestLedger);
        Run("CommitLedger", TestCommitLedger);
        Run("GrantPolicy", TestGrantPolicy);
        Console.WriteLine();
        Console.WriteLine(passed + " checks passed, " + failed + " failed.");
        if (failed > 0)
        {
            Console.WriteLine("CORE TESTS FAILED");
            return 1;
        }
        Console.WriteLine("All core rule tests passed (game integration NOT tested).");
        return 0;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine("[ok]   " + name);
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine("[FAIL] " + name + ": " + ex.Message);
        }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        passed++;
    }

    private static void Eq(long expected, long actual, string name)
    {
        if (expected != actual)
            throw new Exception(name + " expected=" + expected + " actual=" + actual);
        passed++;
    }

    private static void Eq(double expected, double actual, string name)
    {
        if (Math.Abs(expected - actual) > 1e-9)
            throw new Exception(name + " expected=" + expected + " actual=" + actual);
        passed++;
    }

    private static void Eq(string expected, string actual, string name)
    {
        if (expected != actual)
            throw new Exception(name + " expected=" + expected + " actual=" + actual);
        passed++;
    }

    private static ItemFacts Item(string id, long value)
    {
        ItemFacts facts = new ItemFacts();
        facts.Key = id;
        facts.Identifier = id;
        facts.DisplayName = id;
        facts.SelfValue = value;
        return facts;
    }

    private static Settings Defaults()
    {
        return new Settings();
    }

    /// <summary>Protection.Evaluate 返回 null 表示整根可售；测试里统一折算成 None。</summary>
    private static RejectCode Verdict(ItemFacts item, Settings settings)
    {
        RejectReason reason = Protection.Evaluate(item, settings);
        return reason == null ? RejectCode.None : reason.Code;
    }

    // =====================================================================
    private static void TestRules()
    {
        Eq(1, Rules.RoundHalfAwayFromZero(0.5), "round 0.5");
        Eq(2, Rules.RoundHalfAwayFromZero(1.5), "round 1.5");
        Eq(3, Rules.RoundHalfAwayFromZero(2.5), "round 2.5");
        Eq(-1, Rules.RoundHalfAwayFromZero(-0.5), "round -0.5");
        Eq(0, Rules.RoundHalfAwayFromZero(0.0), "round 0");

        Eq(80, Rules.SaleAmount(100, 0.8), "sale 100*0.8");
        Eq(1, Rules.SaleAmount(1, 0.8), "sale 1*0.8 rounds up");
        Eq(4, Rules.SaleAmount(5, 0.8), "sale 5*0.8");
        Eq(0, Rules.SaleAmount(0, 0.8), "sale zero value");
        Eq(0, Rules.SaleAmount(100, 0.0), "sale zero multiplier");
        Eq(125, Rules.SaleAmount(100, 1.25), "sale 100*1.25");
        Eq(1, Rules.SaleAmount(2, 0.5), "sale 2*0.5 exact 1");

        int[] reps = new int[] { 0, 99, 100, 199, 200, 250 };
        bool[] flags = new bool[] { false, true };
        for (int r = 0; r < reps.Length; r++)
        {
            for (int h = 0; h < 2; h++)
            {
                for (int e = 0; e < 2; e++)
                {
                    int rep = reps[r];
                    bool half = flags[h];
                    bool exempt = flags[e];
                    double expected = 2.0;
                    if (exempt && rep >= 200) expected = 0.0;
                    else if (half && rep >= 100) expected = 1.0;
                    double actual = Rules.StolenFactor(rep, half, exempt, 2.0);
                    Eq(expected, actual, "stolen factor rep=" + rep + " half=" + half + " exempt=" + exempt);
                }
            }
        }

        Eq(2000, Rules.CrimeAmount(1000, 0, true, true, 2.0), "crime rep0");
        Eq(1000, Rules.CrimeAmount(1000, 150, true, true, 2.0), "crime rep150 half");
        Eq(0, Rules.CrimeAmount(1000, 200, true, true, 2.0), "crime rep200 exempt");
        Eq(1000, Rules.CrimeAmount(1000, 200, true, false, 2.0), "crime rep200 half when exempt off");
        Eq(2000, Rules.CrimeAmount(1000, 150, false, true, 2.0), "crime rep150 no half");
        Eq(0, Rules.CrimeAmount(0, 0, true, true, 2.0), "crime zero basis");
        Eq(0, Rules.CrimeAmount(1000, 0, true, true, 0.0), "crime zero multiplier");
        Eq(1500, Rules.CrimeAmount(1000, 0, true, false, 1.5), "crime 1.5x");

        Eq(1000, Rules.StolenBasis(1000, 100), "basis 100%");
        Eq(500, Rules.StolenBasis(1000, 50), "basis 50%");
        Eq(501, Rules.StolenBasis(1001, 50), "basis 50% rounds away");
        Eq(500, Rules.StolenBasis(999, 50), "basis 50% rounds away 2");
        Eq(0, Rules.StolenBasis(0, 100), "basis zero");
        Eq(0, Rules.StolenBasis(1000, 0), "basis zero percent");

        Eq(0, Rules.MislabelPenalty(0, false), "mislabel none");
        Eq(1, Rules.MislabelPenalty(0, true), "mislabel min");
        Eq(1, Rules.MislabelPenalty(1, true), "mislabel tiny");
        Eq(1, Rules.MislabelPenalty(149, true), "mislabel 1.49");
        Eq(2, Rules.MislabelPenalty(150, true), "mislabel 1.5 away");
        Eq(20, Rules.MislabelPenalty(2500, true), "mislabel cap");
        Eq(20, Rules.MislabelPenalty(99999, true), "mislabel cap 2");

        Eq(6, Rules.CapacitySizeFor(0), "cap 0");
        Eq(6, Rules.CapacitySizeFor(4), "cap 4");
        Eq(8, Rules.CapacitySizeFor(5), "cap 5");
        Eq(8, Rules.CapacitySizeFor(14), "cap 14");
        Eq(10, Rules.CapacitySizeFor(15), "cap 15");
        Eq(10, Rules.CapacitySizeFor(29), "cap 29");
        Eq(12, Rules.CapacitySizeFor(30), "cap 30");
        Eq(12, Rules.CapacitySizeFor(999), "cap 999");
        Check(Rules.CapacitySizeFor(30) * Rules.CapacitySizeFor(30) > 9 * 8, "max capacity clearly larger than 8x9 native");
        Check(Rules.CapacitySizeFor(30) * Rules.CapacitySizeFor(30) > 7 * 11, "max capacity clearly larger than 7x11 native");

        UpgradeOutcome o = Rules.ApplyUpgradePoints(0, 1);
        Check(o.Accepted && o.NewPoints == 1 && o.NewSize == 6 && o.Overflow == 0, "upgrade junk from 0");
        o = Rules.ApplyUpgradePoints(4, 1);
        Check(o.Accepted && o.NewPoints == 5 && o.NewSize == 8, "upgrade cross 5");
        o = Rules.ApplyUpgradePoints(14, 1);
        Check(o.NewPoints == 15 && o.NewSize == 10, "upgrade cross 15");
        o = Rules.ApplyUpgradePoints(29, 5);
        Check(o.Accepted && o.NewPoints == 30 && o.NewSize == 12 && o.Overflow == 4, "upgrade exact cap absorbs overflow");
        o = Rules.ApplyUpgradePoints(28, 5);
        Check(o.Accepted && o.NewPoints == 30 && o.NewSize == 12 && o.Overflow == 3, "upgrade overflow absorbed");
        o = Rules.ApplyUpgradePoints(30, 5);
        Check(!o.Accepted && o.AtMaxBefore && o.NewPoints == 30 && o.Overflow == 0, "upgrade rejected at max");
        o = Rules.ApplyUpgradePoints(0, 0);
        Check(!o.Accepted, "upgrade non material rejected");

        Check(Rules.CanPurchaseToday(10, 10) == false, "purchase same day blocked");
        Check(Rules.CanPurchaseToday(10, 9) == true, "purchase next day allowed");
        Check(Rules.CanPurchaseToday(10, -1) == true, "purchase never bought");
        Check(Rules.CanPurchaseToday(11, 10) == true, "purchase day advances");
        Check(Rules.ShouldShowPurchase(true, false) == true, "show after buying store");
        Check(Rules.ShouldShowPurchase(false, false) == false, "hide before buying store");
        Check(Rules.ShouldShowPurchase(false, true) == true, "early unlock shows own entry");

        Eq(1, Rules.MaterialPoints("junk"), "material junk");
        Eq(2, Rules.MaterialPoints("scrap_metal"), "material scrap");
        Eq(5, Rules.MaterialPoints("metal_ingot"), "material ingot");
        Eq(0, Rules.MaterialPoints("gold_bar"), "material unknown");

        Settings s = Defaults();
        Settings copy = s.Clone();
        copy.SaleMultiplier = 1.5;
        Eq(0.8, s.SaleMultiplier, "settings clone independent");
    }

    // =====================================================================
    private static void TestProtection()
    {
        Settings settings = Defaults();

        ItemFacts ok = Item("ok", 10);
        Eq((long)RejectCode.None, (long)Verdict(ok, settings), "plain item sellable");

        ItemFacts notOwned = Item("not_owned", 10);
        notOwned.Owned = false;
        Eq((long)RejectCode.NotOwned, (long)Protection.Evaluate(notOwned, settings).Code, "not owned rejected");

        ItemFacts locked = Item("locked", 10);
        locked.BehindLock = true;
        Eq((long)RejectCode.BehindLock, (long)Protection.Evaluate(locked, settings).Code, "behind lock rejected");

        ItemFacts bin = Item("bin", 10);
        bin.IsShippingBin = true;
        Eq((long)RejectCode.ShippingBin, (long)Protection.Evaluate(bin, settings).Code, "shipping bin rejected");

        ItemFacts questContainer = Item("quest_container", 100);
        ItemFacts questChild = Item("quest_child", 10);
        questChild.IsQuestItem = true;
        questContainer.Children.Add(questChild);
        RejectReason questReason = Protection.Evaluate(questContainer, settings);
        Eq((long)RejectCode.QuestItem, (long)questReason.Code, "quest descendant rejects whole root");
        Eq("quest_child", questReason.ItemIdentifier, "reason reports blocking descendant");

        ItemFacts both = Item("both", 10);
        both.BehindLock = true;
        both.IsQuestItem = true;
        Eq((long)RejectCode.BehindLock, (long)Protection.Evaluate(both, settings).Code, "priority locked before quest");

        ItemFacts stolen = Item("stolen", 1000);
        stolen.IsStolen = true;
        stolen.StolenPercent = 100;
        Settings noStolen = Defaults();
        noStolen.AllowStolen = false;
        Eq((long)RejectCode.StolenDisabled, (long)Protection.Evaluate(stolen, noStolen).Code, "stolen disabled");
        Eq((long)RejectCode.None, (long)Verdict(stolen, settings), "stolen allowed by default");

        ItemFacts contraband = Item("contraband", 1000);
        contraband.IsContraband = true;
        Settings noContraband = Defaults();
        noContraband.AllowContraband = false;
        Eq((long)RejectCode.ContrabandDisabled, (long)Protection.Evaluate(contraband, noContraband).Code, "contraband disabled");
        Eq((long)RejectCode.None, (long)Verdict(contraband, settings), "contraband allowed by default");

        ItemFacts cycle = Item("cycle", 10);
        cycle.Children.Add(cycle);
        Eq((long)RejectCode.None, (long)Verdict(cycle, settings), "cycle tolerated");

        ItemFacts nestedBinRoot = Item("outer", 100);
        ItemFacts nestedBin = Item("nested_bin", 10);
        nestedBin.IsShippingBin = true;
        nestedBin.Children.Add(Item("nested_bin_child", 5));
        nestedBinRoot.Children.Add(nestedBin);
        Eq((long)RejectCode.ShippingBin, (long)Protection.Evaluate(nestedBinRoot, settings).Code, "nested shipping bin protects root");

        ItemFacts machine = Item("machine", 100);
        machine.IsProtectedItem = true;
        Eq((long)RejectCode.ProtectedItem, (long)Protection.Evaluate(machine, settings).Code, "machine protected");
        Eq("reason_protected_item", new RejectReason(RejectCode.ProtectedItem, machine).ReasonKey, "reason key protected");

        ItemFacts noValue = Item("no_value", 0);
        Eq("reason_no_value", new RejectReason(RejectCode.NoValue, noValue).ReasonKey, "reason key no value");

        Eq("reason_quest", new RejectReason(RejectCode.QuestItem, questChild).ReasonKey, "reason key quest");
        Eq("reason_locked", new RejectReason(RejectCode.BehindLock, locked).ReasonKey, "reason key locked");
        Eq("reason_stolen_off", new RejectReason(RejectCode.StolenDisabled, stolen).ReasonKey, "reason key stolen");
        Eq("reason_overflow", new RejectReason(RejectCode.ValueOverflow, ok).ReasonKey, "reason key overflow");
    }

    // =====================================================================
    private static BinSnapshot Bin(string id)
    {
        BinSnapshot snapshot = new BinSnapshot();
        snapshot.Bin = Item(id, 0);
        snapshot.Bin.IsShippingBin = true;
        return snapshot;
    }

    private static void TestPlanner()
    {
        Settings settings = Defaults();

        BinSnapshot bin1 = Bin("bin1");
        ItemFacts container = Item("crate", 100);
        container.Children.Add(Item("crate_child", 50));
        bin1.Contents.Add(container);
        NightPlan p1 = Planner.Build(new List<BinSnapshot> { bin1 }, settings, 0);
        Eq(1, p1.Sales.Count, "one sale");
        Eq(150, p1.Sales[0].NativeValue, "container value includes child");
        Eq(120, p1.Sales[0].Amount, "amount uses 0.8");
        Eq(120, p1.TotalAmount, "total amount");
        Eq("bin1", (string)((ItemFacts)p1.Sales[0].Bin).Key, "sale carries original bin");

        BinSnapshot ignoreBin = Bin("ignore_bin");
        ItemFacts ignoreRoot = Item("ignore_root", 100);
        ignoreRoot.IgnoreChildValue = true;
        ignoreRoot.Children.Add(Item("ignored_child", 50));
        ignoreBin.Contents.Add(ignoreRoot);
        NightPlan pIgnore = Planner.Build(new List<BinSnapshot> { ignoreBin }, settings, 0);
        Eq(100, pIgnore.Sales[0].NativeValue, "IGNORE_CHILD_VALUE respected");
        Eq(80, pIgnore.Sales[0].Amount, "IGNORE_CHILD_VALUE amount");

        // 顺序独立：两个箱子交换顺序结果完全一致。
        BinSnapshot a = Bin("bin_a");
        a.Contents.Add(MakeStolen("a_stolen", 1000, 100));
        BinSnapshot b = Bin("bin_b");
        b.Contents.Add(MakeStolen("b_stolen", 500, 50));
        NightPlan forward = Planner.Build(new List<BinSnapshot> { a, b }, settings, 150);
        NightPlan reversed = Planner.Build(new List<BinSnapshot> { b, a }, settings, 150);
        Eq(forward.TotalAmount, reversed.TotalAmount, "order independent amount");
        Eq(forward.TotalCrime, reversed.TotalCrime, "order independent crime");
        Eq(2, forward.Sales.Count, "order independent count");
        // rep150 减半：1000*100%*2*0.5=1000；500*50%*2*0.5=250
        Eq(1250, forward.TotalCrime, "crime snapshot rep150");

        // 嵌套/重复去重：同一物品实例出现在两个箱子里只结算一次。
        ItemFacts shared = Item("shared", 100);
        BinSnapshot d1 = Bin("dedupe_bin1");
        d1.Contents.Add(shared);
        BinSnapshot d2 = Bin("dedupe_bin2");
        d2.Contents.Add(shared);
        NightPlan deduped = Planner.Build(new List<BinSnapshot> { d1, d2 }, settings, 0);
        Eq(1, deduped.Sales.Count, "shared item counted once");
        Eq(80, deduped.TotalAmount, "shared item paid once");

        // 同一箱子被列出两次也只算一次。
        NightPlan doubleBins = Planner.Build(new List<BinSnapshot> { d1, d1 }, settings, 0);
        Eq(1, doubleBins.Sales.Count, "duplicate bin counted once");

        // 嵌套出货箱 -> 保留。
        BinSnapshot holder = Bin("holder");
        ItemFacts outer = Item("outer", 100);
        ItemFacts innerBin = Item("inner_bin", 0);
        innerBin.IsShippingBin = true;
        outer.Children.Add(innerBin);
        holder.Contents.Add(outer);
        NightPlan nested = Planner.Build(new List<BinSnapshot> { holder }, settings, 0);
        Eq(0, nested.Sales.Count, "nested bin not sold");
        Eq(1, nested.Retained.Count, "nested bin retained");
        Eq("reason_shipping_bin", nested.Retained[0].Reason.ReasonKey, "nested bin reason");

        // 任务后代 -> 整根保留。
        BinSnapshot questBin = Bin("quest_bin");
        ItemFacts questOuter = Item("quest_outer", 100);
        ItemFacts questItem = Item("quest_inner", 10);
        questItem.IsQuestItem = true;
        questOuter.Children.Add(questItem);
        questBin.Contents.Add(questOuter);
        NightPlan questPlan = Planner.Build(new List<BinSnapshot> { questBin }, settings, 0);
        Eq(0, questPlan.Sales.Count, "quest root not sold");
        Eq(1, questPlan.Retained.Count, "quest root retained");
        Eq("reason_quest", questPlan.Retained[0].Reason.ReasonKey, "quest reason");

        // 赃物顶层只计一次，后代不重复计罪。
        BinSnapshot stolenBin = Bin("stolen_bin");
        ItemFacts stolenCrate = MakeStolen("stolen_crate", 1000, 100);
        stolenCrate.Children.Add(MakeStolen("stolen_crate_child", 2000, 100));
        stolenBin.Contents.Add(stolenCrate);
        NightPlan stolenPlan = Planner.Build(new List<BinSnapshot> { stolenBin }, settings, 0);
        Eq(3000, stolenPlan.Sales[0].NativeValue, "stolen crate value includes children");
        Eq(3000, stolenPlan.Sales[0].StolenBasis, "stolen basis counted once at tree value");
        Eq(6000, stolenPlan.Sales[0].CrimeAmount, "crime 3000*2");

        // 普通容器里塞一个赃物子物品：价值全含，涉案只按该子物品整树。
        BinSnapshot mixedBin = Bin("mixed_bin");
        ItemFacts plainCrate = Item("plain_crate", 100);
        plainCrate.Children.Add(MakeStolen("stolen_inside", 1000, 100));
        mixedBin.Contents.Add(plainCrate);
        NightPlan mixed = Planner.Build(new List<BinSnapshot> { mixedBin }, settings, 0);
        Eq(1100, mixed.Sales[0].NativeValue, "mixed value");
        Eq(1000, mixed.Sales[0].StolenBasis, "mixed stolen basis only");
        Eq(2000, mixed.TotalCrime, "mixed crime");

        // 违禁品不套赃物公式：自己的基础、自己的类型、自己的倍率。
        BinSnapshot contrabandBin = Bin("contraband_bin");
        ItemFacts contra = Item("contra", 500);
        contra.IsContraband = true;
        contrabandBin.Contents.Add(contra);
        NightPlan contraPlan = Planner.Build(new List<BinSnapshot> { contrabandBin }, settings, 0);
        Eq(1, contraPlan.Sales.Count, "contraband sells by default");
        Eq(0, contraPlan.Sales[0].StolenBasis, "contraband is not stolen");
        Eq(500, contraPlan.Sales[0].ContrabandBasis, "contraband own basis");
        Eq(500, contraPlan.TotalCrime, "contraband default multiplier 1x");
        Eq("CONTRABAND_TRAFFICKING", contraPlan.Crimes[0].Type, "contraband own crime type");
        Eq((long)CrimeKind.Contraband, (long)contraPlan.Crimes[0].Kind, "contraband crime kind");

        // 违禁品不套赃物公式：即使把赃物倍率调到很高，违禁品仍按原版 1:1。
        Settings contrabandRich = Defaults();
        contrabandRich.StolenMultiplier = 5.0;
        NightPlan contraRich = Planner.Build(new List<BinSnapshot> { contrabandBin }, contrabandRich, 0);
        Eq(500, contraRich.TotalCrime, "contraband never borrows the stolen multiplier");

        // 违禁品开关关闭 -> 整根保留。
        Settings noContraband = Defaults();
        noContraband.AllowContraband = false;
        NightPlan contraBlocked = Planner.Build(new List<BinSnapshot> { contrabandBin }, noContraband, 0);
        Eq(0, contraBlocked.Sales.Count, "contraband disabled retains");
        Eq("reason_contraband_off", contraBlocked.Retained[0].Reason.ReasonKey, "contraband disabled reason");

        // 假贴标：全晚合计，1%，至少 1 最多 20。
        BinSnapshot misBin = Bin("mis_bin");
        ItemFacts mis1 = Item("mis1", 500);
        mis1.IsMislabelled = true;
        ItemFacts mis2 = Item("mis2", 500);
        mis2.IsMislabelled = true;
        misBin.Contents.Add(mis1);
        misBin.Contents.Add(mis2);
        NightPlan misPlan = Planner.Build(new List<BinSnapshot> { misBin }, settings, 0);
        Eq(800, misPlan.MislabelledAmount, "mislabel amount sums across bins");
        Eq(8, misPlan.MislabelPenalty, "mislabel penalty 1% total night");
        Eq(2, misPlan.MislabelledCount, "mislabel count");

        // 只有根节点的假贴标记参与归属（子物品不单独算一次）。
        BinSnapshot misChildBin = Bin("mis_child_bin");
        ItemFacts misRoot = Item("mis_root", 100);
        ItemFacts misChild = Item("mis_child", 900);
        misChild.IsMislabelled = true;
        misRoot.Children.Add(misChild);
        misChildBin.Contents.Add(misRoot);
        NightPlan misChildPlan = Planner.Build(new List<BinSnapshot> { misChildBin }, settings, 0);
        Eq(0, misChildPlan.MislabelledAmount, "child mislabel not counted separately");
        Eq(0, misChildPlan.MislabelPenalty, "child mislabel no penalty at root");

        // 关闭赃物允许 -> 保留而不是悄悄少给钱。
        Settings noStolen = Defaults();
        noStolen.AllowStolen = false;
        NightPlan blocked = Planner.Build(new List<BinSnapshot> { stolenBin }, noStolen, 0);
        Eq(0, blocked.Sales.Count, "stolen disabled retains");
        Eq("reason_stolen_off", blocked.Retained[0].Reason.ReasonKey, "stolen disabled reason");

        // 倍率可设置。
        Settings rich = Defaults();
        rich.SaleMultiplier = 2.0;
        NightPlan richPlan = Planner.Build(new List<BinSnapshot> { d1 }, rich, 0);
        Eq(200, richPlan.TotalAmount, "custom multiplier");

        // 零估值物品保留而不是白送。
        BinSnapshot zeroBin = Bin("zero_bin");
        zeroBin.Contents.Add(Item("zero_item", 0));
        NightPlan zeroPlan = Planner.Build(new List<BinSnapshot> { zeroBin }, settings, 0);
        Eq(0, zeroPlan.Sales.Count, "zero value retained");
        Eq("reason_no_value", zeroPlan.Retained[0].Reason.ReasonKey, "zero value reason");

        // 适配层提供原版整树估值时优先使用：后代不再手工求和。
        BinSnapshot vanillaBin = Bin("vanilla_bin");
        ItemFacts vanillaRoot = Item("vanilla_root", 100);
        vanillaRoot.Children.Add(Item("vanilla_child", 900));
        vanillaRoot.VanillaTreeValue = 1000;
        vanillaBin.Contents.Add(vanillaRoot);
        NightPlan vanillaPlan = Planner.Build(new List<BinSnapshot> { vanillaBin }, settings, 0);
        Eq(1000, vanillaPlan.Sales[0].NativeValue, "vanilla tree value preferred");
        Eq(800, vanillaPlan.Sales[0].Amount, "vanilla tree value amount");

        // 适配层给出的原版涉案构成：按类型合并；后代再标的赃物不重复计罪。
        BinSnapshot adapterBin = Bin("adapter_bin");
        ItemFacts adapterRoot = Item("adapter_root", 0);
        adapterRoot.VanillaTreeValue = 1000;
        adapterRoot.CrimeContributions.Add(new CrimeLine("FENCING", 100, CrimeKind.Stolen));
        adapterRoot.CrimeContributions.Add(new CrimeLine("DRUG_TRAFFICKING", 500, CrimeKind.Contraband));
        ItemFacts adapterChild = Item("adapter_child", 0);
        adapterChild.IsStolen = true;
        adapterChild.StolenPercent = 100;
        adapterChild.CrimeContributions.Add(new CrimeLine("FENCING", 999, CrimeKind.Stolen));
        adapterRoot.Children.Add(adapterChild);
        adapterBin.Contents.Add(adapterRoot);
        NightPlan adapterPlan = Planner.Build(new List<BinSnapshot> { adapterBin }, settings, 0);
        Eq(700, adapterPlan.TotalCrime, "adapter crime lines priced per kind");
        Eq(100, adapterPlan.Sales[0].StolenBasis, "adapter stolen basis");
        Eq(500, adapterPlan.Sales[0].ContrabandBasis, "adapter contraband basis");
        Eq(2, adapterPlan.Crimes.Count, "adapter keeps two crime types");
    }

    private static ItemFacts MakeStolen(string id, long value, int percent)
    {
        ItemFacts facts = Item(id, value);
        facts.IsStolen = true;
        facts.StolenPercent = percent;
        return facts;
    }

    // =====================================================================
    // 带故障注入的假世界：模拟真实适配层里“可能部分生效”的每一个副作用，
    // 并记录钱、货、标记的真实状态，供断言核对。
    private sealed class FakeWorld : ICommitWorld
    {
        public List<string> Calls = new List<string>();
        public List<string> Attached = new List<string>();
        public List<string> Returned = new List<string>();
        public List<string> PaidNames = new List<string>();
        public List<string> Destroyed = new List<string>();

        public List<string> FailRevalidate = new List<string>();
        public List<string> ThrowRevalidate = new List<string>();
        public List<string> FailDetachHard = new List<string>();
        public List<string> FailDetachPartial = new List<string>();
        public List<string> ThrowDetach = new List<string>();
        public List<string> FailVerify = new List<string>();
        public List<string> ThrowReturn = new List<string>();
        public List<string> FailReturnVerify = new List<string>();
        public List<string> ThrowPay = new List<string>();
        public List<string> ThrowDestroy = new List<string>();
        public int DestroyThrowTimes;
        public bool CrimeThrows;
        public bool MislabelThrows;
        public bool SettleThrows;

        public long Paid;
        public long Crime;
        public int Mislabel;
        public int SettledDay = -1;

        public FakeWorld()
        {
        }

        private bool Has(List<string> list, SaleEntry entry)
        {
            return list.Contains(entry.DisplayName);
        }

        public bool Revalidate(SaleEntry entry)
        {
            Calls.Add("revalidate:" + entry.DisplayName);
            if (Has(ThrowRevalidate, entry)) throw new InvalidOperationException("revalidate boom");
            return !Has(FailRevalidate, entry) && !Destroyed.Contains(entry.DisplayName);
        }

        public bool Detach(SaleEntry entry)
        {
            Calls.Add("detach:" + entry.DisplayName);
            if (Has(ThrowDetach, entry)) throw new InvalidOperationException("detach boom");
            if (Has(FailDetachHard, entry)) return false;
            if (Has(FailDetachPartial, entry))
            {
                // 模拟“Expel 已经把它摘下来，但返回值/后续回调出错”。
                Attached.Remove(entry.DisplayName);
                return false;
            }
            Attached.Remove(entry.DisplayName);
            return true;
        }

        public bool VerifyDetached(SaleEntry entry)
        {
            Calls.Add("verify:" + entry.DisplayName);
            return !Attached.Contains(entry.DisplayName) && !Has(FailVerify, entry);
        }

        public void ReturnToBin(SaleEntry entry)
        {
            Calls.Add("return:" + entry.DisplayName);
            Returned.Add(entry.DisplayName);
            if (Has(ThrowReturn, entry)) throw new InvalidOperationException("return boom");
            if (Has(FailReturnVerify, entry)) return;
            if (!Attached.Contains(entry.DisplayName)) Attached.Add(entry.DisplayName);
        }

        public bool VerifyReturned(SaleEntry entry)
        {
            Calls.Add("verifyreturn:" + entry.DisplayName);
            return Attached.Contains(entry.DisplayName) && !Has(FailReturnVerify, entry);
        }

        public void Pay(SaleEntry entry)
        {
            Calls.Add("pay:" + entry.DisplayName);
            if (Has(ThrowPay, entry)) throw new InvalidOperationException("pay boom");
            Paid += entry.Amount;
            PaidNames.Add(entry.DisplayName);
        }

        public void Destroy(SaleEntry entry)
        {
            Calls.Add("destroy:" + entry.DisplayName);
            if (Has(ThrowDestroy, entry) || DestroyThrowTimes > 0)
            {
                if (!Has(ThrowDestroy, entry)) DestroyThrowTimes--;
                throw new InvalidOperationException("destroy boom");
            }
            Destroyed.Add(entry.DisplayName);
        }

        public void ApplyCrime(IList<CrimeLine> crimes)
        {
            Calls.Add("crime");
            if (CrimeThrows) throw new InvalidOperationException("crime boom");
            for (int i = 0; i < crimes.Count; i++) Crime += crimes[i].Amount;
        }

        public void ApplyMislabel(int penalty)
        {
            Calls.Add("mislabel");
            if (MislabelThrows) throw new InvalidOperationException("mislabel boom");
            Mislabel += penalty;
        }

        public void MarkSettled(int day)
        {
            Calls.Add("settle");
            if (SettleThrows) throw new InvalidOperationException("settle boom");
            SettledDay = day;
        }
    }

    private static FakeWorld TwoItemWorld()
    {
        FakeWorld w = new FakeWorld();
        w.Attached.Add("item_a");
        w.Attached.Add("item_b");
        return w;
    }

    private static NightPlan TwoItemPlan()
    {
        Settings settings = Defaults();
        BinSnapshot bin = Bin("commit_bin");
        bin.Contents.Add(Item("item_a", 100));
        bin.Contents.Add(Item("item_b", 200));
        return Planner.Build(new List<BinSnapshot> { bin }, settings, 0);
    }

    private static void TestCommit()
    {
        // ---- 正常路径 -------------------------------------------------
        FakeWorld world = TwoItemWorld();
        CommitResult ok = Committer.Run(TwoItemPlan(), world, 7);
        Check(ok.Success, "commit success");
        Eq(240, ok.PaidAmount, "commit paid 100*0.8+200*0.8");
        Eq(2, ok.SoldCount, "commit sold count");
        Eq(7, world.SettledDay, "commit settled day");
        Check(ok.Settled, "commit reports settled");
        Check(!ok.RolledBack, "success is not a rollback");
        Eq(0, ok.Failures.Count, "success has no failure records");
        Eq("settle", world.Calls[world.Calls.Count - 1], "settle written last");
        Check(world.Calls.IndexOf("pay:item_a") > world.Calls.IndexOf("verify:item_b"), "pay after all verify");
        Check(world.Calls.IndexOf("destroy:item_a") > world.Calls.IndexOf("pay:item_b"), "destroy after all pay");
        Eq(0, world.Attached.Count, "success leaves nothing in bins");

        // ---- 取下直接失败 ---------------------------------------------
        FakeWorld detachFail = TwoItemWorld();
        detachFail.FailDetachHard.Add("item_b");
        CommitResult failed = Committer.Run(TwoItemPlan(), detachFail, 7);
        Check(!failed.Success && failed.RolledBack, "detach failure rolls back");
        Eq(0, failed.PaidAmount, "detach failure pays nothing");
        Eq(-1, detachFail.SettledDay, "detach failure writes no day");
        // 回滚清单包含“本次尝试”的条目（它可能已部分取下），ReturnToBin 需幂等；回滚按逆序执行。
        Eq(2, detachFail.Returned.Count, "detach failure returns earlier + current item");
        Eq("item_b", detachFail.Returned[0], "detach failure returns current item first");
        Eq("item_a", detachFail.Returned[1], "detach failure then returns older item");
        Check(detachFail.Calls.IndexOf("pay:item_a") < 0, "detach failure no pay at all");
        Eq(2, detachFail.Attached.Count, "detach failure leaves both items in bins");

        // ---- 部分取下（本次尝试已生效但报失败）必须被纳入回滚 -------
        FakeWorld partial = TwoItemWorld();
        partial.FailDetachPartial.Add("item_b");
        CommitResult partialResult = Committer.Run(TwoItemPlan(), partial, 7);
        Check(!partialResult.Success && partialResult.RolledBack, "partial detach rolls back");
        Check(partial.Returned.Contains("item_b"), "partial detach returns the failed item itself");
        Check(partial.Returned.Contains("item_a"), "partial detach returns earlier item");
        Eq(2, partial.Attached.Count, "partial detach restores both items");
        Eq(0, partialResult.PaidAmount, "partial detach pays nothing");
        Eq(0, partialResult.RollbackFailures.Count, "partial detach rollback verified");

        // ---- 取下抛异常 ------------------------------------------------
        FakeWorld throwDetach = TwoItemWorld();
        throwDetach.ThrowDetach.Add("item_b");
        CommitResult thrown = Committer.Run(TwoItemPlan(), throwDetach, 7);
        Check(!thrown.Success && thrown.RolledBack, "detach exception rolls back");
        Eq(2, throwDetach.Returned.Count, "detach exception returns earlier + current");
        Eq("detach", thrown.FailurePhase, "detach exception records phase");

        // ---- 回滚自身失败：不允许报告 RolledBack=true -----------------
        FakeWorld badReturn = TwoItemWorld();
        badReturn.FailDetachHard.Add("item_b");
        badReturn.ThrowReturn.Add("item_a");
        CommitResult badRollback = Committer.Run(TwoItemPlan(), badReturn, 7);
        Check(!badRollback.Success, "rollback failure is not success");
        Check(!badRollback.RolledBack, "rollback failure must not claim rolled back");
        Check(badRollback.RollbackFailures.Contains("item_a"), "rollback failure recorded by name");
        Eq(1, badRollback.RollbackFailures.Count, "only the failing item is reported");

        FakeWorld verifyReturnFail = TwoItemWorld();
        verifyReturnFail.FailDetachHard.Add("item_b");
        verifyReturnFail.FailReturnVerify.Add("item_a");
        CommitResult verifyBad = Committer.Run(TwoItemPlan(), verifyReturnFail, 7);
        Check(!verifyBad.RolledBack, "unverified return must not claim rolled back");
        Check(verifyBad.RollbackFailures.Contains("item_a"), "unverified return recorded");

        // ---- 验证失败整体回滚 -----------------------------------------
        FakeWorld verifyFail = TwoItemWorld();
        verifyFail.FailVerify.Add("item_a");
        CommitResult verifyFailed = Committer.Run(TwoItemPlan(), verifyFail, 7);
        Check(!verifyFailed.Success && verifyFailed.RolledBack, "verify failure rolls back");
        Eq(2, verifyFail.Returned.Count, "verify failure returns all");
        Eq(0, verifyFail.Paid, "verify failure pays nothing");

        // ---- 复核失败：零副作用 ---------------------------------------
        FakeWorld revalidateFail = TwoItemWorld();
        revalidateFail.FailRevalidate.Add("item_b");
        CommitResult revalidateFailed = Committer.Run(TwoItemPlan(), revalidateFail, 7);
        Check(!revalidateFailed.Success && !revalidateFailed.RolledBack, "revalidate failure aborts cleanly");
        Eq(0, revalidateFail.Calls.FindAll(delegate(string c) { return c.StartsWith("detach:"); }).Count, "revalidate failure detaches nothing");
        Eq(-1, revalidateFail.SettledDay, "revalidate failure writes no day");

        // ---- 付款中途失败：已付款的销毁、未付款的放回 ------------------
        FakeWorld payFail = TwoItemWorld();
        payFail.ThrowPay.Add("item_b");
        CommitResult payFailed = Committer.Run(TwoItemPlan(), payFail, 7);
        Check(!payFailed.Success, "pay failure is reported");
        Check(payFailed.PaymentStarted, "pay failure reports payment started");
        Check(!payFailed.RolledBack, "pay failure is not a clean rollback");
        Eq(80, payFailed.PaidAmount, "pay failure keeps only the first payment");
        Eq(1, payFailed.SoldCount, "pay failure destroys the paid item");
        Check(payFail.Destroyed.Contains("item_a"), "paid item is destroyed");
        Check(payFail.Returned.Contains("item_b"), "unpaid item returns to bin");
        Check(payFail.Attached.Contains("item_b"), "unpaid item really back in bin");
        Eq(-1, payFail.SettledDay, "partial payment writes no day marker");
        Eq("pay", payFailed.FailurePhase, "pay failure phase");

        // ---- 销毁瞬时失败：重试一次即可，钱货一致 ---------------------
        FakeWorld transientDestroy = TwoItemWorld();
        transientDestroy.DestroyThrowTimes = 1;
        CommitResult transient = Committer.Run(TwoItemPlan(), transientDestroy, 7);
        Check(transient.Success, "transient destroy failure retried successfully");
        Eq(240, transient.PaidAmount, "transient destroy keeps money");
        Eq(2, transient.SoldCount, "transient destroy counts both sold");

        // ---- 销毁持续失败：钱已付、如实报告、仍需标记结算 -------------
        FakeWorld destroyFail = TwoItemWorld();
        destroyFail.ThrowDestroy.Add("item_a");
        CommitResult destroyResult = Committer.Run(TwoItemPlan(), destroyFail, 7);
        Check(!destroyResult.Success, "destroy failure is reported");
        Eq(240, destroyResult.PaidAmount, "destroy failure keeps money");
        Eq(2, destroyResult.SoldCount, "destroy failure still counts sold");
        Check(destroyResult.Failures.Count > 0, "destroy failure has detail");
        Check(destroyResult.Settled, "destroy failure still marks settled (money paid)");
        Eq(7, destroyResult.SettledDay, "destroy failure settled day");

        // ---- 犯罪提交异常：钱货已处理，记账失败如实上报，不阻塞假标签 --
        Settings crimeSettings = Defaults();
        BinSnapshot crimeBin = Bin("crime_bin");
        ItemFacts stolen = MakeStolen("crime_stolen", 1000, 100);
        stolen.IsMislabelled = true;
        crimeBin.Contents.Add(stolen);
        NightPlan crimePlan = Planner.Build(new List<BinSnapshot> { crimeBin }, crimeSettings, 0);

        FakeWorld crimeWorld = TwoItemWorld();
        crimeWorld.CrimeThrows = true;
        CommitResult crimeResult = Committer.Run(crimePlan, crimeWorld, 1);
        Check(!crimeResult.Success, "crime failure is reported");
        Eq(800, crimeResult.PaidAmount, "crime failure still pays");
        Check(crimeResult.Settled, "crime failure still settles");
        Eq(8, crimeWorld.Mislabel, "mislabel still applied after crime failure");
        Eq("crime", crimeResult.FailurePhase, "crime failure phase");

        FakeWorld mislabelWorld = TwoItemWorld();
        mislabelWorld.MislabelThrows = true;
        CommitResult mislabelResult = Committer.Run(crimePlan, mislabelWorld, 1);
        Check(!mislabelResult.Success, "mislabel failure is reported");
        Eq(2000, mislabelWorld.Crime, "crime still applied before mislabel failure");
        Check(mislabelResult.Settled, "mislabel failure still settles");

        // ---- 写 day 标记失败：钱货已处理、报告未结算 -------------------
        FakeWorld settleWorld = TwoItemWorld();
        settleWorld.SettleThrows = true;
        CommitResult settleFailed = Committer.Run(TwoItemPlan(), settleWorld, 7);
        Check(!settleFailed.Success, "settle failure is reported");
        Check(!settleFailed.Settled, "settle failure reports not settled");
        Eq(0, settleFailed.SettledDay, "settle failure has no settled day");
        Eq(240, settleFailed.PaidAmount, "settle failure still pays money");

        // ---- 空夜：仍需要写 day，写失败要上报 -------------------------
        FakeWorld emptyWorld = new FakeWorld();
        NightPlan empty = Planner.Build(new List<BinSnapshot>(), Defaults(), 0);
        CommitResult emptyResult = Committer.Run(empty, emptyWorld, 3);
        Check(emptyResult.Success, "empty night succeeds");
        Eq(3, emptyWorld.SettledDay, "empty night still marks settled");

        FakeWorld emptyFailWorld = new FakeWorld();
        emptyFailWorld.SettleThrows = true;
        CommitResult emptyFail = Committer.Run(empty, emptyFailWorld, 3);
        Check(!emptyFail.Success, "empty night settle failure reported");
        Check(!emptyFail.Settled, "empty night settle failure not settled");
    }

    // 调试赠送状态机：失败补领、正常购买不算已赠、每局一次。
    private static void TestGrantPolicy()
    {
        Eq((long)GrantAction.None, (long)GrantPolicy.Decide(false, false, true, false), "disabled keeps pending untouched");
        Eq((long)GrantAction.Grant, (long)GrantPolicy.Decide(true, false, true, false), "pending grants");
        Eq((long)GrantAction.MarkDone, (long)GrantPolicy.Decide(true, false, true, true), "grant token bin marks done");
        Eq((long)GrantAction.MarkDone, (long)GrantPolicy.Decide(true, true, true, true), "token bin wins over pending");
        Eq((long)GrantAction.None, (long)GrantPolicy.Decide(true, true, false, false), "done does not grant again");
        Eq((long)GrantAction.None, (long)GrantPolicy.Decide(true, false, false, false), "normal load without pending grants nothing");

        // 场景：首次赠送因空间不足失败（pending=true，无 token 箱），
        // 玩家随后正常买了一个箱子（无 token）。补领仍应发生，且不会重复。
        bool done = false;
        bool pending = true;
        bool hasTokenBin = false;
        bool purchasedBinExists = true; // 正常购买的箱子没有赠送 token
        Check(purchasedBinExists, "scenario: a purchased bin exists but has no grant token");
        Eq((long)GrantAction.Grant, (long)GrantPolicy.Decide(true, done, pending, hasTokenBin), "purchased bin does not block pending grant");
        // 补领成功后写 done，之后不再赠送。
        done = true;
        pending = false;
        hasTokenBin = true;
        Eq((long)GrantAction.MarkDone, (long)GrantPolicy.Decide(true, done, pending, hasTokenBin), "token bin confirms done");
        Eq((long)GrantAction.None, (long)GrantPolicy.Decide(true, done, pending, false), "done blocks further grants");
    }

    // 两张待售条目都带涉案与假标签，用于验证"只按已付款子集记账"。
    private static NightPlan LedgerPlan()
    {
        Settings settings = Defaults();
        BinSnapshot bin = Bin("ledger_bin");
        ItemFacts a = Item("item_a", 0);
        a.VanillaTreeValue = 100;
        a.IsMislabelled = true;
        a.CrimeContributions.Add(new CrimeLine("FENCING", 100, CrimeKind.Stolen));
        ItemFacts b = Item("item_b", 0);
        b.VanillaTreeValue = 200;
        b.IsMislabelled = true;
        b.CrimeContributions.Add(new CrimeLine("DRUG_TRAFFICKING", 500, CrimeKind.Contraband));
        bin.Contents.Add(a);
        bin.Contents.Add(b);
        return Planner.Build(new List<BinSnapshot> { bin }, settings, 0);
    }

    private sealed class FakeLedger : IObligationLedger
    {
        public bool Result = true;
        public bool Throws;
        public int Calls;
        public List<SaleEntry> Received = new List<SaleEntry>();
        public SettlementLedger Snapshot;

        public bool PersistAndApply(IList<SaleEntry> paid, NightPlan plan, int day)
        {
            Calls++;
            if (Throws) throw new InvalidOperationException("ledger boom");
            Received = new List<SaleEntry>();
            for (int i = 0; i < paid.Count; i++) Received.Add(paid[i]);
            Snapshot = SettlementLedger.FromPaid(paid, day);
            return Result;
        }
    }

    private static void TestLedger()
    {
        NightPlan plan = LedgerPlan();
        Eq(240, plan.TotalAmount, "ledger plan amount");
        Eq(700, plan.TotalCrime, "ledger plan crime");
        Eq(2, plan.MislabelPenalty, "ledger plan mislabel");

        SettlementLedger full = SettlementLedger.FromPaid(plan.Sales, 5);
        Eq(5, full.Day, "ledger day");
        Eq(2, full.SoldCount, "ledger sold count");
        Eq(240, full.PaidAmount, "ledger paid amount");
        Eq(240, full.MislabelledAmount, "ledger mislabelled amount");
        Eq(2, full.Lines.Count, "ledger keeps one line per crime type");
        Eq(3, full.PendingCount, "pending counts two lines plus mislabel");
        Check(!full.AllApplied, "fresh ledger is not applied");
        Check(full.HasObligations, "fresh ledger has obligations");
        List<CrimeLine> remaining = full.RemainingCrimes();
        Eq(2, remaining.Count, "remaining crime lines");
        long remainingTotal = 0;
        for (int i = 0; i < remaining.Count; i++) remainingTotal += remaining[i].Amount;
        Eq(700, remainingTotal, "remaining crime amounts match plan");

        // 只付了第一件：金额、假标签、涉案都必须按这一件重算，不能沿用整单。
        List<SaleEntry> paidSubset = new List<SaleEntry>();
        paidSubset.Add(plan.Sales[0]);
        SettlementLedger subset = SettlementLedger.FromPaid(paidSubset, 5);
        Eq(1, subset.SoldCount, "subset sold count");
        Eq(80, subset.PaidAmount, "subset paid amount");
        Eq(80, subset.MislabelledAmount, "subset mislabelled amount");
        Eq(1, subset.MislabelPenalty, "subset mislabel recomputed from paid items only");
        Eq(1, subset.Lines.Count, "subset keeps only paid item crimes");
        Eq("FENCING", subset.Lines[0].Type, "subset crime type");
        Eq(200, subset.Lines[0].Amount, "subset crime amount");

        string text = full.Encode();
        SettlementLedger copy;
        Check(SettlementLedger.TryDecode(text, out copy), "ledger decode roundtrip");
        Eq(full.Day, copy.Day, "decode day");
        Eq(full.SoldCount, copy.SoldCount, "decode sold");
        Eq(full.PaidAmount, copy.PaidAmount, "decode paid");
        Eq(full.MislabelPenalty, copy.MislabelPenalty, "decode penalty");
        Eq(full.Lines.Count, copy.Lines.Count, "decode line count");
        Eq(full.Lines[0].Type, copy.Lines[0].Type, "decode type");
        Eq(full.Lines[0].Basis, copy.Lines[0].Basis, "decode basis");
        Eq(full.Lines[0].Amount, copy.Lines[0].Amount, "decode amount");

        full.Lines[0].State = LedgerState.Applied;
        SettlementLedger saved;
        Check(SettlementLedger.TryDecode(full.Encode(), out saved), "ledger decode applied state");
        Eq((long)LedgerState.Applied, (long)saved.Lines[0].State, "decode applied state");

        SettlementLedger bad;
        Check(!SettlementLedger.TryDecode(null, out bad), "decode null rejected");
        Check(!SettlementLedger.TryDecode("", out bad), "decode empty rejected");
        Check(!SettlementLedger.TryDecode("SB-LEDGER 2\nD|1|0|0|0|0|0\n", out bad), "decode wrong version rejected");
        Check(!SettlementLedger.TryDecode("SB-LEDGER 1\nD|1|0|0|0|0|0\nL|FENCING|0|1|1|9\n", out bad), "decode bad state rejected");
        Check(!SettlementLedger.TryDecode("SB-LEDGER 1\nD|1|0|0|0|0|0\nL|FENCING|0|1|1|2|7\n", out bad), "decode extra column rejected");

        // 至多一次：遗留 InFlight 一律视为已生效，避免重复扣。
        SettlementLedger state = SettlementLedger.FromPaid(plan.Sales, 5);
        state.Lines[0].State = LedgerState.Applied;
        state.MislabelState = LedgerState.Applied;
        Eq(1, state.PendingCount, "one pending crime line");
        Check(!state.ResolveInFlight(), "no in-flight to resolve");
        state.Lines[1].State = LedgerState.InFlight;
        Check(state.ResolveInFlight(), "in-flight resolved");
        Eq((long)LedgerState.Applied, (long)state.Lines[1].State, "in-flight treated as applied");
        Check(state.AllApplied, "all applied after resolution");
        Eq(0, state.RemainingCrimes().Count, "nothing remains after resolution");

        SettlementLedger mis = SettlementLedger.FromPaid(plan.Sales, 5);
        for (int i = 0; i < mis.Lines.Count; i++) mis.Lines[i].State = LedgerState.Applied;
        mis.MislabelState = LedgerState.InFlight;
        Check(mis.ResolveInFlight(), "in-flight mislabel resolved");
        Check(mis.AllApplied, "mislabel resolution completes ledger");
    }

    private static void TestCommitLedger()
    {
        NightPlan plan = LedgerPlan();

        // 正常：记账单拿到全部已付款条目，直接路径不再重复提交。
        FakeLedger ledger = new FakeLedger();
        FakeWorld world = TwoItemWorld();
        CommitResult ok = Committer.Run(plan, world, 9, ledger);
        Check(ok.Success, "ledger commit succeeds");
        Check(ok.ObligationsDone, "ledger obligations done");
        Eq(9, ok.SettledDay, "ledger commit settled");
        Eq(2, ledger.Received.Count, "ledger got all paid entries");
        Eq(9, ledger.Snapshot.Day, "ledger day passed through");
        Eq(240, ledger.Snapshot.PaidAmount, "ledger snapshot paid");
        Eq(0, world.Crime, "ledger path does not double-apply crime");
        Eq(0, world.Mislabel, "ledger path does not double-apply mislabel");

        // 记账单失败：钱货已处理，但不写 day 标记，留给恢复流程补提交。
        FakeLedger refusing = new FakeLedger();
        refusing.Result = false;
        CommitResult refused = Committer.Run(LedgerPlan(), TwoItemWorld(), 9, refusing);
        Check(!refused.Success, "ledger failure reported");
        Check(!refused.ObligationsDone, "ledger failure leaves obligations pending");
        Check(!refused.Settled, "ledger failure does not settle");
        Eq(240, refused.PaidAmount, "ledger failure keeps money paid");
        Eq(2, refused.PaidEntries.Count, "ledger failure records paid entries");
        Eq("ledger", refused.FailurePhase, "ledger failure phase");
        Eq("log_commit_ledger_failed", refused.FailureReasonKey, "ledger failure key");

        FakeLedger throwing = new FakeLedger();
        throwing.Throws = true;
        CommitResult thrownLedger = Committer.Run(LedgerPlan(), TwoItemWorld(), 9, throwing);
        Check(!thrownLedger.ObligationsDone, "ledger exception leaves obligations pending");
        Check(!thrownLedger.Settled, "ledger exception does not settle");

        // 第二件付款失败：记账单只按已付款的那一件生成，剩余条目回箱。
        FakeLedger partialLedger = new FakeLedger();
        FakeWorld partialWorld = TwoItemWorld();
        partialWorld.ThrowPay.Add("item_b");
        CommitResult partial = Committer.Run(LedgerPlan(), partialWorld, 9, partialLedger);
        Check(!partial.Success, "partial pay failure reported");
        Eq(1, partial.PaidEntries.Count, "partial pay keeps only paid entries");
        Eq(80, partial.PaidAmount, "partial pay amount");
        Eq(1, partialLedger.Received.Count, "ledger only sees paid subset");
        Eq(80, partialLedger.Snapshot.PaidAmount, "ledger subset paid amount");
        Eq(1, partialLedger.Snapshot.MislabelPenalty, "ledger subset mislabel recomputed");
        Eq(1, partialLedger.Snapshot.Lines.Count, "ledger subset crime lines");
        Eq(200, partialLedger.Snapshot.Lines[0].Amount, "ledger subset crime amount");
        Eq(9, partialLedger.Snapshot.Day, "ledger subset day");
        Check(!partial.Settled, "partial pay failure does not settle");
        Check(partialWorld.Returned.Contains("item_b"), "unpaid item returned to bin");
        Eq(0, partialWorld.Crime, "no whole-plan crime after partial pay");
    }
}
