# Copyright (c) Microsoft. All rights reserved.

"""Hosted agent entry point — Copilot Studio agent proxy (hosted).

Talks to an existing Copilot Studio agent over the Direct Line channel, which
works headlessly against agents configured with "No Authentication".

Hosted via ResponsesHostServer from agent-framework-foundry-hosting, which
provides the readiness endpoint required by the Foundry platform.

Required environment variables:
    ENVIRONMENT_ID:            Power Platform environment ID hosting the Copilot Studio agent
    AGENT_IDENTIFIER:          Copilot Studio agent schema name / identifier
    DIRECTLINE_TOKEN_ENDPOINT: (optional) explicit Direct Line token endpoint;
                               derived from the two values above when omitted
"""

from pathlib import Path

try:
    from dotenv import load_dotenv

    load_dotenv(dotenv_path=Path(__file__).with_name(".env"))
except ImportError:
    pass  # dotenv not needed in hosted deployment

from agent_framework_foundry_hosting import ResponsesHostServer
from azure.ai.agentserver.responses import InMemoryResponseProvider
from agent import agent


def main():
    # The Copilot Studio agent keeps its own conversation state, so the Foundry
    # remote history store is not needed. Using an in-memory store avoids a 404
    # on GET /storage/history/item_ids when the client replays a conversation_id
    # that was never persisted server-side.
    server = ResponsesHostServer(agent, store=InMemoryResponseProvider())
    server.run()


if __name__ == "__main__":
    main()
