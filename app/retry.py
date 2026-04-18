from __future__ import annotations

import asyncio
import logging
import random
from collections.abc import Awaitable, Callable
from typing import TypeVar

T = TypeVar("T")


class RetryableError(Exception):
    """Raised when an operation should be retried."""


async def run_with_retries(
    operation: Callable[[], Awaitable[T]],
    *,
    attempts: int,
    logger: logging.Logger,
    operation_name: str,
    retry_exceptions: tuple[type[Exception], ...],
    base_delay: float = 1.0,
    max_delay: float = 8.0,
) -> T:
    last_error: Exception | None = None

    for attempt in range(1, attempts + 1):
        try:
            return await operation()
        except retry_exceptions as exc:
            last_error = exc
            if attempt >= attempts:
                break

            delay = min(max_delay, base_delay * (2 ** (attempt - 1)))
            delay += random.uniform(0, 0.25)
            logger.warning(
                "%s failed on attempt %s/%s: %s. Retrying in %.2f seconds.",
                operation_name,
                attempt,
                attempts,
                exc,
                delay,
            )
            await asyncio.sleep(delay)

    if last_error is None:
        raise RuntimeError(f"{operation_name} failed without an exception")
    raise last_error
