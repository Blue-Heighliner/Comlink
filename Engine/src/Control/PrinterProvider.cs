namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Enumerates the printers available on this computer for the print manager to target.</summary>
public interface IPrinterProvider
{
    /// <summary>Returns the names of every printer currently available on this computer.</summary>
    IReadOnlyList<string> GetAvailablePrinters();
    /// <summary>Returns the name of this computer's default printer, or <see langword="null"/> if none is configured.</summary>
    string? GetDefaultPrinter();
}

/// <summary>
/// Drives a named line printer: prints one line at a time, waiting for confirmation that the line has
/// finished printing before the print queue advances to the next line, and issues a page feed between jobs.
/// See <see cref="ViewModels.IPrintManagerViewModel"/> for the queue that drives this interface.
/// </summary>
public interface ILinePrinter
{
    /// <summary>
    /// Prints a single line to the named printer. The returned task completes once the printer confirms the
    /// line has finished printing — the print queue will not print the next line (or check for a
    /// higher-priority job that should interrupt this one) until this task completes.
    /// </summary>
    /// <param name="printerName">Name of the target printer, as returned by <see cref="IPrinterProvider"/>.</param>
    /// <param name="line">The line of text to print.</param>
    /// <param name="cancellation">Cancellation token.</param>
    Task PrintLine(string printerName, string line, CancellationToken cancellation = default);
    /// <summary>
    /// Issues a page feed on the named printer. Called after the last line of an entry is printed, and also
    /// when a print job is interrupted partway through by a higher-priority job.
    /// </summary>
    /// <param name="printerName">Name of the target printer, as returned by <see cref="IPrinterProvider"/>.</param>
    /// <param name="cancellation">Cancellation token.</param>
    Task PageFeed(string printerName, CancellationToken cancellation = default);
}

/// <summary>
/// Default <see cref="IPrinterProvider"/>/<see cref="ILinePrinter"/> querying and driving the operating
/// system's own printing facilities — Windows Print Spooler (WinSpool) on Windows, CUPS (<c>lp</c>/<c>lpstat</c>)
/// on Linux. Printer discovery is best-effort (see <see cref="GetAvailablePrinters"/>); line printing submits
/// each line as its own raw print job and polls the OS's own job status until it reports the job finished
/// printing before returning, so the print queue's "wait for confirmation" semantics reflect genuine
/// OS-reported completion rather than just "the app handed the bytes off." Members are <see langword="virtual"/>
/// so a host can inherit and override — see <c>Docs/Control.md</c>.
/// </summary>
[ExcludeFromCodeCoverage]
public partial class DefaultPrinterProvider : IPrinterProvider, ILinePrinter
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public virtual IReadOnlyList<string> GetAvailablePrinters()
    {
        if (OperatingSystem.IsWindows()) return GetWindowsPrinters();
        if (OperatingSystem.IsLinux()) return GetLinuxPrinters();
        return [];
    }

    /// <inheritdoc />
    public virtual string? GetDefaultPrinter()
    {
        if (OperatingSystem.IsWindows()) return GetWindowsDefaultPrinter();
        if (OperatingSystem.IsLinux()) return GetLinuxDefaultPrinter();
        return null;
    }

    /// <inheritdoc />
    public virtual Task PrintLine(string printerName, string line, CancellationToken cancellation = default) =>
        PrintRaw(printerName, line + "\r\n", cancellation);

    /// <inheritdoc />
    public virtual Task PageFeed(string printerName, CancellationToken cancellation = default) =>
        PrintRaw(printerName, "\f", cancellation);

    private static Task PrintRaw(string printerName, string content, CancellationToken cancellation)
    {
        if (OperatingSystem.IsWindows()) return PrintRawWindows(printerName, content, cancellation);
        if (OperatingSystem.IsLinux()) return PrintRawLinux(printerName, content, cancellation);
        return Task.CompletedTask;
    }

    // ── Windows (WinSpool) ───────────────────────────────────────────────────

    private static IReadOnlyList<string> GetWindowsPrinters()
    {
        string? output = RunPowerShell("Get-CimInstance -ClassName Win32_Printer | Select-Object -ExpandProperty Name");
        if (output is null) return [];
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static string? GetWindowsDefaultPrinter()
    {
        string? output = RunPowerShell(
            "Get-CimInstance -ClassName Win32_Printer | Where-Object { $_.Default } | Select-Object -First 1 -ExpandProperty Name");
        if (output is null) return null;
        string name = output.Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    private static string? RunPowerShell(string command)
    {
        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -NonInteractive -Command \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            // Drain (rather than inherit) stderr too, so a tool's own diagnostic chatter never leaks to
            // the app's console — this is a best-effort discovery call, so it's discarded, not logged.
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            stderr.Wait();
            return output;
        }
        catch
        {
            return null;
        }
    }

    private static async Task PrintRawWindows(string printerName, string content, CancellationToken cancellation)
    {
        try
        {
            // pDefault = NULL opens the printer with the default access rights, which is sufficient to
            // submit print jobs (the same pattern Microsoft's own raw-printing sample code uses) — it is
            // not the handle that gates OpenPrinter's own outcome, only its bool return is trustworthy;
            // MSDN documents phPrinter as "undefined" (not necessarily zero) when OpenPrinter fails, so
            // cleanup must be gated on that return value rather than on the handle being non-zero.
            if (!OpenPrinter(printerName, out nint printerHandle, 0))
                return;

            try
            {
                DOC_INFO_1 docInfo = new()
                {
                    pDocName = "Comlink line print",
                    pOutputFile = null,
                    pDataType = "RAW"
                };
                int jobId = StartDocPrinter(printerHandle, 1, ref docInfo);
                if (jobId == 0) return;

                bool wroteSuccessfully;
                try
                {
                    if (!StartPagePrinter(printerHandle)) return;
                    try
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(content);
                        wroteSuccessfully = WritePrinter(printerHandle, bytes, bytes.Length, out int written) && written == bytes.Length;
                    }
                    finally
                    {
                        // EndPagePrinter only pairs with a StartPagePrinter that actually succeeded — the
                        // early return above (StartPagePrinter failing) skips this whole inner try, so it
                        // is never called without a matching successful StartPagePrinter.
                        EndPagePrinter(printerHandle);
                    }
                }
                finally
                {
                    EndDocPrinter(printerHandle);
                }

                if (!wroteSuccessfully) return;

                await WaitForWindowsJobCompletion(printerHandle, jobId, cancellation);
            }
            finally
            {
                ClosePrinter(printerHandle);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort, matching the Linux path and printer discovery: an unexpected P/Invoke or
            // marshaling failure must not propagate out of PrintLine/PageFeed and silently kill
            // PrintManagerViewModel's fire-and-forget print loop for the rest of the app's lifetime.
        }
    }

    private static async Task WaitForWindowsJobCompletion(nint printerHandle, int jobId, CancellationToken cancellation)
    {
        const uint TerminalStatus = JOB_STATUS_PRINTED | JOB_STATUS_DELETED | JOB_STATUS_ERROR | JOB_STATUS_COMPLETE;
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < MaxWait)
        {
            cancellation.ThrowIfCancellationRequested();

            GetJob(printerHandle, jobId, 1, 0, 0, out int needed);
            if (needed <= 0) return;

            nint buffer = Marshal.AllocHGlobal(needed);
            try
            {
                if (GetJob(printerHandle, jobId, 1, buffer, needed, out _))
                {
                    JOB_INFO_1 info = Marshal.PtrToStructure<JOB_INFO_1>(buffer);
                    if ((info.Status & TerminalStatus) != 0) return;
                }
                else
                {
                    return;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            await Task.Delay(PollInterval, cancellation);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOC_INFO_1
    {
        public string? pDocName;
        public string? pOutputFile;
        public string? pDataType;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JOB_INFO_1
    {
        public uint JobId;
        public string? pPrinterName;
        public string? pMachineName;
        public string? pUserName;
        public string? pDocument;
        public string? pDatatype;
        public string? pStatus;
        public uint Status;
        public uint Priority;
        public uint Position;
        public uint TotalPages;
        public uint PagesPrinted;
        public SYSTEMTIME Submitted;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public ushort wYear, wMonth, wDayOfWeek, wDay, wHour, wMinute, wSecond, wMilliseconds;
    }

    private const uint JOB_STATUS_ERROR = 0x00000002;
    private const uint JOB_STATUS_PRINTED = 0x00000080;
    private const uint JOB_STATUS_DELETED = 0x00000100;
    private const uint JOB_STATUS_COMPLETE = 0x00001000;

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string pPrinterName, out nint phPrinter, nint pDefault);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(nint hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(nint hPrinter, int level, ref DOC_INFO_1 docInfo);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(nint hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(nint hPrinter);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(nint hPrinter);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(nint hPrinter, byte[] pBytes, int dwCount, out int dwWritten);

    [DllImport("winspool.drv", EntryPoint = "GetJobW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetJob(nint hPrinter, int jobId, int level, nint pJob, int cbBuf, out int pcbNeeded);

    // ── Linux (CUPS) ──────────────────────────────────────────────────────────

    private static IReadOnlyList<string> GetLinuxPrinters()
    {
        string? output = RunCommand("lpstat", "-p");
        if (output is null) return [];

        List<string> printers = [];
        foreach (string line in output.Split('\n'))
        {
            if (!line.StartsWith("printer ", StringComparison.Ordinal)) continue;
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) printers.Add(parts[1]);
        }
        return printers;
    }

    private static string? GetLinuxDefaultPrinter()
    {
        string? output = RunCommand("lpstat", "-d");
        if (output is null) return null;

        const string marker = "system default destination:";
        int index = output.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;

        string name = output[(index + marker.Length)..].Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    private static async Task PrintRawLinux(string printerName, string content, CancellationToken cancellation)
    {
        try
        {
            string? jobId = await SubmitLinuxJob(printerName, content, cancellation);
            if (jobId is null) return;

            Stopwatch elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < MaxWait)
            {
                cancellation.ThrowIfCancellationRequested();

                // lpstat's -o option takes destination (printer) names, not job IDs — list the printer's
                // not-yet-completed jobs and check whether our specific job is still among them. Each line
                // starts with the job ID as its own whitespace-delimited token (e.g. "PRINTER-12  user  ...");
                // comparing only a line prefix would let "PRINTER-2" wrongly match a line for "PRINTER-20".
                string? pending = RunCommand("lpstat", "-W", "not-completed", "-o", printerName);
                bool stillPending = pending is not null &&
                    pending.Split('\n').Any(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [var firstToken, ..] && firstToken == jobId);
                if (!stillPending) return;

                await Task.Delay(PollInterval, cancellation);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort, matching the Windows path: an unexpected process-launch or I/O failure must not
            // propagate out of PrintLine/PageFeed and silently kill the print queue's loop.
        }
    }

    private static async Task<string?> SubmitLinuxJob(string printerName, string content, CancellationToken cancellation)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "lp",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        // ArgumentList passes each argument through verbatim (no shell/argv re-splitting on whitespace),
        // unlike building a single Arguments string — required since printer names routinely contain
        // spaces (e.g. "HP LaserJet 4"), which a single "-d {printerName} -o raw" string would break apart.
        process.StartInfo.ArgumentList.Add("-d");
        process.StartInfo.ArgumentList.Add(printerName);
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add("raw");

        process.Start();
        // Drain (rather than inherit) stderr too, so lp's own diagnostic chatter never leaks to the app's
        // console — PrintRawLinux's caller already treats a submission failure as best-effort.
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellation);
        await process.StandardInput.WriteAsync(content.AsMemory(), cancellation);
        process.StandardInput.Close();
        string output = await process.StandardOutput.ReadToEndAsync(cancellation);
        await process.WaitForExitAsync(cancellation);
        await stderr;

        // Output format: "request id is PRINTER-123 (1 file(s))"
        const string marker = "request id is ";
        int index = output.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;
        string rest = output[(index + marker.Length)..].TrimStart();
        int spaceIndex = rest.IndexOf(' ');
        return spaceIndex < 0 ? rest.Trim() : rest[..spaceIndex];
    }

    private static string? RunCommand(string fileName, params string[] arguments)
    {
        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            foreach (string argument in arguments)
                process.StartInfo.ArgumentList.Add(argument);
            process.Start();
            // Drain (rather than inherit) stderr too, so a tool's own diagnostic chatter — e.g. lpstat's
            // "No destinations added." when no printer is configured — never leaks to the app's console;
            // this is a best-effort discovery/status call, so it's discarded, not logged.
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            stderr.Wait();
            return output;
        }
        catch
        {
            return null;
        }
    }
}
