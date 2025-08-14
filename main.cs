using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Logging;

namespace TelegramBot
{
    class Program
    {
        private static readonly string TOKEN = "";
        private static List<string> BANLIST = new List<string>();
        private static readonly ILogger<Program> _logger;

        static Program()
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder.AddFile("logs/bot.log"); // Логирование
            });
            _logger = factory.CreateLogger<Program>();
        }

        static void LoadBanlist() // Загрузка запрещенных слов
        {
            try
            {
                string banlistPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "banlist.txt");

                if (File.Exists(banlistPath))
                {
                    BANLIST = File.ReadAllLines(banlistPath, System.Text.Encoding.UTF8)
                        .Select(line => line.Trim().ToLower())
                        .Where(line => !string.IsNullOrEmpty(line))
                        .ToList();
                    _logger.LogInformation($"Banlist loaded with {BANLIST.Count} entries: {string.Join(", ", BANLIST)}");
                }
                else
                {
                    File.WriteAllText(banlistPath, "");
                    _logger.LogWarning("Created new empty banlist.txt file");
                    BANLIST = new List<string>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading banlist: {ex.Message}");
                BANLIST = new List<string>();
            }
        }

        static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken) // Логирование сообщений
        {
            if (update.Message?.Chat.Type is ChatType.Group or ChatType.Supergroup) // Проверка типа группы
            {
                string messageText = update.Message.Text ?? "";
                _logger.LogInformation($"Received message: Chat={update.Message.Chat.Type}, Text={messageText}, Entities={update.Message.Entities?.Length ?? 0}");

                bool isMention = false;
                string? matchedMention = null;
                if (update.Message.Entities != null)
                {
                    foreach (var entity in update.Message.Entities)
                    {
                        if (entity.Type == MessageEntityType.Mention)
                        {
                            string mentionText = messageText.Substring(entity.Offset, entity.Length).ToLower();
                            string username = mentionText.Substring(1);
                            if (BANLIST.Contains(username))
                            {
                                isMention = true;
                                matchedMention = mentionText;
                                break;
                            }
                        }
                    }
                }

                bool isBadWord = BANLIST.Any(badWord => messageText.ToLower().Contains(badWord.ToLower())); // Проверка слова на содержание в бан листе

                _logger.LogInformation($"Checks: isMention={isMention} (matched={matchedMention ?? "none"}), isBadWord={isBadWord}");

                if (isMention || isBadWord) // Логика при обнаружении слова из бан листа
                {
                    try
                    {
                        await botClient.DeleteMessage(
                            chatId: update.Message.Chat.Id,
                            messageId: update.Message.MessageId,
                            cancellationToken: cancellationToken);

                        _logger.LogInformation($"Deleted message from @{update.Message.From?.Username ?? "Unknown"}: {messageText}");

                        /*await botClient.SendMessage(
                            chatId: update.Message.Chat.Id,
                            text: $"Сообщение от @{update.Message.From?.Username ?? "Unknown"} удалено.",
                            cancellationToken: cancellationToken);*/ // Отправка сообщения от кого было удалено сообщение
                    }
                    catch (ApiRequestException ex)
                    {
                        _logger.LogError($"Error deleting message {update.Message.MessageId}: {ex.Message}");
                    }
                }
                else
                {
                    _logger.LogInformation($"Message not deleted: {messageText}");
                }
            }
        }

        static async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource handleErrorSource, CancellationToken cancellationToken) // Логирование ошибок
        {
            _logger.LogError($"Error: {exception.Message}"); 
        }

        static async Task Main()
        {
            LoadBanlist();

            var botClient = new TelegramBotClient(TOKEN);

            using var cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message }
            };

            Console.WriteLine("Бот запущен. Загружен бан лист из: " +
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "banlist.txt"));
            _logger.LogInformation("Bot started");

            botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Bot stopped");
            }
        }
    }
}