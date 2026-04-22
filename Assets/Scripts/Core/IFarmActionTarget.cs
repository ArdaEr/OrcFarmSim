namespace OrcFarm.Core
{
    /// <summary>
    /// Implemented by any world object that can receive Feed, Water, and Care actions
    /// from <see cref="FarmFocusDetector"/>.
    ///
    /// The three action methods (OnFeedAction, OnWaterAction, OnCareAction) live on this
    /// interface — not a separate one — because they are the complete action surface of a
    /// farm target. FarmFocusDetector needs only one cast per focused object.
    ///
    /// GetActionContext() is called every frame; implementations must not allocate.
    /// </summary>
    public interface IFarmActionTarget
    {
        /// <summary>
        /// Returns the set of actions available this frame. Called every Update by
        /// FarmFocusDetector — must return a value-type snapshot with no heap allocation.
        /// </summary>
        FarmActionContext GetActionContext();

        /// <summary>Called by FarmFocusDetector when F is pressed and ShowFeed is true.</summary>
        void OnFeedAction();

        /// <summary>Called by FarmFocusDetector when W is pressed and ShowWater is true.</summary>
        void OnWaterAction();

        /// <summary>Called by FarmFocusDetector when C is pressed and ShowCare is true.</summary>
        void OnCareAction();
    }
}
