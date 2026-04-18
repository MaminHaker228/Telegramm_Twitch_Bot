from __future__ import annotations

import asyncio
import html
import logging
from datetime import timezone

from aiogram import Bot, Dispatcher, Router
from aiogram.client.default import DefaultBotProperties
from aiogram.enums import ParseMode
from aiogram.exceptions import (
    TelegramAPIError,
    TelegramNetworkError,
    TelegramRetryAfter,
    TelegramServerError,
)
from aiogram.filters import Command, CommandObject
from aiogram.types import (
    BotCommand,
    InlineKeyboardButton,
    InlineKeyboardMarkup,
    Message,
)

from app.config import Settings
from app.models import StreamInfo
from app.storage import FileStateRepository
from app.twitch import TwitchApiError, TwitchClient


class TelegramBotService:
    def __init__(
        self,
        settings: Settings,
        storage: FileStateRepository,
        twitch_client: TwitchClient,
    ) -> None:
        self.settings = settings
        self.storage = storage
        self.twitch_client = twitch_client
        self.logger = logging.getLogger(self.__class__.__name__)
        self.bot = Bot(
            token=settings.telegram_bot_token,
            default=DefaultBotProperties(parse_mode=ParseMode.HTML),
        )
        self.dispatcher = Dispatcher()
        self.router = Router(name="main-router")
        self.dispatcher.include_router(self.router)
        self._register_handlers()

    async def set_bot_commands(self) -> None:
        commands = [
            BotCommand(command="start", description="Приветствие и краткая справка"),
            BotCommand(command="status", description="Показать текущий статус стримов"),
            BotCommand(command="streamers", description="Список отслеживаемых стримеров"),
            BotCommand(command="add", description="Добавить стримера: /add <username>"),
            BotCommand(command="remove", description="Удалить стримера: /remove <username>"),
            BotCommand(command="help", description="Показать список команд"),
        ]
        await self.bot.set_my_commands(commands)

    async def start_polling(self) -> None:
        self.logger.info("Starting Telegram long polling")
        await self.dispatcher.start_polling(self.bot)

    async def close(self) -> None:
        await self.bot.session.close()

    async def send_stream_notification(self, stream: StreamInfo) -> None:
        message = self._format_stream_notification(stream)
        keyboard = InlineKeyboardMarkup(
            inline_keyboard=[
                [InlineKeyboardButton(text="Смотреть стрим", url=stream.url)]
            ]
        )
        last_error: TelegramAPIError | None = None

        for attempt in range(1, self.settings.max_retries + 1):
            try:
                await self.bot.send_message(
                    chat_id=self.settings.telegram_channel_id,
                    text=message,
                    reply_markup=keyboard,
                )
                self.logger.info(
                    "Notification sent to Telegram channel for %s", stream.user_login
                )
                return
            except TelegramRetryAfter as exc:
                last_error = exc
                self.logger.warning(
                    "Telegram rate limit reached while sending notification for %s. Sleeping %.2f seconds.",
                    stream.user_login,
                    exc.retry_after,
                )
                await asyncio.sleep(exc.retry_after)
            except (TelegramNetworkError, TelegramServerError) as exc:
                last_error = exc
                if attempt >= self.settings.max_retries:
                    raise
                delay = min(8, 2 ** (attempt - 1))
                self.logger.warning(
                    "Temporary Telegram error while sending notification for %s: %s. Retrying in %s seconds.",
                    stream.user_login,
                    exc,
                    delay,
                )
                await asyncio.sleep(delay)
            except TelegramAPIError as exc:
                last_error = exc
                raise

        if last_error is not None:
            raise last_error

    def _register_handlers(self) -> None:
        self.router.message.register(self.handle_start, Command("start"))
        self.router.message.register(self.handle_help, Command("help"))
        self.router.message.register(self.handle_status, Command("status"))
        self.router.message.register(self.handle_streamers, Command("streamers"))
        self.router.message.register(self.handle_add, Command("add"))
        self.router.message.register(self.handle_remove, Command("remove"))

    async def handle_start(self, message: Message) -> None:
        tracked = await self.storage.list_streamers()
        text = (
            "Привет! Я слежу за Twitch-стримерами и отправляю уведомления в Telegram-канал, "
            "когда начинается эфир.\n\n"
            f"Сейчас отслеживаю: <b>{len(tracked)}</b> стример(ов).\n"
            f"Канал для уведомлений: <code>{html.escape(self.settings.telegram_channel_id)}</code>.\n\n"
            "Команды: /status, /streamers, /add, /remove, /help"
        )
        await message.answer(text)

    async def handle_help(self, message: Message) -> None:
        admin_note = (
            "\nКоманды /add и /remove ограничены по TELEGRAM_ADMIN_IDS."
            if self.settings.telegram_admin_ids
            else ""
        )
        text = (
            "<b>Доступные команды</b>\n\n"
            "/start — приветствие и краткое описание\n"
            "/status — показывает, идет ли сейчас стрим\n"
            "/streamers — список отслеживаемых стримеров\n"
            "/add &lt;username&gt; — добавить стримера\n"
            "/remove &lt;username&gt; — удалить стримера\n"
            "/help — список команд\n\n"
            "Можно передавать Twitch-логин, @username или полную ссылку вида "
            "<code>https://twitch.tv/username</code>."
            f"{admin_note}"
        )
        await message.answer(text)

    async def handle_streamers(self, message: Message) -> None:
        streamers = await self.storage.list_streamers()
        if not streamers:
            await message.answer(
                "Пока никого не отслеживаю. Добавь стримера командой <code>/add username</code>."
            )
            return

        lines = ["<b>Отслеживаемые стримеры</b>"]
        for streamer in streamers:
            status = "LIVE" if streamer.is_live else "OFFLINE"
            lines.append(
                f"• <b>{html.escape(streamer.display_name)}</b> "
                f"(<code>{html.escape(streamer.login)}</code>) — {status}"
            )
        await message.answer("\n".join(lines))

    async def handle_status(self, message: Message) -> None:
        streamers = await self.storage.list_streamers()
        if not streamers:
            await message.answer(
                "Список стримеров пуст. Добавь первого командой <code>/add username</code>."
            )
            return

        try:
            live_map = await self.twitch_client.get_streams_by_logins(
                [streamer.login for streamer in streamers]
            )
        except TwitchApiError as exc:
            self.logger.exception("Failed to fetch stream status")
            await message.answer(
                f"Не удалось получить статус стрима: <code>{html.escape(str(exc))}</code>"
            )
            return

        lines = ["<b>Текущий статус</b>"]
        for streamer in streamers:
            live_stream = live_map.get(streamer.login)
            if live_stream is None:
                await self.storage.set_stream_offline(streamer.login)
                lines.append(
                    f"• <b>{html.escape(streamer.display_name)}</b> — OFFLINE"
                )
                continue

            await self.storage.set_stream_live(
                streamer.login,
                live_stream,
                notified=streamer.last_notified_stream_id == live_stream.stream_id,
            )
            lines.append(
                "• <b>{name}</b> — LIVE\n"
                "  Название: <b>{title}</b>\n"
                "  Категория: <b>{game}</b>\n"
                "  Ссылка: {url}".format(
                    name=html.escape(live_stream.user_name),
                    title=html.escape(live_stream.title),
                    game=html.escape(live_stream.game_name),
                    url=html.escape(live_stream.url),
                )
            )

        await message.answer("\n".join(lines))

    async def handle_add(self, message: Message, command: CommandObject) -> None:
        if not await self._ensure_admin(message):
            return

        login = self._extract_login(command)
        if not login:
            await message.answer("Использование: <code>/add username</code>")
            return

        try:
            users = await self.twitch_client.get_users_by_logins([login])
        except TwitchApiError as exc:
            self.logger.exception("Failed to fetch Twitch user for /add")
            await message.answer(
                f"Не удалось проверить стримера: <code>{html.escape(str(exc))}</code>"
            )
            return

        user = users.get(login)
        if user is None:
            await message.answer(
                f"Стример <code>{html.escape(login)}</code> не найден в Twitch."
            )
            return

        previous_state = await self.storage.get_streamer(login)
        _, created = await self.storage.upsert_streamer(user)

        extra_message = ""
        try:
            live_map = await self.twitch_client.get_streams_by_logins([login])
        except TwitchApiError:
            self.logger.exception("Failed to fetch live state while adding streamer")
            live_map = {}

        live_stream = live_map.get(login)
        if live_stream is not None:
            await self.storage.set_stream_live(login, live_stream, notified=False)
            already_notified = (
                previous_state is not None
                and previous_state.last_notified_stream_id == live_stream.stream_id
            )
            if not already_notified:
                try:
                    await self.send_stream_notification(live_stream)
                except TelegramAPIError as exc:
                    self.logger.exception(
                        "Streamer %s is live, but notification send failed", login
                    )
                    extra_message = (
                        "\n\n⚠️ Стример уже в эфире, но отправить уведомление в канал не удалось: "
                        f"<code>{html.escape(str(exc))}</code>"
                    )
                else:
                    await self.storage.set_stream_live(login, live_stream, notified=True)
                    extra_message = (
                        "\n\n🔔 Стример уже был в эфире — уведомление сразу отправлено в канал."
                    )
        elif previous_state is not None and previous_state.is_live:
            await self.storage.set_stream_offline(login)

        action = "добавлен" if created else "обновлен"
        await message.answer(
            f"Стример <b>{html.escape(user.display_name)}</b> "
            f"(<code>{html.escape(user.login)}</code>) {action} в список отслеживания."
            f"{extra_message}"
        )

    async def handle_remove(self, message: Message, command: CommandObject) -> None:
        if not await self._ensure_admin(message):
            return

        login = self._extract_login(command)
        if not login:
            await message.answer("Использование: <code>/remove username</code>")
            return

        removed = await self.storage.remove_streamer(login)
        if not removed:
            await message.answer(
                f"Стример <code>{html.escape(login)}</code> не найден в списке отслеживания."
            )
            return

        await message.answer(
            f"Стример <code>{html.escape(login)}</code> удален из списка отслеживания."
        )

    async def _ensure_admin(self, message: Message) -> bool:
        if not self.settings.telegram_admin_ids:
            return True

        user = message.from_user
        if user is None or user.id not in self.settings.telegram_admin_ids:
            await message.answer("Эта команда доступна только администраторам бота.")
            return False
        return True

    @staticmethod
    def _extract_login(command: CommandObject | None) -> str:
        if command is None or not command.args:
            return ""
        return _normalize_twitch_login(command.args)

    @staticmethod
    def _format_stream_notification(stream: StreamInfo) -> str:
        started_at = stream.started_at.astimezone(timezone.utc).strftime("%d.%m.%Y %H:%M UTC")
        return (
            f"🎬 <b>{html.escape(stream.user_name)} вышел в эфир!</b>\n\n"
            f"<b>Название:</b> {html.escape(stream.title)}\n"
            f"<b>Категория:</b> {html.escape(stream.game_name)}\n"
            f"<b>Ссылка:</b> {html.escape(stream.url)}\n"
            f"<b>Старт:</b> {started_at}"
        )


def _normalize_twitch_login(value: str) -> str:
    normalized = value.strip().lower()
    if not normalized:
        return ""

    if normalized.startswith("https://"):
        normalized = normalized[len("https://") :]
    elif normalized.startswith("http://"):
        normalized = normalized[len("http://") :]

    if normalized.startswith("www."):
        normalized = normalized[len("www.") :]

    if normalized.startswith("twitch.tv/"):
        normalized = normalized.split("/", maxsplit=1)[1]

    return normalized.strip().strip("/").lstrip("@")
