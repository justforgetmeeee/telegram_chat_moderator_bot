
# Telegram Chat Moderator Bot

## Основные функции

- 🚫 Автоматическое удаление сообщений с запрещёнными словами
- 🔇 Блокировка упоминаний забаненных пользователей
- 📝 Логирование всех действий (удалённые сообщения, ошибки)
- 📁 Работа с динамическим списком запрещённых слов (banlist.txt)
- 🖼️ Поддержка модерации подписей к медиафайлам

## Зависимости
- [.NET 9.0.304](https://dotnet.microsoft.com/ru-ru/download/dotnet/9.0)

- Токен телеграм бота
# Запуск бота

Копирование репозитория

```bash
  git clone https://github.com/justforgetmeeee/telegram_chat_moderator_bot.git
```

Переход в папку проекта

```bash
  cd telegram_chat_moderator_bot
```

Установка необходимых пакетов

```bash
  dotnet add package Microsoft.Extensions.Logging
  dotnet add package Serilog.Extensions.Logging.File
  dotnet add package Telegram.Bot
```


Запуск бота для телеграм

```bash
  dotnet run
```

