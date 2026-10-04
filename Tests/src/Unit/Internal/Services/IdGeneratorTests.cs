namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="IdGenerator"/>.</summary>
public sealed class IdGeneratorTests
{
    /// <summary>The first identifier is generated from no previous one, and each later one from the one before it, which is saved every time.</summary>
    [Fact]
    public async Task Next_HandsTheHandlerThePreviousIdAndSavesTheNewOne()
    {
        Mock<IEngineController> controller = new();
        controller.Setup(c => c.NextId(It.IsAny<string?>())).Returns((string? previous) => (previous ?? "0") + "+");
        Mock<ILastIdRepository> repository = new();
        repository.Setup(r => r.Get()).ReturnsAsync((string?)null);
        IdGenerator sut = new(controller.Object, repository.Object);

        string first = await sut.Next();
        string second = await sut.Next();

        Assert.Equal(["0+", "0++"], [first, second]);
        controller.Verify(c => c.NextId(null), Times.Once);
        repository.Verify(r => r.Save("0+"), Times.Once);
        repository.Verify(r => r.Save("0++"), Times.Once);
        repository.Verify(r => r.Get(), Times.Once);
    }

    /// <summary>After a restart the generator continues from the identifier that was saved.</summary>
    [Fact]
    public async Task Next_AfterARestart_ContinuesFromTheSavedId()
    {
        Mock<IEngineController> controller = new();
        controller.Setup(c => c.NextId(It.IsAny<string?>())).Returns((string? previous) => previous + "+");
        Mock<ILastIdRepository> repository = new();
        repository.Setup(r => r.Get()).ReturnsAsync("SAVED");
        IdGenerator sut = new(controller.Object, repository.Object);

        Assert.Equal("SAVED+", await sut.Next());
    }
}
