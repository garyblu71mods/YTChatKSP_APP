using System.Diagnostics;
using YTchatKSP_App.Services;

namespace YTchatKSP_App;

public partial class MainForm : Form
{
    private HttpApiServer _apiServer;
    private YouTubeChatService _chatService;
    private Process? _bridgeProcess;
    private System.Windows.Forms.Timer? _bridgeMonitorTimer;
    private string? _lastConnectedVideoId;

    public MainForm()
    {
        InitializeComponent();
        _apiServer = new HttpApiServer();
        _chatService = new YouTubeChatService();
    }

    private async void MainForm_Load(object sender, EventArgs e)
    {
        try
        {
            await _apiServer.StartAsync();
            _apiServer.OnLog += (msg) => AddLog(msg);
            _apiServer.OnMessageReceived += OnChatMessageReceived;
            _apiServer.OnConnectionStatusChanged += OnConnectionStatusChanged;

            _chatService.OnLog += (msg) => AddLog(msg);
            _chatService.OnConnectionStatusChanged += OnConnectionStatusChanged;

            // Monitor procesu bridge co 10 sekund
            _bridgeMonitorTimer = new System.Windows.Forms.Timer();
            _bridgeMonitorTimer.Interval = 10000;
            _bridgeMonitorTimer.Tick += (s, e) => CheckBridgeProcessHealth();
            _bridgeMonitorTimer.Start();

            AddLog("✓ HTTP API Server initialized");
            AddLog("✓ Chat Service initialized");
        }
        catch (Exception ex)
        {
            AddLog($"✗ Failed to start API: {ex.Message}");
        }
    }

    private void CheckBridgeProcessHealth()
    {
        if (_bridgeProcess != null)
        {
            if (_bridgeProcess.HasExited)
            {
                AddLog("⚠️ Bridge process died unexpectedly - please reconnect");
            }
        }
    }

    private async void ButtonConnect_Click(object? sender, EventArgs e)
    {
        string input = textBoxVideoInput.Text.Trim();
        if (string.IsNullOrEmpty(input))
        {
            MessageBox.Show("Please enter a YouTube URL or Video ID", "Input Required");
            return;
        }

        await ConnectToChatAsync(input);
    }

    private async Task ConnectToChatAsync(string videoInput)
    {
        buttonConnect.Enabled = false;
        try
        {
            AddLog($"🔄 Connecting to YouTube Live Chat: {videoInput}");

            string videoId = _chatService.ExtractVideoId(videoInput);
            if (string.IsNullOrEmpty(videoId))
            {
                AddLog("✗ Invalid YouTube URL or ID");
                return;
            }

            _lastConnectedVideoId = videoId;

            await _chatService.ConnectAsync(videoId);
            await StartYouTubeBridgeAsync(videoId);
        }
        catch (Exception ex)
        {
            AddLog($"✗ Error connecting: {ex.Message}");
        }
        finally
        {
            buttonConnect.Enabled = true;
        }
    }

    private async Task StartYouTubeBridgeAsync(string videoId)
    {
        try
        {
            AddLog($"🔄 Starting YouTube Chat Bridge");

            // Szukaj bridge'a w logicznym miejscu - w folderze YTchatKSP_App\dist
            string[] possiblePaths = new[]
            {
                // 1. Obok exe programu (publication)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "youtube_chat_bridge.exe"),

                // 2. W folderze dist (source)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "YTchatKSP_App", "dist", "youtube_chat_bridge.exe"),

                // 3. Bezpośrednie roowce project
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dist", "youtube_chat_bridge.exe"),
            };

            string bridgePath = null;
            foreach (var path in possiblePaths)
            {
                string fullPath = Path.GetFullPath(path);
                AddLog($"🔍 Checking: {fullPath}");

                if (File.Exists(fullPath))
                {
                    bridgePath = fullPath;
                    AddLog($"✓ Found bridge at: {bridgePath}");
                    break;
                }
            }

            if (string.IsNullOrEmpty(bridgePath) || !File.Exists(bridgePath))
            {
                throw new FileNotFoundException($"youtube_chat_bridge.exe not found. Checked paths: {string.Join(", ", possiblePaths)}");
            }

            AddLog($"✓ Starting: {Path.GetFileName(bridgePath)}");

            _bridgeProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = bridgePath,
                    Arguments = videoId,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            _bridgeProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    AddLog($"[Bridge] {e.Data}");
            };

            _bridgeProcess.Start();
            _bridgeProcess.BeginOutputReadLine();

            AddLog("✓ YouTube Chat Bridge started");

            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            AddLog($"✗ Failed to start bridge: {ex.Message}");
            throw;
        }
    }

    private void StopBridge()
    {
        if (_bridgeProcess != null && !_bridgeProcess.HasExited)
        {
            try
            {
                _bridgeProcess.Kill(true);
                if (!_bridgeProcess.WaitForExit(2000))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/PID {_bridgeProcess.Id} /F /T",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        })?.WaitForExit(1000);
                    }
                    catch { }
                    AddLog("⚠️ Bridge force killed");
                }
                else
                {
                    AddLog("✓ Bridge stopped");
                }
            }
            catch (Exception ex)
            {
                AddLog($"✗ Error stopping bridge: {ex.Message}");
            }
        }
    }

    private void ButtonDisconnect_Click(object? sender, EventArgs e)
    {
        _ = DisconnectFromChatAsync();
    }

    private async Task DisconnectFromChatAsync()
    {
        buttonDisconnect.Enabled = false;
        try
        {
            if (_bridgeProcess != null && !_bridgeProcess.HasExited)
            {
                StopBridge();
                System.Threading.Thread.Sleep(500);
            }

            await _chatService.DisconnectAsync();
            await _apiServer.SendStatusAsync(false);

            _lastConnectedVideoId = null;

            _apiServer.AddMessage(new Models.ChatMessage(
                "system_2",
                "System",
                "Disconnected from YouTube Live Chat"
            ));
        }
        finally
        {
            buttonDisconnect.Enabled = true;
        }
    }

    private void ButtonClear_Click(object? sender, EventArgs e)
    {
        listBoxMessages.Items.Clear();
        textBoxLog.Clear();
        _apiServer.ClearMessages();
        AddLog("✓ Messages and logs cleared");
    }

    private void OnConnectionStatusChanged(bool connected)
    {
        try
        {
            if (InvokeRequired)
            {
                Invoke(() => OnConnectionStatusChanged(connected));
                return;
            }

            buttonConnect.Enabled = !connected;
            buttonDisconnect.Enabled = connected;
            labelStatus.Text = connected ? "Status: Connected" : "Status: Disconnected";
            labelStatus.BackColor = connected ? Color.LightGreen : Color.LightCoral;
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void OnChatMessageReceived(Models.ChatMessage message)
    {
        try
        {
            if (InvokeRequired)
            {
                Invoke(() => OnChatMessageReceived(message));
                return;
            }

            AddLog($"📥 UI received message: Nick='{message.Nick}' | Text='{message.Text}'");

            // Powiadom serwis że otrzymaliśmy wiadomość (dla keep-alive)
            _chatService.OnMessageReceived();

            // Formatuj wiadomość: nick jest oddzielny od tekstu, więc formatujemy tu
            string formattedMessage = $"{message.Nick}: {message.Text}";
            listBoxMessages.Items.Add(formattedMessage);
            listBoxMessages.TopIndex = listBoxMessages.Items.Count - 1;
            labelMessageCount.Text = $"Messages: {listBoxMessages.Items.Count}";
            AddLog($"✓ Message displayed ({listBoxMessages.Items.Count} total)");
        }
        catch (ObjectDisposedException)
        {
            AddLog("⚠️ Form disposed while adding message");
        }
        catch (Exception ex)
        {
            AddLog($"❌ Error adding message to UI: {ex.Message}");
        }
    }

    private void AddLog(string message)
    {
        try
        {
            if (InvokeRequired)
            {
                Invoke(() => AddLog(message));
                return;
            }

            textBoxLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
        }
        catch (ObjectDisposedException)
        {
        }
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);

        // If connection is active, ask for confirmation
        if (_chatService.IsConnected && e.CloseReason == CloseReason.UserClosing)
        {
            DialogResult result = MessageBox.Show(
                "YouTube Chat connection is active.\n\nAre you sure you want to close the application?",
                "Close Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2
            );

            if (result == DialogResult.No)
            {
                e.Cancel = true;
                return;
            }
        }

        try
        {
            // Zatrzymaj monitoring
            if (_bridgeMonitorTimer != null)
            {
                _bridgeMonitorTimer.Stop();
                _bridgeMonitorTimer.Dispose();
            }

            if (_bridgeProcess != null && !_bridgeProcess.HasExited)
            {
                StopBridge();
                System.Threading.Thread.Sleep(500);
            }

            if (_chatService.IsConnected)
                await _chatService.DisconnectAsync();

            await _apiServer.StopAsync();

            _bridgeProcess?.Dispose();
            _apiServer?.Dispose();
            _chatService = null;
        }
        catch (Exception ex)
        {
            AddLog($"Cleanup error: {ex.Message}");
        }
    }
}
