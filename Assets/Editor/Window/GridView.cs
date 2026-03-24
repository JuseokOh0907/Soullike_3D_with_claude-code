using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Renders the 2D grid preview inside a given screen Rect.
/// Pure rendering — no input handling, no data mutation.
///
/// Coordinate spaces
///   Window space  : absolute pixel position inside the EditorWindow.
///   Local space   : pixel position relative to the top-left of the view Rect.
///                   This is what GUI.BeginClip establishes.
///   Grid space    : integer (x, z) cell coordinate.
///
/// CellToPixelRect / TryPixelToCell convert between local space and grid space.
/// The caller (FieldBuilderWindow) converts window-space mouse positions to
/// local space before passing them to GridInputController.
///
/// Zoom / pan
///   _zoom        : uniform scale factor applied to CellPixelSize.
///   _panOffset   : local-space offset of the grid's (0,0) corner.
///   ZoomAround() : zooms while keeping the pivot point stationary.
///   FitToView()  : auto-fits the whole grid into the panel with padding.
///
/// Efficient rendering
///   • Every cell is culled against the view bounds before drawing.
///   • Coord labels only appear when cells are large enough to be legible.
///   • GUIStyles are initialised once on first Draw() call.
///   • The caller (FieldBuilderWindow) controls when Repaint() is invoked;
///     this class never triggers repaints itself.
/// </summary>
public class GridView
{
    // ── Public properties ─────────────────────────────────────────────────────

    /// <summary>Grid data to render. Null = show the "no data" placeholder.</summary>
    public FieldGridData Data
    {
        get => _data;
        set
        {
            if (_data == value) return;
            _data = value;
            // Reset view transform so the new grid isn't stuck at an old position.
            _zoom      = 1f;
            _panOffset = Vector2.zero;
        }
    }

    /// <summary>Selection state used for hover and selection highlights.</summary>
    public SelectionState Selection { get; set; }

    /// <summary>
    /// Base cell size in pixels at zoom = 1.
    /// Actual rendered size = CellPixelSize × Zoom.
    /// </summary>
    public float CellPixelSize { get; set; } = 32f;

    public float   Zoom      => _zoom;
    public Vector2 PanOffset => _panOffset;

    // ── State ─────────────────────────────────────────────────────────────────
    private FieldGridData _data;
    private float         _zoom      = 1f;
    private Vector2       _panOffset = Vector2.zero;
    private Vector2       _viewSize;          // set each Draw() call — used for culling
    private GUIStyle      _coordLabel;
    private GUIStyle      _placeholderLabel;
    private bool          _stylesReady;

    // ── Constants ─────────────────────────────────────────────────────────────
    private const float MIN_ZOOM          = 0.20f;
    private const float MAX_ZOOM          = 8f;
    private const float FIT_PADDING       = 0.88f;   // 88 % of panel = slight margin
    private const float LABEL_VISIBLE_PX  = 26f;     // minimum cell pixel size to show coords
    private const float BORDER_THICKNESS  = 2f;      // selection border base thickness

    // ── Colours ───────────────────────────────────────────────────────────────
    // Static because they never change — avoids per-draw allocation.

    private static readonly Color ColBackground    = new Color(0.16f, 0.16f, 0.16f);
    private static readonly Color ColHoverOverlay  = new Color(1f, 1f, 1f, 0.11f);
    private static readonly Color ColSelectBorder  = new Color(1f, 0.85f, 0f);
    private static readonly Color ColLockedOverlay = new Color(0.78f, 0.10f, 0.10f, 0.22f);
    private static readonly Color ColEmptyOverlay  = new Color(0f, 0f, 0f, 0.52f);
    private static readonly Color ColDoorFill      = new Color(0.08f, 0.08f, 0.08f);        // near-black void
    private static readonly Color ColDoorBorder    = new Color(1f, 0.65f, 0f, 0.90f);       // orange frame
    private static readonly Color ColRectOverlay   = new Color(0.3f, 0.6f, 1f, 0.18f);
    private static readonly Color ColRectBorder    = new Color(0.3f, 0.6f, 1f, 0.85f);
    private static readonly Color ColStatusBar     = new Color(0f, 0f, 0f, 0.45f);

    private static readonly Dictionary<BiomeType, Color> BiomeColors =
        new Dictionary<BiomeType, Color>
        {
            { BiomeType.None,      new Color(0.28f, 0.28f, 0.28f) },
            { BiomeType.Grassland, new Color(0.42f, 0.70f, 0.33f) },
            { BiomeType.Forest,    new Color(0.18f, 0.47f, 0.20f) },
            { BiomeType.Desert,    new Color(0.84f, 0.74f, 0.38f) },
            { BiomeType.Tundra,    new Color(0.70f, 0.84f, 0.90f) },
            { BiomeType.Rocky,     new Color(0.52f, 0.48f, 0.44f) },
            { BiomeType.Swamp,     new Color(0.32f, 0.50f, 0.28f) },
            { BiomeType.Custom,    new Color(0.62f, 0.38f, 0.62f) },
        };

    // ── Public: main draw entry point ─────────────────────────────────────────

    /// <summary>
    /// Draws the entire grid preview into <paramref name="viewRect"/>.
    /// Must be called from EditorWindow.OnGUI.
    /// </summary>
    public void Draw(Rect viewRect)
    {
        EnsureStyles();
        _viewSize = viewRect.size;

        // Background always drawn in window space (outside BeginClip)
        EditorGUI.DrawRect(viewRect, ColBackground);

        if (_data == null)
        {
            DrawPlaceholder(viewRect);
            return;
        }

        // All cell drawing happens in local (clipped) space
        GUI.BeginClip(viewRect);
        DrawAllCells();
        DrawRectSelectionOverlay();
        GUI.EndClip();

        DrawStatusBar(viewRect);
    }

    // ── Public: view transform ────────────────────────────────────────────────

    /// <summary>
    /// Scales the view so the entire grid fits inside <paramref name="viewRect"/>
    /// with a small padding margin, then centres it.
    /// </summary>
    public void FitToView(Rect viewRect)
    {
        if (_data == null) return;

        float zoomX = viewRect.width  / (_data.width  * CellPixelSize);
        float zoomY = viewRect.height / (_data.height * CellPixelSize);
        _zoom = Mathf.Clamp(Mathf.Min(zoomX, zoomY) * FIT_PADDING, MIN_ZOOM, MAX_ZOOM);

        CentreGrid(viewRect.size);
    }

    /// <summary>
    /// Zooms the view around <paramref name="pivotLocal"/> (local space).
    /// <paramref name="delta"/> > 0 zooms in, < 0 zooms out.
    /// </summary>
    public void ZoomAround(Vector2 pivotLocal, float delta)
    {
        float oldZoom = _zoom;
        _zoom = Mathf.Clamp(_zoom * (1f + delta), MIN_ZOOM, MAX_ZOOM);

        if (Mathf.Approximately(oldZoom, _zoom)) return;

        // Shift pan so the pixel under the pivot stays fixed
        float ratio = _zoom / oldZoom;
        _panOffset  = pivotLocal - (pivotLocal - _panOffset) * ratio;
    }

    /// <summary>Translates the view by <paramref name="delta"/> pixels (local space).</summary>
    public void Pan(Vector2 delta) => _panOffset += delta;

    // ── Public: coordinate conversion ─────────────────────────────────────────

    /// <summary>
    /// Returns the local-space pixel Rect for cell (x, z).
    /// The rect respects the current pan offset and zoom level.
    /// </summary>
    public Rect CellToPixelRect(int x, int z)
    {
        float size = CellPixelSize * _zoom;
        float gap  = Mathf.Max(1f, Mathf.Floor(size * 0.04f));   // ~4 % gap as grid line
        return new Rect(
            _panOffset.x + x * size,
            _panOffset.y + z * size,
            size - gap,
            size - gap);
    }

    public Rect CellToPixelRect(Vector2Int c) => CellToPixelRect(c.x, c.y);

    /// <summary>
    /// Converts <paramref name="localPixel"/> (local space) to a grid coordinate.
    /// Returns true if the resulting coordinate is within grid bounds.
    /// </summary>
    public bool TryPixelToCell(Vector2 localPixel, out Vector2Int coord)
    {
        if (_data == null) { coord = default; return false; }

        float size = CellPixelSize * _zoom;
        int   x    = Mathf.FloorToInt((localPixel.x - _panOffset.x) / size);
        int   z    = Mathf.FloorToInt((localPixel.y - _panOffset.y) / size);
        coord      = new Vector2Int(x, z);
        return _data.IsInBounds(x, z);
    }

    // ── Private: rendering ────────────────────────────────────────────────────

    private void DrawAllCells()
    {
        for (int z = 0; z < _data.height; z++)
        {
            for (int x = 0; x < _data.width; x++)
            {
                Rect cellRect = CellToPixelRect(x, z);

                // Visibility cull against local-space view bounds
                if (cellRect.xMax < 0f || cellRect.x > _viewSize.x) continue;
                if (cellRect.yMax < 0f || cellRect.y > _viewSize.y) continue;

                DrawCell(x, z, cellRect);
            }
        }
    }

    private void DrawCell(int x, int z, Rect rect)
    {
        var coord = new Vector2Int(x, z);
        var cell  = _data.GetCell(x, z);

        // ── Base fill (biome colour or profile preview colour) ────────────────
        Color baseColor = GetCellColor(cell);
        EditorGUI.DrawRect(rect, baseColor);

        // ── Status overlays ───────────────────────────────────────────────────
        if (cell != null)
        {
            // Door cell: Empty AND not Walkable — drawn as a distinct void with
            // an orange border so the entrance positions are immediately visible.
            bool isDoor = cell.HasFlag(CellFlags.Empty) && !cell.HasFlag(CellFlags.Walkable);
            if (isDoor)
            {
                EditorGUI.DrawRect(rect, ColDoorFill);
                float t = Mathf.Max(BORDER_THICKNESS, _zoom * 1.2f);
                DrawBorder(rect, ColDoorBorder, t);
                if (CellPixelSize * _zoom >= LABEL_VISIBLE_PX)
                    GUI.Label(rect, "door", _coordLabel);
                // Skip further overlays for door cells
                goto afterOverlays;
            }

            if (cell.HasFlag(CellFlags.Empty))
                EditorGUI.DrawRect(rect, ColEmptyOverlay);

            if (cell.HasFlag(CellFlags.Locked))
                EditorGUI.DrawRect(rect, ColLockedOverlay);
        }
        afterOverlays:;

        // ── Hover ─────────────────────────────────────────────────────────────
        if (Selection != null && Selection.IsHovered(coord))
            EditorGUI.DrawRect(rect, ColHoverOverlay);

        // ── Selection border ──────────────────────────────────────────────────
        if (Selection != null && Selection.IsSelected(coord))
        {
            float t = Mathf.Max(BORDER_THICKNESS, _zoom * 1.5f);
            DrawBorder(rect, ColSelectBorder, t);
        }

        // ── Coordinate label ──────────────────────────────────────────────────
        if (CellPixelSize * _zoom >= LABEL_VISIBLE_PX)
            GUI.Label(rect, $"{x},{z}", _coordLabel);
    }

    private void DrawRectSelectionOverlay()
    {
        if (Selection == null || !Selection.rectSelection.isActive) return;

        RectInt gridRect = Selection.rectSelection.GetRect();

        // Clamp to data bounds before drawing
        int xMin = Mathf.Max(0, gridRect.x);
        int yMin = Mathf.Max(0, gridRect.y);
        int xMax = Mathf.Min(_data.width  - 1, gridRect.xMax - 1);
        int yMax = Mathf.Min(_data.height - 1, gridRect.yMax - 1);
        if (xMax < xMin || yMax < yMin) return;

        Rect topLeft     = CellToPixelRect(xMin, yMin);
        Rect bottomRight = CellToPixelRect(xMax, yMax);
        float size       = CellPixelSize * _zoom;

        var overlayRect = new Rect(
            topLeft.x,
            topLeft.y,
            bottomRight.xMax - topLeft.x,
            bottomRight.yMax - topLeft.y);

        EditorGUI.DrawRect(overlayRect, ColRectOverlay);
        DrawBorder(overlayRect, ColRectBorder, 1.5f);
    }

    private void DrawStatusBar(Rect viewRect)
    {
        float barH  = EditorGUIUtility.singleLineHeight;
        var   barRect = new Rect(viewRect.x, viewRect.yMax - barH, viewRect.width, barH);
        EditorGUI.DrawRect(barRect, ColStatusBar);

        string left = Selection != null && Selection.HasSelection
            ? $"  {Selection.SelectionCount} cell(s) selected"
            : "  No selection";

        string right = $"Zoom {_zoom * 100f:F0}%  ";

        GUI.Label(new Rect(barRect.x, barRect.y, barRect.width * 0.65f, barH),
            left, EditorStyles.miniLabel);
        GUI.Label(new Rect(barRect.x + barRect.width * 0.65f, barRect.y, barRect.width * 0.35f, barH),
            right, _coordLabel);
    }

    private void DrawPlaceholder(Rect viewRect)
    {
        EnsureStyles();
        var centred = new Rect(viewRect.x, viewRect.y + viewRect.height * 0.4f,
                               viewRect.width, viewRect.height * 0.2f);
        GUI.Label(centred, "Assign a Grid Data asset\nto see the preview.", _placeholderLabel);
    }

    // ── Private: utilities ────────────────────────────────────────────────────

    private static Color GetCellColor(CellData cell)
    {
        if (cell == null) return BiomeColors[BiomeType.None];

        // Prefer the profile's preview colour when one is assigned
        if (cell.terrainProfile != null)
            return cell.terrainProfile.previewColor;

        // Fall back to biome colour
        return BiomeColors.TryGetValue(cell.biome, out Color c)
            ? c
            : BiomeColors[BiomeType.None];
    }

    private static void DrawBorder(Rect r, Color color, float t)
    {
        EditorGUI.DrawRect(new Rect(r.x,          r.y,           r.width, t),      color); // top
        EditorGUI.DrawRect(new Rect(r.x,          r.yMax - t,    r.width, t),      color); // bottom
        EditorGUI.DrawRect(new Rect(r.x,          r.y,           t, r.height),     color); // left
        EditorGUI.DrawRect(new Rect(r.xMax - t,   r.y,           t, r.height),     color); // right
    }

    private void CentreGrid(Vector2 viewSize)
    {
        float size  = CellPixelSize * _zoom;
        _panOffset  = new Vector2(
            (viewSize.x - _data.width  * size) * 0.5f,
            (viewSize.y - _data.height * size) * 0.5f);
    }

    /// <summary>
    /// Lazily initialises GUIStyles.
    /// Cannot be done in the constructor because EditorStyles requires the
    /// Unity Editor skin to be ready, which is only guaranteed during OnGUI.
    /// </summary>
    private void EnsureStyles()
    {
        if (_stylesReady) return;

        _coordLabel = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(1f, 1f, 1f, 0.55f) },
        };

        _placeholderLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            fontSize  = 11,
            wordWrap  = true,
            alignment = TextAnchor.MiddleCenter,
        };

        _stylesReady = true;
    }
}
