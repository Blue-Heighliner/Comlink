namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Logging;

/// <summary>Unit tests for <see cref="LogLineFormatter"/>.</summary>
public sealed class LogLineFormatterTests
{
    private static readonly DateTime at = new(2026, 7, 30, 14, 23, 7, 51);

    private static LogLineFormatter Build(LogFieldWidths? widths = null)
    {
        Mock<IEngineController> controller = new();
        controller.Setup(c => c.LogWidths).Returns(widths ?? LogFieldWidths.None);
        return new LogLineFormatter(controller.Object);
    }

    /// <summary>Without a log handler no field is fixed: each is written at its natural length, and a line has no level.</summary>
    [Fact]
    public void Unfixed_FieldsAreWrittenAtTheirNaturalLength()
    {
        LogLineFormatter formatter = Build();

        Assert.Equal("[30-JUL-2026 14:23:07.051] [APP] [ALICE] [66] hello", formatter.Format(at, "App", "ALICE", new EventId(66), "hello"));
        Assert.Equal("[30-JUL-2026 14:23:07.051] [ACTIVITY] [] [] hello", formatter.Format(at, "activity", null, default, "hello"));
    }

    /// <summary>A fixed category or user is padded with hyphens up to the width and cut when longer.</summary>
    [Fact]
    public void FixedCategoryAndUser_ArePaddedWithHyphensAndCut()
    {
        LogLineFormatter formatter = Build(new LogFieldWidths(8, 5, null));

        string line = formatter.Format(at, "Crash", "ALICE-LONG", new EventId(1), "m");
        Assert.Contains("[CRASH---] [ALICE]", line);
        Assert.Contains("[ACTIVITY] [-----]", formatter.Format(at, "ACTIVITY", null, new EventId(1), "m"));
        Assert.Contains("[SOMETHIN]", formatter.Format(at, "SomethingElse", "A", new EventId(1), "m"));
    }

    /// <summary>A fixed ID is padded with hyphens on the right, never cut, and is all hyphens for a line with no event.</summary>
    [Fact]
    public void FixedId_IsPaddedWithHyphensAndNeverCut()
    {
        LogLineFormatter formatter = Build(new LogFieldWidths(null, null, 3));

        Assert.Contains("[7--]", formatter.Format(at, "ERROR", "A", new EventId(7), "m"));
        Assert.Contains("[66-]", formatter.Format(at, "ERROR", "A", new EventId(66), "m"));
        Assert.Contains("[1234]", formatter.Format(at, "ERROR", "A", new EventId(1234), "m"));
        Assert.Contains("[---]", formatter.Format(at, "ERROR", "A", default, "m"));
    }

    /// <summary>With every field fixed the message starts in the same column on every line.</summary>
    [Fact]
    public void AllFixed_AlignsTheMessage()
    {
        LogLineFormatter formatter = Build(new LogFieldWidths(11, 7, 2));
        string[] lines =
        [
            formatter.Format(at, "ACTIVITY", "ALICE", new EventId(66), "x"),
            formatter.Format(at, "ERROR", null, default, "x"),
            formatter.Format(at, "PACKETS", "BARTHOLOMEW", new EventId(1), "x")
        ];

        Assert.Single(lines.Select(line => line.IndexOf(" x", StringComparison.Ordinal)).Distinct());
    }
}
