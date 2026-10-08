namespace BlueHeighliner.Comlink;

/// <summary>Modal dialog that asks the user to confirm an action; closes with <see langword="true"/> only when confirmed.</summary>
[ExcludeFromCodeCoverage]
internal partial class ConfirmDialog : Window
{
    /// <summary>Initializes the dialog with no text; used by the XAML loader.</summary>
    public ConfirmDialog() => InitializeComponent();

    /// <summary>Initializes the dialog with a title, a message, and the label of its confirming button.</summary>
    /// <param name="title">Window title.</param>
    /// <param name="message">Question shown to the user.</param>
    /// <param name="confirmLabel">Label of the button that confirms.</param>
    public ConfirmDialog(string title, string message, string confirmLabel)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        OkBtn.Content = confirmLabel;
        OkBtn.Click += (_, _) => Close(true);
        CancelBtn.Click += (_, _) => Close(false);
        KeyDown += (_, e) =>
        {
            if (e.Key is Key.Escape)
            {
                Close(false);
            }
        };
        Opened += (_, _) => CancelBtn.Focus();
    }
}
