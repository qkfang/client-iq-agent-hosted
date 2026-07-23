"""Hosted Foundry agent that proxies a Microsoft Copilot Studio agent.

This agent is designed for **hosted deployment** to Microsoft Foundry. It talks
to an existing Copilot Studio agent over the Direct Line channel, which works
headlessly against agents configured with "No Authentication" and does not
require an end-user token or the private-preview app-only S2S Direct-to-Engine
capability. It is exposed through the same Foundry hosting protocol as the other
agents in this repository.
"""

import os
from collections.abc import AsyncIterable, Sequence
from pathlib import Path

try:
    from dotenv import load_dotenv

    load_dotenv(dotenv_path=Path(__file__).with_name(".env"))
except ImportError:
    pass  # dotenv not needed in hosted deployment

from agent_framework import (
    AgentResponse,
    AgentResponseUpdate,
    AgentSession,
    BaseAgent,
    Content,
    Message,
    ResponseStream,
    normalize_messages,
)
from agent_framework.exceptions import AgentException
from microsoft_agents.copilotstudio.client import ConnectionSettings, PowerPlatformEnvironment

from directline_client import DirectLineClient

_TOKEN_ENDPOINT_TEMPLATE = (
    "{host}/powervirtualagents/botsbyschema/{schema}/directline/token?api-version=2022-03-01-preview"
)


def _build_token_endpoint(environment_id: str, agent_identifier: str) -> str:
    """Derive the Copilot Studio Direct Line token endpoint for the environment."""
    settings = ConnectionSettings(
        environment_id=environment_id,
        agent_identifier=agent_identifier,
        cloud=None,
        copilot_agent_type=None,
        custom_power_platform_cloud=None,
    )
    host = PowerPlatformEnvironment.get_copilot_studio_connection_url(settings).split("/copilotstudio/")[0]
    return _TOKEN_ENDPOINT_TEMPLATE.format(host=host, schema=agent_identifier)


class DirectLineAgent(BaseAgent):
    """A hosted agent that invokes a Copilot Studio agent over Direct Line."""

    def __init__(self, client: DirectLineClient, *, name: str, description: str) -> None:
        super().__init__(name=name, description=description)
        self.client = client

    async def _ensure_conversation(self, session: AgentSession) -> str:
        conversation_id = session.service_session_id
        if conversation_id is None:
            conversation_id = await self.client.start_conversation()
            session.service_session_id = conversation_id
        if not isinstance(conversation_id, str):
            raise AgentException("DirectLineAgent requires service_session_id to be a string")
        return conversation_id

    def run(self, messages=None, *, stream=False, session=None, **_ignored):
        if stream:
            return self._run_stream_impl(messages, session)
        return self._run_impl(messages, session)

    async def _run_impl(self, messages, session: AgentSession | None) -> AgentResponse:
        session = session or self.create_session()
        conversation_id = await self._ensure_conversation(session)
        question = "\n".join(message.text for message in normalize_messages(messages))

        response_messages = [
            Message(role="assistant", contents=[Content.from_text(text)])
            async for text in self.client.ask(conversation_id, question)
        ]
        response_id = response_messages[0].message_id if response_messages else None
        return AgentResponse(messages=response_messages, response_id=response_id)

    def _run_stream_impl(self, messages, session: AgentSession | None) -> ResponseStream:
        async def _stream() -> AsyncIterable[AgentResponseUpdate]:
            nonlocal session
            session = session or self.create_session()
            conversation_id = await self._ensure_conversation(session)
            question = "\n".join(message.text for message in normalize_messages(messages))
            async for text in self.client.ask(conversation_id, question):
                yield AgentResponseUpdate(role="assistant", contents=[Content.from_text(text)])

        def _finalize(updates: Sequence[AgentResponseUpdate]) -> AgentResponse:
            return AgentResponse.from_updates(updates)

        return ResponseStream(_stream(), finalizer=_finalize)


def _build_agent() -> DirectLineAgent:
    environment_id = os.environ.get("ENVIRONMENT_ID")
    # All AGENT_* env vars are reserved by the Foundry hosting platform, so the
    # hosted deployment injects the identifier as COPILOT_AGENT_IDENTIFIER.
    # Fall back to AGENT_IDENTIFIER for local runs that use .env.
    agent_identifier = os.environ.get("COPILOT_AGENT_IDENTIFIER") or os.environ.get(
        "AGENT_IDENTIFIER"
    )

    if not environment_id or not agent_identifier:
        raise EnvironmentError(
            "ENVIRONMENT_ID and AGENT_IDENTIFIER environment variables are not set. "
            "Copy .env.template to .env and fill in your Copilot Studio agent details."
        )

    token_endpoint = os.environ.get("DIRECTLINE_TOKEN_ENDPOINT") or _build_token_endpoint(
        environment_id, agent_identifier
    )
    client = DirectLineClient(token_endpoint)

    return DirectLineAgent(
        client,
        name="AgentCps",
        description="A hosted agent that invokes a Copilot Studio agent over Direct Line.",
    )


# Module-level export used by main.py.
agent = _build_agent()
