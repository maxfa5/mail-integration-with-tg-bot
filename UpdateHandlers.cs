using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class UpdateHandlers : IUpdateHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly TelegramBotSettings _settings;
    private readonly MultiUserEmailService _emailService;
    private readonly String _instructions = "📋 Чтобы начать, отправьте:\n" +
                         "/start_monitor ваш_email ваш_пароль\n\n" +
                         "🔐 Для Mail.ru используйте специалиальный пароль!\n" +
                         "Как получить: Настройки → Безопасность → Пароли для внешних приложений" +
                         "🔐 Для Mail.surgu.ru используйте ПАРОЛЬ от почты!\n";
    public UpdateHandlers(ITelegramBotClient botClient, TelegramBotSettings settings, MultiUserEmailService emailService)
    {
        _botClient = botClient;
        _settings = settings;
        _emailService = emailService;
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message || message.Text is not { } text)
            return;

        var userId = message.From.Id;

        try
        {
            if(text.StartsWith("🚀 Запустить мониторинг"))
            {
                await botClient.SendMessage(
                        userId,
                        _instructions
                    );
            } else if (text.StartsWith("/start_monitor"))
            {
                // Формат: /start_monitor email password
                var parts = text.Split(' ');
                if (parts.Length >= 3)
                {
                    var email = parts[1];
                    var password = parts[2];
                    var success = _emailService.AddUserEmailService(
                        userId, "mail.surgu.ru", 993, true,
                        email, password, TimeSpan.FromMinutes(2)
                    );

                    await botClient.SendMessage(
                        userId,
                        success ? "✅ Мониторинг запущен!" : "❌ Ошибка запуска мониторинга"
                    );
                }
            }
            else if (text.StartsWith("/stop_monitor") || text.StartsWith("⏹️ Остановить мониторинг"))
            {
                var success = _emailService.RemoveUserEmailService(userId);
                await botClient.SendMessage(
                    userId,
                    success ? "⏹️ Мониторинг остановлен!" : "❌ Мониторинг не был активен"
                );
            }
            else if (text.StartsWith("/status") || text.StartsWith("📊 Статус"))
            {
                var status = _emailService.GetUserStatus(userId);
                await botClient.SendMessage(userId, status);
            }
            else if (text.StartsWith("/start") || text.StartsWith("❓ Помощь"))
            {
                await HandleStartCommand(botClient, userId);
            }
            else
            {
                HandleUnknownCommand(botClient, userId);
            }
        }
        catch (Exception ex)
        {
            await botClient.SendMessage(userId, $"❌ Ошибка: {ex.Message}");
        }
    }

    private async Task HandleStartCommand(ITelegramBotClient botClient, long userId)
    {
        var welcomeMessage = "🤖 Добро пожаловать в Email Monitor Bot!\n\n" +
                           "📧 Я помогу вам отслеживать новые письма на вашей почте";

        var replyMarkup = new ReplyKeyboardMarkup(new[]
        {
        new[]
        {
            new KeyboardButton("🚀 Запустить мониторинг") { RequestContact = false },
            new KeyboardButton("⏹️ Остановить мониторинг") { RequestContact = false }
        },
        new[]
        {
            new KeyboardButton("📊 Статус") { RequestContact = false },
            new KeyboardButton("❓ Помощь") { RequestContact = false }
        }
    })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };

        await botClient.SendMessage(
            chatId: userId,
            text: welcomeMessage,
            parseMode: ParseMode.Html,
            replyMarkup: replyMarkup
        );
        
        await botClient.SendMessage(
            chatId: userId,
            text: _instructions,
            parseMode: ParseMode.Html
        );
    }

    private async Task HandleUnknownCommand(ITelegramBotClient botClient, long userId)
    {
        var helpMessage = "🤔 Неизвестная команда\n\n" +
                        "📋 Доступные команды:\n" +
                        "/start - Показать справку\n" +
                        "/start_monitor - Запустить мониторинг почты\n" +
                        "/stop_monitor - Остановить мониторинг\n" +
                        "/status - Статус мониторинга\n\n" +
                        "📝 Пример:\n" +
                        " '''/start_monitor example@mail.ru ваш_пароль ''' " +
                        " ''' /start_monitor example@edu.surgu.ru ваш_пароль '''";

        await botClient.SendMessage(
            chatId: userId,
            text: helpMessage,
            parseMode: ParseMode.Html
        );
    }
}