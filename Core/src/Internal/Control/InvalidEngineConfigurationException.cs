namespace BlueHeighliner.Comlink;

/// <summary>Thrown when the host's engine configuration, stated through the builder, is invalid; unlike a problem in the configuration file it is never logged, since only the host can fix it.</summary>
/// <param name="message">What is wrong with the configuration.</param>
/// <param name="innerException">The error that revealed it, if any.</param>
internal sealed class InvalidEngineConfigurationException(string message, Exception? innerException = null) : InvalidOperationException(message, innerException);
