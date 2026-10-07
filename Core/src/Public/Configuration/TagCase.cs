namespace BlueHeighliner.Comlink;

/// <summary>Which letter case message tags are kept in (see <see cref="IDraftHandler{TPriority, TLevel, TAspect}.TagCase"/>).</summary>
public enum TagCase
{
    /// <summary>Tags keep whatever case they are written in.</summary>
    Mixed,

    /// <summary>Tags are forced to lowercase.</summary>
    Lower,

    /// <summary>Tags are forced to uppercase.</summary>
    Upper
}
