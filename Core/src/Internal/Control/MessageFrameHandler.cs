namespace BlueHeighliner.Comlink.Control;

/// <summary>The engine's untyped view of the host's <see cref="IMessageHandler{TFrame}"/>, working on frames as <see cref="object"/>.</summary>
internal interface IMessageFrameHandler
{
    /// <summary>Returns whether <paramref name="frame"/> is a message.</summary>
    bool IsValid(object frame);
    /// <summary>Creates a message frame carrying <paramref name="context"/>.</summary>
    object Create(MessageCreateContext context);
    /// <summary>Gets the body text of <paramref name="frame"/>.</summary>
    string GetBody(object frame);
    /// <summary>Gets whether <paramref name="frame"/> is an alert.</summary>
    bool GetIsAlert(object frame);
    /// <summary>Gets the priority number of <paramref name="frame"/>.</summary>
    int GetPriority(object frame);
    /// <summary>Gets the tag of <paramref name="frame"/>.</summary>
    string GetTag(object frame);
    /// <summary>Gets the security level name of <paramref name="frame"/>.</summary>
    string GetSecurityLevel(object frame);
}

/// <summary>Adapts a typed <see cref="IMessageHandler{TFrame}"/> to <see cref="IMessageFrameHandler"/>.</summary>
internal sealed class MessageFrameHandler<TFrame>(IMessageHandler<TFrame> handler) : IMessageFrameHandler where TFrame : class
{
    /// <inheritdoc />
    public bool IsValid(object frame) => handler.IsValid((TFrame)frame);

    /// <inheritdoc />
    public object Create(MessageCreateContext context) => handler.Create(context);

    /// <inheritdoc />
    public string GetBody(object frame) => handler.GetBody((TFrame)frame);

    /// <inheritdoc />
    public bool GetIsAlert(object frame) => handler.GetIsAlert((TFrame)frame);

    /// <inheritdoc />
    public int GetPriority(object frame) => handler.GetPriority((TFrame)frame);

    /// <inheritdoc />
    public string GetTag(object frame) => handler.GetTag((TFrame)frame);

    /// <inheritdoc />
    public string GetSecurityLevel(object frame) => handler.GetSecurityLevel((TFrame)frame);
}
