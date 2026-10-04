namespace BlueHeighliner.Comlink;

/// <summary>
/// ViewModel for the retrieve screen: asking the server a message is stored on to send
/// back copies of the messages it stored that fit a date range, authors, destinations and message IDs. Registered as
/// a DI singleton (see <see cref="MainViewModel.Retrieve"/>) so what was entered survives navigating away and back.
/// </summary>
internal interface IRetrieveViewModel
{
    /// <summary>Gets the storage servers a request can be sent to, from <see cref="IEngineController.StorageServers"/>.</summary>
    IReadOnlyList<string> AvailableServers { get; }
    /// <summary>Gets or sets the server the request is sent to; the first available server by default.</summary>
    string? SelectedServer { get; set; }
    /// <summary>Gets or sets the date of the inclusive lower bound on a message's original sent time.</summary>
    DateTimeOffset? DateFrom { get; set; }
    /// <summary>Gets or sets the time of day paired with <see cref="DateFrom"/>; midnight when unset.</summary>
    TimeSpan? TimeFrom { get; set; }
    /// <summary>Gets or sets the date of the inclusive upper bound on a message's original sent time.</summary>
    DateTimeOffset? DateTo { get; set; }
    /// <summary>Gets or sets the time of day paired with <see cref="DateTo"/>; the last instant of the day when unset, so picking only a date covers that whole day.</summary>
    TimeSpan? TimeTo { get; set; }
    /// <summary>Gets or sets the sender names to match, separated by commas, semicolons or new lines; empty for any sender.</summary>
    string Authors { get; set; }
    /// <summary>Gets or sets the addressee names to match, separated by commas, semicolons or new lines; empty for any addressee.</summary>
    string Destinations { get; set; }
    /// <summary>Gets or sets the message IDs to match, separated by commas, semicolons or new lines; empty for any message.</summary>
    string Ids { get; set; }
    /// <summary>Gets a value indicating whether a request is currently being sent.</summary>
    bool IsRequesting { get; }
    /// <summary>Gets the message shown after a request attempt.</summary>
    string? StatusMessage { get; }
    /// <summary>Sends the request for the criteria entered to <see cref="SelectedServer"/>.</summary>
    IAsyncRelayCommand RequestCommand { get; }
    /// <summary>Clears every criterion back to unset, leaving <see cref="SelectedServer"/> alone.</summary>
    IRelayCommand ResetCommand { get; }
}

/// <inheritdoc cref="IRetrieveViewModel" />
internal sealed partial class RetrieveViewModel : ObservableObject, IRetrieveViewModel
{
    /// <summary>Initializes a new <see cref="RetrieveViewModel"/> with the configured storage servers and the service that sends the request.</summary>
    /// <param name="engineController">Supplies the storage servers a request can go to.</param>
    /// <param name="retrievalService">Sends the request.</param>
    public RetrieveViewModel(IEngineController engineController, IRetrievalService retrievalService)
    {
        this.retrievalService = retrievalService;
        AvailableServers = engineController.StorageServers;
        selectedServer = AvailableServers.FirstOrDefault();
    }

    private readonly IRetrievalService retrievalService;
    private readonly char[] separators = [',', ';', '\n', '\r'];
    /// <summary>The last instant of a day (23:59:59.999), paired with an unset <see cref="TimeTo"/>.</summary>
    private readonly TimeSpan endOfDay = new(0, 23, 59, 59, 999);

    [ObservableProperty] private string? selectedServer;
    [ObservableProperty] private DateTimeOffset? dateFrom;
    [ObservableProperty] private TimeSpan? timeFrom;
    [ObservableProperty] private DateTimeOffset? dateTo;
    [ObservableProperty] private TimeSpan? timeTo;
    [ObservableProperty] private string authors = string.Empty;
    [ObservableProperty] private string destinations = string.Empty;
    [ObservableProperty] private string ids = string.Empty;
    [ObservableProperty] private string? statusMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RequestCommand))]
    private bool isRequesting;

    /// <inheritdoc />
    public IReadOnlyList<string> AvailableServers { get; }

    private List<string> Split(string text)
        => [.. text.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase)];

    private DateTime ToUtc(DateTime local) => DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();

    private RetrievalCriteria BuildCriteria() => new()
    {
        From = DateFrom is { } from ? ToUtc(from.Date + (TimeFrom ?? TimeSpan.Zero)) : null,
        To = DateTo is { } to ? ToUtc(to.Date + (TimeTo ?? endOfDay)) : null,
        Authors = Split(Authors),
        Destinations = Split(Destinations),
        Ids = Split(Ids)
    };

    [RelayCommand(CanExecute = nameof(CanRequest))]
    private async Task Request()
    {
        if (SelectedServer is not { } server) { StatusMessage = "Select a server"; return; }

        IsRequesting = true;
        StatusMessage = null;
        try
        {
            bool sent = await retrievalService.Request(server, BuildCriteria());
            StatusMessage = sent
                ? $"Requested from {server}; matching messages will arrive in your Inbox"
                : $"Could not reach {server}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Retrieval failed: {ex.Message}";
        }
        finally
        {
            IsRequesting = false;
        }
    }

    private bool CanRequest() => !IsRequesting;

    [RelayCommand]
    private void Reset()
    {
        DateFrom = null;
        TimeFrom = null;
        DateTo = null;
        TimeTo = null;
        Authors = string.Empty;
        Destinations = string.Empty;
        Ids = string.Empty;
        StatusMessage = null;
    }
}
