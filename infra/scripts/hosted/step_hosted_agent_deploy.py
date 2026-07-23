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
    HOSTED_CPS_AGENT_NAME,
    HOSTED_AGENT_NAME,
    HOSTED_ONBOARDING_AGENT_NAME,
    ONBOARDING_CRM_SERVER_LABEL,
    build_and_push_image,
    create_or_update_hosted_agent,
    get_next_image_tag,
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
        source_dir: Directory containing the agent source (``src/hosted``).
        cpu: CPU allocation for the hosted agent.
        memory: Memory allocation for the hosted agent.
    """
    logger.info("   Creating KB MCP project connection...")
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

    logger.info(f"   Building container image from '{source_dir}'...")
    _image_tag = get_next_image_tag(container_registry_name, HOSTED_AGENT_NAME)
    _image = build_and_push_image(
        registry_name=container_registry_name,
        image_name=HOSTED_AGENT_NAME,
        image_tag=_image_tag,
        source_dir=source_dir,
    )
    logger.info(f"      Image: {_image}")

    logger.info("   Initialising AI Project client (preview features enabled)...")
    _agent_client = create_agent_client(agent_endpoint, allow_preview=True)

    logger.info(f"   Deploying hosted agent '{HOSTED_AGENT_NAME}'...")
    with _agent_client:
        _agent = create_or_update_hosted_agent(
            project_client=_agent_client,
            agent_name=HOSTED_AGENT_NAME,
            image=_image,
            cpu=cpu,
            memory=memory,
            mcp_endpoint=_mcp_ep,
            connection_name=kb_mcp_connection_name,
            # Only non-reserved env vars may be set. The platform injects
            # FOUNDRY_PROJECT_ENDPOINT (and all FOUNDRY_*/AGENT_* vars) itself,
            # so the model deployment name is passed via a non-reserved var.
            environment_variables={
                "AZURE_AI_MODEL_DEPLOYMENT_NAME": agent_model,
            },
        )
    logger.info(f"   Hosted agent '{_agent.name}' ready (id: {_agent.id})")


def deploy_cps_hosted_agent(
    *,
    agent_endpoint: str,
    container_registry_name: str,
    source_dir: str,
    cpu: str,
    memory: str,
    environment_id: str,
    agent_identifier: str,
    tenant_id: str,
    client_id: str,
    client_secret: str,
) -> None:
    """Build, push, and deploy the Copilot Studio proxy agent from ``src/hosted-agent-cps``.

    Unlike the knowledge-base agent, this agent proxies an existing Copilot
    Studio agent and therefore does not use the Knowledge Base MCP tool. It is
    authenticated to Copilot Studio with an Entra app registration, so the
    service-principal credentials are passed as container environment variables.

    Args:
        agent_endpoint: Azure AI Project endpoint URL.
        container_registry_name: Azure Container Registry name.
        source_dir: Directory containing the agent source (``src/hosted-agent-cps``).
        cpu: CPU allocation for the hosted agent.
        memory: Memory allocation for the hosted agent.
        environment_id: Power Platform environment ID hosting the Copilot Studio agent.
        agent_identifier: Copilot Studio agent schema name / identifier.
        tenant_id: Microsoft Entra tenant ID of the app registration.
        client_id: App registration client ID.
        client_secret: App registration client secret.
    """
    logger.info(f"   Building container image from '{source_dir}'...")
    _image_tag = get_next_image_tag(container_registry_name, HOSTED_CPS_AGENT_NAME)
    _image = build_and_push_image(
        registry_name=container_registry_name,
        image_name=HOSTED_CPS_AGENT_NAME,
        image_tag=_image_tag,
        source_dir=source_dir,
    )
    logger.info(f"      Image: {_image}")

    logger.info("   Initialising AI Project client (preview features enabled)...")
    _agent_client = create_agent_client(agent_endpoint, allow_preview=True)

    logger.info(f"   Deploying hosted agent '{HOSTED_CPS_AGENT_NAME}'...")
    with _agent_client:
        _agent = create_or_update_hosted_agent(
            project_client=_agent_client,
            agent_name=HOSTED_CPS_AGENT_NAME,
            image=_image,
            cpu=cpu,
            memory=memory,
            # All FOUNDRY_* and AGENT_* env vars are reserved by the platform,
            # so the Copilot Studio agent identifier is passed under a
            # non-reserved name that the agent reads at startup.
            environment_variables={
                "ENVIRONMENT_ID": environment_id,
                "COPILOT_AGENT_IDENTIFIER": agent_identifier,
                "AZURE_TENANT_ID": tenant_id,
                "AZURE_CLIENT_ID": client_id,
                "AZURE_CLIENT_SECRET": client_secret,
            },
        )
    logger.info(f"   Hosted agent '{_agent.name}' ready (id: {_agent.id})")


def deploy_onboarding_hosted_agent(
    *,
    agent_endpoint: str,
    agent_model: str,
    search_endpoint: str,
    knowledge_base_names: list[str],
    kb_mcp_connection_name: str,
    subscription_id: str,
    resource_group: str,
    ai_service_name: str,
    ai_project_name: str,
    container_registry_name: str,
    source_dir: str,
    cpu: str,
    memory: str,
    webapp_mcp_url: str | None = None,
) -> None:
    """Build, push, and deploy the onboarding Foundry agent from ``src/hosted-agent-onboarding``.

    Unlike the knowledge-base chat agent, this agent runs the full customer
    onboarding workflow, so it is wired up with the same tool set as the .NET
    OnboardingAgent: one Knowledge Base MCP tool per IQ knowledge base (Foundry
    IQ, Fabric IQ, Web IQ), Work IQ, web (Bing) search and — when the web app's
    MCP endpoint is known — the finalize_customer_onboarding MCP tool used to
    create the CRM record.

    Args:
        agent_endpoint: Azure AI Project endpoint URL.
        agent_model: Chat model deployment name for the agent container.
        search_endpoint: Azure AI Search service endpoint URL.
        knowledge_base_names: KB names to wire as Knowledge Base MCP tools
            (e.g. Foundry IQ, Fabric IQ and Web IQ knowledge bases).
        kb_mcp_connection_name: Project connection name for the first KB. Extra
            KBs derive their own connection name as ``<kb-name>-mcp-connection``.
        subscription_id: Azure subscription ID.
        resource_group: Azure resource group name.
        ai_service_name: Azure AI Services account name.
        ai_project_name: Azure AI Project name.
        container_registry_name: Azure Container Registry name.
        source_dir: Directory containing the agent source (``src/hosted-agent-onboarding``).
        cpu: CPU allocation for the hosted agent.
        memory: Memory allocation for the hosted agent.
        webapp_mcp_url: Web app MCP endpoint hosting finalize_customer_onboarding.
            Omit to deploy without the finalize tool.
    """
    from azure.ai.projects.models import MCPTool, WebSearchTool

    from foundry.agent_api import build_workiq_mcp_tool

    # One Knowledge Base MCP tool per IQ knowledge base, each with its own
    # project connection and a distinct server label (matches the .NET config).
    _kb_tools = []
    for _index, _kb_name in enumerate(knowledge_base_names):
        _connection_name = (
            kb_mcp_connection_name if _index == 0 else f"{_kb_name}-mcp-connection"
        )
        logger.info(f"   Creating KB MCP project connection for '{_kb_name}'...")
        _mcp_ep = (
            f"{search_endpoint.rstrip('/')}/knowledgebases/{_kb_name}"
            f"/mcp?api-version=2025-11-01-preview"
        )
        _connected = create_kb_mcp_connection(
            search_endpoint=search_endpoint,
            kb_name=_kb_name,
            connection_name=_connection_name,
            subscription_id=subscription_id,
            resource_group=resource_group,
            ai_service_name=ai_service_name,
            project_name=ai_project_name,
        )
        if not _connected:
            logger.warning(
                f"   MCP connection for '{_kb_name}' may have failed — "
                "create it manually in the Foundry portal if needed"
            )
        _kb_tools.append(
            MCPTool(
                server_label=f"kb-{_kb_name}",
                server_url=_mcp_ep,
                require_approval="never",
                allowed_tools=["knowledge_base_retrieve"],
                project_connection_id=_connection_name,
            )
        )

    # Match the .NET OnboardingAgent tool set: the IQ knowledge bases, Work IQ,
    # web (Bing) search and — when the web app is deployed — the finalize tool.
    _tools = [*_kb_tools, build_workiq_mcp_tool(), WebSearchTool()]
    if webapp_mcp_url:
        _tools.append(
            MCPTool(
                server_label=ONBOARDING_CRM_SERVER_LABEL,
                server_url=webapp_mcp_url,
                require_approval="never",
                allowed_tools=["finalize_customer_onboarding"],
            )
        )

    logger.info(f"   Building container image from '{source_dir}'...")
    _image_tag = get_next_image_tag(container_registry_name, HOSTED_ONBOARDING_AGENT_NAME)
    _image = build_and_push_image(
        registry_name=container_registry_name,
        image_name=HOSTED_ONBOARDING_AGENT_NAME,
        image_tag=_image_tag,
        source_dir=source_dir,
    )
    logger.info(f"      Image: {_image}")

    logger.info("   Initialising AI Project client (preview features enabled)...")
    _agent_client = create_agent_client(agent_endpoint, allow_preview=True)

    logger.info(f"   Deploying hosted agent '{HOSTED_ONBOARDING_AGENT_NAME}'...")
    with _agent_client:
        _agent = create_or_update_hosted_agent(
            project_client=_agent_client,
            agent_name=HOSTED_ONBOARDING_AGENT_NAME,
            image=_image,
            cpu=cpu,
            memory=memory,
            extra_tools=_tools,
            environment_variables={
                "AZURE_AI_MODEL_DEPLOYMENT_NAME": agent_model,
            },
        )
    logger.info(f"   Hosted agent '{_agent.name}' ready (id: {_agent.id})")
