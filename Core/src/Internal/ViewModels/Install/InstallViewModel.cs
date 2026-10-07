namespace BlueHeighliner.Comlink;

/// <summary>ViewModel interface for the user-installation screen.</summary>
internal interface IInstallViewModel
{
    /// <summary>Raised after a user is successfully installed, providing the resulting user information.</summary>
    event Func<UserInfo, Task>? InstallSucceeded;

    /// <summary>Gets or sets the user name entered by the user (auto-uppercased).</summary>
    string UserName { get; set; }
    /// <summary>Gets or sets the error message to display, or <see langword="null"/> when there is no error.</summary>
    string? ErrorMessage { get; set; }
    /// <summary>Gets or sets a value indicating whether an install operation is in progress.</summary>
    bool IsLoading { get; set; }
    /// <summary>Installs the user via the service connection.</summary>
    IAsyncRelayCommand InstallCommand { get; }
}

/// <summary>ViewModel for the user-installation screen, handling user name entry and the install command.</summary>
internal sealed partial class InstallViewModel : ObservableObject, IInstallViewModel
{
    /// <summary>Initializes a new <see cref="InstallViewModel"/> with the required service connection.</summary>
    /// <param name="connection">Service connection used to install the user.</param>
    /// <param name="loggerFactory">Factory for the activity logger that records why an install failed.</param>
    /// <param name="engineController">Words the messages shown with the host's name for a user.</param>
    public InstallViewModel(IServiceConnection connection, ILoggerFactory loggerFactory, IEngineController engineController)
    {
        this.connection = connection;
        this.engineController = engineController;
        logger = loggerFactory.CreateLogger(LogCategories.App);
    }

    private readonly IServiceConnection connection;
    private readonly IEngineController engineController;
    private readonly ILogger logger;

    [ObservableProperty] private string userName = string.Empty;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isLoading;

    partial void OnUserNameChanged(string value)
    {
        string upper = value.ToUpperInvariant();
        if (value != upper) { UserName = upper; }
    }

    /// <summary>Raised after a user is successfully installed, providing the resulting user information.</summary>
    public event Func<UserInfo, Task>? InstallSucceeded;

    [RelayCommand]
    private async Task Install()
    {
        if (string.IsNullOrWhiteSpace(UserName))
        {
            ErrorMessage = engineController.Display("Please enter a user name.");
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            UserInfo? userInfo = await connection.InstallUser(UserName.Trim());
            if (userInfo is null)
            {
                ErrorMessage = engineController.Display("No such user. Please try again.");
                logger.Record(LogEvents.InstallFailed, "Install of {UserName} failed: {Reason}", UserName.Trim(), "the network has no such user");
                return;
            }

            if (InstallSucceeded is not null)
            {
                await InstallSucceeded(userInfo);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Install failed: {ex.Message}";
            logger.Record(LogEvents.InstallFailed, "Install of {UserName} failed: {Reason}", UserName.Trim(), ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
