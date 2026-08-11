namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Plays a looping synthesized beep tone via <c>paplay</c> (PulseAudio) while alarming. Best-effort: any
/// failure (missing binary, no audio device, unsupported platform) is swallowed so the alert box and quick
/// confirmation still work with no sound rather than crashing the app.
/// </summary>
public sealed class SampleAlertSoundPlayer : IAlertSoundPlayer
{
    private const int SampleRate = 44100;
    private const double ToneHz = 880;
    private const double BeepSeconds = 0.15;
    private const double SilenceSeconds = 0.85;

    private readonly object _lock = new();
    private CancellationTokenSource? _cts;

    /// <inheritdoc />
    public void Play()
    {
        lock (_lock)
        {
            if (_cts is not null) return;
            CancellationTokenSource cts = new();
            _cts = cts;
            _ = Task.Run(() => PlayLoop(cts.Token));
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }

    private static async Task PlayLoop(CancellationToken cancellation)
    {
        byte[] frame = BuildFrame();
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
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                await process.StandardInput.BaseStream.WriteAsync(frame, cancellation);
                process.StandardInput.Close();
                await process.WaitForExitAsync(cancellation);
            }
            catch
            {
                return;
            }
        }
    }

    private static byte[] BuildFrame()
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
}
