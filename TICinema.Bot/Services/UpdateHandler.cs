using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TICinema.Bot.Configurations;
using TICinema.Contracts.Protos.Identity;

namespace TICinema.Bot.Services;

public class UpdateHandler(ITelegramBotClient bot, IOptions<BotSettings> settings, AuthService.AuthServiceClient authServiceClient)
{
    // В будущем сюда внедрим IIdentityClient и ICacheService
    private readonly BotSettings _settings = settings.Value;
    private static readonly ConcurrentDictionary<long, string> _sessions = new();

    public async Task OnMessage(Message msg, UpdateType type)
{
    // 1. Обработка команды /start (Сохраняем sessionId)
    if (msg.Text is { } text && text.StartsWith("/start"))
    {
        var parts = text.Split(' ');
        if (parts.Length > 1)
        {
            var sessionId = parts[1];
            _sessions[msg.Chat.Id] = sessionId; // Аналог записи в ctx.session

            await bot.SendMessage(msg.Chat, "Для регистрации поделитесь номером телефона:",
                replyMarkup: new ReplyKeyboardMarkup(KeyboardButton.WithRequestContact("Отправить номер")) 
                { ResizeKeyboard = true });
        }
        return;
    }

    // 2. Обработка Контакта (Логика со скриншота №1)
    if (msg.Contact is { } contact)
    {
        var phone = contact.PhoneNumber;

        // Проверка сессии (аналог if (!ctx.session.id) в видео)
        if (!_sessions.TryGetValue(msg.Chat.Id, out var sessionId))
        {
            await bot.SendMessage(msg.Chat, "Произошла ошибка. Пожалуйста, начните процесс через сайт.");
            return;
        }

        try
        {
            // Подготовка запроса (как на скриншоте: const request: TelegramCompleteRequest)
            var request = new TelegramCompleteRequest
            {
                SessionId = sessionId,
                Phone = phone
            };

            // Вызов gRPC (аналог lastValueFrom(authClient.telegramComplete))
            await authServiceClient.TelegramCompleteAsync(request);

            // Ответ с кнопкой возврата (Логика со скриншота №2)
            var inlineKeyboard = new InlineKeyboardMarkup(
                InlineKeyboardButton.WithUrl(
                    "Вернуться на сайт", 
                    $"https://ticinema.kz/auth/tg-finalize?sessionId={sessionId}"
                )
            );

            await bot.SendMessage(
                chatId: msg.Chat,
                text: "Регистрация успешно завершена!",
                replyMarkup: inlineKeyboard
            );

            // Очищаем временную сессию
            _sessions.TryRemove(msg.Chat.Id, out _);
        }
        catch (Exception ex)
        {
            await bot.SendMessage(msg.Chat, $"Ошибка при связи с сервером: {ex.Message}");
        }
    }
}

    public Task OnError(Exception ex, HandleErrorSource source)
    {
        Console.WriteLine($"[ERROR] {source}: {ex.Message}");
        return Task.CompletedTask;
    }
}