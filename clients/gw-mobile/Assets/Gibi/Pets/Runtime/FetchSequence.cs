namespace Gibi.Pets
{
    /// <summary>
    /// Deterministic phases of one fetch. Presentation and world positions stay in
    /// PetController; this class owns only legal ordering so it remains EditMode-testable.
    /// </summary>
    public enum FetchStage
    {
        Idle,
        Outbound,
        Pickup,
        Returning,
        Drop,
    }

    public enum FetchTransition
    {
        None,
        StartPickup,
        StartReturn,
        StartDrop,
        Completed,
    }

    public sealed class FetchSequence
    {
        public FetchStage Stage { get; private set; } = FetchStage.Idle;
        public int CompletedCount { get; private set; }

        public bool Begin()
        {
            if (Stage != FetchStage.Idle) return false;
            Stage = FetchStage.Outbound;
            return true;
        }

        public FetchTransition ReachedTarget()
        {
            switch (Stage)
            {
                case FetchStage.Outbound:
                    Stage = FetchStage.Pickup;
                    return FetchTransition.StartPickup;
                case FetchStage.Returning:
                    Stage = FetchStage.Drop;
                    return FetchTransition.StartDrop;
                default:
                    return FetchTransition.None;
            }
        }

        public FetchTransition ActionFinished()
        {
            switch (Stage)
            {
                case FetchStage.Pickup:
                    Stage = FetchStage.Returning;
                    return FetchTransition.StartReturn;
                case FetchStage.Drop:
                    Stage = FetchStage.Idle;
                    CompletedCount++;
                    return FetchTransition.Completed;
                default:
                    return FetchTransition.None;
            }
        }

        public bool Cancel()
        {
            if (Stage == FetchStage.Idle) return false;
            Stage = FetchStage.Idle;
            return true;
        }
    }
}
