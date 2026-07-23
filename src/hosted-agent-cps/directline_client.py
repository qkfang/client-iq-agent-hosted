"""Minimal async Direct Line client for a Copilot Studio agent.

Retrieves an anonymous Direct Line token from the Copilot Studio token
endpoint (works for agents configured with "No Authentication"), starts a
conversation, sends user messages, and reads bot replies. This provides a
fully headless path that does not require an end-user token or the private
preview app-only S2S Direct-to-Engine capability.
"""

from __future__ import annotations

import asyncio
from collections.abc import AsyncIterable

import aiohttp

_DIRECT_LINE_BASE = "https://directline.botframework.com/v3/directline"
_USER_ID = "hosted-agent"


class DirectLineClient:
    """Talks to a Copilot Studio agent over the Direct Line channel."""

    def __init__(self, token_endpoint: str, *, poll_interval: float = 1.0, poll_timeout: float = 30.0) -> None:
        self._token_endpoint = token_endpoint
        self._poll_interval = poll_interval
        self._poll_timeout = poll_timeout

    async def _get_token(self, session: aiohttp.ClientSession) -> str:
        async with session.get(self._token_endpoint) as response:
            response.raise_for_status()
            payload = await response.json()
        return payload["token"]

    async def start_conversation(self) -> str:
        """Start a new Direct Line conversation and return its id."""
        async with aiohttp.ClientSession() as session:
            token = await self._get_token(session)
            headers = {"Authorization": f"Bearer {token}"}
            async with session.post(f"{_DIRECT_LINE_BASE}/conversations", headers=headers) as response:
                response.raise_for_status()
                payload = await response.json()
        self._token = token
        return payload["conversationId"]

    async def ask(self, conversation_id: str, question: str) -> AsyncIterable[str]:
        """Send a question and yield the text of each bot reply."""
        headers = {"Authorization": f"Bearer {self._token}"}
        activity = {"type": "message", "from": {"id": _USER_ID}, "text": question}

        async with aiohttp.ClientSession() as session:
            async with session.post(
                f"{_DIRECT_LINE_BASE}/conversations/{conversation_id}/activities",
                headers=headers,
                json=activity,
            ) as response:
                response.raise_for_status()

            watermark: str | None = None
            deadline = asyncio.get_event_loop().time() + self._poll_timeout
            while asyncio.get_event_loop().time() < deadline:
                await asyncio.sleep(self._poll_interval)
                url = f"{_DIRECT_LINE_BASE}/conversations/{conversation_id}/activities"
                if watermark:
                    url += f"?watermark={watermark}"
                async with session.get(url, headers=headers) as response:
                    response.raise_for_status()
                    payload = await response.json()

                watermark = payload.get("watermark")
                replies = [
                    a.get("text", "")
                    for a in payload.get("activities", [])
                    if a.get("type") == "message" and a.get("from", {}).get("role") == "bot" and a.get("text")
                ]
                if replies:
                    for text in replies:
                        yield text
                    return
