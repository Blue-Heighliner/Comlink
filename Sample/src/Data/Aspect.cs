namespace BlueHeighliner.Comlink.Sample;

/// <summary>The Sample's message aspects. The values are stored in drafts and every frame, so a member's value must never change or be reused, even if the member stops being used.</summary>
public enum Aspect
{
    /// <summary>The message is meant to travel encrypted.</summary>
    Encrypted = 0,

    /// <summary>The message is signed by its sender.</summary>
    Signed = 1
}
