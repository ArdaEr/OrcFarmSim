namespace OrcFarm.Farming
{
    /// <summary>
    /// Legs are fully grown. The player harvests one leg per E interaction.
    ///
    /// Each interaction takes the next alive fish, calculates its quality from condition
    /// scores against the pond's BaseQuality, spawns a HarvestedLeg, and immediately
    /// places it in the player's carry slot (dropping whatever was carried first).
    ///
    /// Dead fish are skipped — only alive fish produce a leg.
    /// After all alive fish are harvested the pond transitions to Empty.
    /// </summary>
    internal sealed class LegPondReadyToHarvestState : ILegPondState
    {
        private readonly ILegPondStateContext _ctx;

        internal LegPondReadyToHarvestState(ILegPondStateContext ctx) => _ctx = ctx;

        public bool CanInteract => true;
        public void OnEnter()   { }
        public void OnExit()    { }
        public void Update()    { }

        public void OnInteract()
        {
            _ctx.HarvestNextLeg();

            if (_ctx.AliveRemainingFishCount == 0)
                _ctx.TransitionTo(LegPondState.Empty);
        }
    }
}
