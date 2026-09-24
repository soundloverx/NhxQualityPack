namespace NhxQualityPack
{
    internal enum QuickStackOutcome
    {
        NoContainersFound,
        NothingToDeposit,
        NoMatchingItems,
        NoRoomForItems,
        ItemsDeposited
    }

    internal readonly struct QuickStackResult
    {
        internal readonly QuickStackOutcome Outcome;
        internal readonly int TotalEligible;
        internal readonly int Deposited;

        internal QuickStackResult(QuickStackOutcome outcome, int totalEligible, int deposited)
        {
            Outcome = outcome;
            TotalEligible = totalEligible;
            Deposited = deposited;
        }
    }
}
