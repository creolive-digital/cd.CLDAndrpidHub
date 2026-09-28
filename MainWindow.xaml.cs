using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AndroidHub;

public partial class MainWindow : Window
{
    private readonly string _runtime = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory, "RuntimeAssets");
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CLD Android Hub", "settings.json");
    private readonly MirrorSession _mirror = new();
    private readonly CancellationTokenSource _lifetime = new();
    private bool _refreshing, _closing, _canClose, _starting;
    private string? _recording;
    private sealed record Device(string Id, string State, string Label)
    {
        public override string ToString() => Label;
    }
    private sealed record Preferences(int Quality, bool Audio, bool Awake, bool Top, bool ScreenOff, bool Fullscreen);
    private Device? SelectedDevice => DeviceCombo.SelectedItem as Device;

    public MainWindow()
    {
        InitializeComponent();
        LoadPreferences();
        _mirror.Message += line => Dispatcher.BeginInvoke(() =>
        {
            if (_closing) return;
            if (line.Contains("Texture:")) { SetStatus("LIVE · Streaming", true); SessionText.Text = "Live session"; }
            if (line.Contains(" fps")) StreamStatsText.Text = line.Replace("INFO: ", "");
            if (line.Contains("ERROR:") || line.Contains("WARN:")) FooterText.Text = line;
        });
        _mirror.Ended += code => Dispatcher.BeginInvoke(() =>
        {
            if (_closing) return;
            SetSessionControls(false);
            SetStatus(code == 0 ? "Mirror closed" : "Mirror interrupted", false);
            SessionText.Text = "Ready when you are";
            StreamStatsText.Text = "Stream idle";
            FooterText.Text = code == 0 ? (_recording is null ? "Session ended." : $"Recording: {_recording}") : "The mirror stopped. Open session log for details, then check the connection.";
            _recording = null;
        });
        Loaded += async (_, _) => await RefreshDevicesAsync();
        Closing += async (_, e) =>
        {
            if (_canClose) return;
            e.Cancel = true;
            if (_closing) return;
            _closing = true;
            SavePreferences();
            _lifetime.Cancel();
            await _mirror.StopAsync();
            _canClose = true;
            Close();
        };
    }

    private async Task<(int Code, string Output, byte[] Bytes)> AdbAsync(bool binary, params string[] args)
    {
        var path = Path.Combine(_runtime, "adb.exe");
        if (!File.Exists(path)) throw new FileNotFoundException("RuntimeAssets/adb.exe is missing. Keep RuntimeAssets next to CLD Android Hub.exe.");
        var info = new ProcessStartInfo(path) { WorkingDirectory = _runtime, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start ADB.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(args.Contains("install") || args.Contains("push") ? 300 : 20));
        using var buffer = new MemoryStream();
        var output = process.StandardOutput.BaseStream.CopyToAsync(buffer, timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await Task.WhenAll(output, error, process.WaitForExitAsync(timeout.Token)); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            throw new TimeoutException(_closing ? "Application closing." : "The phone did not respond in time. Check its connection and try again.");
        }
        var bytes = buffer.ToArray();
        var text = (binary ? "" : System.Text.Encoding.UTF8.GetString(bytes)) + await error;
        if (process.ExitCode != 0) throw new InvalidOperationException(text.Trim());
        return (process.ExitCode, text.Trim(), bytes);
    }

    private async Task RefreshDevicesAsync()
    {
        if (_refreshing || _closing || _mirror.IsRunning || _starting) return;
        _refreshing = true;
        RefreshButton.IsEnabled = false;
        StartButton.IsEnabled = false;
        SetStatus("Scanning devices…");
        var selectedId = SelectedDevice?.Id;
        var discovered = false;
        try
        {
            var result = await AdbAsync(false, "devices", "-l");
            discovered = true;
            var devices = new List<Device>();
            foreach (var line in result.Output.Split('\n'))
            {
                var parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || parts[0] == "List" || parts[0].StartsWith('*')) continue;
                if (parts[1] is not ("device" or "offline" or "unauthorized" or "recovery" or "sideload")) continue;
                var model = parts.FirstOrDefault(p => p.StartsWith("model:"))?[6..].Replace('_', ' ') ?? parts[0];
                devices.Add(new(parts[0], parts[1], $"{model} · {parts[0]} · {parts[1]}"));
            }
            DeviceCombo.ItemsSource = devices;
            DeviceCombo.SelectedItem = devices.FirstOrDefault(d => d.Id == selectedId) ?? devices.FirstOrDefault(d => d.State == "device") ?? devices.FirstOrDefault();
            DeviceCountText.Text = $"{devices.Count(d => d.State == "device")} READY";
            UpdateDeviceState();
            if (SelectedDevice is { State: "device" } device)
            {
                var info = await AdbAsync(false, "-s", device.Id, "shell", "getprop", "ro.build.version.release");
                var battery = await AdbAsync(false, "-s", device.Id, "shell", "dumpsys", "battery");
                var level = battery.Output.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("level:"))?.Trim();
                if (SelectedDevice?.Id == device.Id) DeviceInfoText.Text = $"Android {info.Output}   /   Battery {level?.Split(':').Last().Trim() ?? "—"}%";
            }
        }
        catch (Exception ex)
        {
            if (!discovered) { DeviceCombo.ItemsSource = null; DeviceCountText.Text = "0 READY"; UpdateDeviceState(); }
            FooterText.Text = ex.Message;
            SetStatus("Connection check failed");
        }
        finally { _refreshing = false; RefreshButton.IsEnabled = !_mirror.IsRunning; StartButton.IsEnabled = SelectedDevice?.State == "device" && !_mirror.IsRunning && !_starting; }
    }

    private void UpdateDeviceState()
    {
        var ready = SelectedDevice is { State: "device" };
        DeviceNameText.Text = ready ? SelectedDevice!.Label.Split('·')[0].Trim() : "Connect your Android";
        DeviceHintText.Text = ready ? "Authorized and ready to stream." : SelectedDevice?.State == "unauthorized" ? "Unlock your phone and accept the USB debugging prompt." : SelectedDevice is null ? "Connect USB, enable USB debugging, then scan devices." : "Phone is offline or unavailable. Reconnect and scan again.";
        ActionsPanel.IsEnabled = ready;
        StartButton.IsEnabled = ready && !_mirror.IsRunning && !_starting && !_refreshing;
        DeviceCombo.IsEnabled = !_mirror.IsRunning && !_starting;
        if (!_mirror.IsRunning) SetStatus(ready ? "Device ready" : "Waiting for device", ready);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshDevicesAsync();
    private void DeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded) { PreviewImage.Source = null; DeviceInfoText.Text = "USB or wireless debugging"; UpdateDeviceState(); } }
    private void SetStatus(string text, bool ready = false) { StatusText.Text = text; StatusDot.Fill = (Brush)FindResource(ready ? "Mint" : "BusyPurple"); }

    private async void StartMirror_Click(object sender, RoutedEventArgs e)
    {
        if (_starting || _mirror.IsRunning || SelectedDevice is not { State: "device" } device) return;
        _starting = true;
        SetSessionControls(true);
        string? recording = null;
        if (RecordCheck.IsChecked == true)
        {
            var save = new SaveFileDialog { Title = "CLD Android Hub · Save recording", Filter = "Matroska video (*.mkv)|*.mkv", FileName = $"CLD-Android-Hub-{DateTime.Now:yyyyMMdd-HHmmss}.mkv" };
            if (save.ShowDialog(this) != true) { _starting = false; SetSessionControls(false); return; }
            recording = save.FileName;
        }
        try
        {
            var profile = QualityCombo.SelectedIndex switch { 1 => (Size: 1920, Rate: "12M", Fps: 60, Buffer: 0), 2 => (Size: 1024, Rate: "4M", Fps: 60, Buffer: 50), 3 => (Size: 1920, Rate: "16M", Fps: 120, Buffer: 0), _ => (Size: 1280, Rate: "8M", Fps: 60, Buffer: 0) };
            var args = new List<string> { $"--serial={device.Id}", "--window-title=CLD Android Hub · Live mirror", "--video-codec=h264", $"--max-size={profile.Size}", $"--max-fps={profile.Fps}", $"--video-bit-rate={profile.Rate}", $"--video-buffer={profile.Buffer}", "--window-height=800", "--push-target=/sdcard/Download/", "--print-fps" };
            if (FullscreenCheck.IsChecked == true) args.Add("--fullscreen");
            if (AudioCheck.IsChecked != true) args.Add("--no-audio");
            if (AwakeCheck.IsChecked == true) args.Add("--stay-awake");
            if (TopCheck.IsChecked == true) args.Add("--always-on-top");
            if (ScreenOffCheck.IsChecked == true) args.Add("--turn-screen-off");
            if (recording is not null) args.Add($"--record={recording}");
            _recording = recording;
            _mirror.Start(_runtime, args);
            SessionText.Text = "Opening your live mirror…";
            SetStatus("Starting video stream…");
            FooterText.Text = "The mirror opens in its own desktop window. F11 toggles fullscreen; Alt + Q closes it.";
            SavePreferences();
        }
        catch (Exception ex) { _recording = null; FooterText.Text = ex.Message; SetSessionControls(false); SetStatus("Could not start mirror"); }
        finally { _starting = false; UpdateDeviceState(); }
        await Task.CompletedTask;
    }

    private void SetSessionControls(bool running)
    {
        StartButton.IsEnabled = !running && SelectedDevice?.State == "device";
        StopButton.IsEnabled = running;
        FocusButton.IsEnabled = running;
        SettingsPanel.IsEnabled = !running;
        DeviceCombo.IsEnabled = !running;
        RefreshButton.IsEnabled = !running;
        WirelessPanel.IsEnabled = !running;
    }
    private async void StopMirror_Click(object sender, RoutedEventArgs e) { StopButton.IsEnabled = false; await _mirror.StopAsync(); }
    private void FocusMirror_Click(object sender, RoutedEventArgs e) => _mirror.Focus();

    private async Task WithPhoneAsync(Func<Device, Task> action)
    {
        if (SelectedDevice is not { State: "device" } device) return;
        ActionsPanel.IsEnabled = false;
        try { await action(device); }
        catch (Exception ex) { FooterText.Text = ex.Message; }
        finally { ActionsPanel.IsEnabled = SelectedDevice?.State == "device"; }
    }
    private async void PhoneKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string code }) await WithPhoneAsync(async d => { await AdbAsync(false, "-s", d.Id, "shell", "input", "keyevent", code); FooterText.Text = "Phone control sent."; });
    }
    private async void InstallApk_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "CLD Android Hub · Install APK", Filter = "Android package (*.apk)|*.apk" };
        if (dialog.ShowDialog(this) == true) await WithPhoneAsync(async d => { FooterText.Text = "Installing app…"; var result = await AdbAsync(false, "-s", d.Id, "install", "-r", dialog.FileName); FooterText.Text = result.Output; });
    }
    private async void PushFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "CLD Android Hub · Send file" };
        if (dialog.ShowDialog(this) == true) await WithPhoneAsync(async d => { FooterText.Text = "Sending file…"; await AdbAsync(false, "-s", d.Id, "push", dialog.FileName, "/sdcard/Download/"); FooterText.Text = "File sent to your phone’s Downloads folder."; });
    }
    private async void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = "CLD Android Hub · Save screenshot", Filter = "PNG image (*.png)|*.png", FileName = $"CLD-Android-Hub-{DateTime.Now:yyyyMMdd-HHmmss}.png" };
        if (dialog.ShowDialog(this) == true) await WithPhoneAsync(async d =>
        {
            var result = await AdbAsync(true, "-s", d.Id, "exec-out", "screencap", "-p");
            using var stream = new MemoryStream(result.Bytes);
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
            await File.WriteAllBytesAsync(dialog.FileName, result.Bytes);
            PreviewImage.Source = bitmap;
            FooterText.Text = $"Screenshot saved: {dialog.FileName}";
        });
    }
    private static bool ValidAddress(string value) => !value.Any(char.IsWhiteSpace) && Uri.TryCreate("tcp://" + value, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host) && uri.Port is > 0 and <= 65535 && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
    private async void WirelessConnect_Click(object sender, RoutedEventArgs e)
    {
        var pair = (sender as Button)?.Tag as string == "pair";
        var address = (pair ? PairAddress.Text : WirelessAddress.Text).Trim();
        if (!ValidAddress(address)) { FooterText.Text = "Enter a valid host:port address from Wireless debugging settings."; return; }
        var code = PairCode.Password.Trim();
        if (pair && (code.Length != 6 || !code.All(char.IsAsciiDigit))) { FooterText.Text = "Enter the six-digit pairing code shown on the phone."; return; }
        WirelessPanel.IsEnabled = false;
        try
        {
            var result = pair ? await AdbAsync(false, "pair", address, code) : await AdbAsync(false, "connect", address);
            if (pair) PairCode.Clear();
            await RefreshDevicesAsync();
            FooterText.Text = result.Output;
        }
        catch (Exception ex) { FooterText.Text = ex.Message; }
        finally { WirelessPanel.IsEnabled = true; }
    }
    private void Log_Click(object sender, RoutedEventArgs e)
    {
        new Window { Owner = this, Icon = (ImageSource)FindResource("CldLogo"), Title = "CLD Android Hub · Session log", Width = 850, Height = 480, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new TextBox { Text = string.IsNullOrWhiteSpace(_mirror.Log) ? "No mirror session yet." : _mirror.Log, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), Padding = new Thickness(18) } }.Show();
    }
    private void LoadPreferences()
    {
        try
        {
            var source = File.Exists(_settingsPath) ? _settingsPath : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AndroidHub", "settings.json");
            var p = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(source));
            if (p is null) return;
            QualityCombo.SelectedIndex = Math.Clamp(p.Quality, 0, 3); AudioCheck.IsChecked = p.Audio; AwakeCheck.IsChecked = p.Awake; TopCheck.IsChecked = p.Top; ScreenOffCheck.IsChecked = p.ScreenOff; FullscreenCheck.IsChecked = p.Fullscreen;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
    }
    private void SavePreferences()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!); File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new Preferences(QualityCombo.SelectedIndex, AudioCheck.IsChecked == true, AwakeCheck.IsChecked == true, TopCheck.IsChecked == true, ScreenOffCheck.IsChecked == true, FullscreenCheck.IsChecked == true))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { FooterText.Text = "Could not save preferences: " + ex.Message; }
    }
}
