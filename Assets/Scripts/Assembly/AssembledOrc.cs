using OrcFarm.Workers;
using UnityEngine;

namespace OrcFarm.Assembly
{
    /// <summary>
    /// Placeholder component marking an assembled orc world object.
    ///
    /// Exposes <see cref="Quality"/> so the sell system can determine sale price.
    /// Quality defaults to Normal; a future sprint will set it based on assembly
    /// inputs (head trait + body-part combination).
    ///
    /// Attach to any GameObject that visually represents an assembled orc in the scene.
    /// <see cref="AssemblyStation"/> instantiates this from a prefab on each assembly.
    /// </summary>
    public sealed class AssembledOrc : MonoBehaviour
    {
        /// <summary>
        /// Quality tier of this orc. Readable by the sell system to determine price.
        /// Defaults to Normal until assembly logic assigns it.
        /// </summary>
        public OrcQuality Quality { get; } = OrcQuality.Normal;
    }
}
