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
        private static readonly string TOKEN = "8341488531:AAHWLr656LN476mYHpEapn_aAv1eeVmjz0s";
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

        static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message?.Chat.Type is ChatType.Group or ChatType.Supergroup)
            {
                // Получаем основной текст ИЛИ подпись медиа
                string content = update.Message.Text ?? update.Message.Caption ?? "";
                
                // Пропускаем, если контент пустой
                if (string.IsNullOrEmpty(content))
                {
                    _logger.LogInformation("Received empty content, skipping.");
                    return;
                }

                _logger.LogInformation($"Received content: {content}");

                bool isBadWord = false;
                bool isMention = false;
                string? matchedMention = null;

                // 1. Проверяем на плохие слова в тексте или подписи
                isBadWord = BANLIST.Any(word => content.ToLower().Contains(word.ToLower()));

                // 2. Проверяем сущности в основном тексте
                if (update.Message.Entities != null)
                {
                    foreach (var entity in update.Message.Entities)
                    {
                        if (entity.Type == MessageEntityType.Mention)
                        {
                            string mention = content.Substring(entity.Offset, entity.Length).ToLower();
                            if (BANLIST.Contains(mention[1..])) // Убираем '@'
                            {
                                isMention = true;
                                matchedMention = mention;
                                break;
                            }
                        }
                    }
                }

                // 3. Проверяем сущности в подписи (для медиа)
                if (!isMention && update.Message.CaptionEntities != null)
                {
                    foreach (var entity in update.Message.CaptionEntities)
                    {
                        if (entity.Type == MessageEntityType.Mention)
                        {
                            string mention = content.Substring(entity.Offset, entity.Length).ToLower();
                            if (BANLIST.Contains(mention[1..]))
                            {
                                isMention = true;
                                matchedMention = mention;
                                break;
                            }
                        }
                    }
                }

                _logger.LogInformation($"Checks: isMention={isMention} (matched: {matchedMention ?? "none"}), isBadWord={isBadWord}");

                // Удаляем сообщение, если есть плохое слово или запрещённое упоминание
                if (isMention || isBadWord)
                {
                    try
                    {
                        await botClient.DeleteMessage(
                            chatId: update.Message.Chat.Id,
                            messageId: update.Message.MessageId,
                            cancellationToken: cancellationToken);

                        _logger.LogInformation($"Deleted message from @{update.Message.From?.Username ?? "Unknown"}. Reason: {(isMention ? $"Banned mention: {matchedMention}" : "Banned word")}");

                        /*await botClient.SendTextMessageAsync(
                            chatId: update.Message.Chat.Id,
                            text: $"Сообщение от @{update.Message.From?.Username ?? "Unknown"} удалено.",
                            cancellationToken: cancellationToken);*/
                    }
                    catch (ApiRequestException ex)
                    {
                        _logger.LogError($"Delete error: {ex.Message}");
                    }
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
