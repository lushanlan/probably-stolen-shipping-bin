using System;
using System.Collections.Generic;

namespace ShippingBin.Core
{
    // ---------------------------------------------------------------------
    // 纯规则层的数据快照。
    //
    // GameItem 的运行时语义（tag、feature、父节点）由游戏侧适配层翻译成这里的
    // 纯数据，规则层永远不引用游戏程序集，因此可以在游戏外完整验证。
    //
    // 字段来源（全部已在 docs/il/*.md 中标注了 IL 证据）：
    //   Owned          GeneralHelper.IsItemEffectivelyOwned
    //   BehindLock     ITEM_BEHIND_LOCK_TAG / LOCKED_TAG / 双向锁库存
    //   IsQuestItem    原生 GeneralHelper.importantStoryItemIds（仅两个剧情物）
    //   IsProtectedItem 机器、文档、以及“IMPORTANT_TAG 但不是容器”的普通重要物
    //   IsContainer    CONTAINER_TAG
    //   IsStolen       STOLEN_TAG（原版是否拥有以未拥有表示）
    //   StolenPercent  STOLEN_VALUE_INT（原版百分比）
    //   IsContraband   CONTRABAND_ITEM_TAG
    //   IsMislabelled  VendingMachineHelper.IsMislabelled（feature 层判定）
    //   IgnoreChildValue IGNORE_CHILD_VALUE
    // ---------------------------------------------------------------------

    public enum CrimeKind
    {
        Stolen = 0,
        Contraband = 1
    }

    public enum RejectCode
    {
        None = 0,
        NullItem,
        ShippingBin,
        BehindLock,
        QuestItem,
        ProtectedItem,
        StolenDisabled,
        ContrabandDisabled,
        NotOwned,
        NoValue,
        ValueOverflow
    }

    /// <summary>单个物品的涉案金额构成：原版基础金额 + 原版犯罪类型。</summary>
    public sealed class CrimeLine
    {
        public string Type = string.Empty;
        public long Basis;
        public CrimeKind Kind;
        public long Amount;

        public CrimeLine()
        {
        }

        public CrimeLine(string type, long basis, CrimeKind kind)
        {
            Type = type;
            Basis = basis;
            Kind = kind;
        }

        public CrimeLine Clone()
        {
            CrimeLine copy = new CrimeLine(Type, Basis, Kind);
            copy.Amount = Amount;
            return copy;
        }
    }

    public sealed class ItemFacts
    {
        /// <summary>稳定身份（游戏侧传入 GameItem 引用），用于跨箱去重与防环。</summary>
        public object Key;
        public string Identifier = string.Empty;
        public string DisplayName = string.Empty;

        public bool Owned = true;
        public bool BehindLock;
        public bool IsShippingBin;
        public bool IsQuestItem;
        public bool IsProtectedItem;
        public bool IsContainer;
        public bool IsStolen;
        public int StolenPercent;
        public bool IsContraband;
        public bool IsMislabelled;
        public bool IgnoreChildValue;

        /// <summary>该节点自身的估值；适配层未提供整树估值时的递归求和基础。</summary>
        public long SelfValue;

        /// <summary>原版整树估值（GetCurrentValue）。小于 0 表示适配层未提供。</summary>
        public long VanillaTreeValue = -1;

        /// <summary>该节点产生的原版涉案金额构成（赃物按 STOLEN_VALUE_INT 百分比）。</summary>
        public List<CrimeLine> CrimeContributions = new List<CrimeLine>();

        public List<ItemFacts> Children = new List<ItemFacts>();
    }

    /// <summary>拒绝原因；ReasonKey 是本地化键，界面/夜间日志直接取键。</summary>
    public sealed class RejectReason
    {
        public RejectCode Code;
        public string ItemIdentifier = string.Empty;
        public string ItemName = string.Empty;
        public string ReasonKey = string.Empty;

        public RejectReason()
        {
        }

        public RejectReason(RejectCode code, ItemFacts item)
        {
            Code = code;
            if (item != null)
            {
                ItemIdentifier = item.Identifier == null ? string.Empty : item.Identifier;
                ItemName = item.DisplayName == null ? string.Empty : item.DisplayName;
            }
            ReasonKey = KeyFor(code);
        }

        public static string KeyFor(RejectCode code)
        {
            switch (code)
            {
                case RejectCode.NullItem: return "reason_null_item";
                case RejectCode.ShippingBin: return "reason_shipping_bin";
                case RejectCode.BehindLock: return "reason_locked";
                case RejectCode.QuestItem: return "reason_quest";
                case RejectCode.ProtectedItem: return "reason_protected_item";
                case RejectCode.StolenDisabled: return "reason_stolen_off";
                case RejectCode.ContrabandDisabled: return "reason_contraband_off";
                case RejectCode.NotOwned: return "reason_not_owned";
                case RejectCode.NoValue: return "reason_no_value";
                case RejectCode.ValueOverflow: return "reason_overflow";
                default: return "reason_unknown";
            }
        }
    }

    public static class Protection
    {
        /// <summary>返回 null 表示整个待售根（含全部后代）可以出售。</summary>
        public static RejectReason Evaluate(ItemFacts root, Settings settings)
        {
            if (root == null) return new RejectReason(RejectCode.NullItem, null);
            List<ItemFacts> visited = new List<ItemFacts>();
            return EvaluateNode(root, settings, visited);
        }

        private static RejectReason EvaluateNode(ItemFacts item, Settings settings, List<ItemFacts> visited)
        {
            if (item == null) return new RejectReason(RejectCode.NullItem, null);
            for (int i = 0; i < visited.Count; i++)
            {
                if (object.ReferenceEquals(visited[i], item)) return null;
            }
            visited.Add(item);

            RejectCode code = SingleNodeCode(item, settings);
            if (code != RejectCode.None) return new RejectReason(code, item);

            if (item.Children != null)
            {
                for (int i = 0; i < item.Children.Count; i++)
                {
                    RejectReason child = EvaluateNode(item.Children[i], settings, visited);
                    if (child != null) return child;
                }
            }
            return null;
        }

        /// <summary>
        /// 单节点判定顺序（越靠前优先级越高）：
        /// 空 -> 出货箱自身 -> 锁 -> 剧情物 -> 机器/文档/普通重要物
        /// -> 赃物开关 -> 违禁品开关 -> 非玩家物品。
        /// 容器本身不再被当作禁止项：容器是合法的待售单位，由后代判定决定整树去留。
        /// </summary>
        public static RejectCode SingleNodeCode(ItemFacts item, Settings settings)
        {
            if (item == null) return RejectCode.NullItem;
            if (item.IsShippingBin) return RejectCode.ShippingBin;
            if (item.BehindLock) return RejectCode.BehindLock;
            if (item.IsQuestItem) return RejectCode.QuestItem;
            if (item.IsProtectedItem) return RejectCode.ProtectedItem;
            if (item.IsStolen && !settings.AllowStolen) return RejectCode.StolenDisabled;
            if (item.IsContraband && !settings.AllowContraband) return RejectCode.ContrabandDisabled;
            if (!item.Owned && !item.IsStolen && !item.IsContraband) return RejectCode.NotOwned;
            return RejectCode.None;
        }
    }
}
