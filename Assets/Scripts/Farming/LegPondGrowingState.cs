namespace OrcFarm.Farming
{
    /// <summary>
    /// Legs are actively growing. Decays per-fish FeedScore and CareScore each frame.
    /// A fish whose FeedScore hits zero dies; if all fish die the pond enters Starved.
    /// Opens the NeedsCare window at the configured checkpoint fraction.
    /// Completes growth when the full duration is reached.
    /// </summary>
    internal sealed class LegPondGrowingState : ILegPondState
    {
        private readonly ILegPondStateContext _ctx;

        internal LegPondGrowingState(ILegPondStateContext ctx) => _ctx = ctx;

        public bool CanInteract => false;
        public void OnEnter()   { }
        public void OnExit()    { }
        public void OnInteract(){ }

        public void Update()
        {
            float delta = UnityEngine.Time.deltaTime;
            _ctx.IncrementGrowthTimer(delta);

            bool allDead = _ctx.DecayFishScores(
                _ctx.Config.FeedDecayRate * delta,
                _ctx.Config.CareDecayRate * delta);

            if (allDead)
            {
                _ctx.TransitionTo(LegPondState.Starved);
                return;
            }

            if (!_ctx.CareGiven && _ctx.GrowthTimer >= _ctx.Config.CareCheckpointTime)
            {
                _ctx.TransitionTo(LegPondState.NeedsCare);
                return;
            }

            if (_ctx.GrowthTimer >= _ctx.Config.GrowthDuration)
                _ctx.TransitionTo(LegPondState.ReadyToHarvest);
        }
    }
}
