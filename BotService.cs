using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

public class BotService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly TelegramBotSettings _settings;
    private readonly UpdateHandlers _updateHandlers;
    private readonly MultiUserEmailService _emailService;

    public BotService(IOptions<TelegramBotSettings> settings)
    {
        _settings = settings.Value;
        var botToken = _settings.GetToken();
        _botClient = new TelegramBotClient(botToken);
        _emailService = new MultiUserEmailService(_botClient);
        _updateHandlers = new UpdateHandlers(_botClient, _settings, _emailService);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await _botClient.GetMe(stoppingToken);
        Console.WriteLine($"Бот @{me.Username} запущен!");

        _botClient.StartReceiving(
            updateHandler: _updateHandlers.HandleUpdateAsync,
            errorHandler: _updateHandlers.HandleErrorAsync,
            receiverOptions: new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
            cancellationToken: stoppingToken
        );

        Console.WriteLine("Мультипользовательский мониторинг почты готов к работе!");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override void Dispose()
    {
        _emailService?.Dispose();
        base.Dispose();
    }
}