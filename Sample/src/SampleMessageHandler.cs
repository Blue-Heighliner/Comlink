namespace BlueHeighliner.Comlink.Sample;

/// <summary>Treats a <see cref="SampleFrame"/> as a message when its <see cref="SampleFrame.IsMessage"/> flag is set, mapping the message content onto the frame's own differently named fields.</summary>
public sealed class SampleMessageHandler : IMessageHandler<SampleFrame>
{
    /// <inheritdoc />
    public bool IsValid(SampleFrame frame) => frame.IsMessage;

    /// <inheritdoc />
    public SampleFrame Create(MessageCreateContext context)
        => new()
        {
            IsMessage = true,
            Title = context.Subject,
            Text = context.Body,
            Alert = context.IsAlert,
            Importance = context.Priority,
            Category = context.Tag,
            Classification = context.SecurityLevel
        };

    /// <inheritdoc />
    public string GetSubject(SampleFrame frame) => frame.Title;

    /// <inheritdoc />
    public string GetBody(SampleFrame frame) => frame.Text;

    /// <inheritdoc />
    public bool GetIsAlert(SampleFrame frame) => frame.Alert;

    /// <inheritdoc />
    public int GetPriority(SampleFrame frame) => frame.Importance;

    /// <inheritdoc />
    public string GetTag(SampleFrame frame) => frame.Category;

    /// <inheritdoc />
    public string GetSecurityLevel(SampleFrame frame) => frame.Classification;
}
