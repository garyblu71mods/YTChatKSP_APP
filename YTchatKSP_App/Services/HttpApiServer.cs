using System.Net;
using System.Text;
using Newtonsoft.Json;
using YTchatKSP_App.Models;

namespace YTchatKSP_App.Services;

public class HttpApiServer : IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _serverTask;
    private readonly Queue<ChatMessage> _messages = new();
    private const int MaxMessages = 100;
    private string _currentVideoId = "";
    private bool _isConnected = false;
    private bool _disposed = false;

    // Deduplication - track last N message IDs to prevent duplicates
    private readonly HashSet<string> _recentMessageIds = new();
    private const int MaxRecentIds = 50;

    public event Action<string>? OnLog;
    public event Action<ChatMessage>? OnMessageReceived;
    public event Action<bool>? OnConnectionStatusChanged;

    public async Task StartAsync(int port = 5000)
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
            OnLog?.Invoke($"✓ HTTP Server started on localhost:{port}");

            _cancellationTokenSource = new CancellationTokenSource();
            _serverTask = HandleRequestsAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"✗ Error starting server: {ex.Message}");
            throw;
        }
    }

    public async Task StopAsync()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            if (_serverTask != null)
            {
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
                    {
                        await _serverTask.ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { }
            }
            _listener?.Stop();
            _listener?.Close();
            _listener = null;
            OnLog?.Invoke("✓ HTTP Server stopped");
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"✗ Error stopping server: {ex.Message}");
        }
    }

    private async Task HandleRequestsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = HandleContextAsync(context, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"✗ Error handling request: {ex.Message}");
            }
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;
            var method = request.HttpMethod;
            var path = request.RawUrl?.Split('?')[0] ?? "/";

            int statusCode = 404;
            string responseBody = "{}";

            if (path == "/messages" && method == "GET")
            {
                var msgList = GetMessages();
                statusCode = 200;
                responseBody = JsonConvert.SerializeObject(msgList);
            }
            else if (path == "/messages" && method == "POST")
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string body = await reader.ReadToEndAsync();
                    OnLog?.Invoke($"📨 Received POST /messages: {body}");

                    var sendRequest = JsonConvert.DeserializeObject<SendMessageRequest>(body);
                    if (sendRequest?.Nick != null && sendRequest?.Text != null)
                    {
                        OnLog?.Invoke($"✓ Valid message: {sendRequest.Nick}: {sendRequest.Text}");

                        var msg = new ChatMessage(
                            Guid.NewGuid().ToString(),
                            sendRequest.Nick,
                            sendRequest.Text
                        );

                        if (_isConnected)
                        {
                            AddMessage(msg);
                            statusCode = 200;
                            responseBody = JsonConvert.SerializeObject(new { success = true, message = msg });
                        }
                        else
                        {
                            statusCode = 403;
                            OnLog?.Invoke($"⚠️ Message rejected - not connected");
                            responseBody = JsonConvert.SerializeObject(new { success = false, error = "Not connected" });
                        }
                    }
                    else
                    {
                        statusCode = 400;
                        OnLog?.Invoke($"⚠️ Invalid message format - missing Nick or Text");
                        responseBody = JsonConvert.SerializeObject(new { success = false, error = "Invalid request" });
                    }
                }
            }
            else if (path == "/set-video" && method == "POST")
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string body = await reader.ReadToEndAsync();
                    var setVideoRequest = JsonConvert.DeserializeObject<SetVideoRequest>(body);
                    if (setVideoRequest?.VideoId != null)
                    {
                        _currentVideoId = setVideoRequest.VideoId;
                        OnLog?.Invoke($"✓ Video ID set to: {_currentVideoId}");
                        statusCode = 200;
                        responseBody = JsonConvert.SerializeObject(new { success = true, videoId = _currentVideoId });
                    }
                    else
                    {
                        statusCode = 400;
                        responseBody = JsonConvert.SerializeObject(new { success = false, error = "Invalid request" });
                    }
                }
            }
            else if (path == "/status" && method == "POST")
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string body = await reader.ReadToEndAsync();
                    var statusRequest = JsonConvert.DeserializeObject<StatusRequest>(body);
                    if (statusRequest?.Connected.HasValue == true)
                    {
                        await SendStatusAsync(statusRequest.Connected.Value);
                        statusCode = 200;
                        responseBody = JsonConvert.SerializeObject(new { success = true, connected = _isConnected });
                    }
                    else
                    {
                        statusCode = 400;
                        responseBody = JsonConvert.SerializeObject(new { success = false, error = "Invalid request" });
                    }
                }
            }
            else if (path == "/health" && method == "GET")
            {
                statusCode = 200;
                responseBody = JsonConvert.SerializeObject(new { status = "ok", messagesCount = _messages.Count });
            }
            else
            {
                statusCode = 404;
                responseBody = JsonConvert.SerializeObject(new { error = "Endpoint not found" });
            }

            response.StatusCode = statusCode;
            byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"✗ Error in HandleContextAsync: {ex.Message}");
        }
    }

    public void AddMessage(ChatMessage message)
    {
        // Check for duplicates - if exact same message was added recently, skip it
        if (_recentMessageIds.Contains(message.Id))
        {
            OnLog?.Invoke($"⚠️ Duplicate message blocked: {message.Id}");
            return;
        }

        lock (_messages)
        {
            _messages.Enqueue(message);
            if (_messages.Count > MaxMessages)
                _messages.Dequeue();
        }

        // Track this message ID
        _recentMessageIds.Add(message.Id);
        if (_recentMessageIds.Count > MaxRecentIds)
        {
            // Remove oldest - just clear some when too many
            var toRemove = _recentMessageIds.Take(_recentMessageIds.Count - MaxRecentIds).ToList();
            foreach (var id in toRemove)
                _recentMessageIds.Remove(id);
        }

        OnLog?.Invoke($"📤 Message added to queue (total: {_messages.Count}): {message.Text}");
        OnMessageReceived?.Invoke(message);
    }

    public List<ChatMessage> GetMessages()
    {
        lock (_messages)
        {
            return _messages.ToList();
        }
    }

    public void ClearMessages()
    {
        lock (_messages)
        {
            _messages.Clear();
        }
        _recentMessageIds.Clear();
    }

    public async Task SendStatusAsync(bool connected)
    {
        _isConnected = connected;

        if (!connected)
        {
            ClearMessages();
        }

        OnConnectionStatusChanged?.Invoke(connected);
        await Task.CompletedTask;
    }

    public string CurrentVideoId => _currentVideoId;
    public bool IsConnected => _isConnected;

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _listener?.Stop();
            _listener?.Close();
        }
        catch { }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~HttpApiServer()
    {
        Dispose();
    }
}
