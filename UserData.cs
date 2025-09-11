using System;
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Imap;
using MimeKit;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

public class UserData : IDisposable
{
    private UserEmailService _userEmailService;
    private Task _autoUpdateTask;
    private CancellationTokenSource _globalCts;
    public UserEmailService UserEmailService { get { return _userEmailService; } }
    public UserData()
    {
        Console.WriteLine("UserData init");
        _globalCts = new CancellationTokenSource(); // Инициализируем здесь
    }
    private int _lastMessageCount = 0;
    private DateTime _lastCheck = DateTime.MinValue;

    private ConcurrentDictionary<string, string> _userMails = new();
    public ConcurrentDictionary<string, string> UserMails
    {
        get { return new ConcurrentDictionary<string, string>(_userMails); }// Копия для иммутабельности
    }
    public void SetUserEmailService(UserEmailService service)
    {
        _userEmailService = service;
        Console.WriteLine("UserEmailService установлен, запускаем автообновление...");

        // Запускаем автообновление только после установки сервиса
        StartAutoUpdate(TimeSpan.FromMinutes(5));
    }

    private void StartAutoUpdate(TimeSpan interval)
    {
        _autoUpdateTask = Task.Run(async () =>
        {
            while (!_globalCts.Token.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine("🔄 Автоматическое обновление данных...");
                    await UpdateDataAsync();
                    await Task.Delay(interval, _globalCts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("AAAAAAAAAAAAAAAAAAAA");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка автоматического обновления: {ex.Message}");
                    await Task.Delay(TimeSpan.FromMinutes(1), _globalCts.Token);
                }
            }
        }, _globalCts.Token);
    }

        public bool AddUserMail(string email, string password)
    {
        return _userMails.TryAdd(email, password);
    }
    public bool RemoveUserMail(string email)
    {
        return _userMails.TryRemove(email, out _);
    }
    public int LastMessageCount
    {
        get => _lastMessageCount;
        set => _lastMessageCount = value;
    }

    public DateTime LastCheck
    {
        get => _lastCheck;
        set => _lastCheck = value;
    }



    public async Task UpdateDataAsync()
    {
        if (_userEmailService != null)
        {
            _lastMessageCount = await _userEmailService.GetCurrentMessageCountAsync();
            Console.WriteLine($"Текущее количество сообщений: {LastMessageCount}");
            LastCheck = DateTime.Now;
        }
        else
        {
            Console.WriteLine("UserEmailService не установлен.");
        }
    }

    public void Dispose()
    {
        _globalCts?.Cancel();
        _autoUpdateTask?.Wait();
        _globalCts?.Dispose();
        _userEmailService?.Dispose();
    }
}
