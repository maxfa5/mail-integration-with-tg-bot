using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Imap;
using MimeKit;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

public class UserEmailService : IDisposable
{
    private readonly ImapClient _client;
    private readonly string _email;
    private readonly string _password;
    private readonly string _host;
    private readonly int _port;
    private readonly bool _useSsl;
    private readonly ITelegramBotClient _botClient;
    private readonly long _userId;
    private readonly TimeSpan _checkInterval;

    private CancellationTokenSource _cts;
    private Task _monitoringTask;
    private int _lastMessageCount { get; set; } = 0;
    private DateTime _lastCheck { get; set; } = DateTime.MinValue;

    public UserEmailService(string host, int port, bool useSsl, string email, string password,
                          ITelegramBotClient botClient, long userId, TimeSpan checkInterval)
    {
        _host = host;
        _port = port;
        _useSsl = useSsl;
        _email = email;
        _password = password;
        _botClient = botClient;
        _userId = userId;
        _checkInterval = checkInterval;

        _client = new ImapClient();

        Console.WriteLine($"UserEmailService created for {email}");
    }

    public async Task<int> GetCurrentMessageCountAsync()
    {
        try
        {
            if (!_client.IsConnected || !_client.IsAuthenticated)
            {
                await ConnectAndAuthenticateAsync();
            }
            return _lastMessageCount;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка получения количества сообщений: {ex.Message}");
            return -1;
        }
    }

    private async Task<bool> ConnectAndAuthenticateAsync()
    {
        try
        {
            Console.WriteLine($"Connecting to {_host}:{_port}...");

            if (!_client.IsConnected)
            {
                _client.Connect(_host, _port, true);
                Console.WriteLine("Connected to server");
            }

            if (!_client.IsAuthenticated)
            {
                Console.WriteLine($"Authenticating as {_email}... {_password}");
                _client.Authenticate(_email, _password);
                Console.WriteLine("Authentication successful");
            }

            Console.WriteLine($"✅ Успешное подключение для пользователя {_userId}");
            return true;
        }
        catch (AuthenticationException ex)
        {
            var errorMessage = $"❌ Ошибка аутентификации для {_email}\n" +
                             "Для Mail.ru необходим ПАРОЛЬ ПРИЛОЖЕНИЯ!\n\n" +
                             "📋 Как получить пароль приложения:\n" +
                             "1. Зайдите в Настройки почты Mail.ru\n" +
                             "2. Перейдите в раздел 'Безопасность'\n" +
                             "3. Найдите 'Пароли для внешних приложений'\n" +
                             "4. Создайте новый пароль для почты\n" +
                             "5. Используйте этот пароль вместо основного\n\n" +
                             "🔗 Ссылка: https://help.mail.ru/mail/security/protection/external";

            Console.WriteLine(errorMessage);
            await SendTelegramMessageAsync(errorMessage);
            return false;
        }
        catch (Exception ex)
        {
            var errorMessage = $"❌ Ошибка подключения для {_email}: {ex.Message}";
            Console.WriteLine(errorMessage);
            await SendTelegramMessageAsync(errorMessage);
            return false;
        }
    }

    public async Task StartMonitoringAsync()
    {
        Console.WriteLine("StartMonitoringAsync called");
        if (_monitoringTask != null && !_monitoringTask.IsCompleted)
        {
            Console.WriteLine("Monitoring already running");
            return;
        }

        // Сначала пробуем подключиться
        var connected = await ConnectAndAuthenticateAsync();
        if (!connected)
        {
            Console.WriteLine("Connect failed, stopping monitoring");
            return;
        }

        _cts = new CancellationTokenSource();
        Console.WriteLine("Before MonitorEmailsAsync");

        // Запускаем мониторинг
        _monitoringTask = Task.Run(() => MonitorEmailsAsync(_cts.Token), _cts.Token);

        Console.WriteLine("After MonitorEmailsAsync task started");
        await SendTelegramMessageAsync("✅ Мониторинг почты запущен! Я буду присылать уведомления о новых письмах.");
    }

    public void StopMonitoring()
    {
        Console.WriteLine("StopMonitoring called");
        _cts?.Cancel();
        _monitoringTask?.Wait();
        _ = SendTelegramMessageAsync("⏹️ Мониторинг почты остановлен.");
    }

    private async Task MonitorEmailsAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("MonitorEmailsAsync started");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Console.WriteLine($"Checking emails for {_email} at {DateTime.Now:HH:mm:ss}");

                // Проверяем подключение
                if (!_client.IsConnected || !_client.IsAuthenticated)
                {
                    Console.WriteLine("Reconnecting...");
                    var connected = await ConnectAndAuthenticateAsync();
                    if (!connected)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                        continue;
                    }
                }

                var newMessages = await CheckForNewMessagesAsync();
                if (!string.IsNullOrEmpty(newMessages))
                {
                    Console.WriteLine("New messages found, sending notification");
                    await SendTelegramMessageAsync(newMessages);
                }
                else
                {
                    Console.WriteLine("No new messages");
                }

                _lastCheck = DateTime.Now;
                Console.WriteLine($"Next check in {_checkInterval.TotalMinutes} minutes");

                await Task.Delay(_checkInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Monitoring cancelled");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка мониторинга для {_userId}: {ex.Message}");
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
        }

        Console.WriteLine("MonitorEmailsAsync finished");
    }

    private async Task<string> CheckForNewMessagesAsync()
    {
        try
        {
            Console.WriteLine($"Checking for new messages in {_email}");

            var inbox = _client.Inbox;
            await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly, _cts.Token);

            int currentMessageCount = inbox.Count;
            Console.WriteLine($"📊 {_email}: Всего {currentMessageCount}, было {_lastMessageCount}");

            if (currentMessageCount <= _lastMessageCount)
            {
                Console.WriteLine("No new messages");
                _lastMessageCount = currentMessageCount;
                return null;
            }
            var countNewMessages = currentMessageCount - _lastMessageCount;
            Console.WriteLine($"Found {countNewMessages} new messages");
            string newMessages;
            if (countNewMessages > 3)
            {
                newMessages = $"📧 <b>Новые письма для {_email}:</b>\n\n";
                for (int i = _lastMessageCount; i < 3; i++)
                {
                    var message = await inbox.GetMessageAsync(i, _cts.Token);

                    newMessages += $"━━━━━━━━━━━━━━━━━━━━\n";
                    newMessages += $"📨 <b>От:</b> {FormatSender(message.From)}\n";
                    newMessages += $"📋 <b>Тема:</b> {message.Subject}\n";
                    newMessages += $"📅 <b>Дата:</b> {message.Date.LocalDateTime:dd.MM.yyyy HH:mm}\n";

                }
                newMessages += "...";
            }
            else
            {
                newMessages = $"📧 <b>Новые письма для {_email}:</b>\n\n";
                for (int i = _lastMessageCount; i < currentMessageCount; i++)
                {
                    var message = await inbox.GetMessageAsync(i, _cts.Token);

                    newMessages += $"━━━━━━━━━━━━━━━━━━━━\n";
                    newMessages += $"📨 <b>От:</b> {FormatSender(message.From)}\n";
                    newMessages += $"📋 <b>Тема:</b> {message.Subject}\n";
                    newMessages += $"📅 <b>Дата:</b> {message.Date.LocalDateTime:dd.MM.yyyy HH:mm}\n";

                    if (!string.IsNullOrEmpty(message.TextBody))
                    {
                        var preview = message.TextBody.Trim();
                        preview = preview.Length > 100 ? preview.Substring(0, 100) + "..." : preview;
                        newMessages += $"📝 <b>Текст:</b> {EscapeHtml(preview)}\n";
                    }
                }
            }
            _lastMessageCount = currentMessageCount;
            Console.WriteLine(newMessages);
            return newMessages;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка проверки сообщений для {_userId}: {ex.Message}");
            return null;
        }
    }

    private async Task SendTelegramMessageAsync(string message)
    {
        try
        {
            if (_botClient != null)
            {
                await _botClient.SendMessage(
                    chatId: _userId,
                    text: message,
                    parseMode: ParseMode.Html,
                    cancellationToken: _cts?.Token ?? default
                );
            }
            else
            {
                Console.WriteLine("❌ botClient is null!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка отправки сообщения пользователю {_userId}: {ex.Message}");
        }
    }

    public string GetStatus()
    {
        return $"📊 Статус для {_email}:\n" +
               $"• Активен: {(_monitoringTask != null && !_monitoringTask.IsCompleted)}\n" +
               $"• Подключен: {_client.IsConnected}\n" +
               $"• Аутентифицирован: {_client.IsAuthenticated}\n" +
               $"• Интервал: {_checkInterval.TotalMinutes} мин\n" +
               $"• Последняя проверка: {_lastCheck:dd.MM.yyyy HH:mm}\n" +
               $"• Всего писем: {_lastMessageCount}";
    }

    private string FormatSender(InternetAddressList from)
    {
        if (from == null || from.Count == 0) return "Неизвестный отправитель";
        return from[0] is MailboxAddress mailbox ?
            $"{mailbox.Name} ({mailbox.Address})" :
            from[0].ToString();
    }

    private string EscapeHtml(string text) => text.Replace("<", "&lt;").Replace(">", "&gt;");

    public void Dispose()
    {
        Console.WriteLine("Disposing UserEmailService");
        StopMonitoring();
        if (_client.IsConnected)
        {
            _client.Disconnect(true);
        }
        _client.Dispose();
        _cts?.Dispose();
    }
}