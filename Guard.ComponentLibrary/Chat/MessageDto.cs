namespace Guard.ComponentLibrary.Chat
{
  public class ChatMessageDto
  {
    public bool IsOwner { get; set; } = false;
    public string UserName { get; set; } = "";
    public string UserInitials { get; set; } = "";
    public DateTime PublishDateTime { get; set; }
    /// <summary>
    /// Текст сообщения
    /// </summary>
    public string Text { get; set; } = string.Empty;
  }
}
