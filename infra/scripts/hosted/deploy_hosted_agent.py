#!/usr/bin/env python3
"""
Hosted Agent Deployment for the Microsoft IQ Solution Accelerator

Builds and deploys the hosted Foundry agent in ``src/hosted``, wiring it up
to the Foundry IQ knowledge base created by the main solution installer
(``install_microsoft_iq_solution.py``).

This is a standalone, optional deployment script — it is not part of the
``azd`` provisioning hooks. Run it after the main installer has created the
knowledge base and after an Azure Container Registry has been created for
the agent image.

Usage:
    python deploy_hosted_agent.py

Environment Variables:
    The following variables are automatically set by 'azd' from main.bicep outputs and
    must be present in the environment before running this script:

    AZURE_AI_SEARCH_ENDPOINT             (required) Azure AI Search service endpoint URL.
    AZURE_AI_AGENT_ENDPOINT              (required) Azure AI Project endpoint URL.
    AI_SERVICE_NAME                      (required) Azure AI Services account name.
    AZURE_AI_PROJECT_NAME                (required) Azure AI Project name.
    AZURE_SUBSCRIPTION_ID                (required) Azure subscription ID.
    AZURE_RESOURCE_GROUP                 (required) Azure resource group name.
    SOLUTION_SUFFIX                      (required) Suffix used for resource naming.

    The following variable is not a main.bicep output and must be set manually
    (create an Azure Container Registry and provide its name):

    AZURE_CONTAINER_REGISTRY_NAME        (required) Azure Container Registry name.

    The following variables are optional and may be overridden manually:

    AZURE_CHAT_MODEL                     (optional) Chat model deployment name for the
                                                    agent container. Defaults to gpt-5.6-sol.
    KB_MCP_CONNECTION_NAME               (optional) Project connection name for the
                                                    Knowledge Base MCP tool.
                                                    Defaults to <SOLUTION_SUFFIX>-kb-mcp-connection.
    HOSTED_AGENT_CPU                      (optional) CPU allocation. Defaults to "0.5".
    HOSTED_AGENT_MEMORY                   (optional) Memory allocation. Defaults to "1.0Gi".

    The container image tag is auto-incremented (v1, v2, v3, …) from the tags
    already present in the registry, so no tag variable is required.
"""

import logging
import os
import sys
from datetime import datetime

# Add infra/scripts/ to path so the foundry and hosted packages can be imported
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from common.logging_config import setup_logging

# Configure logging before any other imports so that library modules
# inherit the root logger's settings.
setup_logging()

logger = logging.getLogger(__name__)

from common.config import REPO_ROOT, SOLUTION_NAME
from common.env_utils import get_required_env_var
from hosted.step_hosted_agent_deploy import deploy_hosted_agent


def main() -> None:
    """Build and deploy the hosted Foundry agent."""

    search_endpoint = get_required_env_var("AZURE_AI_SEARCH_ENDPOINT")
    agent_endpoint = get_required_env_var("AZURE_AI_AGENT_ENDPOINT")
    ai_service_name = get_required_env_var("AI_SERVICE_NAME")
    ai_project_name = get_required_env_var("AZURE_AI_PROJECT_NAME")
    subscription_id = get_required_env_var("AZURE_SUBSCRIPTION_ID")
    resource_group = get_required_env_var("AZURE_RESOURCE_GROUP")
    solution_suffix = get_required_env_var("SOLUTION_SUFFIX")
    container_registry_name = get_required_env_var("AZURE_CONTAINER_REGISTRY_NAME")

    agent_model = os.getenv("AZURE_AI_AGENT_MODEL_DEPLOYMENT_NAME") or os.getenv("AZURE_CHAT_MODEL", "gpt-5.6-sol")
    knowledge_base_name = f"{solution_suffix}-kb"
    kb_mcp_connection_name = os.getenv(
        "KB_MCP_CONNECTION_NAME", f"{solution_suffix}-kb-mcp-connection"
    )
    cpu = os.getenv("HOSTED_AGENT_CPU", "0.5")
    memory = os.getenv("HOSTED_AGENT_MEMORY", "1.0Gi")
    source_dir = os.path.join(REPO_ROOT, "src", "hosted")

    logger.info(f"🚀 {SOLUTION_NAME} – Hosted Agent Deployment")
    logger.info("=" * 60)
    logger.info(f"Agent Endpoint:      {agent_endpoint}")
    logger.info(f"Agent Model:         {agent_model}")
    logger.info(f"Search Endpoint:     {search_endpoint}")
    logger.info(f"Knowledge Base:      {knowledge_base_name}")
    logger.info(f"KB MCP Connection:   {kb_mcp_connection_name}")
    logger.info(f"Container Registry:  {container_registry_name}")
    logger.info(f"Source Directory:    {source_dir}")
    logger.info(f"Start time:          {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    logger.info("=" * 60)

    deploy_hosted_agent(
        agent_endpoint=agent_endpoint,
        agent_model=agent_model,
        search_endpoint=search_endpoint,
        knowledge_base_name=knowledge_base_name,
        kb_mcp_connection_name=kb_mcp_connection_name,
        subscription_id=subscription_id,
        resource_group=resource_group,
        ai_service_name=ai_service_name,
        ai_project_name=ai_project_name,
        container_registry_name=container_registry_name,
        source_dir=source_dir,
        cpu=cpu,
        memory=memory,
    )

    logger.info("🎉 HOSTED AGENT DEPLOYMENT COMPLETE!")


if __name__ == "__main__":
    main()
