namespace OrcFarm.Interaction
{
    /// <summary>
    /// Optional hook for farm targets that need to react when the player's farm focus changes.
    /// </summary>
    public interface IFarmFocusTarget
    {
        /// <summary>Called by FarmFocusDetector when this target gains or loses focus.</summary>
        void SetFarmFocused(bool focused);
    }
}
