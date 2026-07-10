namespace YTchatKSP_App.Models;

public class ChatMessage
{
    public string Id { get; set; }
    public string Nick { get; set; }
    public string Text { get; set; }
    public DateTime Timestamp { get; set; }

    public ChatMessage(string id, string nick, string text)
    {
        Id = id;
        Nick = nick;
        Text = text;
        Timestamp = DateTime.UtcNow;
    }

    public override string ToString() => $"[{Timestamp:HH:mm:ss}] {Nick}: {Text}";
}
