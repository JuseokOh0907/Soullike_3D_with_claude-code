using UnityEditor;
using UnityEngine;

/// <summary>
/// Centralises every Undo.* and EditorUtility.SetDirty call in the tool.
///
/// Rules enforced here
///   • RecordDataChange must be called BEFORE a data mutation.
///   • MarkDataDirty must be called AFTER the same mutation.
///   • Brush strokes and multi-cell batch edits use BeginGroup / EndGroup
///     so the entire stroke collapses into a single Ctrl-Z step.
///   • Scene-object creation and destruction go through RegisterCreated /
///     DestroySceneObject so the scene undo stack stays consistent.
///
/// No other file in this project should call Undo.* or SetDirty directly.
/// </summary>
public static class FieldEditorUndo
{
    // ── Data asset mutations ──────────────────────────────────────────────────

    /// <summary>
    /// Call this immediately BEFORE modifying any field on <paramref name="data"/>.
    /// Unity snapshots the object state at this point.
    /// </summary>
    public static void RecordDataChange(FieldGridData data, string label) =>
        Undo.RecordObject(data, label);

    /// <summary>
    /// Call this immediately AFTER the mutation to mark the asset as unsaved.
    /// Without this, the change may not be written to disk.
    /// </summary>
    public static void MarkDataDirty(FieldGridData data) =>
        EditorUtility.SetDirty(data);

    // ── Undo grouping (for multi-step operations) ─────────────────────────────

    /// <summary>
    /// Names the current undo group. Call before a batch of RecordDataChange +
    /// mutation pairs (e.g. a brush stroke across many cells).
    /// </summary>
    public static void BeginGroup(string label) =>
        Undo.SetCurrentGroupName(label);

    /// <summary>
    /// Collapses all undo records created since the matching BeginGroup into a
    /// single Ctrl-Z step.
    /// </summary>
    public static void EndGroup() =>
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

    // ── Scene object lifecycle ────────────────────────────────────────────────

    /// <summary>Registers a newly instantiated scene object so it can be undone.</summary>
    public static void RegisterCreated(GameObject go, string label) =>
        Undo.RegisterCreatedObjectUndo(go, label);

    /// <summary>Destroys a scene object in an undo-safe way.</summary>
    public static void DestroySceneObject(GameObject go) =>
        Undo.DestroyObjectImmediate(go);
}
