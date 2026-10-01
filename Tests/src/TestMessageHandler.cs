namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IMessageHandler{TFrame}"/> for <see cref="TestFrame"/>: every frame not marked hidden is a message.</summary>
public sealed class TestMessageHandler : IMessageHandler<TestFrame>
{
    /// <inheritdoc />
    public bool IsValid(TestFrame frame) => !frame.IsHidden;

    /// <inheritdoc />
    public TestFrame Create(MessageCreateContext context)
        => new()
        {
            Body = context.Body,
            IsAlert = context.IsAlert,
            Priority = context.Priority,
            Tag = context.Tag,
            SecurityLevel = context.SecurityLevel
        };

    /// <inheritdoc />
    public string GetBody(TestFrame frame) => frame.Body;

    /// <inheritdoc />
    public bool GetIsAlert(TestFrame frame) => frame.IsAlert;

    /// <inheritdoc />
    public int GetPriority(TestFrame frame) => frame.Priority;

    /// <inheritdoc />
    public string GetTag(TestFrame frame) => frame.Tag;

    /// <inheritdoc />
    public string GetSecurityLevel(TestFrame frame) => frame.SecurityLevel;
}
