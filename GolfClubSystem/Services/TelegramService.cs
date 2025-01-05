using GolfClubSystem.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GolfClubSystem.Services;

public class TelegramService
{
    private readonly TelegramBotClient _botClient;
    private readonly UnitOfWork _unitOfWork;

    public TelegramService(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            throw new ArgumentNullException(nameof(token));
        }
        _unitOfWork = new UnitOfWork();
        _botClient = new TelegramBotClient(token);
    }
    
    public async Task StartListeningAsync()
    {
        var me = await _botClient.GetMeAsync();
        Log.Information($"Telegram bot {me.Username} started.");

        _botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update is { Type: UpdateType.Message, Message: not null })
            {
                var chatId = update.Message.Chat.Id;
                var username = update.Message.Chat.Username;

                // Обработка команды /start
                if (update.Message.Text == "/start")
                {
                    string responseMessage;
                    
                    if (username != null)
                    {
                        var worker = await _unitOfWork.WorkerRepository.GetAllAsync()
                            .Where(w => w.DeletedAt == null)
                            .FirstOrDefaultAsync(w => w.TelegramUsername != null && w.TelegramUsername.ToLower() == username, cancellationToken);
                    
                        if (worker is not null)
                        {
                            if (worker.ChatId != null)
                            {
                                responseMessage = $"Вы уже были зарегистрированы в системе {worker.FullName}";
                            }
                            else
                            {
                                responseMessage = $"Добро пожаловать, {worker.FullName}! Вы зарегистрированы в системе.";
                                worker.ChatId = chatId;
                                await _unitOfWork.SaveAsync();
                                Log.Information($"Зарегистрирован через бот: {username} (ChatId: {chatId})");
                            }
                        }
                        else
                        {
                            responseMessage = "К сожалению Вас не смогли найти в базе работников!";
                        }
                    }
                    else
                    {
                        responseMessage = "Вам нужно добавить \"UserName\" в профиле телеграмм, чтобы мы подписали вас на уведомления.\nПосле добавления нажмите или отправьте повторно комманду: /start";
                    }
                    
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: responseMessage,
                        cancellationToken: cancellationToken
                    );
                    
                    return;
                }

                // Ответ на любые другие сообщения
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Не отвечайте боту, автоматизированный бот, мы не видим ваши сообщения!",
                    cancellationToken: cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Ошибка с хендлером телеграммом: {ex.Message}");
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Log.Error($"Ошибка телеграмм бота: {exception.Message}");
        return Task.CompletedTask;
    }

    public async Task SendMessageByUsernameAsync(int workerId, string message)
    {
        var worker = await _unitOfWork.WorkerRepository
            .GetAllAsync()
            .Where(w => w.DeletedAt == null)
            .FirstOrDefaultAsync(w => w.Id == workerId);

        if (worker is not null)
        {
            if(worker.ChatId is null) return;
            await _botClient.SendMessage(worker.ChatId, message);
        }
        else
        {
            Log.Information($"Работник с ID:{workerId} не найден для отправки запроса");
        }
    }
}