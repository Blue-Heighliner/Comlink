namespace BlueHeighliner.Comlink;

/// <summary>Says which user this node runs as, which is the user its certificate names (see <see cref="NodeIdentity"/>), or why that cannot be told, and checks the certificate again when asked.</summary>
internal interface IUserService
{
    /// <summary>Raised after <see cref="Refresh"/> changed who the current user is, including to nobody when the certificate can no longer be used, once the new state is in place.</summary>
    event Action? Changed;
    /// <summary>Gets who this node is as of the last check: the user and the thumbprints of the certificates it proves it with. It names no user while <see cref="Problem"/> is set.</summary>
    NodeIdentity Identity { get; }
    /// <summary>Gets why the node cannot be identified as of the last check, which the UI shows in place of the app, or <see langword="null"/> when it can.</summary>
    string? Problem { get; }
    /// <summary>Returns a <see cref="IUserInfo"/> for the user this node runs as, or <see langword="null"/> while <see cref="Problem"/> is set.</summary>
    IUserInfo? GetCurrentUserInfo();
    /// <summary>
    /// Reads the certificate files again, with the network file as it is now, and works out who the node is: when the certificate now names another user, that user becomes current, and when it can no longer be used
    /// nobody is current and <see cref="Problem"/> says why; <see cref="Changed"/> announces either. A certificate that is replaced but still names the same user changes <see cref="Identity"/> only, which the caller sees by comparing thumbprints.
    /// </summary>
    /// <returns><see langword="true"/> when the current user changed, including to or from nobody.</returns>
    Task<bool> Refresh(CancellationToken cancellation = default);
}

/// <inheritdoc />
internal sealed class UserService : IUserService
{
    /// <summary>Starts from what the check at startup found; the host logs a problem found then once it has logged that it is starting.</summary>
    /// <param name="engineController">Describes the current user.</param>
    /// <param name="currentUserProvider">Holds the name of the current user, which this changes.</param>
    /// <param name="builder">What the host stated, for the certificate files.</param>
    /// <param name="network">The users of the network and the command-line arguments.</param>
    /// <param name="initial">What the check at startup found.</param>
    /// <param name="loggerFactory">Factory for the logger that records a problem a refresh finds.</param>
    public UserService(IEngineController engineController, ICurrentUserProvider currentUserProvider, EngineBuilder builder, NetworkConfig network, IdentityCheck initial, ILoggerFactory loggerFactory)
    {
        this.engineController = engineController;
        this.currentUserProvider = currentUserProvider;
        this.builder = builder;
        this.network = network;
        logger = loggerFactory.CreateLogger(LogCategories.App);
        identity = initial.Identity;
        problem = initial.Problem;
    }

    private readonly IEngineController engineController;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly EngineBuilder builder;
    private readonly NetworkConfig network;
    private readonly MicrosoftLogger logger;
    private volatile NodeIdentity identity;
    private volatile string? problem;

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public NodeIdentity Identity => identity;

    /// <inheritdoc />
    public string? Problem => problem;

    /// <inheritdoc />
    public IUserInfo? GetCurrentUserInfo() => currentUserProvider.UserName is { Length: > 0 } name ? engineController.GetUserInfo(name) : null;

    /// <inheritdoc />
    public Task<bool> Refresh(CancellationToken cancellation = default)
    {
        string? previous = currentUserProvider.UserName;
        IdentityCheck check = MsmtCertificateLookup.CheckIdentity(builder, network);
        identity = check.Identity;
        problem = check.Problem;
        currentUserProvider.UserName = check.Problem is null ? check.Identity.UserName : null;
        if (check.Problem is not null)
        {
            logger.Record(LogEvents.IdentityFailed, "Cannot identify the user: {Problem}", check.Problem);
        }

        bool changed = !string.Equals(previous, currentUserProvider.UserName, StringComparison.OrdinalIgnoreCase);
        if (changed)
        {
            if (currentUserProvider.UserName is { } name)
            {
                logger.Record(LogEvents.UserChanged, "Now running as {UserName}", name);
            }

            Changed?.Invoke();
        }

        return Task.FromResult(changed);
    }
}
