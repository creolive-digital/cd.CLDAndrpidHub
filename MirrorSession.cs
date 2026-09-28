using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AndroidHub;

internal sealed class MirrorSession
{
    private Process? _process;
    private readonly Queue<string> _log = new();
    public event Action<string>? Message;
    public event Action<int>? Ended;
    public bool IsRunning => _process is { HasExited: false };
    public string Log { get { lock (_log) return string.Join(Environment.NewLine, _log); } }
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);

    public void Start(string runtime, IEnumerable<string> arguments)
    {
        if (IsRunning) throw new InvalidOperationException("A mirror is already running.");
        var info = new ProcessStartInfo(Path.Combine(runtime, "scrcpy.exe"))
        {
            WorkingDirectory = runtime, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        info.Environment["ADB"] = Path.Combine(runtime, "adb.exe");
        info.Environment["SCRCPY_SERVER_PATH"] = Path.Combine(runtime, "scrcpy-server");
        info.Environment["SCRCPY_ICON_DIR"] = Path.GetFullPath(Path.Combine(runtime, "..", "BrandAssets"));
        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        lock (_log) _log.Clear();
        var process = new Process { StartInfo = info };
        _process = process;
        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);
        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _ = ObserveAsync(process);
        }
        catch { _process = null; process.Dispose(); throw; }
    }

    private void Append(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        lock (_log) { _log.Enqueue(line); while (_log.Count > 300) _log.Dequeue(); }
        Message?.Invoke(line);
    }

    private async Task ObserveAsync(Process process)
    {
        await process.WaitForExitAsync();
        var code = process.ExitCode;
        if (_process == process) _process = null;
        Ended?.Invoke(code);
        process.Dispose();
    }

    public void Focus()
    {
        if (_process is not { HasExited: false } process) return;
        process.Refresh();
        SetForegroundWindow(process.MainWindowHandle);
    }

    public async Task StopAsync()
    {
        if (_process is not { HasExited: false } process) return;
        try
        {
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero)
                PostMessage(process.MainWindowHandle, 0x0010, IntPtr.Zero, IntPtr.Zero);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { if (!process.HasExited) process.Kill(true); }
        }
        catch (InvalidOperationException) { }
    }
}
