using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using TICinema.Bot.Configurations;
using TICinema.Bot.Services;
using TICinema.Contracts.Protos.Identity;

var builder = Host.CreateApplicationBuilder(args);

// 1. Привязываем настройки
builder.Services.Configure<BotSettings>(builder.Configuration.GetSection("BotSettings"));

builder.Services.AddGrpcClient<AuthService.AuthServiceClient>(o =>
{
    var settings = builder.Configuration.GetSection("BotSettings").Get<BotSettings>();
    o.Address = new Uri(settings!.IdentityGrpcUrl);
});

builder.Services.AddSingleton<TelegramBotClient>(sp => 
{
    var settings = sp.GetRequiredService<IOptions<BotSettings>>().Value;
    if (string.IsNullOrEmpty(settings.Token)) 
        throw new Exception("Bot token is missing in appsettings.json!");
        
    return new TelegramBotClient(settings.Token);
});

// 2. Регистрируем ИНТЕРФЕЙС (чтобы работало внедрение в конструктор UpdateHandler)
// Мы говорим: "Если кто-то просит ITelegramBotClient, дай ему уже созданный TelegramBotClient"
builder.Services.AddSingleton<ITelegramBotClient>(sp => 
    sp.GetRequiredService<TelegramBotClient>());

// 3. Регистрируем обработчик
// Сделаем его Singleton, так как бот у нас один и живет всё время работы приложения
builder.Services.AddSingleton<UpdateHandler>(); 

using var host = builder.Build();

// ТЕПЕРЬ ЭТО СРАБОТАЕТ:
var bot = host.Services.GetRequiredService<TelegramBotClient>();
var handler = host.Services.GetRequiredService<UpdateHandler>();

bot.OnMessage += handler.OnMessage;
bot.OnError += handler.OnError;

var me = await bot.GetMe();
Console.WriteLine($"[SYSTEM] @{me.Username} запущен в режиме DI.");

await host.RunAsync();