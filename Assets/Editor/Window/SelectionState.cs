using System.Collections.Generic;
using UnityEngine;

// ── Enums ─────────────────────────────────────────────────────────────────────

public enum SelectionMode    { Single, Rect, Brush }
public enum BrushShape       { Square, Circle }
public enum BrushOperation   { Paint, Erase, Fill }

// ── RectSelectionState ────────────────────────────────────────────────────────

/// <summary>
/// Tracks an in-progress rectangle drag.
/// Owned by SelectionState; written directly by GridInputController.
/// </summary>
public class RectSelectionState
{
    public bool       isActive;   // true while the mouse button is held
    public Vector2Int anchor;     // cell where the drag started
    public Vector2Int current;    // cell the cursor is over right now

    /// <summary>Returns the grid-space rect described by anchor and current (inclusive).</summary>
    public RectInt GetRect()
    {
        int xMin = Mathf.Min(anchor.x, current.x);
        int yMin = Mathf.Min(anchor.y, current.y);
        int w    = Mathf.Abs(current.x - anchor.x) + 1;
        int h    = Mathf.Abs(current.y - anchor.y) + 1;
        return new RectInt(xMin, yMin, w, h);
    }
}

// ── BrushSettings ─────────────────────────────────────────────────────────────

/// <summary>
/// Configuration for the paint-brush selection mode.
/// radius 0 = single cell  |  1 = 3×3  |  2 = 5×5  …
/// </summary>
public class BrushSettings
{
    public int            radius    = 0;
    public BrushShape     shape     = BrushShape.Square;
    public BrushOperation operation = BrushOperation.Paint;

    /// <summary>
    /// Returns every cell affected by this brush centred on <paramref name="center"/>,
    /// clipped to <paramref name="gridBounds"/>.
    /// Pure geometry — no scene or data dependency.
    /// </summary>
    public IEnumerable<Vector2Int> GetAffectedCells(Vector2Int center, RectInt gridBounds)
    {
        for (int dz = -radius; dz <= radius; dz++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (shape == BrushShape.Circle && dx * dx + dz * dz > radius * radius)
                    continue;

                var cell = new Vector2Int(center.x + dx, center.y + dz);
                if (gridBounds.Contains(cell))
                    yield return cell;
            }
        }
    }
}

// ── SelectionState ────────────────────────────────────────────────────────────

/// <summary>
/// Runtime-only selection state owned by FieldBuilderWindow.
/// Not serialized — it does not survive domain reload by design.
///
/// Responsibilities
///   • Tracks which cells are selected (any count).
///   • Tracks which cell is hovered (at most one).
///   • Holds sub-state objects for rect and brush modes.
///   • All mutations go through the methods below so future logic
///     (events, validation) has a single place to hook into.
///
/// Who writes what
///   • hoveredCell  — written directly by GridInputController (low-level cursor tracking).
///   • selectedCells — written via SelectSingle / CommitRect / ApplyBrush,
///                     called by FieldBuilderWindow in response to controller events.
/// </summary>
public class SelectionState
{
    // ── Core state ────────────────────────────────────────────────────────────
    private readonly HashSet<Vector2Int> _selected = new();

    public Vector2Int?         hoveredCell;
    public SelectionMode       mode          = SelectionMode.Single;
    public RectSelectionState  rectSelection = new();
    public BrushSettings       brush         = new();

    // ── Queries ───────────────────────────────────────────────────────────────
    public bool IsSelected(Vector2Int c)                     => _selected.Contains(c);
    public bool IsHovered (Vector2Int c)                     => hoveredCell == c;
    public bool HasSelection                                 => _selected.Count > 0;
    public int  SelectionCount                               => _selected.Count;
    public IReadOnlyCollection<Vector2Int> SelectedCells     => _selected;

    /// <summary>Returns the single selected cell, or null if zero or multiple are selected.</summary>
    public Vector2Int? SingleSelectedCell =>
        _selected.Count == 1 ? (Vector2Int?)GetFirstSelected() : null;

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Selects <paramref name="cell"/>.
    /// If <paramref name="additive"/> is false the previous selection is cleared first.
    /// Toggling: clicking an already-selected cell while additive deselects it.
    /// </summary>
    public void SelectSingle(Vector2Int cell, bool additive)
    {
        if (!additive)
        {
            _selected.Clear();
            _selected.Add(cell);
            return;
        }

        // Toggle
        if (!_selected.Add(cell))
            _selected.Remove(cell);
    }

    /// <summary>
    /// Commits a completed rectangle drag and selects all cells inside it.
    /// Clears existing selection first unless <paramref name="additive"/> is true.
    /// Also marks rectSelection.isActive = false.
    /// </summary>
    public void CommitRect(RectInt rect, bool additive)
    {
        if (!additive)
            _selected.Clear();

        for (int y = rect.y; y < rect.y + rect.height; y++)
            for (int x = rect.x; x < rect.x + rect.width; x++)
                _selected.Add(new Vector2Int(x, y));

        rectSelection.isActive = false;
    }

    /// <summary>
    /// Applies the brush at <paramref name="center"/> within <paramref name="gridBounds"/>.
    /// Paint adds cells; Erase removes them; Fill selects the whole grid.
    /// </summary>
    public void ApplyBrush(Vector2Int center, RectInt gridBounds)
    {
        if (brush.operation == BrushOperation.Fill)
        {
            CommitRect(gridBounds, additive: false);
            return;
        }

        foreach (var cell in brush.GetAffectedCells(center, gridBounds))
        {
            if (brush.operation == BrushOperation.Erase)
                _selected.Remove(cell);
            else
                _selected.Add(cell);
        }
    }

    public void Deselect(Vector2Int cell) => _selected.Remove(cell);

    public void ClearAll()
    {
        _selected.Clear();
        rectSelection.isActive = false;
    }

    // ── Private ───────────────────────────────────────────────────────────────
    private Vector2Int GetFirstSelected()
    {
        foreach (var c in _selected) return c;
        return default;
    }
}
