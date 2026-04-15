using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Soil is covered. Not interactable. Auto-transitions to Growing once
    /// <see cref="HeadFarmTileData.CoverDelay"/> seconds have elapsed.
    /// The timer is reset on <see cref="OnEnter"/> so each visit starts fresh.
    /// </summary>
    internal sealed class HeadTileCoveredState : IHeadTileState
    {
        private readonly IHeadTileStateContext _ctx;

        internal HeadTileCoveredState(IHeadTileStateContext ctx) => _ctx = ctx;

        public bool   CanInteract    => false;
        public string InteractPrompt => string.Empty;
        public void   OnEnter()      => _ctx.ResetTimer();
        public void   OnExit()       { }
        public void   OnInteract()   { }

        public void Update()
        {
            _ctx.IncrementTimer(Time.deltaTime);

            if (_ctx.Timer >= _ctx.Data.CoverDelay)
                _ctx.TransitionTo(HeadTileState.Growing);
        }
    }
}
