# Telegram Bot: Twitch Stream Notifier + WPF Manager

В этом репозитории я собрал две части одного проекта:

- Python-бот на `aiogram`, который отслеживает Twitch-стримы и отправляет уведомления в Telegram-канал
- desktop-приложение на `WPF`, через которое удобно запускать бота, редактировать `.env`, смотреть живой лог и управлять настройками без ручной правки файлов

## Что умеет проект

### Python-бот

- отслеживает один или несколько Twitch-каналов через Twitch Helix API
- отправляет уведомление в Telegram-канал при старте стрима
- добавляет inline-кнопку `Смотреть стрим`
- не отправляет дубликаты для одного и того же эфира
- сбрасывает статус после завершения стрима
- поддерживает команды `/start`, `/status`, `/streamers`, `/add`, `/remove`, `/help`
- автоматически получает и обновляет Twitch access token
- пишет лог в консоль и файл
- сохраняет состояние между перезапусками в `data/state.json`

### WPF desktop-приложение

- выбирает папку Python-проекта
- читает и редактирует `.env`
- показывает Telegram-канал для уведомлений
- показывает Twitch-стримеров из конфига
- запускает, останавливает и перезапускает Python-бота
- показывает статус процесса и путь к интерпретатору
- ведёт живой лог по `bot.log`, `stderr.log` и `stdout.log`
- поддерживает светлую и тёмную тему
- собирается в self-contained `EXE` для Windows

## Стек

- Python 3.10+
- aiogram 3
- httpx
- python-dotenv
- asyncio
- .NET 8
- WPF

## Структура проекта

```text
.
├── TwitchBotManager.sln
├── app
│   ├── __init__.py
│   ├── config.py
│   ├── logging_config.py
│   ├── models.py
│   ├── monitor.py
│   ├── retry.py
│   ├── storage.py
│   ├── telegram_bot.py
│   └── twitch.py
├── desktop
│   └── TwitchBotManager
│       ├── Infrastructure
│       ├── Models
│       ├── Resources
│       ├── Services
│       ├── ViewModels
│       ├── App.xaml
│       ├── MainWindow.xaml
│       └── TwitchBotManager.csproj
├── .env.example
├── LICENSE
├── main.py
├── requirements.txt
└── README.md
```

## WPF-приложение

WPF-проект лежит в `desktop/TwitchBotManager`.

### Что можно делать через UI

- указывать путь к папке бота
- менять `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHANNEL_ID`, `TWITCH_CLIENT_ID`, `TWITCH_CLIENT_SECRET`
- менять список Twitch-стримеров
- запускать, останавливать и перезапускать Python-бота кнопками
- смотреть живой лог в реальном времени
- переключать тему интерфейса: светлая / тёмная

## Готовый EXE для Windows

В релизах GitHub лежит уже собранная self-contained версия desktop-приложения.

### Установка EXE

1. Скачать `TwitchBotManager-win-x64.zip` из раздела Releases.
2. Распаковать архив в отдельную папку, например `C:\TwitchBotManager`.
3. Запустить `TwitchBotManager.exe`.
4. В приложении указать путь к папке Python-бота, где лежат `main.py` и `.env`.
5. Проверить токены и настройки.
6. Нажать кнопку запуска бота.

### Важно для EXE

- Это self-contained сборка, отдельный `.NET Runtime` не требуется.
- Приложение лучше запускать из распакованной папки, а не напрямую из архива.
- Если Windows SmartScreen покажет предупреждение, нужно нажать `Подробнее`, затем `Выполнить в любом случае`.

## Быстрый старт для Python-бота

### 1. Создание `.env`

```powershell
Copy-Item .env.example .env
```

### 2. Пример `.env`

```env
TELEGRAM_BOT_TOKEN=1234567890:YOUR_TELEGRAM_BOT_TOKEN
TELEGRAM_CHANNEL_ID=@your_channel_or_-1001234567890
TWITCH_CLIENT_ID=your_twitch_client_id
TWITCH_CLIENT_SECRET=your_twitch_client_secret
TWITCH_USERNAME=example_streamer
TWITCH_USERNAMES=example_streamer,another_streamer
CHECK_INTERVAL_SECONDS=90
REQUEST_TIMEOUT_SECONDS=15
MAX_RETRIES=3
LOG_LEVEL=INFO
STATE_FILE=data/state.json
LOG_FILE=logs/bot.log
TELEGRAM_ADMIN_IDS=123456789,987654321
```

### 3. Установка зависимостей

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
pip install -r requirements.txt
```

### 4. Запуск бота

```powershell
python main.py
```

или без активации окружения:

```powershell
.\.venv\Scripts\python.exe main.py
```

## Запуск WPF из исходников

### Через `dotnet run`

```powershell
dotnet run --project .\desktop\TwitchBotManager\TwitchBotManager.csproj
```

### Сборка solution

```powershell
dotnet build .\TwitchBotManager.sln
```

### Публикация self-contained EXE

```powershell
dotnet publish .\desktop\TwitchBotManager\TwitchBotManager.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\desktop\publish\TwitchBotManager-win-x64
```

Файл запуска после публикации:

```text
desktop/publish/TwitchBotManager-win-x64/TwitchBotManager.exe
```

## Запуск в VS Code

1. Открыть проект в VS Code.
2. Выбрать интерпретатор Python из `.venv`.
3. Убедиться, что `.env` заполнен.
4. Запустить `python main.py`.

## Запуск в Visual Studio / Rider

1. Открыть `TwitchBotManager.sln`.
2. Выбрать проект `desktop/TwitchBotManager`.
3. Нажать `Run` или `F5`.
4. Внутри приложения указать путь к корню Python-проекта.
5. При необходимости переключить тему интерфейса.

## Настройка Telegram

### Создание бота через BotFather

1. Открыть Telegram.
2. Найти `@BotFather`.
3. Отправить `/newbot`.
4. Указать имя и username бота.
5. Сохранить токен и вставить его в `TELEGRAM_BOT_TOKEN`.

### Настройка канала

1. Создать Telegram-канал или использовать существующий.
2. Добавить бота в канал как администратора.
3. Выдать право на отправку сообщений.
4. Указать `TELEGRAM_CHANNEL_ID`.

## Настройка Twitch API

1. Открыть `https://dev.twitch.tv/console/apps`.
2. Создать приложение.
3. Указать `OAuth Redirect URL`, например `http://localhost`.
4. Скопировать `Client ID` и `Client Secret`.
5. Записать их в `.env`.

### Как бот получает Twitch access token

Бот использует `Client Credentials Grant` и сам обновляет access token.

Пример ручного запроса:

```bash
curl -X POST "https://id.twitch.tv/oauth2/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=YOUR_CLIENT_ID&client_secret=YOUR_CLIENT_SECRET&grant_type=client_credentials"
```

## Команды бота

- `/start` — приветствие и описание
- `/status` — статус стримов
- `/streamers` — список отслеживаемых стримеров
- `/add <username>` — добавить стримера
- `/remove <username>` — удалить стримера
- `/help` — список команд

## Где смотреть логи

- `logs/bot.log`
- `runtime/stderr.log`
- `runtime/stdout.log`
- `data/state.json`

## Частые проблемы

### Бот не пишет в канал

Проверь:

- бот добавлен в канал
- бот является администратором
- `TELEGRAM_CHANNEL_ID` указан правильно

### Уведомления не приходят

Проверь:

- `TWITCH_CLIENT_ID` и `TWITCH_CLIENT_SECRET`
- правильность логина Twitch
- ошибки в `logs/bot.log`

### EXE не запускается

Проверь:

- архив полностью распакован
- приложение запускается не из `.zip`
- Windows не заблокировал файл в свойствах
- антивирус не поместил файл в карантин

## Лицензия

Этот проект не является open source.

- Авторские права принадлежат владельцу репозитория `MaminHaker228`.
- Используется закрытая лицензия `All Rights Reserved`.
- Копирование, переработка, распространение и использование кода без письменного разрешения правообладателя запрещены.

Подробности описаны в `LICENSE`.

## Официальные ссылки

- Twitch authentication: `https://dev.twitch.tv/docs/authentication/getting-tokens-oauth`
- Twitch API reference: `https://dev.twitch.tv/docs/api/reference`
- Telegram Bot API: `https://core.telegram.org/bots/api`
- aiogram documentation: `https://docs.aiogram.dev/`
