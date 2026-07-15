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
    private int _reconnectAttempts = 0;
    private const int MaxReconnectAttempts = 5; // Więcej prób z backoff
    private DateTime _lastReconnectTime = DateTime.MinValue;

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
            _chatService.OnConnectionLost += OnChatConnectionLost;

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

    private async void OnChatConnectionLost()
    {
        AddLog("⚠️ Chat connection lost - attempting auto-reconnect...");

        if (_lastConnectedVideoId != null && _reconnectAttempts < MaxReconnectAttempts)
        {
            _reconnectAttempts++;

            // Exponential backoff: 1s, 2s, 4s, 8s, 16s
            int delaySeconds = (int)Math.Pow(2, _reconnectAttempts - 1);
            if (delaySeconds > 30) delaySeconds = 30; // Max 30 sekund

            AddLog($"🔄 Auto-reconnect attempt {_reconnectAttempts}/{MaxReconnectAttempts} (waiting {delaySeconds}s)");

            try
            {
                _lastReconnectTime = DateTime.Now;
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                await ConnectToChatAsync(_lastConnectedVideoId);
                _reconnectAttempts = 0; // Reset licznika przy pomyślnym reconnect
            }
            catch (Exception ex)
            {
                AddLog($"✗ Auto-reconnect failed: {ex.Message}");

                if (_reconnectAttempts >= MaxReconnectAttempts)
                {
                    AddLog("❌ Max reconnect attempts reached. Please reconnect manually.");
                    _lastConnectedVideoId = null;
                }
            }
        }
    }

    private void CheckBridgeProcessHealth()
    {
        if (_bridgeProcess != null)
        {
            if (_bridgeProcess.HasExited)
            {
                AddLog("⚠️ Bridge process died unexpectedly");
                OnChatConnectionLost();
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
            _reconnectAttempts = 0;

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

            string bridgePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "youtube_chat_bridge.exe");

            if (!File.Exists(bridgePath))
            {
                var binFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "YTchatKSP_App", "dist");
                bridgePath = Path.Combine(binFolder, "youtube_chat_bridge.exe");

                if (!File.Exists(bridgePath))
                {
                    bridgePath = Path.Combine(
                        Path.GetDirectoryName(binFolder) ?? "",
                        "YTchatKSP_App",
                        "dist",
                        "youtube_chat_bridge.exe"
                    );
                }
            }

            if (!File.Exists(bridgePath))
            {
                throw new FileNotFoundException($"youtube_chat_bridge.exe not found at {bridgePath}");
            }

            AddLog($"✓ Found bridge: {bridgePath}");

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
            _reconnectAttempts = 0;

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

            // Powiadom serwis że otrzymaliśmy wiadomość (dla health check'a)
            _chatService.OnMessageReceived();

            listBoxMessages.Items.Add(message.Text);
            listBoxMessages.TopIndex = listBoxMessages.Items.Count - 1;
            labelMessageCount.Text = $"Messages: {listBoxMessages.Items.Count}";
        }
        catch (ObjectDisposedException)
        {
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

        // Jeśli połączenie jest aktywne, poproś potwierdzenie
        if (_chatService.IsConnected && e.CloseReason == CloseReason.UserClosing)
        {
            DialogResult result = MessageBox.Show(
                "Połączenie z YouTube Chat jest aktywne.\n\nNa pewno chcesz zamknąć aplikację?",
                "Potwierdzenie zamknięcia",
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
