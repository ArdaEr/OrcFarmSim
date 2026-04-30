using System.Collections.Generic;
using OrcFarm.Carry;
using OrcFarm.Inventory;
using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Owns a rectangular grid of <see cref="HeadFarmTile"/> child GameObjects.
    ///
    /// In Edit Mode, changing <see cref="_rows"/> or <see cref="_columns"/> rebuilds
    /// child tiles from <see cref="_tilePrefab"/> and caches them in row-major order.
    /// On Awake this component validates that cached tile count matches
    /// <c>_rows × _columns</c>, then assigns each tile its row and column index
    /// (§8.1 / §8.3).
    ///
    /// Tile state machines will be added in the next task.
    /// </summary>
    public sealed class HeadFarmPlot : MonoBehaviour
    {
        [Tooltip("Prefab used to generate HeadFarmTile children in Edit Mode.")]
        [SerializeField] private HeadFarmTile _tilePrefab;

        [Tooltip("Number of rows in the tile grid.")]
        [Min(1)]
        [SerializeField] private int _rows = 3;

        [Tooltip("Number of columns in the tile grid.")]
        [Min(1)]
        [SerializeField] private int _columns = 3;

        [Tooltip("Local X/Z distance between generated tiles.")]
        [SerializeField] private Vector2 _tileSpacing = new Vector2(1f, 1f);

        [Header("Generated Tile References")]
        [Tooltip("Scene HarvestedHeadPool assigned to generated HeadFarmTile children.")]
        [SerializeField] private HarvestedHeadPool _headPool;

        [Tooltip("Scene PlayerInventory assigned to generated HeadFarmTile children.")]
        [SerializeField] private PlayerInventory _inventory;

        [Tooltip("Scene CarryController assigned to generated HeadFarmTile children.")]
        [SerializeField] private CarryController _carryController;

        private readonly List<HeadFarmTile> _tiles = new List<HeadFarmTile>();

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            CacheChildTiles();
            AssignTileSceneReferences();

            if (!AssignTileIndices())
            {
                enabled = false;
            }
        }

#if UNITY_EDITOR
        private bool _rebuildQueued;

        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            _rows        = Mathf.Max(1, _rows);
            _columns     = Mathf.Max(1, _columns);
            _tileSpacing = new Vector2(
                Mathf.Max(0f, _tileSpacing.x),
                Mathf.Max(0f, _tileSpacing.y));

            QueueEditorRebuild();
        }

        [ContextMenu("Rebuild Tile Grid")]
        private void RebuildTileGridInEditor()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning(
                    $"[HeadFarmPlot '{gameObject.name}'] Use this command in Edit Mode. " +
                    "Play Mode only validates the generated tile grid.", this);
                return;
            }

            RebuildTileGrid();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void QueueEditorRebuild()
        {
            if (_rebuildQueued)
                return;

            _rebuildQueued = true;
            UnityEditor.EditorApplication.delayCall += RebuildTileGridAfterValidate;
        }

        private void RebuildTileGridAfterValidate()
        {
            _rebuildQueued = false;

            if (this == null || Application.isPlaying)
                return;

            RebuildTileGrid();
        }

        private void RebuildTileGrid()
        {
            int expected = _rows * _columns;

            CacheChildTiles();

            if (_tilePrefab == null)
            {
                RefreshExistingTileGrid();
                UnityEditor.EditorUtility.SetDirty(this);
                return;
            }

            for (int i = _tiles.Count - 1; i >= expected; i--)
            {
                if (_tiles[i] != null)
                    UnityEditor.Undo.DestroyObjectImmediate(_tiles[i].gameObject);

                _tiles.RemoveAt(i);
            }

            while (_tiles.Count < expected)
            {
                HeadFarmTile tile = CreateTile(_tiles.Count);
                if (tile == null)
                    break;

                _tiles.Add(tile);
            }

            for (int i = 0; i < _tiles.Count; i++)
            {
                HeadFarmTile tile = _tiles[i];
                if (tile == null)
                    continue;

                int row = i / _columns;
                int col = i % _columns;

                tile.name = $"Tile {row + 1}-{col + 1}";
                tile.transform.SetSiblingIndex(i);
                tile.transform.localPosition = GetTileLocalPosition(row, col);
                tile.AssignSceneReferences(_headPool, _inventory, _carryController);
                tile.AssignIndex(row, col);
                UnityEditor.EditorUtility.SetDirty(tile);
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }

        private HeadFarmTile CreateTile(int index)
        {
            UnityEditor.PrefabAssetType prefabAssetType =
                UnityEditor.PrefabUtility.GetPrefabAssetType(_tilePrefab.gameObject);

            GameObject instance = prefabAssetType == UnityEditor.PrefabAssetType.NotAPrefab
                ? UnityEngine.Object.Instantiate(_tilePrefab.gameObject, transform)
                : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(
                    _tilePrefab.gameObject,
                    transform);

            if (instance == null)
                return null;

            UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Create Head Farm Tile");

            if (!instance.TryGetComponent(out HeadFarmTile tile))
            {
                Debug.LogError(
                    $"[HeadFarmPlot '{gameObject.name}'] Tile prefab '{_tilePrefab.name}' " +
                    "does not have a HeadFarmTile component.", this);
                UnityEditor.Undo.DestroyObjectImmediate(instance);
                return null;
            }

            int row = index / _columns;
            int col = index % _columns;

            instance.name = $"Tile {row + 1}-{col + 1}";
            instance.transform.localPosition = GetTileLocalPosition(row, col);
            instance.transform.localRotation = Quaternion.identity;

            return tile;
        }

        private void RefreshExistingTileGrid()
        {
            CacheChildTiles();
            AssignTileSceneReferences();

            if (_tiles.Count == _rows * _columns)
                AssignTileIndices();

            foreach (HeadFarmTile tile in _tiles)
            {
                if (tile != null)
                    UnityEditor.EditorUtility.SetDirty(tile);
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void CacheChildTiles()
        {
            _tiles.Clear();

            foreach (Transform child in transform)
            {
                if (child.TryGetComponent(out HeadFarmTile tile))
                    _tiles.Add(tile);
            }
        }

        private bool AssignTileIndices()
        {
            int expected = _rows * _columns;

            if (_tiles.Count != expected)
            {
                Debug.LogError(
                    $"[HeadFarmPlot '{gameObject.name}'] Tile count mismatch: " +
                    $"expected {expected} ({_rows} rows × {_columns} cols) " +
                    $"but {_tiles.Count} generated tiles were found. " +
                    "Assign a tile prefab and rebuild the grid in Edit Mode.", this);
                return false;
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
                            $"(row={r}, col={c}) is null. Rebuild the grid in Edit Mode.", this);
                        return false;
                    }

                    tile.AssignIndex(r, c);
                }
            }

            return true;
        }

        private void AssignTileSceneReferences()
        {
            foreach (HeadFarmTile tile in _tiles)
            {
                if (tile != null)
                    tile.AssignSceneReferences(_headPool, _inventory, _carryController);
            }
        }

        /// <summary>
        /// Returns the number of tiles currently in an active crop state
        /// (Seeded, Covered, Growing, or ReadyToHarvest).
        /// Called by <see cref="HeadFarmTile"/> during influence evaluation at harvest.
        /// </summary>
        public int GetActiveTileCount()
        {
            int count = 0;
            for (int i = 0; i < _tiles.Count; i++)
            {
                HeadTileState s = _tiles[i].State;
                if (s != HeadTileState.Empty && s != HeadTileState.Dead && s != HeadTileState.Tilled)
                    count++;
            }
            return count;
        }

        private Vector3 GetTileLocalPosition(int row, int column) =>
            new Vector3(column * _tileSpacing.x, 0f, -row * _tileSpacing.y);
    }
}
