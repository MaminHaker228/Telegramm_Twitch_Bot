from __future__ import annotations

import asyncio
import json
import logging
from datetime import datetime, timezone
from pathlib import Path

from app.models import AppState, StreamInfo, StreamerState, TwitchUser


class FileStateRepository:
    def __init__(self, state_file: Path) -> None:
        self._state_file = state_file
        self._lock = asyncio.Lock()
        self._state = AppState()
        self._logger = logging.getLogger(self.__class__.__name__)

    async def load(self) -> None:
        async with self._lock:
            self._state_file.parent.mkdir(parents=True, exist_ok=True)
            if not self._state_file.exists():
                self._state = AppState()
                await self._save_locked()
                return

            try:
                raw_data = self._state_file.read_text(encoding="utf-8")
                payload = json.loads(raw_data)
                self._state = AppState.from_dict(payload)
            except (OSError, json.JSONDecodeError, ValueError) as exc:
                backup = self._state_file.with_name(
                    f"{self._state_file.stem}.corrupted-{datetime.now(timezone.utc).strftime('%Y%m%d%H%M%S')}{self._state_file.suffix}"
                )
                self._logger.error(
                    "State file is invalid (%s). Moving it to %s and recreating.",
                    exc,
                    backup,
                )
                try:
                    self._state_file.replace(backup)
                except OSError:
                    self._logger.exception("Failed to move corrupted state file")
                self._state = AppState()
                await self._save_locked()

    async def list_streamers(self) -> list[StreamerState]:
        async with self._lock:
            return [
                self._clone_streamer(streamer)
                for streamer in sorted(
                    self._state.streamers.values(),
                    key=lambda item: item.display_name.lower(),
                )
            ]

    async def get_streamer(self, login: str) -> StreamerState | None:
        async with self._lock:
            streamer = self._state.streamers.get(login.lower())
            return self._clone_streamer(streamer) if streamer else None

    async def upsert_streamer(self, user: TwitchUser) -> tuple[StreamerState, bool]:
        async with self._lock:
            login = user.login.lower()
            streamer = self._state.streamers.get(login)
            created = streamer is None

            if streamer is None:
                streamer = StreamerState(
                    login=login,
                    display_name=user.display_name,
                    user_id=user.user_id,
                )
                self._state.streamers[login] = streamer
            else:
                streamer.display_name = user.display_name
                streamer.user_id = user.user_id

            await self._save_locked()
            return self._clone_streamer(streamer), created

    async def add_missing_streamers(self, users: list[TwitchUser]) -> list[StreamerState]:
        async with self._lock:
            added: list[StreamerState] = []
            changed = False

            for user in users:
                login = user.login.lower()
                if login in self._state.streamers:
                    existing = self._state.streamers[login]
                    if existing.display_name != user.display_name:
                        existing.display_name = user.display_name
                        changed = True
                    if existing.user_id != user.user_id:
                        existing.user_id = user.user_id
                        changed = True
                    continue

                streamer = StreamerState(
                    login=login,
                    display_name=user.display_name,
                    user_id=user.user_id,
                )
                self._state.streamers[login] = streamer
                added.append(self._clone_streamer(streamer))
                changed = True

            if changed:
                await self._save_locked()

            return added

    async def remove_streamer(self, login: str) -> bool:
        async with self._lock:
            removed = self._state.streamers.pop(login.lower(), None)
            if removed is None:
                return False

            await self._save_locked()
            return True

    async def set_stream_live(
        self,
        login: str,
        stream: StreamInfo,
        *,
        notified: bool,
    ) -> StreamerState | None:
        async with self._lock:
            streamer = self._state.streamers.get(login.lower())
            if streamer is None:
                return None

            streamer.is_live = True
            streamer.current_stream_id = stream.stream_id
            streamer.last_known_title = stream.title
            streamer.last_known_game_name = stream.game_name
            streamer.last_stream_started_at = stream.started_at.isoformat()
            if notified:
                streamer.last_notified_stream_id = stream.stream_id
                streamer.last_notification_at = datetime.now(timezone.utc).isoformat()

            await self._save_locked()
            return self._clone_streamer(streamer)

    async def set_stream_offline(self, login: str) -> StreamerState | None:
        async with self._lock:
            streamer = self._state.streamers.get(login.lower())
            if streamer is None:
                return None

            streamer.is_live = False
            streamer.current_stream_id = None
            streamer.last_known_title = None
            streamer.last_known_game_name = None
            streamer.last_stream_started_at = None

            await self._save_locked()
            return self._clone_streamer(streamer)

    async def _save_locked(self) -> None:
        self._state_file.parent.mkdir(parents=True, exist_ok=True)
        payload = json.dumps(self._state.to_dict(), ensure_ascii=False, indent=2)
        temp_file = self._state_file.with_suffix(f"{self._state_file.suffix}.tmp")
        temp_file.write_text(payload, encoding="utf-8")
        temp_file.replace(self._state_file)

    @staticmethod
    def _clone_streamer(streamer: StreamerState | None) -> StreamerState | None:
        if streamer is None:
            return None
        return StreamerState.from_dict(streamer.to_dict())
