namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>High-level client API for interacting with a running Engine service instance.</summary>
public interface IServiceConnection
{
    /// <summary>Raised when a new inbound message arrives.</summary>
    event Func<MessageReceivedEvent, Task>? MessageReceived;
    /// <summary>Raised when the delivery status of an outbound message changes.</summary>
    event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;
    /// <summary>Establishes the connection to the Engine service.</summary>
    Task Connect(CancellationToken cancellation = default);
    /// <summary>Returns this site's own <see cref="SiteInfo"/>, or <see langword="null"/> if not yet registered.</summary>
    Task<SiteInfo?> GetSiteInfo(CancellationToken cancellation = default);
    /// <summary>Returns the names of all known sites in the messaging system.</summary>
    Task<List<string>> GetSiteNames(CancellationToken cancellation = default);
    /// <summary>Registers this instance as a site using <paramref name="siteCode"/> and returns the resulting <see cref="SiteInfo"/>.</summary>
    Task<SiteInfo?> InstallSite(string siteCode, CancellationToken cancellation = default);
    /// <summary>Sends a message with the given <paramref name="subject"/> and <paramref name="body"/> to the specified <paramref name="addresses"/>.</summary>
    Task<SendMessageResult?> SendMessage(string subject, string body, List<AddressRequest> addresses, CancellationToken cancellation = default);
}
