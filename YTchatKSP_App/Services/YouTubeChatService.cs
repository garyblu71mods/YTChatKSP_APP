namespace YTchatKSP_App.Services;

public class YouTubeChatService
{
    private bool _isConnected = false;

    public event Action<bool>? OnConnectionStatusChanged;
    public event Action<string>? OnLog;

    public bool IsConnected => _isConnected;

    public async Task ConnectAsync(string videoId)
    {
        _isConnected = true;
        OnConnectionStatusChanged?.Invoke(true);
        await Task.CompletedTask;
    }

    public async Task DisconnectAsync()
    {
        _isConnected = false;
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
}
