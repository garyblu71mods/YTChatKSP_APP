namespace YTchatKSP_App.Models;

public class SendMessageRequest
{
    public string Nick { get; set; }
    public string Text { get; set; }
}

public class SetVideoRequest
{
    public string VideoId { get; set; }
}

public class StatusRequest
{
    public bool? Connected { get; set; }
}
