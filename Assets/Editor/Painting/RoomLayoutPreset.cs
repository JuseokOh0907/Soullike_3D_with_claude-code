using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies a souls-like room layout to a FieldGridData in one call.
///
/// Layout rules
/// ────────────
///   Perimeter cells  → wall profile + elevated heightOffset
///   Interior cells   → floor profile + ground heightOffset (0)
///   Door cells       → CellFlags.Empty (no tile generated = open gap)
///
/// Door positions
///   Each of the 4 edges gets one door opening centred on that edge.
///   DoorWidth controls how many cells wide the opening is.
///   For even-width grids the opening is left-of-centre by one cell.
///
///   Example 5×5, DoorWidth=1:
///     Top    door at (2, 0)
///     Bottom door at (2, 4)
///     Left   door at (0, 2)
///     Right  door at (4, 2)
///
/// All mutations go through FieldEditorUndo so the operation is one Ctrl-Z step.
/// </summary>
public static class RoomLayoutPreset
{
    // ── Config struct ─────────────────────────────────────────────────────────

    public struct RoomConfig
    {
        /// <summary>Profile assigned to perimeter (wall / hill) cells.</summary>
        public TerrainProfile wallProfile;

        /// <summary>Profile assigned to interior (floor) cells.</summary>
        public TerrainProfile floorProfile;

        /// <summary>Y offset applied to wall cells to raise them above the floor.</summary>
        public float wallHeight;

        /// <summary>Y offset for interior floor cells (usually 0).</summary>
        public float floorHeight;

        /// <summary>Number of cells wide each door opening is (min 1).</summary>
        public int doorWidth;

        public static RoomConfig Default => new RoomConfig
        {
            wallHeight  = 2f,
            floorHeight = 0f,
            doorWidth   = 1,
        };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Configures every cell in <paramref name="data"/> according to the room
    /// layout rules.  The entire operation is wrapped in a single undo group.
    /// </summary>
    public static void Apply(FieldGridData data, RoomConfig cfg)
    {
        if (data == null) return;

        cfg.doorWidth = Mathf.Max(1, cfg.doorWidth);

        FieldEditorUndo.BeginGroup("Apply Room Layout");
        FieldEditorUndo.RecordDataChange(data, "Apply Room Layout");

        for (int z = 0; z < data.height; z++)
        {
            for (int x = 0; x < data.width; x++)
            {
                var cell = data.GetCell(x, z);
                if (cell == null) continue;

                bool edge = IsEdge(x, z, data.width, data.height);
                bool door = edge && IsDoor(x, z, data.width, data.height, cfg.doorWidth);

                if (door)
                {
                    ApplyDoor(cell);
                }
                else if (edge)
                {
                    ApplyWall(cell, cfg);
                }
                else
                {
                    ApplyFloor(cell, cfg);
                }

                cell.isDirty = true;
            }
        }

        FieldEditorUndo.MarkDataDirty(data);
        FieldEditorUndo.EndGroup();
    }

    // ── Private: cell role tests ──────────────────────────────────────────────

    private static bool IsEdge(int x, int z, int width, int height) =>
        x == 0 || x == width - 1 || z == 0 || z == height - 1;

    /// <summary>
    /// Returns true when (x, z) falls within the door opening on its edge.
    /// Doors are centred on each edge; openings are <paramref name="doorWidth"/> cells wide.
    /// </summary>
    private static bool IsDoor(int x, int z, int width, int height, int doorWidth)
    {
        // Top / bottom edges — door spans x-axis
        if (z == 0 || z == height - 1)
        {
            int start = (width - doorWidth) / 2;
            return x >= start && x < start + doorWidth;
        }

        // Left / right edges — door spans z-axis
        if (x == 0 || x == width - 1)
        {
            int start = (height - doorWidth) / 2;
            return z >= start && z < start + doorWidth;
        }

        return false;
    }

    // ── Private: per-cell mutations ───────────────────────────────────────────

    private static void ApplyDoor(CellData cell)
    {
        cell.SetFlag(CellFlags.Empty);
        cell.ClearFlag(CellFlags.Walkable);
        cell.terrainProfile        = null;
        cell.terrainPrefabOverride = null;
        cell.heightOffset          = 0f;
    }

    private static void ApplyWall(CellData cell, RoomConfig cfg)
    {
        cell.ClearFlag(CellFlags.Empty);
        cell.SetFlag(CellFlags.Walkable);
        cell.terrainProfile = cfg.wallProfile;
        cell.heightOffset   = cfg.wallHeight;
        cell.randomSeed     = -1;   // vary tiles for natural look
    }

    private static void ApplyFloor(CellData cell, RoomConfig cfg)
    {
        cell.ClearFlag(CellFlags.Empty);
        cell.SetFlag(CellFlags.Walkable);
        cell.terrainProfile = cfg.floorProfile;
        cell.heightOffset   = cfg.floorHeight;
        cell.randomSeed     = -1;
    }
}
