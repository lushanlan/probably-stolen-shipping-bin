namespace ShippingBin.Core
{
    // 调试赠送的状态机（纯规则，可在游戏外测试）。
    //
    // 关键语义：
    //  * 只在“新开一局”时置 pending，普通读档不置位，因此不会每次读档都送。
    //  * 失败时保留 pending，之后的时机继续补领；成功才写 done。
    //  * 是否“已赠送”只看世界里有没有带赠送 token 的箱子，不看正常购买的箱子。
    public enum GrantAction
    {
        /// <summary>什么都不做（开关关闭，或没有待发放）。</summary>
        None = 0,

        /// <summary>本局已确认赠送过：写 done 标记并清 pending。</summary>
        MarkDone = 1,

        /// <summary>执行一次赠送尝试。</summary>
        Grant = 2
    }

    public static class GrantPolicy
    {
        public static GrantAction Decide(bool enabled, bool done, bool pending, bool hasGrantTokenBin)
        {
            if (!enabled)
            {
                // 关闭时不动标记：重新打开后仍可补领，也不会重复。
                return GrantAction.None;
            }
            if (hasGrantTokenBin)
            {
                // 世界里已经存在“本模组赠送”的箱子 -> 视为已赠送。
                return GrantAction.MarkDone;
            }
            if (done)
            {
                return GrantAction.None;
            }
            if (!pending)
            {
                return GrantAction.None;
            }
            return GrantAction.Grant;
        }
    }
}
