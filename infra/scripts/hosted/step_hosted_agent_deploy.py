#!/usr/bin/env python3
"""
Hosted agent deployment step for the ``src/hosted`` Foundry agent.

Extracts the deploy flow into a single top-level function callable from the
entry-point script, following the same pattern as
``foundry/step_agent_setup.py``.
"""

import logging

from foundry.agent_api import create_agent_client, create_kb_mcp_connection
from hosted.hosted_agent_api import (
    HOSTED_AGENT_NAME,
    build_and_push_image,
    create_or_update_hosted_agent,
)

# Module-level logger — inherits configuration from the root logger set up
# by setup_logging() in the entry-point scripts.
logger = logging.getLogger(__name__)


def deploy_hosted_agent(
    *,
    agent_endpoint: str,
    agent_model: str,
    search_endpoint: str,
    knowledge_base_name: str,
    kb_mcp_connection_name: str,
    subscription_id: str,
    resource_group: str,
    ai_service_name: str,
    ai_project_name: str,
    container_registry_name: str,
    image_tag: str,
    source_dir: str,
    cpu: str,
    memory: str,
) -> None:
    """Build, push, and deploy the hosted Foundry agent from ``src/hosted``.

    Args:
        agent_endpoint: Azure AI Project endpoint URL.
        agent_model: Chat model deployment name for the agent container.
        search_endpoint: Azure AI Search service endpoint URL.
        knowledge_base_name: KB name to use for the MCP connection.
        kb_mcp_connection_name: Project connection name for the KB MCP tool.
        subscription_id: Azure subscription ID.
        resource_group: Azure resource group name.
        ai_service_name: Azure AI Services account name.
        ai_project_name: Azure AI Project name.
        container_registry_name: Azure Container Registry name.
        image_tag: Tag to apply to the built container image.
        source_dir: Directory containing the agent source (``src/hosted``).
        cpu: CPU allocation for the hosted agent.
        memory: Memory allocation for the hosted agent.
    """
    logger.info("   Creating KB MCP project connection…")
    _mcp_ep = (
        f"{search_endpoint.rstrip('/')}/knowledgebases/{knowledge_base_name}"
        f"/mcp?api-version=2025-11-01-preview"
    )
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

    logger.info(f"   Building container image from '{source_dir}'…")
    _image = build_and_push_image(
        registry_name=container_registry_name,
        image_name=HOSTED_AGENT_NAME,
        image_tag=image_tag,
        source_dir=source_dir,
    )
    logger.info(f"      Image: {_image}")

    logger.info("   Initialising AI Project client (preview features enabled)…")
    _agent_client = create_agent_client(agent_endpoint, allow_preview=True)

    logger.info(f"   Deploying hosted agent '{HOSTED_AGENT_NAME}'…")
    with _agent_client:
        _agent = create_or_update_hosted_agent(
            project_client=_agent_client,
            agent_name=HOSTED_AGENT_NAME,
            image=_image,
            cpu=cpu,
            memory=memory,
            mcp_endpoint=_mcp_ep,
            connection_name=kb_mcp_connection_name,
            environment_variables={
                "FOUNDRY_MODEL": agent_model,
                "FOUNDRY_PROJECT_ENDPOINT": agent_endpoint,
            },
        )
    logger.info(f"   Hosted agent '{_agent.name}' ready (id: {_agent.id})")
