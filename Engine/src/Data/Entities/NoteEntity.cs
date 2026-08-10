namespace BlueHeighliner.Comlink.Engine.Data.Entities;

/// <summary>LiteDB document representing a user note.</summary>
public sealed class NoteEntity
{
    /// <summary>Unique document identifier.</summary>
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    /// <summary>Text body of the note.</summary>
    public string Body { get; set; } = string.Empty;
    /// <summary>UTC timestamp when this note was first created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>UTC timestamp of the most recent modification.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Identifier of the folder this note belongs to.</summary>
    public string FolderId { get; set; } = string.Empty;
}
