"""Hosted Foundry agent that proxies a Microsoft Copilot Studio agent.

This agent is designed for **hosted deployment** to Microsoft Foundry. It uses
the Microsoft Agent Framework Copilot Studio integration (Copilot SDK) to
invoke an existing Copilot Studio agent via the Direct-to-Engine API, and
exposes it through the same Foundry hosting protocol as the other agents in
this repository.
"""

import os
from pathlib import Path

try:
    from dotenv import load_dotenv

    load_dotenv(dotenv_path=Path(__file__).with_name(".env"))
except ImportError:
    pass  # dotenv not needed in hosted deployment

import msal
from agent_framework_copilotstudio import CopilotStudioAgent
from microsoft_agents.copilotstudio.client import (
    ConnectionSettings,
    CopilotClient,
    PowerPlatformEnvironment,
)


def _acquire_token(settings: ConnectionSettings) -> str:
    """Acquire an app-only access token for the Copilot Studio Direct-to-Engine API."""
    tenant_id = os.environ.get("AZURE_TENANT_ID")
    client_id = os.environ.get("AZURE_CLIENT_ID")
    client_secret = os.environ.get("AZURE_CLIENT_SECRET")

    if not all([tenant_id, client_id, client_secret]):
        raise EnvironmentError(
            "AZURE_TENANT_ID, AZURE_CLIENT_ID and AZURE_CLIENT_SECRET environment "
            "variables are required. Copy .env.template to .env and fill them in."
        )

    app = msal.ConfidentialClientApplication(
        client_id,
        authority=f"https://login.microsoftonline.com/{tenant_id}",
        client_credential=client_secret,
    )
    scope = PowerPlatformEnvironment.get_token_audience(settings)
    result = app.acquire_token_for_client(scopes=[scope])

    if "access_token" not in result:
        raise EnvironmentError(
            f"Failed to acquire Copilot Studio token: {result.get('error_description', result.get('error'))}"
        )

    return result["access_token"]


class _HostedCopilotStudioAgent(CopilotStudioAgent):
    """CopilotStudioAgent that tolerates extra runtime kwargs from the host.

    The Foundry hosting layer always forwards an ``options`` keyword to
    ``run``, which the base ``CopilotStudioAgent.run`` does not accept. Drop
    any unsupported keyword arguments before delegating to the base agent.
    """

    def run(self, messages=None, *, stream=False, session=None, **_ignored):
        return super().run(messages, stream=stream, session=session)


def _build_agent() -> CopilotStudioAgent:
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

    settings = ConnectionSettings(
        environment_id=environment_id,
        agent_identifier=agent_identifier,
        cloud=None,
        copilot_agent_type=None,
        custom_power_platform_cloud=None,
    )
    token = _acquire_token(settings)
    client = CopilotClient(settings=settings, token=token)

    return _HostedCopilotStudioAgent(
        client=client,
        name="AgentCps",
        description="A hosted agent that invokes a Copilot Studio agent via the Copilot SDK.",
    )


# Module-level export used by main.py.
agent = _build_agent()
