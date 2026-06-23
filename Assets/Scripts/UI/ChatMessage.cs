public struct ChatMessage
{
    public ChatChannel       Channel;
    public string            SenderName;
    public string            Text;
    public System.DateTime   Timestamp;

    public ChatMessage(ChatChannel channel, string sender, string text)
    {
        Channel    = channel;
        SenderName = sender;
        Text       = text;
        Timestamp  = System.DateTime.Now;
    }
}
