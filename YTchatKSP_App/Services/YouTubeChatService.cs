namespace YTchatKSP_App.Services;

public class YouTubeChatService
{
    private bool _isConnected = false;
    private CancellationTokenSource? _healthCheckCancellation;
    private Task? _healthCheckTask;
    private DateTime _lastMessageTime = DateTime.MinValue;

    // Konfiguracja timeoutów (wartości domyślne można zmienić)
    private int _healthCheckIntervalMs = 5000; // Co 5 sekund sprawdzamy
    private int _messageTimeoutMs = 45000; // 45 sekund bez wiadomości = timeout
    private int _maxHealthCheckFailures = 3; // 3 consecutive failures

    private int _healthCheckFailureCount = 0;

    public event Action<bool>? OnConnectionStatusChanged;
    public event Action<string>? OnLog;
    public event Action? OnConnectionLost; // Nowy event dla utraty połączenia

    public bool IsConnected => _isConnected;

    public async Task ConnectAsync(string videoId)
    {
        _isConnected = true;
        _lastMessageTime = DateTime.Now;
        _healthCheckFailureCount = 0;
        OnLog?.Invoke($"📡 Health check started for video: {videoId}");
        OnConnectionStatusChanged?.Invoke(true);

        // Uruchom health check w tle
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
        _healthCheckFailureCount = 0;
        OnLog?.Invoke("✓ Message received - connection alive");
    }

    private async Task HealthCheckLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _isConnected)
            {
                await Task.Delay(_healthCheckIntervalMs, cancellationToken);

                // Sprawdzenie czy timeout z braku wiadomości
                TimeSpan timeSinceLastMessage = DateTime.Now - _lastMessageTime;
                if (timeSinceLastMessage.TotalMilliseconds > _messageTimeoutMs)
                {
                    _healthCheckFailureCount++;
                    OnLog?.Invoke($"⚠️ No messages for {timeSinceLastMessage.TotalSeconds:F0}s (fail count: {_healthCheckFailureCount}/{_maxHealthCheckFailures})");

                    if (_healthCheckFailureCount >= _maxHealthCheckFailures)
                    {
                        OnLog?.Invoke($"🔴 Connection timeout - disconnected due to inactivity ({timeSinceLastMessage.TotalSeconds:F0}s)");
                        _isConnected = false;
                        OnConnectionStatusChanged?.Invoke(false);
                        OnConnectionLost?.Invoke();
                        break;
                    }
                }
                else
                {
                    // Jeśli znowu są wiadomości, resetuj licznik
                    if (_healthCheckFailureCount > 0)
                    {
                        _healthCheckFailureCount = 0;
                        OnLog?.Invoke("✓ Connection recovered - messages received");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            OnLog?.Invoke("Health check cancelled");
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"❌ Health check error: {ex.Message}");
        }
    }

    /// <summary>
    /// Metoda do konfiguracji timeoutów (opcjonalnie)
    /// </summary>
    public void ConfigureTimeouts(int healthCheckIntervalMs = 5000, int messageTimeoutMs = 45000, int maxFailures = 3)
    {
        _healthCheckIntervalMs = healthCheckIntervalMs;
        _messageTimeoutMs = messageTimeoutMs;
        _maxHealthCheckFailures = maxFailures;
        OnLog?.Invoke($"⚙️ Timeout configured: check={healthCheckIntervalMs}ms, timeout={messageTimeoutMs}ms, maxFailures={maxFailures}");
    }
}
