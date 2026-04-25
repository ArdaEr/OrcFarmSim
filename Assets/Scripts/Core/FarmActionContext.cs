namespace OrcFarm.Core
{
    /// <summary>
    /// Immutable per-frame snapshot of which farm actions are available for a focused tile.
    /// Each button has an independent <c>Visible</c> flag (show/hide the button entirely) and
    /// an <c>Active</c> flag (enabled vs. greyed-out while visible).
    ///
    /// Returned by <see cref="IFarmActionTarget.GetActionContext"/> every frame — readonly struct
    /// for zero heap allocation (§3.1).
    ///
    /// <c>ShowFeed</c>, <c>ShowWater</c>, <c>ShowCare</c> are kept as computed properties
    /// so <see cref="FarmFocusDetector"/> key-routing logic compiles without changes.
    /// The three-parameter constructor preserves backward compatibility for existing
    /// <c>HeadFarmTile</c> callers — visible implies active in that path.
    /// </summary>
    public readonly struct FarmActionContext
    {
        // ── Per-button visible / active ────────────────────────────────────────

        /// <summary>Feed button is rendered. May be greyed when <see cref="FeedActive"/> is false.</summary>
        public bool FeedVisible  { get; }

        /// <summary>Feed action is available — responds to the F key.</summary>
        public bool FeedActive   { get; }

        /// <summary>Water button is rendered. May be greyed when <see cref="WaterActive"/> is false.</summary>
        public bool WaterVisible { get; }

        /// <summary>Water action is available — responds to the W key.</summary>
        public bool WaterActive  { get; }

        /// <summary>Care button is rendered. May be greyed when <see cref="CareActive"/> is false.</summary>
        public bool CareVisible  { get; }

        /// <summary>Care action is available — responds to the C key.</summary>
        public bool CareActive   { get; }

        // ── Computed — backward compatibility for FarmFocusDetector ───────────

        /// <summary>True when Feed is both visible and active. Used by FarmFocusDetector for key routing.</summary>
        public bool ShowFeed  => FeedVisible  && FeedActive;

        /// <summary>True when Water is both visible and active. Used by FarmFocusDetector for key routing.</summary>
        public bool ShowWater => WaterVisible && WaterActive;

        /// <summary>True when Care is both visible and active. Used by FarmFocusDetector for key routing.</summary>
        public bool ShowCare  => CareVisible  && CareActive;

        /// <summary>True when at least one button is visible. Drives panel root visibility.</summary>
        public bool HasAny => FeedVisible || WaterVisible || CareVisible;

        /// <summary>Empty context — all buttons hidden. Returned when no tile is focused.</summary>
        public static readonly FarmActionContext None = default;

        // ── Constructors ───────────────────────────────────────────────────────

        /// <summary>
        /// Full constructor — specify visible and active state independently per button.
        /// Use this for tiles that show all buttons but grey out unavailable actions.
        /// </summary>
        public FarmActionContext(
            bool feedVisible,  bool feedActive,
            bool waterVisible, bool waterActive,
            bool careVisible,  bool careActive)
        {
            FeedVisible  = feedVisible;
            FeedActive   = feedActive;
            WaterVisible = waterVisible;
            WaterActive  = waterActive;
            CareVisible  = careVisible;
            CareActive   = careActive;
        }

        /// <summary>
        /// Backward-compatible constructor — visible implies active for each button.
        /// Existing <c>HeadFarmTile.GetActionContext</c> callers use this path.
        /// </summary>
        public FarmActionContext(bool showFeed, bool showWater, bool showCare)
            : this(showFeed, showFeed, showWater, showWater, showCare, showCare) { }
    }
}
