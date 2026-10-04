namespace BlueHeighliner.Comlink;

/// <summary>
/// Names and words the app shows its users. State one with <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel}.Display{THandler}"/>. Every member is optional: returning <see langword="null"/> (or an empty string) keeps the engine's own
/// text, so a handler overrides only what it wants to. The current user's entry in the network configuration file still overrides the alert and tag labels it states.
/// </summary>
public interface IDisplayHandler
{
    /// <summary>Gets the application name, used in the title bar and log headers. Defaults to the entry assembly's name. It never affects where data is kept: see <see cref="DataFolderName"/>.</summary>
    string? AppName => null;

    /// <summary>Gets the name of the folder under the application data root that holds the app's data (and its logs), a different member from <see cref="AppName"/> so renaming the app never moves, and so appears to lose, its data. Defaults to the entry assembly's name. Changing it after data has been stored moves the app to an empty folder.</summary>
    string? DataFolderName => null;

    /// <summary>Gets the window icon: an <c>avares://</c> URI of an Avalonia asset, or else the path of an image file. Defaults to the operating system's.</summary>
    string? Icon => null;

    /// <summary>Gets the text shown in the content area when no entry is selected. Defaults to <c>HOME</c>.</summary>
    string? HomeText => null;

    /// <summary>Gets the text shown in the title bar's alert box while alarming, and the draft editor's alert checkbox label. Defaults to <c>ALERT</c>.</summary>
    string? AlertLabel => null;

    /// <summary>Gets the name of the tag input in the draft editor (for example <c>Category</c>). Defaults to <c>Tag</c>.</summary>
    string? TagLabel => null;

    /// <summary>Gets the name of the priority concept, as the entry list's priority filter and the help call it. Defaults to <c>Priority</c>.</summary>
    string? PriorityLabel => null;

    /// <summary>Gets the name of the security level concept, as the entry list's security level filter calls it. Defaults to <c>Security Level</c>.</summary>
    string? SecurityLevelLabel => null;

    /// <summary>Gets the plural of <see cref="AlertLabel"/>, for text such as "Alerts". Defaults to <see cref="AlertLabel"/> with an <c>s</c> added when that is stated, otherwise <c>ALERTS</c>.</summary>
    string? AlertPluralLabel => null;

    /// <summary>Gets the plural of <see cref="TagLabel"/>. Defaults to <see cref="TagLabel"/> with an <c>s</c> added when that is stated, otherwise <c>Tags</c>.</summary>
    string? TagPluralLabel => null;

    /// <summary>Gets the plural of <see cref="PriorityLabel"/>. Defaults to <see cref="PriorityLabel"/> with an <c>s</c> added when that is stated, otherwise <c>Priorities</c>.</summary>
    string? PriorityPluralLabel => null;

    /// <summary>Gets the plural of <see cref="SecurityLevelLabel"/>. Defaults to <see cref="SecurityLevelLabel"/> with an <c>s</c> added when that is stated, otherwise <c>Security Levels</c>.</summary>
    string? SecurityLevelPluralLabel => null;

    /// <summary>Gets what the app calls <c>Inbox</c> wherever the user interface mentions it, in the case style of the text it replaces. Defaults to <c>Inbox</c>.</summary>
    string? InboxLabel => null;

    /// <summary>Gets what the app calls <c>Outbox</c> wherever the user interface mentions it, in the case style of the text it replaces. Defaults to <c>Outbox</c>.</summary>
    string? OutboxLabel => null;

    /// <summary>Gets what the app calls <c>Drafts</c> wherever the user interface mentions it, in the case style of the text it replaces. Defaults to <c>Drafts</c>.</summary>
    string? DraftsLabel => null;

    /// <summary>Gets what the app calls <c>Draft</c> wherever the user interface mentions it, in the case style of the text it replaces. Defaults to <c>Draft</c>.</summary>
    string? DraftLabel => null;

    /// <summary>Gets what the app calls <c>Notes</c> wherever the user interface mentions it, in the case style of the text it replaces. Defaults to <c>Notes</c>.</summary>
    string? NotesLabel => null;

    /// <summary>Gets what the app calls <c>Note</c> wherever the user interface mentions it, in the case style of the text it replaces. Defaults to <c>Note</c>.</summary>
    string? NoteLabel => null;

    /// <summary>Gets what the app calls <c>Activity</c> wherever the user interface mentions it, in the case style of the text it replaces. Defaults to <c>Activity</c>.</summary>
    string? ActivityLabel => null;

    /// <summary>Gets what the app calls <c>Messages</c> wherever the user interface mentions it, in the case style of the text it replaces. Defaults to <c>Messages</c>.</summary>
    string? MessagesLabel => null;

    /// <summary>Gets what the app calls <c>Message</c> wherever the user interface mentions it, in the case style of the text it replaces. Defaults to <c>Message</c>.</summary>
    string? MessageLabel => null;
}
