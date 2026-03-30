using UnityEngine;

/// <summary>
/// Represents one straight wall run defined by two world-space endpoints.
///
/// Pure data — no scene references stored here.
/// WallBuilder reads this to instantiate scene objects.
///
/// Door placements are stored as segment indices along this wall run
/// (0 = first segment closest to Start).
/// </summary>
[System.Serializable]
public class WallSegmentData
{
    // ── Endpoints ─────────────────────────────────────────────────────────────

    /// <summary>World-space start point (snapped to grid).</summary>
    public Vector3 start;

    /// <summary>World-space end point (snapped to grid).</summary>
    public Vector3 end;

    // ── Door openings ─────────────────────────────────────────────────────────

    /// <summary>
    /// Indices of wall-segment slots that are replaced by doors.
    /// Each index is in [0, SegmentCount).
    /// </summary>
    public System.Collections.Generic.List<int> doorSlots = new();

    // ── Derived geometry ──────────────────────────────────────────────────────

    /// <summary>Direction from start to end (normalized).</summary>
    public Vector3 Direction => (end - start).normalized;

    /// <summary>Total straight-line distance from start to end.</summary>
    public float Length => Vector3.Distance(start, end);

    /// <summary>
    /// Number of wall-segment slots that fit along this wall run
    /// given <paramref name="segmentLength"/>.
    /// </summary>
    public int SegmentCount(float segmentLength)
    {
        if (segmentLength <= 0f) return 0;
        return Mathf.Max(1, Mathf.RoundToInt(Length / segmentLength));
    }

    /// <summary>
    /// World-space centre position for segment at index <paramref name="i"/>.
    /// </summary>
    public Vector3 SegmentCenter(int i, float segmentLength)
    {
        float t = (i + 0.5f) * segmentLength / Mathf.Max(Length, 0.001f);
        return Vector3.Lerp(start, end, Mathf.Clamp01(t));
    }

    /// <summary>True when segment <paramref name="i"/> contains a door.</summary>
    public bool HasDoor(int i) => doorSlots.Contains(i);

    /// <summary>Adds a door at segment index <paramref name="i"/> (ignored if already set).</summary>
    public void AddDoor(int i)
    {
        if (!doorSlots.Contains(i)) doorSlots.Add(i);
    }

    /// <summary>Removes the door at segment index <paramref name="i"/>.</summary>
    public void RemoveDoor(int i) => doorSlots.Remove(i);

    /// <summary>Toggles the door at segment index <paramref name="i"/>.</summary>
    public void ToggleDoor(int i)
    {
        if (HasDoor(i)) RemoveDoor(i);
        else            AddDoor(i);
    }
}
