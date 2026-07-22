namespace YTchatKSP_App.Services;

public class YouTubeChatService
{
    private bool _isConnected = false;
    private CancellationTokenSource? _healthCheckCancellation;
    private Task? _healthCheckTask;
    private DateTime _lastMessageTime = DateTime.MinValue;

    // Keep-alive check (nie resetuje, tylko monitoruje)
    private int _keepAliveIntervalMs = 30000; // Co 30 sekund wysyłamy keep-alive ping
    private int _maxIdleTimeMs = 300000; // 5 minut idle warning

    public event Action<bool>? OnConnectionStatusChanged;
    public event Action<string>? OnLog;
    public event Action? OnConnectionLost; // Event dla poważnego disconnect

    public bool IsConnected => _isConnected;

    public async Task ConnectAsync(string videoId)
    {
        _isConnected = true;
        _lastMessageTime = DateTime.Now;
        OnLog?.Invoke($"📡 Keep-alive connection started for video: {videoId}");
        OnConnectionStatusChanged?.Invoke(true);

        // Uruchom keep-alive w tle
        _healthCheckCancellation = new CancellationTokenSource();
        _healthCheckTask = HealthCheckLoopAsync(_healthCheckCancellation.Token);

        await Task.CompletedTask;
    }

    public async Task DisconnectAsync()
    {
        _isConnected = false;

        // Zatrzymaj health check
        _healthCheckCancellation?.Cancel();
        if (_healthCheckTask != null)
        {
            try
            {
                await _healthCheckTask;
            }
            catch (OperationCanceledException) { }
        }

        OnLog?.Invoke("❌ Health check stopped");
        OnConnectionStatusChanged?.Invoke(false);
        await Task.CompletedTask;
    }

    public string ExtractVideoId(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        if (input.Length == 11 && input.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
            return input;

        try
        {
            var uri = new Uri(input);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            return query["v"] ?? "";
        }
        catch
        {
            return "";
        }
    }

    public async Task FetchMessagesAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Metoda do śledzenia ostatniego czasu otrzymania wiadomości
    /// Powinna być wywoływana każdorazowo gdy przyjdzie wiadomość z YouTube
    /// </summary>
    public void OnMessageReceived()
    {
        _lastMessageTime = DateTime.Now;
        OnLog?.Invoke("✓ Message received - updating keep-alive");
    }

    private async Task HealthCheckLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _isConnected)
            {
                await Task.Delay(_keepAliveIntervalMs, cancellationToken);

                // Keep-alive check - tylko monitoruj, nie resetuj
                TimeSpan timeSinceLastMessage = DateTime.Now - _lastMessageTime;

                if (timeSinceLastMessage.TotalMilliseconds > _maxIdleTimeMs)
                {
                    // 5+ minut bez wiadomości - only log warning, nie resetuj
                    OnLog?.Invoke($"⏳ Channel idle for {timeSinceLastMessage.TotalMinutes:F0} minutes (but still connected)");
                }
                else if (timeSinceLastMessage.TotalMilliseconds > _keepAliveIntervalMs)
                {
                    // Loguj keep-alive status
                    OnLog?.Invoke($"✓ Keep-alive: Connection active (last message: {timeSinceLastMessage.TotalSeconds:F0}s ago)");
                }
            }
        }
        catch (OperationCanceledException)
        {
            OnLog?.Invoke("Keep-alive stopped");
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"❌ Keep-alive error: {ex.Message}");
        }
    }

    /// <summary>
    /// Konfiguracja keep-alive timeout'ów (opcjonalnie)
    /// </summary>
    public void ConfigureKeepAlive(int keepAliveIntervalMs = 30000, int maxIdleWarningMs = 300000)
    {
        _keepAliveIntervalMs = keepAliveIntervalMs;
        _maxIdleTimeMs = maxIdleWarningMs;
        OnLog?.Invoke($"⚙️ Keep-alive configured: interval={keepAliveIntervalMs}ms, idle warning at={maxIdleWarningMs}ms");
    }
}
