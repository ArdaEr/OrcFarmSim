namespace OrcFarm.Farming
{
    /// <summary>
    /// Legs are fully grown. A Harvest interaction spawns the leg world object and
    /// immediately places it in the player's carry slot. The pond resets to Empty.
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
            _ctx.SpawnAndCarryLeg();
            _ctx.TransitionTo(LegPondState.Empty);
        }
    }
}
