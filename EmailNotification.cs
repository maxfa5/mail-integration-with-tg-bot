using System;
using System.Security.Authentication;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

class EmailNotification
{
    public static void ReadEmails(string host, int port, bool useSsl,
                                string username, string password)
    {
        using var client = new ImapClient();
        client.Connect(host, port, useSsl);

        try
        {
            client.Authenticate(username, password);
        }
        catch (AuthenticationException)
        {
            Console.WriteLine("Ошибка аутентификации. Проверьте логин и пароль.");
        }
        // Открываем папку INBOX
        var inbox = client.Inbox;
        inbox.Open(FolderAccess.ReadOnly);

        Console.WriteLine($"Всего сообщений: {inbox.Count}");
        Console.WriteLine($"Непрочитанных: {inbox.Unread}");

        // Получаем все сообщения
        for (int i = 0; i < 2; i++)
        {
            var message = inbox.GetMessage(i);

            Console.WriteLine($"\n--- Сообщение {i + 1} ---");
            Console.WriteLine($"От: {message.From}");
            Console.WriteLine($"Кому: {message.To}");
            Console.WriteLine($"Тема: {message.Subject}");
            Console.WriteLine($"Дата: {message.Date}");
            Console.WriteLine($"Размер: {message.TextBody?.Length ?? 0} байт");

            // Выводим текст сообщения
            if (!string.IsNullOrEmpty(message.TextBody))
            {
                Console.WriteLine("\nТекст:");
                Console.WriteLine(message.TextBody);
            }
        }

        client.Disconnect(true);
    }
}
