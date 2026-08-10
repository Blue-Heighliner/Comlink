namespace BlueHeighliner.Comlink.Engine.Data;

/// <summary>Represents the delivery state of a message to a single destination site.</summary>
public enum DestinationStatus
{
    /// <summary>The message is currently being transmitted.</summary>
    Sending,
    /// <summary>The message was transmitted but delivery has not been confirmed.</summary>
    Sent,
    /// <summary>Delivery failed with an error.</summary>
    Failed,
    /// <summary>The destination site acknowledged receipt.</summary>
    Confirmed
}
