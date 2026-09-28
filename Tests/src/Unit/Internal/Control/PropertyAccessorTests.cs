namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>Unit tests for <see cref="PropertyAccessor"/>, which builds a getter and setter from a property access.</summary>
public sealed class PropertyAccessorTests
{
    private sealed class Inner
    {
        public string Name { get; set; } = "";
    }

    private sealed class Owner
    {
        public string Text { get; set; } = "";
        public string InitOnly { get; init; } = "";
        public string Field = "";
        public string ReadOnly { get; } = "fixed";
        public Inner Nested { get; } = new();
    }

    /// <summary>The getter reads and the setter writes the property the expression names.</summary>
    [Fact]
    public void Create_Property_ReadsAndWrites()
    {
        (Func<Owner, string> get, Action<Owner, string> set) = PropertyAccessor.Create<Owner, string>(o => o.Text);
        Owner owner = new() { Text = "before" };

        set(owner, "after");

        Assert.Equal("after", owner.Text);
        Assert.Equal("after", get(owner));
    }

    /// <summary>A field works the same as a property.</summary>
    [Fact]
    public void Create_Field_ReadsAndWrites()
    {
        (Func<Owner, string> get, Action<Owner, string> set) = PropertyAccessor.Create<Owner, string>(o => o.Field);
        Owner owner = new();

        set(owner, "value");

        Assert.Equal("value", get(owner));
    }

    /// <summary>An init-only property can be set, since the engine builds messages after construction.</summary>
    [Fact]
    public void Create_InitOnlyProperty_CanBeSet()
    {
        (Func<Owner, string> get, Action<Owner, string> set) = PropertyAccessor.Create<Owner, string>(o => o.InitOnly);
        Owner owner = new() { InitOnly = "before" };

        set(owner, "after");

        Assert.Equal("after", get(owner));
    }

    /// <summary>A nested access reads and writes the inner object's property.</summary>
    [Fact]
    public void Create_NestedProperty_ReadsAndWrites()
    {
        (Func<Owner, string> get, Action<Owner, string> set) = PropertyAccessor.Create<Owner, string>(o => o.Nested.Name);
        Owner owner = new();

        set(owner, "inner");

        Assert.Equal("inner", owner.Nested.Name);
        Assert.Equal("inner", get(owner));
    }

    /// <summary>A property with no setter is refused up front, with a message that points at the explicit overload.</summary>
    [Fact]
    public void Create_ReadOnlyProperty_Throws()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => PropertyAccessor.Create<Owner, string>(o => o.ReadOnly));

        Assert.Contains("cannot be assigned", error.Message);
        Assert.Contains("setter", error.Message);
    }

    /// <summary>Anything that is not a member access, such as a method call, is refused up front.</summary>
    [Fact]
    public void Create_NotAMemberAccess_Throws()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => PropertyAccessor.Create<Owner, string>(o => o.Text.ToUpper()));

        Assert.Contains("must read a property or field", error.Message);
    }
}
