namespace OrcFarm.Farming
{
    /// <summary>
    /// Care checkpoint reached. Fish scores continue decaying at the same rates as Growing.
    /// Per-fish starvation death continues — any fish whose FeedScore hits 0 dies;
    /// if all fish die the pond enters Starved.
    ///
    /// The window runs for <see cref="LegPondConfig.NeedsCareDuration"/> seconds.
    /// Feed (F) and Care (C) work identically to Growing via the existing action methods on
    /// <see cref="LegPond"/>. The E key is blocked (CanInteract is false).
    ///
    /// When the window closes the pond returns to Growing with the growth timer preserved.
    /// <see cref="ILegPondStateContext.SetCareGiven"/> is called on exit so the Growing
    /// checkpoint check does not re-trigger immediately.
    /// </summary>
    internal sealed class LegPondNeedsCareState : ILegPondState
    {
        private readonly ILegPondStateContext _ctx;

        internal LegPondNeedsCareState(ILegPondStateContext ctx) => _ctx = ctx;

        public bool CanInteract => false;
        public void OnEnter()    { }
        public void OnExit()     { }
        public void OnInteract() { }

        public void Update()
        {
            float delta = UnityEngine.Time.deltaTime;
            _ctx.IncrementStarvationTimer(delta);

            bool allDead = _ctx.DecayFishScores(
                _ctx.Config.FeedDecayRate * delta,
                _ctx.Config.CareDecayRate * delta);

            if (allDead)
            {
                _ctx.TransitionTo(LegPondState.Starved);
                return;
            }

            if (_ctx.StarvationTimer >= _ctx.Config.NeedsCareDuration)
            {
                _ctx.SetCareGiven();
                _ctx.TransitionTo(LegPondState.Growing);
            }
        }
    }
}
