namespace BlueHeighliner.Comlink.ViewModels;

/// <summary>ViewModel interface for the help window: a tabbed guide to using the application in its different ways.</summary>
public interface IHelpViewModel
{
    /// <summary>Gets the application name shown in the help window's title.</summary>
    string AppName { get; }
    /// <summary>Gets the tabs of the guide, each covering one way of using the application. Which tabs exist depends on the configured <see cref="NodeRole"/> and message composition settings.</summary>
    IReadOnlyList<HelpTab> Tabs { get; }
}

/// <summary>Builds the help guide's content from the running configuration, so it only describes what this instance actually offers.</summary>
public sealed class HelpViewModel : IHelpViewModel
{
    /// <summary>Initializes a new <see cref="HelpViewModel"/> whose tabs reflect <paramref name="engineController"/>.</summary>
    /// <param name="engineController">Provides the application name, node role, and which optional compose features are enabled.</param>
    public HelpViewModel(IEngineController engineController)
    {
        AppName = engineController.AppName;
        Tabs = engineController.Role == NodeRole.Server ? BuildServerTabs() : BuildMessagingTabs(engineController);
    }

    /// <inheritdoc />
    public string AppName { get; }

    /// <inheritdoc />
    public IReadOnlyList<HelpTab> Tabs { get; }

    private static List<HelpTab> BuildServerTabs() =>
    [
        new HelpTab("Overview",
        [
            new HelpSection("What this is", "This instance is a server. It routes messages between the client users that belong to it and the other servers in its cluster. It has no inbox, outbox, notes or drafts of its own, and it does not store the messages it routes."),
            new HelpSection("Switching views", "Use CONNECTIONS and ACTIVITY at the left of the title bar to switch between the two views. CONNECTIONS is shown when the window opens.")
        ]),
        new HelpTab("Connections",
        [
            new HelpSection("Servers and clients", "The SERVERS table lists the other servers in the cluster and the CLIENTS table lists the client users that belong to this server. A table is hidden while it has no rows."),
            new HelpSection("Reading a row", "A row shows the user it is for, whether the connection is up right now, when it last connected and when it last disconnected. Hover a time to see it in full."),
            new HelpSection("When a row stays disconnected", "The server keeps trying to reach every listed user, so a row that stays disconnected means that user is not running, cannot be reached at its configured address or port, or presented a certificate this server does not recognize."),
            new HelpSection("Closing a connection", "Right-click a row and choose Close to drop that connection and stop it coming back: the server no longer tries to reach that user and turns away any connection the user makes to it. The row turns grey and reads CLOSED. Choose Open on the same row to let it connect again. A closed connection is forgotten when the application restarts."),
            new HelpSection("Refreshing a connection", "Right-click a row and choose Refresh to drop the connection and form a new one straight away, which is useful when a row looks stuck. Refresh is not available on a closed connection.")
        ]),
        new HelpTab("Activity",
        [
            new HelpSection("Daily log", "ACTIVITY shows the activity log: one entry per day, newest events first. It records connections coming up and going down, and anything the server could not route."),
            new HelpSection("Picking a day", "Choose a day in the middle list to read its events on the right.")
        ])
    ];

    private static List<HelpTab> BuildMessagingTabs(IEngineController engineController)
    {
        List<HelpTab> tabs =
        [
            new HelpTab("Getting started",
            [
                new HelpSection("The first time", "If the window asks for a user code, enter the code you were given and press INSTALL. This ties the application to your user name, and it only happens once."),
                new HelpSection("The three columns", "Folders are on the left, the entries in the selected folder are in the middle, and the selected entry opens on the right. Inbox holds received messages, Outbox holds sent messages, Drafts holds messages you have not sent, Notes holds your own notes, and Activity holds a log for each day."),
                new HelpSection("Title bar", "NEW DRAFT and NEW NOTE start something new. EXPORT and IMPORT back up and restore your entries, and PRINTS opens the print queue. The i button shows the application version and the ? button opens this guide.")
            ]),
            BuildSendingTab(engineController),
            BuildReceivingTab(engineController),
            new HelpTab("Notes and drafts",
            [
                new HelpSection("Notes", "NEW NOTE opens a blank note. Write in it and press SAVE. Notes are private to you and are never sent anywhere."),
                new HelpSection("Drafts", "A draft is a message you are still working on. Press SAVE to keep it in Drafts and come back to it later, and open it from the Drafts folder to carry on. Sending a draft moves it to Outbox."),
                new HelpSection("Finding them again", "Select the Notes or Drafts folder and pick the entry from the middle column. Notes and drafts are listed most recent first, and the sort button at the top of the list switches between Sort: Recent and Sort: A-Z.")
            ]),
            new HelpTab("Folders and entries",
            [
                new HelpSection("Browsing", "Select a folder to list its entries. When there are more entries than fit on a page, use Prev and Next at the top of the list, which also shows which page you are on. COLLAPSE above the folders folds the folder tree back up."),
                new HelpSection("Selecting several", "Click an entry to open it. Shift-click selects a range and Ctrl-click adds or removes single entries. Selecting several is how you choose what to export."),
                new HelpSection("Your own folders", "Right-click a folder and choose New Folder to add a subfolder. You can delete a subfolder you created, but only once it is empty. Drag an entry onto a folder of the matching kind to move it there: messages go to Inbox or Outbox folders, drafts to Drafts folders and notes to Notes folders."),
                new HelpSection("Deleting and printing", "Right-click an entry to print it, or to delete it where deleting is allowed for that kind of entry.")
            ]),
            new HelpTab("Backup and restore",
            [
                new HelpSection("Exporting", "Press EXPORT, choose the destination drive, name the file, and choose All entries or Some entries. For Some, click or shift-click entries in the folders to add them, and remove any you did not mean to add. Press START EXPORT. The result is a single .export.zip file on the drive."),
                new HelpSection("Importing", "Press IMPORT, choose the drive holding the export, then choose the package to restore. Messages and activity logs that already exist are merged in."),
                new HelpSection("Name clashes", "If a draft or note in the package has the same name as one you already have, you are asked whether to keep yours, overwrite it, or overwrite every clash. The import waits for your answer, and you can leave the screen and come back to it."),
                new HelpSection("Drives not listed", "Press REFRESH after plugging a drive in.")
            ]),
            new HelpTab("Printing",
            [
                new HelpSection("Printing one entry", "Right-click an entry and choose Print to add it to the print queue."),
                new HelpSection("The queue", "PRINTS shows everything waiting. Choose a printer at the top of the screen and printing starts. Entries you added yourself print first, then automatically queued ones by priority, then in the order they arrived. Remove one entry from the queue with its x button, or press PURGE to clear the queue."),
                new HelpSection("Printing received messages", "Tick Automatically print received messages to queue every message as it arrives. How many copies each message gets is decided by this installation, so an important message may print more than once.")
            ])
        ];

        if (engineController.Role == NodeRole.Client)
        {
            tabs.Add(new HelpTab("Connection",
            [
                new HelpSection("The status row", "This instance sends and receives through a server. The row at the bottom of the window shows whether that connection is up, when it last connected and when it last dropped."),
                new HelpSection("When it is down", "Messages you send while the connection is down fail rather than waiting. The application keeps trying to reconnect, and the row turns to connected as soon as it succeeds. Send again after that."),
                new HelpSection("Closing and refreshing", "Right-click the row to choose Close, which drops the connection and stops the application reconnecting, or Refresh, which drops it and forms a new one straight away. While it is closed the row is grey and reads CLOSED, sending fails, and connections from the server are turned away; choose Open on the row to go back to normal. A closed connection is forgotten when the application restarts.")
            ]));
        }

        return tabs;
    }

    private static HelpTab BuildSendingTab(IEngineController engineController)
    {
        List<HelpSection> sections =
        [
            new HelpSection("Starting a message", "Press NEW DRAFT. Type the name of each recipient in the address box and press ADD. Choose To or CC for each one first. Names complete as you type, and you can also address a group, which delivers to everyone in it once."),
            new HelpSection("Writing it", "Give the message a subject and write the body. The FILL-IN button puts a blank in the text at the cursor. Click the blank to build a list of options for it and choose the one to use."),
            new HelpSection("Spelling out letters and digits", "The PLSO button cycles OFF, ON and SPACES. While it is on, each letter or digit you type is written as its phonetic word, for example G as GOLF and 5 as FIVE, with a space after each word in SPACES. Backspace removes a whole word at a time.")
        ];

        if (engineController.TagsEnabled)
        {
            sections.Add(new HelpSection($"{engineController.TagLabel}", $"Fill in the {engineController.TagLabel} box to label the message. Some {engineController.TagLabel} and priority combinations are not allowed, and a blocked choice is either not offered or is put back to the last allowed one."));
        }

        sections.Add(new HelpSection("Priority", "Choose a priority to control how urgently the message is sent when several are waiting. Higher priorities go first."));

        if (engineController.ComposeAlertsEnabled)
        {
            sections.Add(new HelpSection($"Sending as {engineController.AlertLabel}", $"Tick the {engineController.AlertLabel} box to make the message an alert. Every recipient's window sounds an alarm until they have read it."));
        }

        sections.Add(new HelpSection("Sending", "Press SEND to send it, or SAVE to keep it as a draft. A sent message moves to Outbox and shows a delivery status for each recipient, described under Receiving messages."));
        return new HelpTab("Sending a message", sections);
    }

    private static HelpTab BuildReceivingTab(IEngineController engineController)
    {
        List<HelpSection> sections =
        [
            new HelpSection("New messages", "Messages arrive in Inbox. Opening one marks it as read and tells the sender you have read it."),
            new HelpSection("Delivery status", "Open a message from Outbox and expand its delivery status to see each recipient. Sending means it is on its way, Sent means it left this computer, Confirmed means the recipient's application has it, Read means the recipient has opened it, and Failed means it could not be delivered, in which case send it again. The overall status is only Read once every recipient has read it.")
        ];

        sections.Add(new HelpSection(engineController.AlertLabel, $"When an {engineController.AlertLabel} message arrives, a red box appears in the title bar and an alarm sounds. The sound stops by itself after a while, but the box stays until every {engineController.AlertLabel} message has been read. Open the message to read it. If quick confirmation is on for this installation, you can also click the box or press Space or Enter, which reads the most recent one and does the next on each further press."));
        return new HelpTab("Receiving messages", sections);
    }
}
