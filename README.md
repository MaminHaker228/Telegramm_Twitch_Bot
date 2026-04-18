# Telegram Bot: Twitch Stream Notifier + WPF Manager

Готовый проект, который состоит из двух частей:

- Python-бот на `aiogram`, отслеживающий Twitch-стримы и отправляющий уведомления в Telegram-канал
- Windows desktop-приложение на `WPF`, через которое удобно запускать бота, редактировать `.env`, смотреть логи и управлять настройками

## Что умеет проект

### Python-бот

- отслеживает один или несколько Twitch-каналов через Twitch Helix API
- отправляет уведомление в Telegram-канал при старте стрима
- добавляет inline-кнопку `Смотреть стрим`
- не шлёт дубликаты для одного и того же эфира
- сбрасывает статус после завершения стрима
- поддерживает команды `/start`, `/status`, `/streamers`, `/add`, `/remove`, `/help`
- автоматически получает и обновляет Twitch access token
- ведёт лог в консоль и файл
- сохраняет состояние между перезапусками в `data/state.json`

### WPF desktop-приложение

- позволяет выбрать папку Python-проекта
- читает и редактирует `.env`
- показывает, в какой Telegram-канал уходят уведомления
- показывает Twitch-стримеров из конфига
- запускает, останавливает и перезапускает Python-бота
- показывает статус процесса
- показывает последние строки из `logs/bot.log`
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
- редактировать `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHANNEL_ID`, `TWITCH_CLIENT_ID`, `TWITCH_CLIENT_SECRET`
- менять список Twitch-стримеров
- запускать и останавливать Python-бота кнопками
- смотреть лог и текущий статус
- переключать тему интерфейса: светлая / тёмная

## Готовый EXE для Windows

В релизах GitHub доступна уже собранная self-contained версия desktop-приложения.

### Как установить EXE

1. Открой страницу релиза на GitHub.
2. Скачай `TwitchBotManager-win-x64.zip`.
3. Распакуй архив в отдельную папку, например `C:\TwitchBotManager`.
4. Запусти `TwitchBotManager.exe`.
5. В приложении укажи путь к папке Python-бота, где лежат `main.py` и `.env`.
6. Проверь токены и настройки.
7. Нажми кнопку запуска бота.

### Важно для EXE

- Это self-contained сборка, поэтому отдельный `.NET Runtime` ставить не нужно.
- Лучше запускать приложение из распакованной папки, а не напрямую из архива.
- Если Windows SmartScreen покажет предупреждение, нажми `Подробнее`, затем `Выполнить в любом случае`.

## Быстрый старт для Python-бота

### 1. Создай `.env`

```powershell
Copy-Item .env.example .env
```

### 2. Заполни `.env`

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

### 3. Установи зависимости

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
pip install -r requirements.txt
```

### 4. Запусти бота

```powershell
python main.py
```

или без активации окружения:

```powershell
.\.venv\Scripts\python.exe main.py
```

## Запуск через WPF UI

### Запуск из исходников

```powershell
dotnet run --project .\desktop\TwitchBotManager\TwitchBotManager.csproj
```

### Сборка решения

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

Основной файл запуска после публикации:

```text
desktop/publish/TwitchBotManager-win-x64/TwitchBotManager.exe
```

## Запуск в VS Code

1. Открой проект в VS Code.
2. Выбери интерпретатор Python из `.venv`.
3. Убедись, что `.env` заполнен.
4. Запусти `python main.py`.

## Запуск в Visual Studio / Rider

1. Открой `TwitchBotManager.sln`.
2. Выбери проект `desktop/TwitchBotManager`.
3. Нажми `Run` или `F5`.
4. Внутри приложения укажи путь к корню Python-проекта.
5. При желании выбери светлую или тёмную тему.

## Настройка Telegram

### Как создать бота через BotFather

1. Открой Telegram.
2. Найди `@BotFather`.
3. Отправь `/newbot`.
4. Укажи имя и username бота.
5. Сохрани токен и вставь его в `TELEGRAM_BOT_TOKEN`.

### Как настроить канал

1. Создай Telegram-канал или используй существующий.
2. Добавь бота в канал как администратора.
3. Выдай право на отправку сообщений.
4. Укажи `TELEGRAM_CHANNEL_ID`.

## Настройка Twitch API

1. Открой `https://dev.twitch.tv/console/apps`.
2. Создай приложение.
3. Укажи `OAuth Redirect URL`, например `http://localhost`.
4. Скопируй `Client ID` и `Client Secret`.
5. Запиши их в `.env`.

### Как проект получает токен Twitch

Проект использует `Client Credentials Grant` и сам обновляет access token.

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
- Если репозиторий остаётся публичным на GitHub, другие пользователи всё равно смогут просматривать его содержимое и делать fork средствами GitHub. Для более жёсткого ограничения доступа репозиторий лучше сделать приватным.

Подробности смотри в файле `LICENSE`.

## Официальные ссылки

- Twitch authentication: `https://dev.twitch.tv/docs/authentication/getting-tokens-oauth`
- Twitch API reference: `https://dev.twitch.tv/docs/api/reference`
- Telegram Bot API: `https://core.telegram.org/bots/api`
- aiogram documentation: `https://docs.aiogram.dev/`
