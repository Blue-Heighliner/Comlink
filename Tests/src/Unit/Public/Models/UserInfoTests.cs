namespace BlueHeighliner.Comlink.Tests.Unit.Public.Models;

/// <summary>Unit tests for <see cref="UserInfo"/> and <see cref="Folder"/> model types.</summary>
public sealed class UserInfoTests
{
    /// <summary>Verifies that all required properties on <see cref="UserInfo"/> are correctly stored.</summary>
    [Fact]
    public void UserInfo_RequiredProperties_AreSet()
    {
        UserInfo info = new()
        {
            Name = "TestNode"
        };

        Assert.Equal("TestNode", info.Name);
        Assert.Null(info.Role);
        Assert.Null(info.SecurityLevel);
        Assert.False(info.StoresMessages);
        Assert.Empty(info.OutgoingPoints);
        Assert.Empty(info.ChildClients);
        Assert.Empty(info.Data);
    }

    /// <summary>Verifies that a new <see cref="Folder"/> has an empty children collection by default.</summary>
    [Fact]
    public void Folder_ChildrenDefaultToEmpty()
    {
        Folder folder = new()
        {
            Id = "root-inbox",
            Name = "Inbox",
            RootType = FolderType.Inbox
        };

        Assert.Empty(folder.Children);
    }

    /// <summary>Verifies that a <see cref="Folder"/> initialized with children exposes them via the Children list.</summary>
    [Fact]
    public void Folder_WithChildren_ExposesChildList()
    {
        Folder child = new()
        {
            Id = "child-1",
            Name = "Work",
            RootType = FolderType.Inbox,
            ParentId = "root-inbox"
        };

        Folder folder = new()
        {
            Id = "root-inbox",
            Name = "Inbox",
            RootType = FolderType.Inbox,
            Children = [child]
        };

        Assert.Single(folder.Children);
        Assert.Equal("Work", folder.Children[0].Name);
    }
}
