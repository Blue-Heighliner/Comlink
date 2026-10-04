namespace BlueHeighliner.Comlink.Sample;

/// <summary>The Sample's security levels. The values are stored in drafts and every frame, so a member's value must never change or be reused, even if the member stops being used. The ranking is the order <see cref="EngineConfiguration"/> states them in, lowest first.</summary>
public enum SecurityLevel
{
    /// <summary>The lowest security level.</summary>
    Public = 0,

    /// <summary>The middle security level.</summary>
    Internal = 1,

    /// <summary>The highest security level.</summary>
    Restricted = 2
}
