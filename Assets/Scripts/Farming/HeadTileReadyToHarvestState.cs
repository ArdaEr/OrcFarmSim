using OrcFarm.Core;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Growth completed. A Harvest interaction spawns a HarvestedHead from the pool
    /// and resets the tile to Empty.
    /// </summary>
    internal sealed class HeadTileReadyToHarvestState : IHeadTileState
    {
        private readonly IHeadTileStateContext _ctx;

        internal HeadTileReadyToHarvestState(IHeadTileStateContext ctx) => _ctx = ctx;

        public bool   CanInteract    => true;
        public string InteractPrompt => "Harvest head";
        public void   OnEnter()      => _ctx.SetGrowthVisualProgress(1f);
        public void   OnExit()       { }
        public void   Update()       { }

        public void OnInteract()
        {
            float avg = (_ctx.FeedScore + _ctx.WaterScore + _ctx.CareScore) / 3f;
            OrcQuality quality;
            if      (avg >= _ctx.Data.HighQualityThreshold)   quality = OrcQuality.High;
            else if (avg >= _ctx.Data.NormalQualityThreshold) quality = OrcQuality.Normal;
            else                                               quality = OrcQuality.Low;

            _ctx.SpawnHarvestedHead(quality);
            _ctx.TransitionTo(HeadTileState.Empty);
        }
    }
}
