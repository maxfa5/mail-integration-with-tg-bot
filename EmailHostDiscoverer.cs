using System;
using System.Linq;
using System.Net;

public static class DnsDiscoverer
{
    public static void DiscoverEmailServers(string domain)
    {
        Console.WriteLine("Поиск MX записей...");

        try
        {
            var mxRecords = System.Net.Dns.GetHostEntry(domain);

            Console.WriteLine($"MX: {mxRecords} (приоритет: {mxRecords})");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка получения MX: {ex.Message}");
        }

        Console.WriteLine("\nПоиск возможных IMAP серверов...");

        CheckHost("imap." + domain);
        CheckHost("mail." + domain);
        CheckHost("imap1." + domain);
        CheckHost("email." + domain);
        CheckHost("mx." + domain);
    }

    private static void CheckHost(string host)
    {
        try
        {
            var addresses = Dns.GetHostAddresses(host);
            Console.WriteLine($"✓ {host} -> {string.Join(", ", addresses.Select(a => a.ToString()))}");
        }
        catch
        {
            Console.WriteLine($"✗ {host} - не resolves");
        }
    }
}