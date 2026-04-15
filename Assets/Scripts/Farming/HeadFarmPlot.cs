using System.Collections.Generic;
using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Owns a rectangular grid of <see cref="HeadFarmTile"/> child GameObjects.
    ///
    /// Tiles are pre-placed in the scene and serialized into <see cref="_tiles"/>;
    /// none are instantiated at runtime (§3.6).  On Awake this component validates
    /// that the serialized tile count matches <c>_rows × _columns</c>, then assigns
    /// each tile its row and column index (§8.1 / §8.3).
    ///
    /// Tile state machines will be added in the next task.
    /// </summary>
    public sealed class HeadFarmPlot : MonoBehaviour
    {
        [Tooltip("Number of rows in the tile grid.")]
        [Min(1)]
        [SerializeField] private int _rows = 3;

        [Tooltip("Number of columns in the tile grid.")]
        [Min(1)]
        [SerializeField] private int _columns = 3;

        [Tooltip("Pre-placed HeadFarmTile child GameObjects, ordered row-major " +
                 "(row 0 col 0, row 0 col 1, … row N col M).")]
        [SerializeField] private List<HeadFarmTile> _tiles = new List<HeadFarmTile>();

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            int expected = _rows * _columns;

            if (_tiles.Count != expected)
            {
                Debug.LogError(
                    $"[HeadFarmPlot '{gameObject.name}'] Tile count mismatch: " +
                    $"expected {expected} ({_rows} rows × {_columns} cols) " +
                    $"but {_tiles.Count} tiles are serialized. " +
                    "Assign all tiles in the Inspector.", this);
                enabled = false;
                return;
            }

            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _columns; c++)
                {
                    int          index = r * _columns + c;
                    HeadFarmTile tile  = _tiles[index];

                    if (tile == null)
                    {
                        Debug.LogError(
                            $"[HeadFarmPlot '{gameObject.name}'] Tile at list index {index} " +
                            $"(row={r}, col={c}) is null. Assign all tiles in the Inspector.", this);
                        enabled = false;
                        return;
                    }

                    tile.AssignIndex(r, c);
                }
            }
        }
    }
}
