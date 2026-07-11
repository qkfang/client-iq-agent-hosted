#!/usr/bin/env python3
"""
OnboardingAgent setup step for the Microsoft IQ deployment.

Creates an AI Foundry agent focused on helping new users find onboarding
and reference documentation. It follows the same pattern as
``step_agent_setup.py`` (ChatAgent) and wires up the Foundry IQ Knowledge
Base MCP tool plus the hosted Work IQ MCP tool — Fabric IQ and Web IQ do
not yet expose a Python/MCP integration surface in this repository. Once
their MCP endpoints are confirmed, add the corresponding tool(s) to the
``tools`` list built in ``setup_onboarding_agent()`` below.
"""

import logging

from foundry.agent_api import (
    ONBOARDING_AGENT_NAME,
    build_kb_mcp_tool,
    build_onboarding_agent_instructions,
    build_workiq_mcp_tool,
    create_agent_client,
    create_kb_mcp_connection,
    create_or_update_agent,
)

# Module-level logger — inherits configuration from the root logger set up
# by setup_logging() in the entry-point scripts.
logger = logging.getLogger(__name__)


def setup_onboarding_agent(
    *,
    solution_name: str,
    agent_endpoint: str,
    agent_model: str,
    search_endpoint: str,
    knowledge_base_name: str,
    kb_mcp_connection_name: str,
    subscription_id: str,
    resource_group: str,
    ai_service_name: str | None,
    ai_project_name: str | None,
) -> None:
    """Create or update an AI Foundry OnboardingAgent wired up to the Knowledge Base MCP tool.

    Args:
        solution_name: Overall generic name for the project/solution.
        agent_endpoint: Azure AI Project endpoint URL.
        agent_model: Chat model deployment name for the agent.
        search_endpoint: Azure AI Search service endpoint URL.
        knowledge_base_name: KB name to use for the MCP connection.
        kb_mcp_connection_name: Project connection name for the KB MCP tool.
        subscription_id: Azure subscription ID.
        resource_group: Azure resource group name.
        ai_service_name: Azure AI Services account name (required for MCP connection).
        ai_project_name: Azure AI Project name (required for MCP connection).
    """
    _instructions = build_onboarding_agent_instructions(solution_name)
    logger.debug(f"      Built instructions ({len(_instructions)} chars)")

    logger.info("   Initialising AI Project client…")
    _agent_client = create_agent_client(agent_endpoint)

    logger.info("   Creating KB MCP project connection…")
    _mcp_ep = (
        f"{search_endpoint.rstrip('/')}/knowledgebases/{knowledge_base_name}"
        f"/mcp?api-version=2025-11-01-preview"
    )
    if ai_service_name and ai_project_name:
        _connected = create_kb_mcp_connection(
            search_endpoint=search_endpoint,
            kb_name=knowledge_base_name,
            connection_name=kb_mcp_connection_name,
            subscription_id=subscription_id,
            resource_group=resource_group,
            ai_service_name=ai_service_name,
            project_name=ai_project_name,
        )
        if not _connected:
            logger.warning(
                "   MCP connection creation may have failed — "
                "create it manually in the Foundry portal if needed"
            )
    else:
        logger.warning(
            "   Skipping MCP connection: AI_SERVICE_NAME and "
            "AZURE_AI_PROJECT_NAME must both be set"
        )

    # Tools attached to the agent. Fabric IQ / Web IQ tools should be
    # appended here once their MCP endpoints are confirmed.
    _tools = [build_kb_mcp_tool(_mcp_ep, kb_mcp_connection_name), build_workiq_mcp_tool()]

    logger.info(f"   Creating agent '{ONBOARDING_AGENT_NAME}'…")
    with _agent_client:
        _agent = create_or_update_agent(
            project_client=_agent_client,
            agent_name=ONBOARDING_AGENT_NAME,
            model=agent_model,
            instructions=_instructions,
            tools=_tools,
        )
    logger.info(f"   Agent '{_agent.name}' ready (id: {_agent.id})")
