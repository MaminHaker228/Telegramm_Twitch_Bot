from __future__ import annotations

import os
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path

from dotenv import load_dotenv

BASE_DIR = Path(__file__).resolve().parent.parent
ENV_FILE = BASE_DIR / ".env"
load_dotenv(ENV_FILE)


@dataclass(slots=True, frozen=True)
class Settings:
    telegram_bot_token: str
    telegram_channel_id: str
    twitch_client_id: str
    twitch_client_secret: str
    twitch_usernames: tuple[str, ...]
    check_interval_seconds: int
    request_timeout_seconds: float
    max_retries: int
    state_file: Path
    log_file: Path
    log_level: str
    telegram_admin_ids: frozenset[int]


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    required = {
        "TELEGRAM_BOT_TOKEN": os.getenv("TELEGRAM_BOT_TOKEN", "").strip(),
        "TELEGRAM_CHANNEL_ID": os.getenv("TELEGRAM_CHANNEL_ID", "").strip(),
        "TWITCH_CLIENT_ID": os.getenv("TWITCH_CLIENT_ID", "").strip(),
        "TWITCH_CLIENT_SECRET": os.getenv("TWITCH_CLIENT_SECRET", "").strip(),
    }
    missing = [name for name, value in required.items() if not value]
    if missing:
        raise ValueError(
            "Missing required environment variables: " + ", ".join(sorted(missing))
        )

    usernames = _deduplicate_preserve(
        [
            *_parse_csv(os.getenv("TWITCH_USERNAME")),
            *_parse_csv(os.getenv("TWITCH_USERNAMES")),
        ]
    )

    check_interval_seconds = _parse_int(
        name="CHECK_INTERVAL_SECONDS",
        value=os.getenv("CHECK_INTERVAL_SECONDS", "90"),
        minimum=60,
        maximum=120,
    )
    request_timeout_seconds = _parse_float(
        name="REQUEST_TIMEOUT_SECONDS",
        value=os.getenv("REQUEST_TIMEOUT_SECONDS", "15"),
        minimum=5,
    )
    max_retries = _parse_int(
        name="MAX_RETRIES",
        value=os.getenv("MAX_RETRIES", "3"),
        minimum=1,
        maximum=10,
    )

    return Settings(
        telegram_bot_token=required["TELEGRAM_BOT_TOKEN"],
        telegram_channel_id=required["TELEGRAM_CHANNEL_ID"],
        twitch_client_id=required["TWITCH_CLIENT_ID"],
        twitch_client_secret=required["TWITCH_CLIENT_SECRET"],
        twitch_usernames=tuple(usernames),
        check_interval_seconds=check_interval_seconds,
        request_timeout_seconds=request_timeout_seconds,
        max_retries=max_retries,
        state_file=_resolve_path(os.getenv("STATE_FILE"), BASE_DIR / "data" / "state.json"),
        log_file=_resolve_path(os.getenv("LOG_FILE"), BASE_DIR / "logs" / "bot.log"),
        log_level=os.getenv("LOG_LEVEL", "INFO").strip().upper() or "INFO",
        telegram_admin_ids=frozenset(_parse_admin_ids(os.getenv("TELEGRAM_ADMIN_IDS"))),
    )


def _parse_csv(raw_value: str | None) -> list[str]:
    if not raw_value:
        return []

    values: list[str] = []
    for chunk in raw_value.replace("\n", ",").split(","):
        normalized = _normalize_twitch_login(chunk)
        if normalized:
            values.append(normalized)
    return values


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


def _deduplicate_preserve(values: list[str]) -> list[str]:
    seen: set[str] = set()
    result: list[str] = []
    for value in values:
        if value in seen:
            continue
        seen.add(value)
        result.append(value)
    return result


def _parse_int(
    *,
    name: str,
    value: str,
    minimum: int,
    maximum: int | None = None,
) -> int:
    try:
        parsed = int(value)
    except ValueError as exc:
        raise ValueError(f"{name} must be an integer") from exc

    if parsed < minimum:
        raise ValueError(f"{name} must be >= {minimum}")
    if maximum is not None and parsed > maximum:
        raise ValueError(f"{name} must be <= {maximum}")
    return parsed


def _parse_float(*, name: str, value: str, minimum: float) -> float:
    try:
        parsed = float(value)
    except ValueError as exc:
        raise ValueError(f"{name} must be a number") from exc

    if parsed < minimum:
        raise ValueError(f"{name} must be >= {minimum}")
    return parsed


def _parse_admin_ids(raw_value: str | None) -> list[int]:
    if not raw_value:
        return []

    ids: list[int] = []
    for chunk in raw_value.replace("\n", ",").split(","):
        cleaned = chunk.strip()
        if not cleaned:
            continue
        try:
            ids.append(int(cleaned))
        except ValueError as exc:
            raise ValueError("TELEGRAM_ADMIN_IDS must contain only integers") from exc
    return ids


def _resolve_path(raw_value: str | None, default: Path) -> Path:
    if not raw_value:
        return default
    candidate = Path(raw_value)
    if candidate.is_absolute():
        return candidate
    return BASE_DIR / candidate
