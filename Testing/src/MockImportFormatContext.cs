namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="IImportFormatContext{TPriority, TLevel}"/> a custom import format's reader is handed. Every <c>Add</c> method completes at once; verify them to see what the reader added.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
public sealed class MockImportFormatContext<TPriority, TLevel> : Mock<IImportFormatContext<TPriority, TLevel>> where TPriority : struct, Enum where TLevel : struct, Enum
{
    /// <summary>Initializes the mock.</summary>
    public MockImportFormatContext()
    {
        Setup(context => context.AddMessage(It.IsAny<MessageExportData>())).Returns(Task.CompletedTask);
        Setup(context => context.AddDraft(It.IsAny<DraftExportData>())).Returns(Task.CompletedTask);
        Setup(context => context.AddNote(It.IsAny<NoteExportData>())).Returns(Task.CompletedTask);
    }
}
