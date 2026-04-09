using UnityEngine;

namespace OrcFarm.Assembly
{
    /// <summary>
    /// Placeholder component marking an assembled orc world object.
    ///
    /// No logic for MVP — exists as a type anchor so future systems (trait display,
    /// worker assignment, bazaar listing) have a known component to query.
    ///
    /// Attach to any GameObject that visually represents an assembled orc in the scene.
    /// <see cref="AssemblyStation"/> repositions and activates this component's
    /// GameObject on each successful assembly.
    /// </summary>
    public sealed class AssembledOrc : MonoBehaviour
    {
        // Intentionally empty for MVP.
        // Future: assembled trait data, tendency, quality, worker-assignment hook.
    }
}
