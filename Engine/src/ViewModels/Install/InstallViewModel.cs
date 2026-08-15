namespace BlueHeighliner.Comlink.Engine.ViewModels.Install;

/// <summary>ViewModel interface for the user-installation screen.</summary>
public interface IInstallViewModel
{
    /// <summary>Raised after a user is successfully installed, providing the resulting user information.</summary>
    event Func<UserInfo, Task>? InstallSucceeded;

    /// <summary>Gets or sets the user activation code entered by the user (auto-uppercased).</summary>
    string UserCode { get; set; }
    /// <summary>Gets or sets the error message to display, or <see langword="null"/> when there is no error.</summary>
    string? ErrorMessage { get; set; }
    /// <summary>Gets or sets a value indicating whether an install operation is in progress.</summary>
    bool IsLoading { get; set; }
    /// <summary>Validates the user code and installs the user via the service connection.</summary>
    IAsyncRelayCommand InstallCommand { get; }
}

/// <summary>ViewModel for the user-installation screen, handling user code entry and the install command.</summary>
public sealed partial class InstallViewModel : ObservableObject, IInstallViewModel
{
    /// <summary>Initializes a new <see cref="InstallViewModel"/> with the required service connection.</summary>
    /// <param name="connection">Service connection used to install the user.</param>
    public InstallViewModel(IServiceConnection connection)
    {
        this.connection = connection;
    }

    private readonly IServiceConnection connection;

    [ObservableProperty] private string userCode = string.Empty;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isLoading;

    partial void OnUserCodeChanged(string value)
    {
        string upper = value.ToUpperInvariant();
        if (value != upper) { UserCode = upper; }
    }

    /// <summary>Raised after a user is successfully installed, providing the resulting user information.</summary>
    public event Func<UserInfo, Task>? InstallSucceeded;

    [RelayCommand]
    private async Task Install()
    {
        if (string.IsNullOrWhiteSpace(UserCode))
        {
            ErrorMessage = "Please enter a user code.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            UserInfo? userInfo = await connection.InstallUser(UserCode.Trim());
            if (userInfo is null)
            {
                ErrorMessage = "Invalid user code. Please try again.";
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
        }
        finally
        {
            IsLoading = false;
        }
    }
}
