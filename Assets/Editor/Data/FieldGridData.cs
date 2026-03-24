using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The root data asset for one field layout.
/// Serialized to disk as a .asset file; one file = one field configuration.
///
/// Design notes
/// ────────────
/// • ScriptableObject so it is a proper Unity asset:
///     - Lives in the Project window, survives domain reloads
///     - Undo.RecordObject / EditorUtility.SetDirty work correctly
///     - Can be assigned to the editor window via an ObjectField
///
/// • Cells are stored in a flat List&lt;CellData&gt; because Unity does not
///   serialize 2D arrays natively. Index = z * width + x.
///   All public access goes through GetCell / SetCell to hide this detail.
///
/// • Resize preserves existing cell data at matching coordinates (top-left origin).
///   Cells that fall outside the new bounds are discarded; new cells get defaults.
///
/// Create via: right-click Project window → Field Builder → Grid Data
/// </summary>
[CreateAssetMenu(menuName = "Field Builder/Grid Data", fileName = "NewFieldGridData")]
public class FieldGridData : ScriptableObject
{
    // ── Grid dimensions ───────────────────────────────────────────────────────
    [Header("Grid")]
    [Min(1)] public int   width    = 5;
    [Min(1)] public int   height   = 5;
    [Min(0.1f)] public float tileSize = 10f;

    // ── Global defaults ───────────────────────────────────────────────────────
    [Header("Defaults")]
    [Tooltip("Used for any cell whose terrainProfile is null.")]
    public TerrainProfile    defaultTerrainProfile;

    [Tooltip("Used for any cell whose vegetProfile is null.")]
    public VegetationProfile defaultVegetProfile;

    // ── Generation ────────────────────────────────────────────────────────────
    [Header("Generation")]
    [Tooltip("Base seed. Each cell derives its own seed from this unless overridden.")]
    public int    globalSeed    = 42;

    [Tooltip("Name of the root GameObject created in the scene.")]
    public string sceneRootName = "Field";

    // ── Internal storage ──────────────────────────────────────────────────────
    [SerializeField]
    private List<CellData> _cells = new();

    // ── Indexers ──────────────────────────────────────────────────────────────
    /// <summary>Returns the cell at grid coordinate (x, z). Null if out of bounds.</summary>
    public CellData this[int x, int z]         => GetCell(x, z);
    /// <summary>Returns the cell at grid coordinate. Null if out of bounds.</summary>
    public CellData this[Vector2Int coord]      => GetCell(coord.x, coord.y);

    // ── Public API ────────────────────────────────────────────────────────────
    /// <summary>Returns the cell at (x, z), or null if out of bounds.</summary>
    public CellData GetCell(int x, int z)
    {
        if (!IsInBounds(x, z)) return null;
        return _cells[FlatIndex(x, z)];
    }

    /// <summary>
    /// Replaces the cell at (x, z). The coord field of <paramref name="cell"/>
    /// is updated to match to keep data consistent.
    /// Does nothing if the coordinate is out of bounds.
    /// </summary>
    public void SetCell(int x, int z, CellData cell)
    {
        if (!IsInBounds(x, z) || cell == null) return;
        cell.coord = new Vector2Int(x, z);
        _cells[FlatIndex(x, z)] = cell;
    }

    public bool IsInBounds(int x, int z)       => x >= 0 && x < width && z >= 0 && z < height;
    public bool IsInBounds(Vector2Int coord)    => IsInBounds(coord.x, coord.y);

    /// <summary>Read-only access to the full flat cell list (for generation passes).</summary>
    public IReadOnlyList<CellData> Cells => _cells;

    // ── Initialization ────────────────────────────────────────────────────────
    /// <summary>
    /// Populates the cell list to match the current width × height.
    /// Call this once after creating the asset or changing dimensions manually.
    /// </summary>
    public void Initialize()
    {
        _cells = new List<CellData>(width * height);
        for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
                _cells.Add(CreateDefaultCell(x, z));
    }

    // ── Resize ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Resizes the grid to <paramref name="newWidth"/> × <paramref name="newHeight"/>.
    /// Existing cells are preserved at matching (x, z) coordinates (top-left origin).
    /// New cells receive default values; cells outside the new bounds are discarded.
    ///
    /// Caller is responsible for Undo.RecordObject before calling this.
    /// </summary>
    public void Resize(int newWidth, int newHeight)
    {
        newWidth  = Mathf.Max(1, newWidth);
        newHeight = Mathf.Max(1, newHeight);

        var newCells = new List<CellData>(newWidth * newHeight);

        for (int z = 0; z < newHeight; z++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                // Preserve existing cell data when the coordinate still fits
                CellData existing = (x < width && z < height) ? GetCell(x, z) : null;
                newCells.Add(existing ?? CreateDefaultCell(x, z));
            }
        }

        width  = newWidth;
        height = newHeight;
        _cells = newCells;
    }

    // ── Utility ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Resets all cells to defaults without changing grid dimensions.
    /// Caller is responsible for Undo.RecordObject before calling this.
    /// </summary>
    public void ResetAllCells()
    {
        for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
                _cells[FlatIndex(x, z)] = CreateDefaultCell(x, z);
    }

    /// <summary>
    /// Resets a single cell to its default state.
    /// Caller is responsible for Undo.RecordObject before calling this.
    /// </summary>
    public void ResetCell(int x, int z)
    {
        if (!IsInBounds(x, z)) return;
        _cells[FlatIndex(x, z)] = CreateDefaultCell(x, z);
    }

    /// <summary>
    /// Computes the world-space position for the centre of cell (x, z).
    /// Y is determined by the cell's heightOffset.
    /// </summary>
    public Vector3 CellToWorldPosition(int x, int z)
    {
        CellData cell = GetCell(x, z);
        float halfW = (width  - 1) * tileSize * 0.5f;
        float halfH = (height - 1) * tileSize * 0.5f;
        float y     = cell != null ? cell.heightOffset : 0f;
        return new Vector3(x * tileSize - halfW, y, z * tileSize - halfH);
    }

    public Vector3 CellToWorldPosition(Vector2Int coord) =>
        CellToWorldPosition(coord.x, coord.y);

    // ── Unity callbacks ───────────────────────────────────────────────────────
    private void OnEnable()
    {
        // Ensure the list is always the right size after deserialization
        // (guards against manual dimension edits in the Inspector).
        if (_cells == null || _cells.Count != width * height)
            RebuildMissingCells();
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    private int FlatIndex(int x, int z) => z * width + x;

    private CellData CreateDefaultCell(int x, int z) => new CellData
    {
        coord        = new Vector2Int(x, z),
        heightOffset = 0f,
        rotationY    = 0f,
        scale        = 1f,
        flags        = CellFlags.Walkable,
        biome        = BiomeType.Grassland,
        randomSeed   = -1,
    };

    /// <summary>
    /// Called from OnEnable: fills any missing entries after a dimension mismatch
    /// without discarding existing data.
    /// </summary>
    private void RebuildMissingCells()
    {
        int expected = width * height;
        if (_cells == null) _cells = new List<CellData>(expected);

        // Grow
        while (_cells.Count < expected)
        {
            int i = _cells.Count;
            int x = i % width;
            int z = i / width;
            _cells.Add(CreateDefaultCell(x, z));
        }

        // Shrink
        if (_cells.Count > expected)
            _cells.RemoveRange(expected, _cells.Count - expected);

        // Re-stamp coords (cheap; guards against corruption)
        for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
                if (_cells[FlatIndex(x, z)] != null)
                    _cells[FlatIndex(x, z)].coord = new Vector2Int(x, z);
    }
}
