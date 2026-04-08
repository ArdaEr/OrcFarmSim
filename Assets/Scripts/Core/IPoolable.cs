namespace OrcFarm.Core
{
    /// <summary>
    /// Contract for any object that participates in an object pool (§3.5).
    ///
    /// Implementations must guarantee that <see cref="ResetState"/> leaves the
    /// object in exactly the same condition as a freshly instantiated prefab,
    /// preventing stale-data bugs across pool reuses.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>Called by the pool immediately after the object is retrieved for use.</summary>
        void OnGetFromPool();

        /// <summary>Called by the pool immediately before the object is deactivated and stored.</summary>
        void OnReturnToPool();

        /// <summary>Resets all runtime state to the initial post-instantiation values.</summary>
        void ResetState();
    }
}
