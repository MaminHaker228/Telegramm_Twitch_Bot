from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from typing import Any


@dataclass(slots=True, frozen=True)
class TwitchUser:
    user_id: str
    login: str
    display_name: str


@dataclass(slots=True, frozen=True)
class StreamInfo:
    stream_id: str
    user_id: str
    user_login: str
    user_name: str
    title: str
    game_name: str
    started_at: datetime
    viewer_count: int | None = None

    @property
    def url(self) -> str:
        return f"https://twitch.tv/{self.user_login}"


@dataclass(slots=True)
class StreamerState:
    login: str
    display_name: str
    user_id: str
    is_live: bool = False
    current_stream_id: str | None = None
    last_notified_stream_id: str | None = None
    last_known_title: str | None = None
    last_known_game_name: str | None = None
    last_stream_started_at: str | None = None
    last_notification_at: str | None = None

    def to_dict(self) -> dict[str, Any]:
        return {
            "login": self.login,
            "display_name": self.display_name,
            "user_id": self.user_id,
            "is_live": self.is_live,
            "current_stream_id": self.current_stream_id,
            "last_notified_stream_id": self.last_notified_stream_id,
            "last_known_title": self.last_known_title,
            "last_known_game_name": self.last_known_game_name,
            "last_stream_started_at": self.last_stream_started_at,
            "last_notification_at": self.last_notification_at,
        }

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "StreamerState":
        return cls(
            login=str(data["login"]).lower(),
            display_name=str(data.get("display_name") or data["login"]),
            user_id=str(data.get("user_id") or ""),
            is_live=bool(data.get("is_live", False)),
            current_stream_id=data.get("current_stream_id"),
            last_notified_stream_id=data.get("last_notified_stream_id"),
            last_known_title=data.get("last_known_title"),
            last_known_game_name=data.get("last_known_game_name"),
            last_stream_started_at=data.get("last_stream_started_at"),
            last_notification_at=data.get("last_notification_at"),
        )


@dataclass(slots=True)
class AppState:
    streamers: dict[str, StreamerState] = field(default_factory=dict)

    def to_dict(self) -> dict[str, Any]:
        return {
            "streamers": {
                login: streamer.to_dict()
                for login, streamer in sorted(self.streamers.items())
            }
        }

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "AppState":
        raw_streamers = data.get("streamers", {})
        if not isinstance(raw_streamers, dict):
            raise ValueError("Invalid state format: 'streamers' must be an object")

        return cls(
            streamers={
                str(login).lower(): StreamerState.from_dict(streamer_data)
                for login, streamer_data in raw_streamers.items()
            }
        )
