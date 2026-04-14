using OrcFarm.Workers;
using UnityEngine;

namespace OrcFarm.Assembly
{
    /// <summary>
    /// Placeholder component marking an assembled orc world object.
    ///
    /// Exposes <see cref="Quality"/> so the sell system can determine sale price.
    /// Quality is set by <see cref="AssemblyStation"/> immediately after spawning,
    /// based on the quality of the parts used in assembly.
    ///
    /// Attach to any GameObject that visually represents an assembled orc in the scene.
    /// <see cref="AssemblyStation"/> instantiates this from a prefab on each assembly.
    /// </summary>
    public sealed class AssembledOrc : MonoBehaviour
    {
        /// <summary>
        /// Quality tier of this orc. Readable by the sell system to determine price.
        /// Set by <see cref="AssemblyStation"/> via <see cref="SetQuality"/> after spawn.
        /// </summary>
        public OrcQuality Quality { get; private set; } = OrcQuality.Normal;

        /// <summary>
        /// Assigns the quality tier. Called by <see cref="AssemblyStation"/> immediately
        /// after instantiating this orc from the prefab.
        /// </summary>
        public void SetQuality(OrcQuality quality)
        {
            Quality = quality;
        }
    }
}
