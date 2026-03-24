using System;

/// <summary>
/// Arbitrary string key-value pair stored on a cell.
/// Provides an open-ended extension mechanism without modifying CellData.
///
/// Design note: using List&lt;CellTag&gt; instead of Dictionary&lt;string,string&gt;
/// because Unity serializes lists natively — dictionaries require custom
/// serialization. For editor lookups, use CellData.GetTag / SetTag helpers.
///
/// Examples:
///   new CellTag("EnemyGroup", "Goblins")
///   new CellTag("LightIntensity", "2.5")
///   new CellTag("QuestZone", "Act1_Forest")
/// </summary>
[Serializable]
public class CellTag
{
    public string key   = string.Empty;
    public string value = string.Empty;

    public CellTag() { }

    public CellTag(string key, string value)
    {
        this.key   = key;
        this.value = value;
    }

    public override string ToString() => $"[{key}={value}]";
}
