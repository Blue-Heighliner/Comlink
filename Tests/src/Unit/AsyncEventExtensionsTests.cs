namespace BlueHeighliner.Comlink.Tests.Unit;

/// <summary>Unit tests for <see cref="AsyncEventExtensions"/>.</summary>
public sealed class AsyncEventExtensionsTests
{
    /// <summary>Every handler is awaited, in subscription order, not just the last one's task as an ordinary delegate invocation would return.</summary>
    [Fact]
    public async Task InvokeAll_AwaitsEveryHandlerInOrder()
    {
        List<string> log = [];
        Func<int, Task>? handlers = null;
        handlers += async value => { log.Add($"first-start {value}"); await Task.Delay(50); log.Add("first-end"); };
        handlers += value => { log.Add($"second {value}"); return Task.CompletedTask; };

        await handlers.InvokeAll(7);

        Assert.Equal(["first-start 7", "first-end", "second 7"], log);
    }

    /// <summary>With no handlers there is nothing to do.</summary>
    [Fact]
    public async Task InvokeAll_NoHandlers_DoesNothing()
    {
        Func<int, Task>? none = null;

        await none.InvokeAll(1);
    }

    /// <summary>A handler that fails does not stop the ones after it, and its failure is raised once they have all run.</summary>
    [Fact]
    public async Task InvokeAll_OneHandlerFails_OthersStillRun_AndItsExceptionIsRaised()
    {
        bool secondRan = false;
        Func<int, Task>? handlers = null;
        handlers += _ => throw new InvalidOperationException("boom");
        handlers += _ => { secondRan = true; return Task.CompletedTask; };

        await Assert.ThrowsAsync<InvalidOperationException>(() => handlers.InvokeAll(1));

        Assert.True(secondRan);
    }

    /// <summary>More than one failure is raised together.</summary>
    [Fact]
    public async Task InvokeAll_SeveralHandlersFail_RaisesAggregate()
    {
        Func<int, Task>? handlers = null;
        handlers += _ => Task.FromException(new InvalidOperationException("a"));
        handlers += _ => Task.FromException(new IOException("b"));

        AggregateException error = await Assert.ThrowsAsync<AggregateException>(() => handlers.InvokeAll(1));

        Assert.Equal(2, error.InnerExceptions.Count);
    }

    /// <summary>The two and three argument forms pass every argument on.</summary>
    [Fact]
    public async Task InvokeAll_MultipleArguments_ArePassedOn()
    {
        string? two = null;
        string? three = null;
        Func<string, int, Task>? twoArgs = (a, b) => { two = $"{a}{b}"; return Task.CompletedTask; };
        Func<string, int, bool, Task>? threeArgs = (a, b, c) => { three = $"{a}{b}{c}"; return Task.CompletedTask; };

        await twoArgs.InvokeAll("x", 1);
        await threeArgs.InvokeAll("y", 2, true);

        Assert.Equal("x1", two);
        Assert.Equal("y2True", three);
    }
}
