namespace BlueHeighliner.Comlink.Sample;

/// <summary>A CSV reader that stages one send per <c>User,Body</c> line for the user to review and send from the staged send screen, one at a time a second apart.</summary>
public sealed class CsvImportFormat : IImportFormat<MessagePriority, MessageLevel>
{
    /// <inheritdoc />
    public string Name { get; } = "CSV";

    /// <inheritdoc />
    public StagedSendMode StagedSendMode => StagedSendMode.Sequential;

    /// <inheritdoc />
    public TimeSpan? StagedSendDelay { get; } = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    public async Task Import(Stream stream, IImportFormatContext<MessagePriority, MessageLevel> context, CancellationToken cancellation)
    {
        using StreamReader reader = new(stream, leaveOpen: true);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellation)) is not null)
        {
            string[] parts = line.Split(',', 2);
            if (parts.Length < 2)
            {
                continue;
            }
            context.AddStagedSend(new StagedSendData<MessagePriority, MessageLevel> { Body = parts[1], Addresses = [new AddressRequest { UserName = parts[0] }] });
        }
    }
}
