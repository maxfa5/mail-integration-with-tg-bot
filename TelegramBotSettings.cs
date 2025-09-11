public class TelegramBotSettings
{
    public string Token { get; set; } = string.Empty;
    public long AdminId { get; set; } = 0;
    public string GetToken() {  return Token; }
}