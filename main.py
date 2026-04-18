from __future__ import annotations

import asyncio
import logging

from app.config import get_settings
from app.logging_config import configure_logging
from app.monitor import StreamMonitor
from app.storage import FileStateRepository
from app.telegram_bot import TelegramBotService
from app.twitch import TwitchApiError, TwitchClient


async def main() -> None:
    settings = get_settings()
    configure_logging(settings)
    logger = logging.getLogger("main")

    logger.info("Starting Twitch to Telegram notifier")

    storage = FileStateRepository(settings.state_file)
    await storage.load()

    twitch_client = TwitchClient(settings)
    telegram_bot = TelegramBotService(settings, storage, twitch_client)
    monitor = StreamMonitor(settings, storage, twitch_client, telegram_bot)

    try:
        await _seed_streamers_from_env(storage, twitch_client, settings.twitch_usernames)
        await telegram_bot.set_bot_commands()
        await monitor.start()
        await telegram_bot.start_polling()
    finally:
        logger.info("Shutting down services")
        await monitor.stop()
        await telegram_bot.close()
        await twitch_client.close()


async def _seed_streamers_from_env(
    storage: FileStateRepository,
    twitch_client: TwitchClient,
    usernames: tuple[str, ...],
) -> None:
    logger = logging.getLogger("main")
    if not usernames:
        logger.warning(
            "No Twitch streamers configured in TWITCH_USERNAME/TWITCH_USERNAMES. Bot will start with an empty watch list."
        )
        return

    try:
        users = await twitch_client.get_users_by_logins(usernames)
    except TwitchApiError:
        logger.exception("Failed to resolve Twitch usernames from environment")
        return

    missing = [login for login in usernames if login not in users]
    for login in missing:
        logger.warning("Streamer from environment not found on Twitch: %s", login)

    if not users:
        return

    added = await storage.add_missing_streamers(list(users.values()))
    if added:
        logger.info(
            "Added %s streamer(s) from environment: %s",
            len(added),
            ", ".join(streamer.login for streamer in added),
        )


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass
