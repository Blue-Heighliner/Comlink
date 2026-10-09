namespace BlueHeighliner.Comlink;

/// <summary>
/// Configures the MicroGate options used for every serial connection, continuing the fluent chain of the engine builder: one call per option, for example <c>.Hdlc().MaxInfoField(1024).TransmitWindow(4).Display&lt;MyDisplayHandler&gt;()</c>. The line settings must match the station at the other end of the cable. The HDLC address is not an option; it comes from each serial connection point. Anything not stated keeps the MicroGate package's own default.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
public interface IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>States the line encoding, which must match the station at the other end of the cable. Defaults to NRZ.</summary>
    /// <param name="encoding">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Encoding(HdlcEncoding encoding);

    /// <summary>States the CRC, which must match the remote station. Defaults to CRC-32 CCITT.</summary>
    /// <param name="crc">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Crc(HdlcCrc crc);

    /// <summary>States where the receive clock comes from. Defaults to the device's own pin.</summary>
    /// <param name="receiveClockSource">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> ReceiveClockSource(HdlcReceiveClockSource receiveClockSource);

    /// <summary>States where the transmit clock comes from. Defaults to the device's own pin.</summary>
    /// <param name="transmitClockSource">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> TransmitClockSource(HdlcTransmitClockSource transmitClockSource);

    /// <summary>States the divisor the phase locked loop applies when recovering a clock from the data stream. Defaults to divide by 32.</summary>
    /// <param name="phaseLockedLoopDivisor">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> PhaseLockedLoopDivisor(HdlcPhaseLockedLoopDivisor phaseLockedLoopDivisor);

    /// <summary>States the speed, in bits per second, of the device's internal baud rate generator. Defaults to 4800.</summary>
    /// <param name="clockSpeed">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> ClockSpeed(int clockSpeed);

    /// <summary>States the pattern transmitted while the line is idle. Defaults to flags.</summary>
    /// <param name="idlePattern">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IdlePattern(HdlcIdlePattern idlePattern);

    /// <summary>States the length of the preamble transmitted before each frame. Defaults to 8 bits.</summary>
    /// <param name="preambleLength">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> PreambleLength(HdlcPreambleLength preambleLength);

    /// <summary>States the pattern transmitted as a preamble before each frame. Defaults to none.</summary>
    /// <param name="preamblePattern">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> PreamblePattern(HdlcPreamblePattern preamblePattern);

    /// <summary>States what the device does when it runs out of data mid-frame. Defaults to aborting with 7 ones.</summary>
    /// <param name="underrunAction">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> UnderrunAction(HdlcUnderrunAction underrunAction);

    /// <summary>States whether the poll/final bit is left unused. Defaults to false.</summary>
    /// <param name="disablePollFinalBit">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> DisablePollFinalBit(bool disablePollFinalBit);

    /// <summary>States the largest payload, in bytes, of one information frame. Defaults to 1500.</summary>
    /// <param name="maxInfoField">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> MaxInfoField(int maxInfoField);

    /// <summary>States how long to wait before again trying to form the connection. Defaults to 1 second.</summary>
    /// <param name="retryInterval">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> RetryInterval(TimeSpan? retryInterval);

    /// <summary>States how long sent data may go unacknowledged before the remote station is polled for what it has received. Defaults to 1 second.</summary>
    /// <param name="retransmitInterval">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> RetransmitInterval(TimeSpan? retransmitInterval);

    /// <summary>States how long to wait for more frames to arrive before acknowledging the ones received. Defaults to 200 milliseconds.</summary>
    /// <param name="acknowledgeDelay">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> AcknowledgeDelay(TimeSpan acknowledgeDelay);

    /// <summary>States how many times unacknowledged information frames are sent again before the remote peer is considered gone. Defaults to 21.</summary>
    /// <param name="maxRetransmissions">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> MaxRetransmissions(int? maxRetransmissions);

    /// <summary>States how many information frames may be sent before one must be acknowledged. Defaults to 7.</summary>
    /// <param name="transmitWindow">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> TransmitWindow(int transmitWindow);

    /// <summary>States whether the device loops its transmitted data back to its receiver, for testing. Defaults to false.</summary>
    /// <param name="loopback">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Loopback(bool loopback);

    /// <summary>States whether a lost remote station is detected and the connection reported down. Defaults to true.</summary>
    /// <param name="detectDisconnect">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> DetectDisconnect(bool detectDisconnect);

    /// <summary>States whether every frame on the line is made observable. Defaults to false.</summary>
    /// <param name="enableMonitor">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> EnableMonitor(bool enableMonitor);

    /// <summary>States whether the modem wires are reported. Defaults to false.</summary>
    /// <param name="enableSignals">The value to use.</param>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> EnableSignals(bool enableSignals);
}
