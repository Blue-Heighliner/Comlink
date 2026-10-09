namespace BlueHeighliner.Comlink.Sample;

/// <summary>The Sample's message levels. The values are stored in drafts and every frame, so a member's value must never change or be reused, even if the member stops being used. The ranking is the order <see cref="EngineConfiguration"/> states them in, lowest first.</summary>
public enum Level
{
    /// <summary>The lowest message level.</summary>
    Public = 0,

    /// <summary>The middle message level.</summary>
    Internal = 1,

    /// <summary>The highest message level.</summary>
    Restricted = 2
}
