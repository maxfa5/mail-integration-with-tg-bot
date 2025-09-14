using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

public class MultiUserEmailService : IDisposable
{
    private readonly ConcurrentDictionary<long, UserData> _userDataMap = new();
    private readonly ITelegramBotClient _botClient;
    private CancellationTokenSource _globalCts;

    public MultiUserEmailService(ITelegramBotClient botClient)
    {
        _botClient = botClient;
        _globalCts = new CancellationTokenSource();

    }

    public bool AddUserEmailService(long userId, string host, int port, bool useSsl,
                                  string email, string password, TimeSpan checkInterval)
    {
        try
        {
            var userService = new UserEmailService(
                host, port, useSsl, email, password,
                _botClient, userId, checkInterval
            );

            if (_userDataMap.ContainsKey(userId))
            {
                if (_userDataMap.TryGetValue(userId, out var existingUserData))
                {
                    existingUserData.UserEmailService?.StopMonitoring();
                    existingUserData.UserEmailService?.Dispose();
                    userService.SetLastMessageCount(existingUserData.LastMessageCount);
                    existingUserData.SetUserEmailService(userService);
                    existingUserData.AddUserMail(email, password);
                }
            }
            else
            {
                var newUserData = new UserData();
                newUserData.SetUserEmailService(userService);
                newUserData.AddUserMail(email, password);
                _userDataMap.TryAdd(userId, newUserData);
            }

            _ = userService.StartMonitoringAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка добавления сервиса: {ex.Message}");
            return false;
        }
    }

    public bool RemoveUserEmailService(long userId)
    {
        if (_userDataMap.TryGetValue(userId, out var userData))
        {
            if (userData.UserEmailService != null)
            {
                userData.LastMessageCount = userData.UserEmailService.GetLastMessageCount();
                userData.UserEmailService.StopMonitoring();
            }
        }
        return true; //TODO!!
    }

    public string GetUserStatus(long userId)
    {
        //if (_userDatas.TryGetValue(userId, out var service))
        //{
        //    return service.GetStatus();
        //}
        return "Сервис мониторинга не активен";
    }

    //public void StopAllServices()
    //{
    //    foreach (var (userId, service) in _userDatas.GetUserMailsget())
    //    {
    //        service.StopMonitoring();
    //        service.Dispose();
    //    }
    //    _userDatas.getMails.GetUserMailsget().Clear();
    //}

    public void Dispose()
    {
        //StopAllServices();
        _globalCts?.Dispose();
    }
}