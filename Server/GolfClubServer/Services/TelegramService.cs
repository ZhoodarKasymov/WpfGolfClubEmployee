using GolfClubServer.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GolfClubServer.Services;

public class TelegramService: BackgroundService
{
    private readonly TelegramBotClient _botClient;
    private readonly IServiceProvider _serviceProvider;

    public TelegramService(IServiceProvider serviceProvider, string botToken)
    {
        if (string.IsNullOrEmpty(botToken))
        {
            throw new ArgumentNullException(nameof(botToken));
        }
        _serviceProvider = serviceProvider;
        _botClient = new TelegramBotClient(botToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var me = await _botClient.GetMeAsync(stoppingToken);
            Log.Information($"Telegram bot {me.Username} started.");

            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                cancellationToken: stoppingToken
            );

            // Keep the service running until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            Log.Error(ex, "Error starting Telegram bot");
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update is { Type: UpdateType.Message, Message: not null })
            {
                var chatId = update.Message.Chat.Id;
                var username = update.Message.Chat.Username;

                using var scope = _serviceProvider.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<UnitOfWork>();

                if (update.Message.Text == "/start")
                {
                    string responseMessage;

                    if (username != null)
                    {
                        var worker = await unitOfWork.WorkerRepository.GetAll()
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
                                await unitOfWork.SaveAsync();
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

                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: responseMessage,
                        cancellationToken: cancellationToken
                    );

                    return;
                }

                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Не отвечайте боту, автоматизированный бот, мы не видим ваши сообщения!",
                    cancellationToken: cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка с хендлером телеграммом");
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Log.Error(exception, "Ошибка телеграмм бота");
        return Task.CompletedTask;
    }

    public async Task SendMessageByUsernameAsync(int workerId, string message)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<UnitOfWork>();

        var worker = await unitOfWork.WorkerRepository
            .GetAll()
            .Where(w => w.DeletedAt == null)
            .FirstOrDefaultAsync(w => w.Id == workerId);

        if (worker is not null)
        {
            if (worker.ChatId is null)
            {
                Log.Information($"Работник с ID:{workerId} не имеет ChatId для отправки сообщения");
                return;
            }
            await _botClient.SendTextMessageAsync(worker.ChatId.Value, message);
            Log.Information($"Сообщение отправлено работнику ID:{workerId} (ChatId: {worker.ChatId})");
        }
        else
        {
            Log.Information($"Работник с ID:{workerId} не найден для отправки сообщения");
        }
    }
}