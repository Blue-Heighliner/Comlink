namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Utilities;

/// <summary>Unit tests for <see cref="DebugMode"/>.</summary>
public sealed class DebugModeTests
{
    private static Assembly Compile(params (string Key, string Value)[] metadata)
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"DebugModeTest{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        ConstructorInfo constructor = typeof(AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])!;
        foreach ((string key, string value) in metadata)
        {
            assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [key, value]));
        }

        return assembly;
    }

    /// <summary>The application is in debug mode exactly when the package's build targets recorded the DEBUG constant in its assembly.</summary>
    [Fact]
    public void IsDebug_IsWhetherTheAssemblyWasStampedWithTheDebugConstant()
    {
        Assert.True(new DebugMode(Compile((DebugMode.MetadataKey, "true"))).IsDebug);
        Assert.False(new DebugMode(Compile()).IsDebug);
        Assert.False(new DebugMode(Compile((DebugMode.MetadataKey, "false"))).IsDebug);
        Assert.False(new DebugMode(Compile(("Something.Else", "true"))).IsDebug);
        Assert.False(new DebugMode(null).IsDebug);
    }

    /// <summary>A project that imports the package's build targets is stamped when it is compiled with DEBUG and not otherwise.</summary>
    [Fact]
    public void BuildTargets_StampThisAssemblyOnlyInADebugBuild()
    {
        bool stamped = typeof(DebugModeTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Any(metadata => metadata.Key == DebugMode.MetadataKey && metadata.Value == "true");
        Assert.Equal(IsDebugBuild(), stamped);
    }

    private static bool IsDebugBuild()
    {
        bool isDebug = false;
        MarkDebug(ref isDebug);
        return isDebug;
    }

    // A call to a conditional method is compiled in only when this assembly is compiled with DEBUG, which is what is being asked.
    [Conditional("DEBUG")]
    private static void MarkDebug(ref bool isDebug) => isDebug = true;
}
