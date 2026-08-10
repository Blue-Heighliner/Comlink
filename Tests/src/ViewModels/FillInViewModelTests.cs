namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="FillInViewModel"/>.</summary>
public sealed class FillInViewModelTests
{
    // ── Construction ──────────────────────────────────────────────────────────

    /// <summary>Default constructor produces a non-empty ID and no options.</summary>
    [Fact]
    public void DefaultCtor_ProducesNonEmptyIdAndNoOptions()
    {
        FillInViewModel vm = new();

        Assert.NotEmpty(vm.Id);
        Assert.Empty(vm.Options);
        Assert.Null(vm.SelectedOption);
        Assert.Equal("______", vm.DisplayText);
    }

    /// <summary>Parameterized constructor restores options and pre-selected value.</summary>
    [Fact]
    public void ParamCtor_RestoresOptionsAndSelection()
    {
        FillInViewModel vm = new("abc12345", ["Alpha", "Beta"], "Beta");

        Assert.Equal("abc12345", vm.Id);
        Assert.Equal(2, vm.Options.Count);
        Assert.Equal("Beta", vm.SelectedOption);
        Assert.Equal("Beta", vm.DisplayText);
    }

    /// <summary>Parameterized constructor with no pre-selected value leaves SelectedOption null.</summary>
    [Fact]
    public void ParamCtor_NoPreselect_SelectedOptionIsNull()
    {
        FillInViewModel vm = new("abc12345", ["Alpha", "Beta"], null);

        Assert.Null(vm.SelectedOption);
        Assert.Equal("______", vm.DisplayText);
    }

    // ── AddOption ─────────────────────────────────────────────────────────────

    /// <summary>AddOption appends a trimmed option and resets NewOption.</summary>
    [Fact]
    public void AddOption_AppendsTrimmedOptionAndClearsInput()
    {
        FillInViewModel vm = new();
        vm.NewOption = "  Foo  ";

        vm.AddOptionCommand.Execute(null);

        Assert.Single(vm.Options);
        Assert.Equal("Foo", vm.Options[0].Value);
        Assert.Equal(string.Empty, vm.NewOption);
    }

    /// <summary>AddOption ignores blank input.</summary>
    [Fact]
    public void AddOption_BlankInput_DoesNothing()
    {
        FillInViewModel vm = new();
        vm.NewOption = "   ";

        vm.AddOptionCommand.Execute(null);

        Assert.Empty(vm.Options);
    }

    // ── SelectOption ──────────────────────────────────────────────────────────

    /// <summary>SelectOption marks the matching option as selected.</summary>
    [Fact]
    public void SelectOption_MarksMatchingOption()
    {
        FillInViewModel vm = new("id00001a", ["Alpha", "Beta"], null);

        vm.SelectOptionCommand.Execute("Alpha");

        Assert.Equal("Alpha", vm.SelectedOption);
    }

    /// <summary>SelectOption deselects the previously selected option.</summary>
    [Fact]
    public void SelectOption_DeselectedPreviousOption()
    {
        FillInViewModel vm = new("id00001a", ["Alpha", "Beta"], "Alpha");

        vm.SelectOptionCommand.Execute("Beta");

        Assert.Equal("Beta", vm.SelectedOption);
        Assert.False(vm.Options.First(o => o.Value == "Alpha").IsSelected);
    }

    /// <summary>Selecting the already-selected option deselects it (toggle off).</summary>
    [Fact]
    public void SelectOption_AlreadySelected_Deselects()
    {
        FillInViewModel vm = new("id00001a", ["Alpha"], "Alpha");

        vm.SelectOptionCommand.Execute("Alpha");

        Assert.Null(vm.SelectedOption);
    }

    // ── RemoveOption ──────────────────────────────────────────────────────────

    /// <summary>RemoveOption removes the matching option from the list.</summary>
    [Fact]
    public void RemoveOption_RemovesMatchingOption()
    {
        FillInViewModel vm = new("id00001a", ["Alpha", "Beta"], null);

        vm.RemoveOptionCommand.Execute("Alpha");

        Assert.Single(vm.Options);
        Assert.Equal("Beta", vm.Options[0].Value);
    }

    // ── MoveOptionUp / Down ───────────────────────────────────────────────────

    /// <summary>MoveOptionUp moves the item one position earlier.</summary>
    [Fact]
    public void MoveOptionUp_MovesItemUp()
    {
        FillInViewModel vm = new("id00001a", ["Alpha", "Beta", "Gamma"], null);

        vm.MoveOptionUpCommand.Execute("Beta");

        Assert.Equal("Beta", vm.Options[0].Value);
        Assert.Equal("Alpha", vm.Options[1].Value);
    }

    /// <summary>MoveOptionUp on the first item does nothing.</summary>
    [Fact]
    public void MoveOptionUp_FirstItem_DoesNothing()
    {
        FillInViewModel vm = new("id00001a", ["Alpha", "Beta"], null);

        vm.MoveOptionUpCommand.Execute("Alpha");

        Assert.Equal("Alpha", vm.Options[0].Value);
    }

    /// <summary>MoveOptionDown moves the item one position later.</summary>
    [Fact]
    public void MoveOptionDown_MovesItemDown()
    {
        FillInViewModel vm = new("id00001a", ["Alpha", "Beta", "Gamma"], null);

        vm.MoveOptionDownCommand.Execute("Alpha");

        Assert.Equal("Beta", vm.Options[0].Value);
        Assert.Equal("Alpha", vm.Options[1].Value);
    }

    /// <summary>MoveOptionDown on the last item does nothing.</summary>
    [Fact]
    public void MoveOptionDown_LastItem_DoesNothing()
    {
        FillInViewModel vm = new("id00001a", ["Alpha", "Beta"], null);

        vm.MoveOptionDownCommand.Execute("Beta");

        Assert.Equal("Beta", vm.Options[1].Value);
    }

    // ── TogglePopup ───────────────────────────────────────────────────────────

    /// <summary>TogglePopup flips IsPopupOpen from false to true.</summary>
    [Fact]
    public void TogglePopup_OpensClosed()
    {
        FillInViewModel vm = new();
        Assert.False(vm.IsPopupOpen);

        vm.TogglePopupCommand.Execute(null);

        Assert.True(vm.IsPopupOpen);
    }

    /// <summary>TogglePopup flips IsPopupOpen from true to false.</summary>
    [Fact]
    public void TogglePopup_ClosesOpen()
    {
        FillInViewModel vm = new();
        vm.IsPopupOpen = true;

        vm.TogglePopupCommand.Execute(null);

        Assert.False(vm.IsPopupOpen);
    }
}
