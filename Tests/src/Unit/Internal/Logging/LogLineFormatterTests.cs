namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Logging;

/// <summary>Unit tests for <see cref="LogLineFormatter"/>.</summary>
public sealed class LogLineFormatterTests
{
    private static readonly DateTime at = new(2026, 7, 30, 14, 23, 7, 51);

    private static LogLineFormatter Build(LogFieldWidths? widths = null, bool stackTrace = false)
    {
        Mock<IEngineController> controller = new();
        controller.Setup(c => c.LogWidths).Returns(widths ?? LogFieldWidths.None);
        controller.Setup(c => c.IsStackTraceEnabled).Returns(stackTrace);
        return new LogLineFormatter(controller.Object);
    }

    /// <summary>Without a log handler no field is fixed: each is written at its natural length, and a line has no level.</summary>
    [Fact]
    public void Unfixed_FieldsAreWrittenAtTheirNaturalLength()
    {
        LogLineFormatter formatter = Build();

        Assert.Equal("[30-JUL-2026 14:23:07.051] [APP] [ALICE] [CL66] hello", formatter.Format(at, "App", "ALICE", new EventId(66, "CL66"), "hello"));
        Assert.Equal("[30-JUL-2026 14:23:07.051] [ACTIVITY] [] [] hello", formatter.Format(at, "activity", null, default, "hello"));
    }

    /// <summary>A fixed category or user is padded with hyphens up to the width and cut when longer.</summary>
    [Fact]
    public void FixedCategoryAndUser_ArePaddedWithHyphensAndCut()
    {
        LogLineFormatter formatter = Build(new LogFieldWidths(8, 5, null));

        string line = formatter.Format(at, "Crash", "ALICE-LONG", new EventId(1, "CL1"), "m");
        Assert.Contains("[CRASH---] [ALICE]", line);
        Assert.Contains("[ACTIVITY] [-----]", formatter.Format(at, "ACTIVITY", null, new EventId(1, "CL1"), "m"));
        Assert.Contains("[SOMETHIN]", formatter.Format(at, "SomethingElse", "A", new EventId(1, "CL1"), "m"));
    }

    /// <summary>A fixed ID is padded with hyphens on the right, never cut, and is all hyphens for a line with no event.</summary>
    [Fact]
    public void FixedId_IsPaddedWithHyphensAndNeverCut()
    {
        LogLineFormatter formatter = Build(new LogFieldWidths(null, null, 5));

        Assert.Contains("[CL7--]", formatter.Format(at, "ERROR", "A", new EventId(7, "CL7"), "m"));
        Assert.Contains("[CL66-]", formatter.Format(at, "ERROR", "A", new EventId(66, "CL66"), "m"));
        Assert.Contains("[CL1234]", formatter.Format(at, "ERROR", "A", new EventId(1234, "CL1234"), "m"));
        Assert.Contains("[-----]", formatter.Format(at, "ERROR", "A", default, "m"));
    }

    /// <summary>With every field fixed the message starts in the same column on every line.</summary>
    [Fact]
    public void AllFixed_AlignsTheMessage()
    {
        LogLineFormatter formatter = Build(new LogFieldWidths(11, 7, 5));
        string[] lines =
        [
            formatter.Format(at, "ACTIVITY", "ALICE", new EventId(66, "CL66"), "x"),
            formatter.Format(at, "ERROR", null, default, "x"),
            formatter.Format(at, "PACKETS", "BARTHOLOMEW", new EventId(1, "CL1"), "x")
        ];

        Assert.Single(lines.Select(line => line.IndexOf(" x", StringComparison.Ordinal)).Distinct());
    }

    private static Exception Thrown()
    {
        try
        {
            throw new InvalidOperationException("outer", new IOException("inner"));
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>Without stack traces an exception is reported by the type and message of it and of every exception inside it, and no stack trace.</summary>
    [Fact]
    public void FormatException_WithoutStackTraces_IsTheTypeAndMessageOfEachException()
    {
        string text = Build().FormatException(Thrown());

        Assert.Equal("System.InvalidOperationException: outer ---> System.IO.IOException: inner", text);
        Assert.DoesNotContain("   at ", text);
    }

    /// <summary>With stack traces on an exception is reported whole, stack trace included.</summary>
    [Fact]
    public void FormatException_WithStackTraces_IsTheWholeException()
    {
        Exception exception = Thrown();

        string text = Build(stackTrace: true).FormatException(exception);

        Assert.Equal(exception.ToString(), text);
        Assert.Contains("   at ", text);
        Assert.Contains(nameof(Thrown), text);
    }
}
