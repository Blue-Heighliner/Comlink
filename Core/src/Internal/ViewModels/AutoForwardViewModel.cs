namespace BlueHeighliner.Comlink;

/// <summary>
/// ViewModel for the auto forward screen: choosing one of the auto forwarders this instance's own
/// installed user has access to (see <see cref="UserInfo.AutoForwarders"/>), then adding to and
/// removing from its locally-saved target list. Registered as a DI singleton (see <see cref="MainViewModel.AutoForward"/>)
/// so its state survives navigating the content area away to other views and back.
/// </summary>
internal interface IAutoForwardViewModel
{
    /// <summary>Gets the auto forwarders the current user has access to, populated by <see cref="RefreshCommand"/>.</summary>
    IReadOnlyList<AutoForwarderDefinition> AvailableControllers { get; }
    /// <summary>Gets a value indicating whether <see cref="AvailableControllers"/> is non-empty.</summary>
    bool HasControllers { get; }
    /// <summary>Gets or sets the controller whose target list is shown. Setting this reloads <see cref="Targets"/> from local storage.</summary>
    AutoForwarderDefinition? SelectedController { get; set; }
    /// <summary>Gets <see cref="SelectedController"/>'s target list, in the order added.</summary>
    ObservableCollection<string> Targets { get; }
    /// <summary>Gets a value indicating whether <see cref="Targets"/> is non-empty.</summary>
    bool HasTargets { get; }
    /// <summary>Gets or sets the user name being typed into the add-target field.</summary>
    string NewTargetUser { get; set; }
    /// <summary>Gets all known user names available for target auto-complete.</summary>
    IReadOnlyList<string> AllUserNames { get; }
    /// <summary>Re-scans which controllers the current user has access to and refreshes <see cref="AllUserNames"/>, selecting the first available controller.</summary>
    IAsyncRelayCommand RefreshCommand { get; }
    /// <summary>Adds <see cref="NewTargetUser"/> to <see cref="SelectedController"/>'s target list and saves it, unless already present.</summary>
    IAsyncRelayCommand AddTargetCommand { get; }
    /// <summary>Removes the given user from <see cref="SelectedController"/>'s target list and saves it.</summary>
    IAsyncRelayCommand<string> RemoveTargetCommand { get; }
}

/// <inheritdoc cref="IAutoForwardViewModel" />
internal sealed partial class AutoForwardViewModel : ObservableObject, IAutoForwardViewModel
{
    /// <summary>Initializes a new <see cref="AutoForwardViewModel"/> with the configured controllers, current user, target-list storage, and user directory.</summary>
    /// <param name="engineController">Supplies the auto forwarders added via <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.AutoForwarder"/>.</param>
    /// <param name="currentUserProvider">Determines which controllers the current user has access to.</param>
    /// <param name="targetsRepository">Loads and saves each controller's locally-saved target list.</param>
    /// <param name="connection">Supplies known user names for target auto-complete.</param>
    public AutoForwardViewModel(IEngineController engineController, ICurrentUserProvider currentUserProvider, IAutoForwardTargetsRepository targetsRepository, IEngineConnection connection)
    {
        this.engineController = engineController;
        this.currentUserProvider = currentUserProvider;
        this.targetsRepository = targetsRepository;
        this.connection = connection;
        Targets.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTargets));
    }

    private readonly IEngineController engineController;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly IAutoForwardTargetsRepository targetsRepository;
    private readonly IEngineConnection connection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasControllers))]
    private IReadOnlyList<AutoForwarderDefinition> availableControllers = [];
    [ObservableProperty] private AutoForwarderDefinition? selectedController;
    [ObservableProperty] private string newTargetUser = string.Empty;
    [ObservableProperty] private IReadOnlyList<string> allUserNames = [];

    /// <inheritdoc />
    public ObservableCollection<string> Targets { get; } = [];

    /// <inheritdoc />
    public bool HasControllers => AvailableControllers.Count > 0;

    /// <inheritdoc />
    public bool HasTargets => Targets.Count > 0;

    partial void OnSelectedControllerChanged(AutoForwarderDefinition? value) => _ = LoadTargets(value);

    [RelayCommand]
    private async Task Refresh()
    {
        string userName = currentUserProvider.UserName ?? string.Empty;
        AvailableControllers = [.. engineController.AutoForwarders.Where(forwarder => engineController.GetUserInfo(userName).AutoForwarders.Contains(forwarder.Name))];
        AllUserNames = await connection.GetUserNames();
        SelectedController = AvailableControllers.FirstOrDefault();
    }

    private async Task LoadTargets(AutoForwarderDefinition? controller)
    {
        Targets.Clear();
        if (controller is null)
        {
            return;
        }

        AutoForwardTargetsEntity? entity = await targetsRepository.Get(controller.Name);
        if (entity is null)
        {
            return;
        }

        foreach (string target in entity.Targets)
        {
            Targets.Add(target);
        }
    }

    [RelayCommand]
    private async Task AddTarget()
    {
        if (SelectedController is null || string.IsNullOrWhiteSpace(NewTargetUser))
        {
            return;
        }

        string user = NewTargetUser.Trim();
        if (!Targets.Contains(user, StringComparer.OrdinalIgnoreCase))
        {
            Targets.Add(user);
            await targetsRepository.Save(SelectedController.Name, [.. Targets]);
        }
        NewTargetUser = string.Empty;
    }

    [RelayCommand]
    private async Task RemoveTarget(string user)
    {
        if (SelectedController is null)
        {
            return;
        }

        Targets.Remove(user);
        await targetsRepository.Save(SelectedController.Name, [.. Targets]);
    }
}
