namespace BlueHeighliner.Comlink.Engine.Views.Controls;

/// <summary>Modal dialog that prompts the user for a new folder name.</summary>
[ExcludeFromCodeCoverage]
public partial class FolderNameDialog : Window
{
    /// <summary>Initializes the dialog and wires up OK, Cancel, and keyboard handlers.</summary>
    public FolderNameDialog()
    {
        InitializeComponent();
        OkBtn.Click += (_, _) => Confirm();
        CancelBtn.Click += (_, _) => Close(null);
        NameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Confirm();
            else if (e.Key == Key.Escape) Close(null);
        };
        Opened += (_, _) => NameBox.Focus();
    }

    private void Confirm()
    {
        var name = NameBox.Text?.Trim();
        if (!string.IsNullOrEmpty(name))
            Close(name);
    }
}
