from __future__ import annotations

import asyncio
import logging
from contextlib import suppress
from typing import TYPE_CHECKING

from aiogram.exceptions import TelegramAPIError

from app.config import Settings
from app.storage import FileStateRepository
from app.twitch import TwitchApiError, TwitchClient

if TYPE_CHECKING:
    from app.telegram_bot import TelegramBotService


class StreamMonitor:
    def __init__(
        self,
        settings: Settings,
        storage: FileStateRepository,
        twitch_client: TwitchClient,
        telegram_bot: "TelegramBotService",
    ) -> None:
        self._settings = settings
        self._storage = storage
        self._twitch_client = twitch_client
        self._telegram_bot = telegram_bot
        self._logger = logging.getLogger(self.__class__.__name__)
        self._stop_event = asyncio.Event()
        self._task: asyncio.Task[None] | None = None

    async def start(self) -> None:
        if self._task is not None and not self._task.done():
            return
        self._stop_event.clear()
        self._task = asyncio.create_task(self.run(), name="stream-monitor")

    async def stop(self) -> None:
        self._stop_event.set()
        if self._task is None:
            return

        self._task.cancel()
        with suppress(asyncio.CancelledError):
            await self._task
        self._task = None

    async def run(self) -> None:
        self._logger.info(
            "Stream monitor started. Poll interval: %s seconds",
            self._settings.check_interval_seconds,
        )
        while not self._stop_event.is_set():
            try:
                await self.check_once()
            except asyncio.CancelledError:
                raise
            except Exception:
                self._logger.exception("Unhandled error during stream monitor cycle")

            try:
                await asyncio.wait_for(
                    self._stop_event.wait(),
                    timeout=self._settings.check_interval_seconds,
                )
            except asyncio.TimeoutError:
                continue

    async def check_once(self) -> None:
        streamers = await self._storage.list_streamers()
        if not streamers:
            self._logger.debug("No streamers configured, skipping monitor cycle")
            return

        try:
            live_map = await self._twitch_client.get_streams_by_logins(
                [streamer.login for streamer in streamers]
            )
        except TwitchApiError:
            self._logger.exception("Failed to fetch stream statuses from Twitch")
            return

        for streamer in streamers:
            live_stream = live_map.get(streamer.login)
            if live_stream is None:
                if streamer.is_live:
                    await self._storage.set_stream_offline(streamer.login)
                    self._logger.info("Streamer %s went offline", streamer.login)
                continue

            await self._storage.set_stream_live(streamer.login, live_stream, notified=False)
            if streamer.last_notified_stream_id == live_stream.stream_id:
                continue

            try:
                await self._telegram_bot.send_stream_notification(live_stream)
            except TelegramAPIError:
                self._logger.exception(
                    "Failed to send Telegram notification for %s", streamer.login
                )
                continue

            await self._storage.set_stream_live(streamer.login, live_stream, notified=True)
            self._logger.info("Notification stored for %s", streamer.login)
