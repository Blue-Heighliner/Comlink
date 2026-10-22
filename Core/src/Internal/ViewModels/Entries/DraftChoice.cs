namespace BlueHeighliner.Comlink;

/// <summary>Which of a draft's choices the user just changed, so the others adjust around it when the combination is blocked.</summary>
internal enum DraftChoice
{
    /// <summary>Nothing in particular, such as the tag having changed.</summary>
    None,

    /// <summary>The priority.</summary>
    Priority,

    /// <summary>The level.</summary>
    Level,

    /// <summary>The aspect.</summary>
    Aspect
}
