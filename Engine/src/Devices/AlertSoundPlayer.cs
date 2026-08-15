namespace BlueHeighliner.Comlink.Engine.Devices;

/// <summary>
/// Plays and stops the alarm sound while one or more alerts are pending (see <see cref="ViewModels.IAlertViewModel"/>).
/// This is real OS-level audio playback, not configuration — see <see cref="IEngineController"/> for the
/// configurable alarm text/duration. Not a control interface: the engine always provides real behavior for
/// this directly, the same way it always provides real behavior for printer discovery/driving
/// (see <see cref="IPrintDriver"/>) rather than leaving either to a host. Public only because it
/// is a constructor dependency of the public <see cref="ViewModels.AlertViewModel"/> — not meant to be
/// overridden by a host the way control interfaces are.
/// </summary>
public interface IAlertSoundPlayer
{
    /// <summary>
    /// Starts playing the alarm sound on a loop. Called whenever a new alert is received while one or more
    /// alerts are already pending. Idempotent — calling this while already playing does not double up playback.
    /// </summary>
    void Play();
    /// <summary>Stops the alarm sound. Called when the auto-stop duration elapses, or when every pending alert has been read.</summary>
    void Stop();
}

/// <summary>
/// Plays a looping synthesized beep tone using the operating system's own audio facilities — <c>paplay</c>
/// (PulseAudio) on Linux, <c>winmm.dll</c>'s <c>PlaySound</c> on Windows. Best-effort: any failure (missing
/// binary, no audio device, unsupported platform) is swallowed so the rest of the alert flow keeps working
/// with no sound rather than crashing the app.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class AlertSoundPlayer : IAlertSoundPlayer
{
    private const int SampleRate = 44100;
    private const double ToneHz = 880;
    private const double BeepSeconds = 0.15;
    private const double SilenceSeconds = 0.85;

    private readonly object lockObject = new();
    private CancellationTokenSource? cts;

    /// <inheritdoc />
    public void Play()
    {
        lock (lockObject)
        {
            if (cts is not null) { return; }
            CancellationTokenSource newCts = new();
            cts = newCts;
            if (OperatingSystem.IsWindows()) { PlayWindows(newCts.Token); }
            else if (OperatingSystem.IsLinux()) { _ = Task.Run(() => PlayLoopLinux(newCts.Token)); }
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (lockObject)
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
            if (OperatingSystem.IsWindows()) { StopWindows(); }
        }
    }

    private static byte[] BuildPcmFrame()
    {
        int beepSamples = (int)(SampleRate * BeepSeconds);
        int silenceSamples = (int)(SampleRate * SilenceSeconds);
        byte[] frame = new byte[(beepSamples + silenceSamples) * 2];

        for (int i = 0; i < beepSamples; i++)
        {
            short sample = (short)(short.MaxValue * 0.5 * Math.Sin(2 * Math.PI * ToneHz * i / SampleRate));
            BitConverter.GetBytes(sample).CopyTo(frame, i * 2);
        }

        return frame;
    }

    private static async Task PlayLoopLinux(CancellationToken cancellation)
    {
        byte[] frame = BuildPcmFrame();
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                using Process process = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "paplay",
                        Arguments = $"--raw --rate={SampleRate} --channels=1 --format=s16le",
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                // Drain (rather than inherit) stdout/stderr too, so paplay's own diagnostic chatter never
                // leaks to the app's console — playback failure is already best-effort here.
                Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellation);
                Task<string> stderr = process.StandardError.ReadToEndAsync(cancellation);
                await process.StandardInput.BaseStream.WriteAsync(frame, cancellation);
                process.StandardInput.Close();
                await process.WaitForExitAsync(cancellation);
                await Task.WhenAll(stdout, stderr);
            }
            catch
            {
                return;
            }
        }
    }

    private static void PlayWindows(CancellationToken cancellation)
    {
        try
        {
            byte[] wav = BuildWavFile(BuildPcmFrame());
            PlaySound(wav, nint.Zero, SND_MEMORY | SND_ASYNC | SND_LOOP | SND_NODEFAULT);
        }
        catch
        {
            // Best-effort, matching the Linux path.
        }
    }

    private static void StopWindows()
    {
        try
        {
            PlaySound(null, nint.Zero, 0);
        }
        catch
        {
            // Best-effort, matching the Linux path.
        }
    }

    private static byte[] BuildWavFile(byte[] pcm)
    {
        const int channels = 1;
        const int bitsPerSample = 16;
        int byteRate = SampleRate * channels * bitsPerSample / 8;
        int blockAlign = channels * bitsPerSample / 8;

        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + pcm.Length);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(SampleRate);
            writer.Write(byteRate);
            writer.Write((short)blockAlign);
            writer.Write((short)bitsPerSample);
            writer.Write("data"u8);
            writer.Write(pcm.Length);
            writer.Write(pcm);
        }
        return stream.ToArray();
    }

    private const uint SND_ASYNC = 0x0001;
    private const uint SND_LOOP = 0x0008;
    private const uint SND_MEMORY = 0x0004;
    private const uint SND_NODEFAULT = 0x0002;

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool PlaySound(byte[]? pszSound, nint hmod, uint fdwSound);
}
