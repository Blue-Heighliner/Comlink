namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="PrintManagerViewModel"/>.</summary>
public sealed class PrintManagerViewModelTests
{
    private static readonly IEngineController format = new TestEngineController();
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    private sealed class Setup
    {
        public Setup()
        {
            PrintDriver.Setup(p => p.GetAvailablePrinters()).Returns(["PRINTER-A", "PRINTER-B"]);
            PrintDriver.Setup(p => p.GetDefaultPrinter()).Returns("PRINTER-A");
            EngineController.Setup(p => p.PrintReceivedDefaultEnabled).Returns(false);
            EngineController.Setup(r => r.GetPrintCount(It.IsAny<TestMessage>())).Returns(1);
            PrintDriver.Setup(p => p.PrintLine(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, string, CancellationToken>(async (printer, line, _) =>
                {
                    PrintedLines.Add((printer, line));
                    if (OnPrintLine is not null) { await OnPrintLine(printer, line); }
                });
            PrintDriver.Setup(p => p.PageFeed(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((printer, _) =>
                {
                    PageFeeds.Add(printer);
                    return Task.CompletedTask;
                });
        }

        public Mock<IEntryService> EntryService { get; } = new();
        public Mock<IMessageRepository> Messages { get; } = new();
        public Mock<IDraftRepository> Drafts { get; } = new();
        public Mock<INoteRepository> Notes { get; } = new();
        public Mock<IActivityLogRepository> ActivityLogs { get; } = new();
        public Mock<TestEngineController> EngineController { get; } = new() { CallBase = true };
        public Mock<IPrintDriver> PrintDriver { get; } = new();
        public List<(string Printer, string Line)> PrintedLines { get; } = [];
        public List<string> PageFeeds { get; } = [];
        public Func<string, string, Task>? OnPrintLine { get; set; }

        public PrintManagerViewModel Build() => new(
            EntryService.Object,
            Messages.Object,
            Drafts.Object,
            Notes.Object,
            ActivityLogs.Object,
            EngineController.Object,
            PrintDriver.Object,
            noLogger);
    }

    private static MessageEntity MakeMessage(string messageId, string subject, string body, int priority)
    {
        object message = format.CreateMessage();
        format.SetMessageId(message, messageId);
        format.SetSubject(message, subject);
        format.SetBody(message, body);
        format.SetPriority(message, priority);
        return new MessageEntity { MessageId = messageId, Message = message };
    }

    private static EntryItemViewModel MakeEntryItem(string id, string title, EntryType entryType = EntryType.Note)
        => new(id, title, entryType, DateTime.UtcNow);

    /// <summary>SelectedPrinter initializes from IPrintDriver.GetDefaultPrinter().</summary>
    [Fact]
    public void Ctor_SelectedPrinterFromDefaultProvider()
    {
        PrintManagerViewModel vm = new Setup().Build();
        Assert.Equal("PRINTER-A", vm.SelectedPrinter);
    }

    /// <summary>AvailablePrinters is populated from IPrintDriver.GetAvailablePrinters().</summary>
    [Fact]
    public void Ctor_AvailablePrintersFromProvider()
    {
        PrintManagerViewModel vm = new Setup().Build();
        Assert.Equal(["PRINTER-A", "PRINTER-B"], vm.AvailablePrinters);
    }

    /// <summary>PrintReceivedEnabled initializes from IEngineController.PrintReceivedDefaultEnabled.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ctor_PrintReceivedEnabledFromDefaultProvider(bool enabled)
    {
        Setup s = new();
        s.EngineController.Setup(p => p.PrintReceivedDefaultEnabled).Returns(enabled);

        PrintManagerViewModel vm = s.Build();

        Assert.Equal(enabled, vm.PrintReceivedEnabled);
    }

    /// <summary>The queue is empty on construction.</summary>
    [Fact]
    public void Ctor_QueueIsEmpty()
    {
        PrintManagerViewModel vm = new Setup().Build();
        Assert.Empty(vm.Queue);
    }

    /// <summary>EnqueueManual adds a manual entry to the queue.</summary>
    [Fact]
    public void EnqueueManual_AddsToQueue()
    {
        Setup s = new();
        s.PrintDriver.Setup(p => p.GetDefaultPrinter()).Returns((string?)null);
        PrintManagerViewModel vm = s.Build();

        vm.EnqueueManual(MakeEntryItem("N1", "My Note"));

        PrintQueueEntry entry = Assert.Single(vm.Queue);
        Assert.Equal("N1", entry.EntryId);
        Assert.Equal("My Note", entry.Title);
        Assert.True(entry.IsManual);
    }

    /// <summary>Manual entries always sort ahead of automatically-queued received entries, regardless of message priority.</summary>
    [Fact]
    public void Queue_ManualEntry_SortsAheadOfHigherPriorityReceivedEntry()
    {
        Setup s = new();
        s.PrintDriver.Setup(p => p.GetDefaultPrinter()).Returns((string?)null);
        s.EngineController.Setup(p => p.PrintReceivedDefaultEnabled).Returns(true);
        PrintManagerViewModel vm = s.Build();

        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG1", "High priority", "body", 99));
        vm.EnqueueManual(MakeEntryItem("N1", "Manual note"));

        Assert.Equal(2, vm.Queue.Count);
        Assert.True(vm.Queue[0].IsManual);
        Assert.Equal("N1", vm.Queue[0].EntryId);
    }

    /// <summary>MessageInserted does not enqueue anything while PrintReceivedEnabled is false (the default).</summary>
    [Fact]
    public void MessageInserted_PrintReceivedDisabled_DoesNotEnqueue()
    {
        Setup s = new();
        s.PrintDriver.Setup(p => p.GetDefaultPrinter()).Returns((string?)null);
        PrintManagerViewModel vm = s.Build();

        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG1", "Subject", "body", 0));

        Assert.Empty(vm.Queue);
    }

    /// <summary>MessageInserted enqueues one entry per GetPrintCount when PrintReceivedEnabled is true.</summary>
    [Fact]
    public void MessageInserted_PrintReceivedEnabled_EnqueuesGetPrintCountCopies()
    {
        Setup s = new();
        s.PrintDriver.Setup(p => p.GetDefaultPrinter()).Returns((string?)null);
        s.EngineController.Setup(r => r.GetPrintCount(It.IsAny<TestMessage>())).Returns(3);
        PrintManagerViewModel vm = s.Build();
        vm.PrintReceivedEnabled = true;

        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG1", "Subject", "body", 0));

        Assert.Equal(3, vm.Queue.Count);
        Assert.All(vm.Queue, e => Assert.False(e.IsManual));
        Assert.All(vm.Queue, e => Assert.Equal("MSG1", e.EntryId));
    }

    /// <summary>A GetPrintCount of 0 enqueues nothing.</summary>
    [Fact]
    public void MessageInserted_PrintCountZero_EnqueuesNothing()
    {
        Setup s = new();
        s.PrintDriver.Setup(p => p.GetDefaultPrinter()).Returns((string?)null);
        s.EngineController.Setup(r => r.GetPrintCount(It.IsAny<TestMessage>())).Returns(0);
        PrintManagerViewModel vm = s.Build();
        vm.PrintReceivedEnabled = true;

        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG1", "Subject", "body", 0));

        Assert.Empty(vm.Queue);
    }

    /// <summary>Received entries sort by descending message priority.</summary>
    [Fact]
    public void Queue_ReceivedEntries_SortByDescendingPriority()
    {
        Setup s = new();
        s.PrintDriver.Setup(p => p.GetDefaultPrinter()).Returns((string?)null);
        s.EngineController.Setup(p => p.PrintReceivedDefaultEnabled).Returns(true);
        PrintManagerViewModel vm = s.Build();

        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("LOW", "Low", "body", 1));
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("HIGH", "High", "body", 9));

        Assert.Equal(["HIGH", "LOW"], vm.Queue.Select(e => e.EntryId).ToList());
    }

    /// <summary>Equal-priority received entries preserve first-in-first-out order.</summary>
    [Fact]
    public void Queue_EqualPriorityReceivedEntries_PreserveFifoOrder()
    {
        Setup s = new();
        s.PrintDriver.Setup(p => p.GetDefaultPrinter()).Returns((string?)null);
        s.EngineController.Setup(p => p.PrintReceivedDefaultEnabled).Returns(true);
        PrintManagerViewModel vm = s.Build();

        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("FIRST", "First", "body", 5));
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("SECOND", "Second", "body", 5));

        Assert.Equal(["FIRST", "SECOND"], vm.Queue.Select(e => e.EntryId).ToList());
    }

    /// <summary>RemoveCommand removes only the targeted entry.</summary>
    [Fact]
    public void RemoveCommand_RemovesOnlyTargetedEntry()
    {
        Setup s = new();
        s.PrintDriver.Setup(p => p.GetDefaultPrinter()).Returns((string?)null);
        PrintManagerViewModel vm = s.Build();
        vm.EnqueueManual(MakeEntryItem("N1", "First"));
        vm.EnqueueManual(MakeEntryItem("N2", "Second"));
        PrintQueueEntry target = vm.Queue.First(e => e.EntryId == "N1");

        vm.RemoveCommand.Execute(target);

        PrintQueueEntry remaining = Assert.Single(vm.Queue);
        Assert.Equal("N2", remaining.EntryId);
    }

    /// <summary>PurgeCommand clears every entry from the queue.</summary>
    [Fact]
    public void PurgeCommand_ClearsQueue()
    {
        Setup s = new();
        s.PrintDriver.Setup(p => p.GetDefaultPrinter()).Returns((string?)null);
        PrintManagerViewModel vm = s.Build();
        vm.EnqueueManual(MakeEntryItem("N1", "First"));
        vm.EnqueueManual(MakeEntryItem("N2", "Second"));

        vm.PurgeCommand.Execute(null);

        Assert.Empty(vm.Queue);
    }

    /// <summary>A manual note prints each body line individually, then feeds the page, then leaves the queue.</summary>
    [Fact]
    public async Task PrintLoop_PrintsEachLineThenPageFeedsThenRemovesJob()
    {
        Setup s = new();
        ObjectId noteId = new();
        s.Notes.Setup(n => n.Get(noteId)).ReturnsAsync(new NoteEntity { Id = noteId, Body = "Line1\nLine2\nLine3" });
        PrintManagerViewModel vm = s.Build();

        TaskCompletionSource done = new();
        int expectedLines = 3;
        s.OnPrintLine = (_, _) =>
        {
            if (s.PrintedLines.Count == expectedLines) { done.TrySetResult(); }
            return Task.CompletedTask;
        };

        vm.EnqueueManual(new EntryItemViewModel(noteId.ToString(), "My Note", EntryType.Note, DateTime.UtcNow));
        await Task.WhenAny(done.Task, Task.Delay(2000));

        Assert.Equal(["Line1", "Line2", "Line3"], s.PrintedLines.Select(l => l.Line).ToList());
        Assert.All(s.PrintedLines, l => Assert.Equal("PRINTER-A", l.Printer));

        // Allow the loop to finish the page feed and removal after the last line.
        for (int i = 0; i < 50 && vm.Queue.Count > 0; i++)
        {
            await Task.Delay(20);
        }

        Assert.Single(s.PageFeeds);
        Assert.Empty(vm.Queue);
    }

    /// <summary>
    /// A higher-priority received job that arrives mid-print interrupts the current job (page feed, left in
    /// the queue) and, once the higher-priority job finishes, the interrupted job restarts from its first line.
    /// </summary>
    [Fact]
    public async Task PrintLoop_HigherPriorityReceivedJob_InterruptsAndRestartsLowerPriorityJob()
    {
        Setup s = new();
        s.EngineController.Setup(p => p.PrintReceivedDefaultEnabled).Returns(true);
        s.Messages.Setup(m => m.Get("LOW", false))
            .ReturnsAsync(MakeMessage("LOW", "Low", "L1\nL2", 1));
        s.Messages.Setup(m => m.Get("HIGH", false))
            .ReturnsAsync(MakeMessage("HIGH", "High", "H1", 9));
        PrintManagerViewModel vm = s.Build();

        TaskCompletionSource interruptTriggered = new();
        TaskCompletionSource allDone = new();
        bool interruptQueued = false;
        s.OnPrintLine = (printer, line) =>
        {
            // Interrupt exactly once, right after the low-priority job's first line ("Low") prints.
            if (!interruptQueued && line == "Low")
            {
                interruptQueued = true;
                s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("HIGH", "High", "H1", 9));
                interruptTriggered.TrySetResult();
            }
            if (line == "Low" && s.PrintedLines.Count(l => l.Line == "Low") == 2)
            {
                allDone.TrySetResult();
            }
            return Task.CompletedTask;
        };

        // Enqueues LOW (priority 1) and starts the loop.
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("LOW", "Low", "L1\nL2", 1));

        await Task.WhenAny(allDone.Task, Task.Delay(2000));
        for (int i = 0; i < 100 && vm.Queue.Count > 0; i++)
        {
            await Task.Delay(20);
        }

        // LOW's first attempt is interrupted after its title line; HIGH then prints fully; LOW then restarts
        // from its title line and prints to completion.
        List<string> lines = s.PrintedLines.Select(l => l.Line).ToList();
        int highIndex = lines.IndexOf("High");
        Assert.True(highIndex > 0, "HIGH should have printed after being interrupted-in");
        Assert.Equal(["Low", "High", string.Empty, "H1"], lines.Take(4).ToList());
        Assert.Equal(["Low", "", "L1", "L2"], lines.Skip(4).ToList());

        // Page feed once for the interruption, once after HIGH, once after LOW's completed restart.
        Assert.Equal(3, s.PageFeeds.Count);
        Assert.Empty(vm.Queue);
    }

    /// <summary>PurgeCommand while a job is mid-print interrupts it (page feed, no restart) and the queue stays empty.</summary>
    [Fact]
    public async Task PurgeCommand_WhilePrinting_InterruptsCurrentJob()
    {
        Setup s = new();
        ObjectId noteId = new();
        s.Notes.Setup(n => n.Get(noteId)).ReturnsAsync(new NoteEntity { Id = noteId, Body = "Line1\nLine2\nLine3" });
        PrintManagerViewModel vm = s.Build();

        TaskCompletionSource purged = new();
        s.OnPrintLine = (_, line) =>
        {
            if (line == "Line1")
            {
                vm.PurgeCommand.Execute(null);
                purged.TrySetResult();
            }
            return Task.CompletedTask;
        };

        vm.EnqueueManual(new EntryItemViewModel(noteId.ToString(), "My Note", EntryType.Note, DateTime.UtcNow));
        await Task.WhenAny(purged.Task, Task.Delay(2000));
        await Task.Delay(100);

        Assert.Single(s.PrintedLines);
        Assert.Single(s.PageFeeds);
        Assert.Empty(vm.Queue);
    }
}
