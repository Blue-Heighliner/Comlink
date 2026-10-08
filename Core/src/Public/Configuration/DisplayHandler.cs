namespace BlueHeighliner.Comlink;

/// <summary>
/// Names and words the app shows its users. State one with <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Display{THandler}"/>. Every member is optional: returning <see langword="null"/> (or an empty string) keeps the engine's own
/// text, so a handler overrides only what it wants to.
/// </summary>
public interface IDisplayHandler
{
    /// <summary>Gets the application name, used in the title bar and log headers. Defaults to the entry assembly's name. It never affects where data is kept: see <see cref="DataFolderName"/>.</summary>
    string? AppName => null;

    /// <summary>Gets the name of the folder under the application data root that holds the app's data (and its logs), a different member from <see cref="AppName"/> so renaming the app never moves, and so appears to lose, its data. Defaults to the entry assembly's name. Changing it after data has been stored moves the app to an empty folder.</summary>
    string? DataFolderName => null;

    /// <summary>Gets the window icon: an <c>avares://</c> URI of an Avalonia asset, or else the path of an image file. Defaults to the operating system's.</summary>
    string? Icon => null;

    /// <summary>Gets the application version, shown in the title bar and the info popup. Defaults to the entry assembly's <c>major.minor.build</c> version, or <c>1.0.0</c> if it has none.</summary>
    string? Version => null;

    /// <summary>Gets a value indicating whether the main window runs in kiosk mode, which hides the minimize and maximize buttons and has the close button restart rather than exit. Defaults to <see langword="false"/>.</summary>
    bool IsKiosk => false;

    /// <summary>Gets a value indicating whether alert messages are kept apart from the rest: the client then has an alert inbox and a normal inbox, an alert outbox and a normal outbox, and the entry lists have no alert filter. Defaults to <see langword="false"/>, where alerts are mixed in with the other messages and the lists of inboxes and outboxes can be filtered to alerts only. A draft list never has an alert filter.</summary>
    bool SeparateAlerts => false;

    /// <summary>Gets the label of the network indicator in the top bar of a client while it shows online. Defaults to <c>ONLINE</c>.</summary>
    string? NetworkOnlineLabel => null;

    /// <summary>Gets the label of the network indicator while it shows offline. Defaults to <c>OFFLINE</c>.</summary>
    string? NetworkOfflineLabel => null;

    /// <summary>Gets the color of the network indicator while it shows online, as a hex color such as <c>#2E7D32</c>. Defaults to green.</summary>
    string? NetworkOnlineColor => null;

    /// <summary>Gets the color of the network indicator while it shows offline, as a hex color such as <c>#D35400</c>. Defaults to orange.</summary>
    string? NetworkOfflineColor => null;

    /// <summary>Gets the text shown in the content area when no entry is selected. Defaults to <c>HOME</c>.</summary>
    string? HomeText => null;

    /// <summary>Gets the text shown in the title bar's alert box while alarming, and the draft editor's alert checkbox label. Defaults to <c>ALERT</c>.</summary>
    string? AlertLabel => null;

    /// <summary>Gets the word the user interface uses for a user of the network, in the install screen, the help and the connection tables. Defaults to <c>User</c>.</summary>
    string? UserLabel => null;

    /// <summary>Gets the plural of <see cref="UserLabel"/>. Defaults to <see cref="UserLabel"/> with an <c>s</c> added when that is stated, otherwise <c>Users</c>.</summary>
    string? UserPluralLabel => null;

    /// <summary>Gets the name of the tag input in the draft editor (for example <c>Category</c>). Defaults to <c>Tag</c>.</summary>
    string? TagLabel => null;

    /// <summary>Gets the name of the priority concept, as the entry list's priority filter and the help call it. Defaults to <c>Priority</c>.</summary>
    string? PriorityLabel => null;

    /// <summary>Gets the name of the message level concept, as the entry list's message level filter calls it. Defaults to <c>Message Level</c>.</summary>
    string? MessageLevelLabel => null;

    /// <summary>Gets the name of the message aspect concept, as the draft view's selector calls it. Defaults to <c>Message Aspect</c>.</summary>
    string? MessageAspectLabel => null;

    /// <summary>Gets the plural of <see cref="MessageAspectLabel"/>. Defaults to <see cref="MessageAspectLabel"/> with an <c>s</c> added when that is stated, otherwise <c>Message Aspects</c>.</summary>
    string? MessageAspectPluralLabel => null;

    /// <summary>Gets the plural of <see cref="AlertLabel"/>, for text such as "Alerts". Defaults to <see cref="AlertLabel"/> with an <c>s</c> added when that is stated, otherwise <c>ALERTS</c>.</summary>
    string? AlertPluralLabel => null;

    /// <summary>Gets the plural of <see cref="TagLabel"/>. Defaults to <see cref="TagLabel"/> with an <c>s</c> added when that is stated, otherwise <c>Tags</c>.</summary>
    string? TagPluralLabel => null;

    /// <summary>Gets the plural of <see cref="PriorityLabel"/>. Defaults to <see cref="PriorityLabel"/> with an <c>s</c> added when that is stated, otherwise <c>Priorities</c>.</summary>
    string? PriorityPluralLabel => null;

    /// <summary>Gets the plural of <see cref="MessageLevelLabel"/>. Defaults to <see cref="MessageLevelLabel"/> with an <c>s</c> added when that is stated, otherwise <c>Message Levels</c>.</summary>
    string? MessageLevelPluralLabel => null;

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
