using OrcFarm.Inventory;
using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Growth timer is running. Tracks FeedScore, WaterScore, and CareScore each frame.
    ///
    /// Feed / Water / Care rules:
    ///   • FeedScore decays at <see cref="HeadFarmTileData.FeedDecayRate"/> per second.
    ///     Hitting 0 immediately transitions to <see cref="HeadTileState.Dead"/>.
    ///   • WaterScore and CareScore decay independently but do not cause death.
    ///   • Player interaction (E) always restores CareScore by
    ///     <see cref="HeadFarmTileData.CareRestoreAmount"/>.
    ///   • If not carrying: has Fertilizer → consumes one → restores FeedScore to 1.
    ///     If not carrying: no Fertilizer → restores WaterScore to 1 (no item consumed).
    ///
    /// Auto-transitions to <see cref="HeadTileState.ReadyToHarvest"/> when the
    /// grow timer reaches <see cref="HeadFarmTileData.GrowDuration"/>.
    /// </summary>
    internal sealed class HeadTileGrowingState : IHeadTileState
    {
        private readonly IHeadTileStateContext _ctx;

        internal HeadTileGrowingState(IHeadTileStateContext ctx) => _ctx = ctx;

        // ── IHeadTileState ─────────────────────────────────────────────────────

        /// <summary>
        /// True — the player can feed, water, or care for the tile during growth.
        /// </summary>
        public bool CanInteract => true;

        /// <summary>
        /// Live score readout shown to the player while the crop is growing.
        /// Format: "Feed [F:x.x W:x.x C:x.x]"
        ///
        /// Note: this builds a new string each time it is queried. The HUD currently
        /// falls back to a generic prompt for HeadFarmTile, so this is not called
        /// per-frame. When the HUD is updated to surface this prompt, consider
        /// rebuilding only when score values change by ≥ 0.1 (one F1 step).
        /// </summary>
        public string InteractPrompt =>
            "Feed [F:" + _ctx.FeedScore.ToString("F1") +
            " W:" + _ctx.WaterScore.ToString("F1") +
            " C:" + _ctx.CareScore.ToString("F1") + "]";

        public void OnEnter()
        {
            _ctx.ResetTimer();
            _ctx.ResetConditionScores();
            _ctx.ResetGrowthVisual();
        }

        public void OnExit() { }

        public void Update()
        {
            float dt = Time.deltaTime;

            // Advance grow timer.
            _ctx.IncrementTimer(dt);
            _ctx.SetGrowthVisualProgress(_ctx.Timer / _ctx.Data.GrowDuration);

            // Decay all three scores — clamp handled inside each setter.
            _ctx.SetFeedScore (_ctx.FeedScore  - _ctx.Data.FeedDecayRate  * dt);
            _ctx.SetWaterScore(_ctx.WaterScore - _ctx.Data.WaterDecayRate * dt);
            _ctx.SetCareScore (_ctx.CareScore  - _ctx.Data.CareDecayRate  * dt);

            // Starvation check runs before grow-complete so a crop that hits 0 feed
            // on the same frame it would finish does not produce a harvest.
            if (_ctx.FeedScore <= 0f)
            {
                _ctx.TransitionTo(HeadTileState.Dead);
                return;
            }

            if (_ctx.Timer >= _ctx.Data.GrowDuration)
                _ctx.TransitionTo(HeadTileState.ReadyToHarvest);
        }

        public void OnInteract()
        {
            // Care is always restored regardless of what the player is carrying.
            _ctx.SetCareScore(Mathf.Min(1f, _ctx.CareScore + _ctx.Data.CareRestoreAmount));

            // Feed and Water actions are blocked while the player holds an item.
            if (_ctx.IsPlayerCarrying)
                return;

            // Feed: consume one Fertilizer, restore FeedScore to full.
            if (_ctx.TryConsumeItem(ItemType.Fertilizer))
            {
                _ctx.SetFeedScore(1f);
                return;
            }

            // Water: no item required, restore WaterScore to full.
            _ctx.SetWaterScore(1f);
        }
    }
}
