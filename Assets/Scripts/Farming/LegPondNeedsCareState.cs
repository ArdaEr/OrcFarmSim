using OrcFarm.Inventory;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Care checkpoint reached. Growth continues while the pond waits for a feed.
    ///
    /// Quality rules:
    ///   - Feeding (consuming one FeedItem) raises quality Low → Normal and returns
    ///     the pond to Growing.
    ///   - Missing the feed window is a quality penalty only — quality stays Low and
    ///     growth continues toward ReadyToHarvest.
    ///
    /// Starvation rule:
    ///   - The neglect timer counts up whenever no interaction occurs. Any player
    ///     interaction (feed or otherwise) resets the timer to zero.
    ///   - If <see cref="LegPondConfig.NeglectDeadline"/> elapses with zero interaction,
    ///     the pond enters Starved.
    /// </summary>
    internal sealed class LegPondNeedsCareState : ILegPondState
    {
        private readonly ILegPondStateContext _ctx;

        internal LegPondNeedsCareState(ILegPondStateContext ctx) => _ctx = ctx;

        public bool CanInteract => true;
        public void OnEnter()   { }
        public void OnExit()    { }

        public void Update()
        {
            float delta = UnityEngine.Time.deltaTime;
            _ctx.IncrementGrowthTimer(delta);
            _ctx.IncrementStarvationTimer(delta);

            // Growth completion takes priority — harvest even if still in NeedsCare.
            if (_ctx.GrowthTimer >= _ctx.Config.GrowthDuration)
            {
                _ctx.TransitionTo(LegPondState.ReadyToHarvest);
                return;
            }

            // Total neglect — player never interacted since NeedsCare opened.
            if (_ctx.StarvationTimer >= _ctx.Config.NeglectDeadline)
                _ctx.TransitionTo(LegPondState.Starved);
        }

        public void OnInteract()
        {
            // Any interaction resets the neglect clock (req 6).
            _ctx.ResetStarvationTimer();

            // Without a FeedItem the player has acknowledged the pond but not fed it.
            // Quality stays Low; pond remains in NeedsCare and continues growing.
            if (!_ctx.TryConsumeItem(ItemType.FeedItem))
                return;

            _ctx.UpgradeQuality();
            _ctx.SetCareGiven();
            _ctx.TransitionTo(LegPondState.Growing);
        }
    }
}
