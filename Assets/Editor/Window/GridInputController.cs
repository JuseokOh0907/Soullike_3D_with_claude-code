using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Translates raw Unity Editor events into high-level grid interactions.
/// Pure input — never draws anything, never modifies CellData directly.
///
/// Responsibilities
///   • Convert pixel positions to grid coordinates via GridView.
///   • Manage hover state (writes directly to SelectionState.hoveredCell).
///   • Fire events upward to FieldBuilderWindow; the window decides what
///     to do with them (update SelectionState, call Repaint, etc.).
///
/// Controls
///   Left-click              Select cell (+ Ctrl = additive toggle)
///   Left-drag  (Rect mode)  Rectangle selection
///   Left-drag  (Brush mode) Brush stroke
///   Middle-drag / Alt+drag  Pan the view
///   Scroll wheel            Zoom around cursor
///   F                       Fit grid to view
///   Escape                  Clear selection
///   Delete / Backspace      Notify window that delete was requested
///
/// Drag disambiguation
///   A left-button drag that travels fewer than DragThreshold pixels before
///   lifting is treated as a click, not a drag.  This prevents accidental
///   rect-starts when the user just clicks on a cell.
/// </summary>
public class GridInputController
{
    // ── Dependencies (injected) ───────────────────────────────────────────────
    private readonly GridView      _view;
    private readonly SelectionState _selection;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the user left-clicks a valid cell.</summary>
    public event Action<Vector2Int> OnCellClicked;

    /// <summary>Fired when the hovered cell changes (null = cursor left the grid).</summary>
    public event Action<Vector2Int?> OnCellHovered;

    /// <summary>Fired when a rectangle drag is committed (mouse-up after rect drag).</summary>
    public event Action<RectInt> OnRectCommitted;

    /// <summary>
    /// Fired on each mouse-drag step in Brush mode.
    /// Payload is the cells in the current brush stamp (not the accumulated selection).
    /// </summary>
    public event Action<IEnumerable<Vector2Int>> OnBrushStroke;

    /// <summary>Fired once on MouseDown when Brush mode is active (stroke start).</summary>
    public event Action OnBrushStarted;

    /// <summary>Fired once on MouseUp after a brush stroke (stroke end).</summary>
    public event Action OnBrushEnded;

    /// <summary>Fired when Delete / Backspace is pressed.</summary>
    public event Action OnDeletePressed;

    /// <summary>Fired when F is pressed (request fit-to-view).</summary>
    public event Action OnFitRequested;

    // ── Configuration ─────────────────────────────────────────────────────────
    private const float ZoomSensitivity = 0.08f;   // per scroll-wheel tick
    private const float DragThreshold   = 4f;       // pixels before a drag begins

    // ── Private state ─────────────────────────────────────────────────────────
    private bool      _isPanning;
    private bool      _isDragging;          // left-button drag started inside view
    private Vector2   _dragStartLocal;      // local-space position where drag began
    private Vector2Int _dragStartCell;
    private bool      _dragBecameRect;      // drag exceeded threshold in Rect mode
    private bool      _brushStrokeActive;   // true between OnBrushStarted and OnBrushEnded

    // ── Constructor ───────────────────────────────────────────────────────────
    public GridInputController(GridView view, SelectionState selection)
    {
        _view      = view      ?? throw new ArgumentNullException(nameof(view));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Process <paramref name="e"/> for the given <paramref name="viewRect"/>.
    /// Call this from EditorWindow.OnGUI, skipping Layout and Used events.
    /// Returns true if the event was consumed (caller need not process it further).
    /// </summary>
    public bool HandleEvent(Event e, Rect viewRect)
    {
        if (e.type == EventType.Layout || e.type == EventType.Used) return false;

        // Local-space mouse position (relative to view top-left)
        Vector2 localMouse = e.mousePosition - viewRect.position;
        bool    inView     = viewRect.Contains(e.mousePosition);

        switch (e.type)
        {
            case EventType.MouseMove:
                return HandleMouseMove(e, localMouse, inView);

            case EventType.MouseDown:
                if (inView) return HandleMouseDown(e, localMouse);
                break;

            case EventType.MouseDrag:
                return HandleMouseDrag(e, localMouse);

            case EventType.MouseUp:
                return HandleMouseUp(e, localMouse);

            case EventType.ScrollWheel:
                if (inView) return HandleScrollWheel(e, localMouse);
                break;

            case EventType.KeyDown:
                if (inView || _isDragging) return HandleKeyDown(e);
                break;
        }

        return false;
    }

    // ── Private: per-event handlers ───────────────────────────────────────────

    private bool HandleMouseMove(Event e, Vector2 localMouse, bool inView)
    {
        Vector2Int? newHover = null;

        if (inView && _view.TryPixelToCell(localMouse, out Vector2Int cell))
            newHover = cell;

        if (newHover != _selection.hoveredCell)
        {
            _selection.hoveredCell = newHover;
            OnCellHovered?.Invoke(newHover);
            return true;   // signal that a repaint is needed
        }

        return false;
    }

    private bool HandleMouseDown(Event e, Vector2 localMouse)
    {
        // Middle button OR Alt + Left button → begin pan
        if (e.button == 2 || (e.button == 0 && e.alt))
        {
            _isPanning = true;
            e.Use();
            return true;
        }

        // Left button → begin potential drag / click
        if (e.button == 0)
        {
            _isDragging      = true;
            _dragBecameRect  = false;
            _dragStartLocal  = localMouse;
            _view.TryPixelToCell(localMouse, out _dragStartCell);

            if (_selection.mode == SelectionMode.Brush)
            {
                _brushStrokeActive = true;
                OnBrushStarted?.Invoke();
            }

            e.Use();
            return true;
        }

        return false;
    }

    private bool HandleMouseDrag(Event e, Vector2 localMouse)
    {
        // ── Pan ───────────────────────────────────────────────────────────────
        if (_isPanning && (e.button == 2 || (e.button == 0 && e.alt)))
        {
            _view.Pan(e.delta);
            e.Use();
            return true;
        }

        if (!_isDragging || e.button != 0) return false;

        float dragDist = Vector2.Distance(localMouse, _dragStartLocal);

        // ── Rect selection ────────────────────────────────────────────────────
        if (_selection.mode == SelectionMode.Rect)
        {
            if (dragDist >= DragThreshold)
            {
                _dragBecameRect = true;
                _selection.rectSelection.isActive = true;
                _selection.rectSelection.anchor   = _dragStartCell;

                if (_view.TryPixelToCell(localMouse, out Vector2Int cur))
                    _selection.rectSelection.current = cur;

                e.Use();
                return true;
            }
        }

        // ── Brush stroke ──────────────────────────────────────────────────────
        if (_selection.mode == SelectionMode.Brush)
        {
            if (_view.TryPixelToCell(localMouse, out Vector2Int brushCell))
            {
                var bounds = new RectInt(0, 0,
                    _view.Data?.width ?? 0, _view.Data?.height ?? 0);

                // Materialise stamp cells BEFORE ApplyBrush mutates selection,
                // so the event payload is exactly this step's footprint (not the
                // growing accumulated set).  This is what BrushPainter needs.
                var stamp = new List<Vector2Int>(
                    _selection.brush.GetAffectedCells(brushCell, bounds));

                _selection.ApplyBrush(brushCell, bounds);
                OnBrushStroke?.Invoke(stamp);
                e.Use();
                return true;
            }
        }

        return false;
    }

    private bool HandleMouseUp(Event e, Vector2 localMouse)
    {
        // ── End pan ───────────────────────────────────────────────────────────
        if (e.button == 2 || (e.button == 0 && _isPanning))
        {
            _isPanning = false;
            if (e.button == 2) e.Use();
            return true;
        }

        if (!_isDragging || e.button != 0) return false;
        _isDragging = false;

        if (_brushStrokeActive)
        {
            _brushStrokeActive = false;
            OnBrushEnded?.Invoke();
        }

        // ── Commit rect ───────────────────────────────────────────────────────
        if (_dragBecameRect && _selection.mode == SelectionMode.Rect)
        {
            _dragBecameRect = false;
            OnRectCommitted?.Invoke(_selection.rectSelection.GetRect());
            e.Use();
            return true;
        }

        // ── Single click ──────────────────────────────────────────────────────
        if (!_dragBecameRect && _view.TryPixelToCell(localMouse, out Vector2Int cell))
        {
            OnCellClicked?.Invoke(cell);
            e.Use();
            return true;
        }

        // Click on empty space → deselect
        if (!_dragBecameRect)
        {
            _selection.ClearAll();
            OnCellHovered?.Invoke(_selection.hoveredCell);
            e.Use();
            return true;
        }

        return false;
    }

    private bool HandleScrollWheel(Event e, Vector2 localMouse)
    {
        float delta = -e.delta.y * ZoomSensitivity;
        _view.ZoomAround(localMouse, delta);
        e.Use();
        return true;
    }

    private bool HandleKeyDown(Event e)
    {
        switch (e.keyCode)
        {
            case KeyCode.Escape:
                _selection.ClearAll();
                e.Use();
                return true;

            case KeyCode.Delete:
            case KeyCode.Backspace:
                OnDeletePressed?.Invoke();
                e.Use();
                return true;

            case KeyCode.F:
                OnFitRequested?.Invoke();
                e.Use();
                return true;

            // Quick mode switches
            case KeyCode.Alpha1:
                _selection.mode = SelectionMode.Single;
                e.Use();
                return true;

            case KeyCode.Alpha2:
                _selection.mode = SelectionMode.Rect;
                e.Use();
                return true;

            case KeyCode.Alpha3:
                _selection.mode = SelectionMode.Brush;
                e.Use();
                return true;
        }

        return false;
    }
}
