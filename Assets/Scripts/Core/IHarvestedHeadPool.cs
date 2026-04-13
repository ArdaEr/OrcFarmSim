using UnityEngine;

namespace OrcFarm.Core
{
    /// <summary>
    /// Contract for the HarvestedHead object pool (§3.4).
    ///
    /// Placed in OrcFarm.Core so that assemblies which cannot reference OrcFarm.Farming
    /// (e.g. OrcFarm.Carry, whose circular-dependency constraint prevents it from
    /// referencing Farming) can still depend on the pool abstraction.
    ///
    /// The concrete implementation is <see cref="OrcFarm.Farming.HarvestedHeadPool"/>.
    /// </summary>
    public interface IHarvestedHeadPool
    {
        /// <summary>
        /// Retrieves an inactive pooled head, positions it at <paramref name="position"/>
        /// with <paramref name="rotation"/>, activates it, and calls
        /// <see cref="IPoolable.OnGetFromPool"/> on its <see cref="IPoolable"/> component.
        /// Returns <c>null</c> and logs a warning in the editor if the pool is exhausted.
        /// </summary>
        GameObject Get(Vector3 position, Quaternion rotation);

        /// <summary>
        /// Returns <paramref name="head"/> to the pool: resets its state, re-parents it
        /// under the pool root, and deactivates it. Safe to call with <c>null</c> (no-op).
        /// </summary>
        void Return(GameObject head);
    }
}
