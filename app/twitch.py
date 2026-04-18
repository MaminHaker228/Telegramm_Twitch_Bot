from __future__ import annotations

import asyncio
import logging
from datetime import datetime, timedelta, timezone
from typing import Any

import httpx

from app.config import Settings
from app.models import StreamInfo, TwitchUser
from app.retry import RetryableError, run_with_retries


class TwitchApiError(Exception):
    """Raised when Twitch API returns a non-retryable error."""


class TwitchClient:
    TOKEN_URL = "https://id.twitch.tv/oauth2/token"
    HELIX_BASE_URL = "https://api.twitch.tv/helix"

    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._logger = logging.getLogger(self.__class__.__name__)
        self._client = httpx.AsyncClient(timeout=settings.request_timeout_seconds)
        self._access_token: str | None = None
        self._token_expires_at = datetime.now(timezone.utc)
        self._token_lock = asyncio.Lock()

    async def close(self) -> None:
        await self._client.aclose()

    async def get_users_by_logins(self, logins: list[str] | tuple[str, ...]) -> dict[str, TwitchUser]:
        normalized = [login.lower() for login in logins if login]
        result: dict[str, TwitchUser] = {}

        for chunk in _chunked(normalized, size=100):
            params = [("login", login) for login in chunk]
            payload = await self._helix_request("GET", "/users", params=params)
            for item in payload.get("data", []):
                user = TwitchUser(
                    user_id=str(item["id"]),
                    login=str(item["login"]).lower(),
                    display_name=str(item["display_name"]),
                )
                result[user.login] = user

        return result

    async def get_streams_by_logins(
        self,
        logins: list[str] | tuple[str, ...],
    ) -> dict[str, StreamInfo]:
        normalized = [login.lower() for login in logins if login]
        result: dict[str, StreamInfo] = {}

        for chunk in _chunked(normalized, size=100):
            params = [("user_login", login) for login in chunk]
            payload = await self._helix_request("GET", "/streams", params=params)
            for item in payload.get("data", []):
                login = str(item["user_login"]).lower()
                result[login] = StreamInfo(
                    stream_id=str(item["id"]),
                    user_id=str(item["user_id"]),
                    user_login=login,
                    user_name=str(item["user_name"]),
                    title=str(item.get("title") or "Без названия"),
                    game_name=str(item.get("game_name") or "Категория не указана"),
                    started_at=_parse_twitch_datetime(item["started_at"]),
                    viewer_count=int(item["viewer_count"])
                    if item.get("viewer_count") is not None
                    else None,
                )

        return result

    async def _helix_request(
        self,
        method: str,
        path: str,
        *,
        params: list[tuple[str, str]] | None = None,
    ) -> dict[str, Any]:
        await self._ensure_access_token()

        async def operation() -> dict[str, Any]:
            response = await self._client.request(
                method,
                f"{self.HELIX_BASE_URL}{path}",
                params=params,
                headers={
                    "Client-Id": self._settings.twitch_client_id,
                    "Authorization": f"Bearer {self._access_token}",
                },
            )
            return await self._handle_helix_response(
                response=response,
                method=method,
                path=path,
                params=params,
            )

        return await run_with_retries(
            operation,
            attempts=self._settings.max_retries,
            logger=self._logger,
            operation_name=f"Twitch request {method} {path}",
            retry_exceptions=(httpx.HTTPError, RetryableError),
        )

    async def _handle_helix_response(
        self,
        *,
        response: httpx.Response,
        method: str,
        path: str,
        params: list[tuple[str, str]] | None,
    ) -> dict[str, Any]:
        if response.status_code == 401:
            self._logger.warning(
                "Received 401 from Twitch Helix. Refreshing token and retrying once."
            )
            await self._ensure_access_token(force_refresh=True)
            retry_response = await self._client.request(
                method,
                f"{self.HELIX_BASE_URL}{path}",
                params=params,
                headers={
                    "Client-Id": self._settings.twitch_client_id,
                    "Authorization": f"Bearer {self._access_token}",
                },
            )
            response = retry_response

        if response.status_code in {429, 500, 502, 503, 504}:
            raise RetryableError(
                f"Twitch temporary error {response.status_code}: {response.text}"
            )

        if response.status_code >= 400:
            raise TwitchApiError(
                f"Twitch API error {response.status_code}: {response.text}"
            )

        return response.json()

    async def _ensure_access_token(self, *, force_refresh: bool = False) -> None:
        should_refresh = force_refresh or self._access_token is None
        if not should_refresh and datetime.now(timezone.utc) < self._token_expires_at:
            return

        async with self._token_lock:
            should_refresh = force_refresh or self._access_token is None
            if not should_refresh and datetime.now(timezone.utc) < self._token_expires_at:
                return
            await self._fetch_access_token()

    async def _fetch_access_token(self) -> None:
        async def operation() -> None:
            response = await self._client.post(
                self.TOKEN_URL,
                data={
                    "client_id": self._settings.twitch_client_id,
                    "client_secret": self._settings.twitch_client_secret,
                    "grant_type": "client_credentials",
                },
                headers={"Content-Type": "application/x-www-form-urlencoded"},
            )

            if response.status_code in {429, 500, 502, 503, 504}:
                raise RetryableError(
                    f"Twitch token endpoint temporary error {response.status_code}: {response.text}"
                )
            if response.status_code >= 400:
                raise TwitchApiError(
                    f"Unable to fetch Twitch access token: {response.status_code} {response.text}"
                )

            payload = response.json()
            expires_in = int(payload["expires_in"])
            self._access_token = str(payload["access_token"])
            self._token_expires_at = datetime.now(timezone.utc) + timedelta(
                seconds=max(expires_in - 120, 60)
            )
            self._logger.info("Successfully refreshed Twitch app access token")

        await run_with_retries(
            operation,
            attempts=self._settings.max_retries,
            logger=self._logger,
            operation_name="Twitch token refresh",
            retry_exceptions=(httpx.HTTPError, RetryableError),
        )


def _chunked(items: list[str], *, size: int) -> list[list[str]]:
    return [items[index : index + size] for index in range(0, len(items), size)]


def _parse_twitch_datetime(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))
