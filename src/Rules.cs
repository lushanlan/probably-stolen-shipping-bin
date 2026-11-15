using System;

namespace ShippingBin
{
    // Pure arithmetic only. Game integration and settlement are still pending.
    public static class Rules
    {
        public static double StolenFactor(int reputation, bool halfEnabled, bool exemptEnabled, double multiplier)
        {
            if (exemptEnabled && reputation >= 200) return 0;
            return multiplier * (halfEnabled && reputation >= 100 ? 0.5 : 1.0);
        }

        public static int MislabelPenalty(long proceeds, bool hasMislabelSale)
        {
            if (!hasMislabelSale) return 0;
            return (int)Math.Min(20, Math.Max(1, Math.Round(proceeds / 100.0, MidpointRounding.AwayFromZero)));
        }
    }
}
