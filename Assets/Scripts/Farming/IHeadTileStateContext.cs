using OrcFarm.Core;
using OrcFarm.Inventory;

namespace OrcFarm.Farming
{
    /// <summary>
    /// The surface that tile state classes can call on their owning <see cref="HeadFarmTile"/>.
    /// States receive this interface — never the concrete tile type (§1.9, §7.1).
    /// </summary>
    internal interface IHeadTileStateContext
    {
        /// <summary>Tunable data for timers and spawn offsets.</summary>
        HeadFarmTileData Data { get; }

        /// <summary>Seconds elapsed in the current timed state (Covered or Growing).</summary>
        float Timer { get; }

        /// <summary>Advances the state timer by <paramref name="delta"/> seconds.</summary>
        void IncrementTimer(float delta);

        /// <summary>Resets the state timer to zero. Call in OnEnter of timed states.</summary>
        void ResetTimer();

        /// <summary>Resets the crop visual to its starting growth scale.</summary>
        void ResetGrowthVisual();

        /// <summary>Scales the crop visual to match current growth progress, clamped 0-1.</summary>
        void SetGrowthVisualProgress(float progress);

        /// <summary>True if the player's inventory contains at least one <paramref name="type"/>.</summary>
        bool HasItem(ItemType type);

        /// <summary>
        /// Attempts to remove one <paramref name="type"/> from the player's inventory.
        /// Returns false if the inventory is unassigned or lacks quantity.
        /// </summary>
        bool TryConsumeItem(ItemType type);

        /// <summary>
        /// Gets a pooled <see cref="HarvestedHead"/>, assigns its quality, and places it at
        /// a random XZ offset around the tile. Defined entirely on the tile — states do not
        /// need to know about the pool or spawn math.
        /// </summary>
        void SpawnHarvestedHead(OrcQuality quality);

        /// <summary>Requests a transition to <paramref name="next"/>.</summary>
        void TransitionTo(HeadTileState next);

        // ── Condition scores (Growing state only) ─────────────────────────────

        /// <summary>Feed health 0–1. Reaching 0 kills the crop.</summary>
        float FeedScore { get; }

        /// <summary>Water health 0–1. Tracked for quality — does not cause death.</summary>
        float WaterScore { get; }

        /// <summary>Care health 0–1. Tracked for quality — does not cause death.</summary>
        float CareScore { get; }

        /// <summary>
        /// Sets FeedScore to <paramref name="value"/> clamped 0–1 and fires
        /// <see cref="HeadFarmTile.OnConditionChanged"/>.
        /// </summary>
        void SetFeedScore(float value);

        /// <summary>
        /// Sets WaterScore to <paramref name="value"/> clamped 0–1 and fires
        /// <see cref="HeadFarmTile.OnConditionChanged"/>.
        /// </summary>
        void SetWaterScore(float value);

        /// <summary>
        /// Sets CareScore to <paramref name="value"/> clamped 0–1 and fires
        /// <see cref="HeadFarmTile.OnConditionChanged"/>.
        /// </summary>
        void SetCareScore(float value);

        /// <summary>Resets all three condition scores to 1.0. Call in Growing OnEnter.</summary>
        void ResetConditionScores();

        /// <summary>
        /// Accumulates per-frame influence data for trait evaluation at harvest.
        /// Called by <see cref="HeadTileGrowingState"/> each frame after score decay.
        /// </summary>
        void TrackGrowthFrame(float dt);

        /// <summary>True while the player is carrying any item. Used to gate Feed/Water actions.</summary>
        bool IsPlayerCarrying { get; }
    }
}
