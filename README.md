# Telegram Bot: Twitch Stream Notifier

Готовый бот на Python 3.10+, который следит за Twitch-стримерами через Twitch Helix API и отправляет сообщение в Telegram-канал, когда стрим начинается.

## Что делает бот

- Проверяет, идет ли стрим у одного или нескольких Twitch-каналов.
- Отправляет уведомление в Telegram-канал при старте стрима.
- Вкладывает inline-кнопку `Смотреть стрим`.
- Не отправляет дубликаты для одного и того же эфира.
- Сбрасывает состояние, когда стрим заканчивается.
- Поддерживает команды `/start`, `/status`, `/streamers`, `/add`, `/remove`, `/help`.
- Автоматически получает и обновляет Twitch access token.
- Пишет логи в консоль и файл.
- Сохраняет состояние между перезапусками в `data/state.json`.

## Стек

- Python 3.10+
- aiogram 3
- httpx
- python-dotenv
- asyncio

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
│       ├── Services
│       ├── ViewModels
│       ├── App.xaml
│       ├── MainWindow.xaml
│       └── TwitchBotManager.csproj
├── .env.example
├── main.py
├── requirements.txt
└── README.md
```

## Desktop UI на WPF

В репозитории есть отдельное Windows-приложение на WPF для удобного управления ботом.

Что умеет desktop-приложение:

- выбирать папку Python-проекта с ботом
- читать и редактировать `.env`
- показывать, в какой Telegram-канал уходят уведомления
- показывать список Twitch-стримеров из конфига
- запускать, останавливать и перезапускать Python-бота
- показывать путь до используемого Python
- подтягивать последние строки из `logs/bot.log`

WPF-проект лежит в `desktop/TwitchBotManager`.

## Быстрый старт

### 1. Создай `.env`

Если шаблон уже подготовлен, скопируй его:

```powershell
Copy-Item .env.example .env
```

Важно: реальные токены и секреты должны храниться в `.env`. Файл `.env.example` лучше оставлять как шаблон без боевых значений.

### 2. Заполни `.env`

Пример:

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

#### Windows PowerShell

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
pip install -r requirements.txt
```

#### Linux / macOS

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install --upgrade pip
pip install -r requirements.txt
```

### 4. Запусти бота

#### Обычный запуск

```powershell
.\.venv\Scripts\python.exe main.py
```

или, если окружение уже активировано:

```powershell
python main.py
```

#### Запуск через WPF-панель

Если хочешь управлять ботом через UI:

```powershell
dotnet run --project .\desktop\TwitchBotManager\TwitchBotManager.csproj
```

После запуска WPF-приложение позволит:

- выбрать папку `E:\Twitch`
- загрузить текущий `.env`
- изменить токены, channel id и Twitch-настройки
- нажать `Запустить бота` или `Остановить`

После запуска бот:

- проверит настройки из `.env`
- получит Twitch app access token
- поднимет Telegram long polling
- запустит фоновый мониторинг стримов

### 5. Проверь, что бот стартовал

Успешный запуск выглядит примерно так:

```text
2026-04-18 17:24:15 | INFO     | main | Starting Twitch to Telegram notifier
2026-04-18 17:24:16 | INFO     | TwitchClient | Successfully refreshed Twitch app access token
2026-04-18 17:24:17 | INFO     | TelegramBotService | Starting Telegram long polling
2026-04-18 17:24:17 | INFO     | StreamMonitor | Stream monitor started. Poll interval: 90 seconds
```

## Как запускать бота

Ниже — практические варианты запуска.

### Вариант 1. Запуск из PowerShell

Открой папку проекта и выполни:

```powershell
cd E:\Twitch
.\.venv\Scripts\Activate.ps1
python main.py
```

### Вариант 2. Запуск без активации окружения

```powershell
cd E:\Twitch
.\.venv\Scripts\python.exe main.py
```

### Вариант 3. Запуск в VS Code

1. Открой проект в VS Code.
2. Открой встроенный терминал `Terminal -> New Terminal`.
3. Выбери интерпретатор из `.venv` через `Python: Select Interpreter`.
4. Убедись, что файл `.env` заполнен.
5. Запусти:

```powershell
python main.py
```

### Вариант 4. Запуск desktop-приложения из Visual Studio или Rider

1. Открой `TwitchBotManager.sln`.
2. Выбери проект `desktop/TwitchBotManager`.
3. Нажми `Run` или `F5`.
4. Внутри приложения укажи путь к корню Python-проекта, обычно `E:\Twitch`.

### Вариант 5. Сборка WPF-приложения

```powershell
dotnet build .\TwitchBotManager.sln
```

Готовый exe после сборки появится здесь:

```text
desktop/TwitchBotManager/bin/Debug/net8.0-windows/
```

### Как остановить бота

Если бот запущен в текущем окне терминала, нажми `Ctrl + C`.

Если бот запущен как фоновый процесс, останови его по PID:

```powershell
Stop-Process -Id <PID>
```

## Публикация на GitHub

Проект уже подготовлен к публикации:

- добавлен `.gitignore`
- добавлен `.gitattributes`
- очищен `.env.example` от реальных секретов
- инициализирован git-репозиторий
- добавлен GitHub Actions workflow `.github/workflows/ci.yml`

### Как отправить проект в свой GitHub-репозиторий

1. Создай пустой репозиторий на GitHub, например `twitch-telegram-bot`.
2. Открой PowerShell в папке проекта.
3. Выполни команды:

```powershell
git add .
git commit -m "Initial commit"
git remote add origin https://github.com/YOUR_USERNAME/YOUR_REPOSITORY.git
git push -u origin main
```

Если у тебя на GitHub уже создан репозиторий и в нем есть ветка `main`, этого достаточно.

Важно:

- файл `.env` в git не попадет
- папки `logs/`, `runtime/`, `data/`, `.venv/` в git не попадут
- секреты нужно хранить только локально в `.env`

## Настройка Telegram

### Как создать бота через BotFather

1. Открой Telegram.
2. Найди `@BotFather`.
3. Отправь команду `/newbot`.
4. Задай имя бота.
5. Задай username бота, который заканчивается на `bot`.
6. Сохрани токен, который выдаст BotFather.
7. Вставь этот токен в `TELEGRAM_BOT_TOKEN`.

### Как настроить канал

1. Создай Telegram-канал или используй существующий.
2. Добавь бота в канал как администратора.
3. Дай боту право отправлять сообщения.
4. Укажи `TELEGRAM_CHANNEL_ID`.

### Что указывать в `TELEGRAM_CHANNEL_ID`

Можно использовать:

- публичный username канала, например `@my_stream_alerts`
- numeric chat id, например `-1001234567890`

## Настройка Twitch API

### Как получить `TWITCH_CLIENT_ID` и `TWITCH_CLIENT_SECRET`

1. Открой Twitch Developer Console: `https://dev.twitch.tv/console/apps`
2. Нажми `Register Your Application`.
3. Укажи название приложения.
4. В поле `OAuth Redirect URL` можно указать `http://localhost`.
5. После создания приложения скопируй `Client ID`.
6. В разделе управления приложением скопируй `Client Secret`.
7. Запиши значения в `.env`.

### Как проект получает Twitch access token

Проект использует `Client Credentials Grant` и сам получает `app access token`.

Эквивалент ручного запроса:

```bash
curl -X POST "https://id.twitch.tv/oauth2/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=YOUR_CLIENT_ID&client_secret=YOUR_CLIENT_SECRET&grant_type=client_credentials"
```

После этого Twitch Helix API вызывается с заголовками:

```http
Client-Id: YOUR_CLIENT_ID
Authorization: Bearer YOUR_ACCESS_TOKEN
```

Вручную токен в проекте задавать не нужно: бот обновляет его автоматически.

## Переменные окружения

- `TELEGRAM_BOT_TOKEN` — токен Telegram-бота от BotFather.
- `TELEGRAM_CHANNEL_ID` — канал, куда бот будет отправлять уведомления.
- `TWITCH_CLIENT_ID` — Client ID Twitch-приложения.
- `TWITCH_CLIENT_SECRET` — Client Secret Twitch-приложения.
- `TWITCH_USERNAME` — один Twitch-логин для отслеживания.
- `TWITCH_USERNAMES` — список Twitch-логинов через запятую.
- `CHECK_INTERVAL_SECONDS` — интервал проверки стримов. Допустимый диапазон: от `60` до `120` секунд.
- `REQUEST_TIMEOUT_SECONDS` — timeout HTTP-запросов.
- `MAX_RETRIES` — количество повторных попыток при временных ошибках.
- `LOG_LEVEL` — уровень логирования.
- `STATE_FILE` — файл, в котором хранится состояние бота.
- `LOG_FILE` — путь к лог-файлу.
- `TELEGRAM_ADMIN_IDS` — список Telegram user id через запятую. Если указан, только эти пользователи смогут вызывать `/add` и `/remove`.

## Команды бота

- `/start` — приветствие и краткое описание.
- `/status` — показывает, идет ли сейчас стрим.
- `/streamers` — список отслеживаемых стримеров.
- `/add <username>` — добавить стримера в список отслеживания.
- `/remove <username>` — удалить стримера из списка отслеживания.
- `/help` — список команд.

`/add` и `/remove` принимают:

- `username`
- `@username`
- `https://twitch.tv/username`

## Пример сообщения в канале

```text
🎬 example_streamer вышел в эфир!

Название: Ranked grind
Категория: VALORANT
Ссылка: https://twitch.tv/example_streamer
Старт: 18.04.2026 09:00 UTC
```

Под сообщением будет inline-кнопка `Смотреть стрим`.

## Где смотреть логи

- основной лог: `logs/bot.log`
- текущее состояние: `data/state.json`

Если запускаешь бот вручную, основные сообщения также выводятся прямо в консоль.

## Как бот работает

1. Загружает настройки из `.env`.
2. Проверяет обязательные переменные окружения.
3. Получает Twitch app access token.
4. Стартует Telegram long polling.
5. Каждые `60–120` секунд опрашивает Twitch Helix API.
6. Если начался новый стрим, отправляет сообщение в Telegram-канал.
7. Если стрим уже был уведомлен, повторно сообщение не отправляется.
8. После завершения стрима состояние сбрасывается.

## Что уже сделано для стабильности

- автообновление Twitch токена
- retry-логика для Twitch API и Telegram API
- защита от дублей по `stream_id`
- файловое хранение состояния без обязательной базы данных
- логирование в консоль и файл
- обработка временных сетевых ошибок
- поддержка нескольких стримеров

## Частые проблемы

### Ошибка `Missing required environment variables`

В `.env` не заполнена одна или несколько обязательных переменных.

### Ошибка `CHECK_INTERVAL_SECONDS must be >= 60`

Укажи значение от `60` до `120`.

### Бот не пишет в канал

Проверь:

- бот добавлен в канал
- бот является администратором канала
- `TELEGRAM_CHANNEL_ID` указан правильно

### Уведомления не приходят

Проверь:

- правильность `TWITCH_CLIENT_ID` и `TWITCH_CLIENT_SECRET`
- существует ли Twitch-логин
- есть ли ошибки в `logs/bot.log`

## Официальные ссылки

- Twitch authentication: `https://dev.twitch.tv/docs/authentication/getting-tokens-oauth`
- Twitch authentication overview: `https://dev.twitch.tv/docs/authentication`
- Twitch Helix API reference: `https://dev.twitch.tv/docs/api/reference`
- Telegram Bot API: `https://core.telegram.org/bots/api`
- aiogram documentation: `https://docs.aiogram.dev/`
