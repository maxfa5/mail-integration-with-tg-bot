using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Org.BouncyCastle.Asn1.Cmp;
using Org.BouncyCastle.Crypto;
using System.Reflection;

public static partial class Program
{
    public static async Task Main(string[]? args)
    {

        DnsDiscoverer.DiscoverEmailServers("surgu.ru");
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddJsonFile("AppProperties.json", optional: false, reloadOnChange: true);

        builder.Services.AddHostedService<BotService>();
        builder.Services.Configure<TelegramBotSettings>(
        builder.Configuration.GetSection("TelegramBotSettings"));

        var host = builder.Build();

        Console.WriteLine("Запуск бота...");
        await host.RunAsync();

    }
}
