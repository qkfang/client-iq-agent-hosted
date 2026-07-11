#!/usr/bin/env python3
"""
Microsoft IQ Solution Installer

This script provides the deployment entry-point for the Microsoft IQ solution.
It performs the following steps to bootstrap the solution:

    1. setup_knowledge_base   - Create Azure AI Search indexes, upload documents,
                                create Foundry IQ Knowledge Sources and Knowledge Bases
                                (supply chain + customer onboarding document sets)
    2. setup_agent            - Create AI Foundry ChatAgents with Knowledge Base MCP tool
                                (supply chain + customer onboarding scenarios)
    3. setup_onboarding_agent - Create AI Foundry OnboardingAgent with Knowledge Base MCP tool
    4. setup_pipeline_agents  - Create the onboarding-form pipeline agents (IntakeAgent,
                                OrchestratorAgent, OpportunityAgent, InsightAgent, CrmAgent,
                                LegoAgent) used by the onboarding Function App
    5. setup_workspace        - Create and configure the Fabric workspace/capacity
    6. setup_administrators   - Add workspace administrators
    7. upload_installer       - Upload the installer notebook to the workspace
    8. run_installer          - Execute the installer notebook end-to-end
    9. deploy_hosted_agent    - Build and deploy the hosted Foundry agent
                                (optional; runs only when
                                AZURE_CONTAINER_REGISTRY_NAME is set)

The installer notebook (fabric_solution_installer.ipynb) handles the remaining
solution-specific steps (lakehouse creation, data ingestion, notebook deployment,
post-deployment tasks, …) once it has been uploaded and started.

Usage:
    python install_microsoft_iq_solution.py

Environment Variables:
    The following variables are automatically set by 'azd' from main.bicep outputs and
    must be present in the environment before running this script:

    AZURE_FABRIC_CAPACITY_NAME           (required) Name of the Fabric capacity resource.
    SOLUTION_SUFFIX                      (required) Suffix used for resource naming.
    AZURE_SUBSCRIPTION_ID                (required) Azure subscription ID.
    AZURE_RESOURCE_GROUP                 (required) Azure resource group name.
    AZURE_FABRIC_CAPACITY_ADMINISTRATORS (required) JSON array of capacity administrator
                                                    identities.
    AZURE_AI_SEARCH_ENDPOINT             (required) Azure AI Search service endpoint URL.
    AZURE_STORAGE_BLOB_ENDPOINT          (required) Azure Blob Storage service endpoint URL.
    AZURE_AI_AGENT_ENDPOINT              (required) Azure AI Project endpoint URL. Also
                                                    used to derive AZURE_AI_ENDPOINT when
                                                    AZURE_AI_ENDPOINT is not provided.
    AI_SERVICE_NAME                      (required) Azure AI Services account name.
    AZURE_AI_PROJECT_NAME                (required) Azure AI Project name.

    The following variables are optional and may be overridden manually:

    FABRIC_WORKSPACE_NAME                (optional) Override the default workspace name
                                                    (defaults to "<SOLUTION_NAME> - <SOLUTION_SUFFIX>").
    FABRIC_WORKSPACE_ADMINISTRATORS      (optional) Comma-separated list of additional
                                                    workspace administrator identities.
    GITHUB_TOKEN                         (optional) GitHub personal access token. When set,
                                                    the installer notebook is patched to
                                                    include the token for private repo access.
    AZURE_AI_ENDPOINT                    (optional) Azure AI Services / OpenAI endpoint URL.
                                                    Falls back to AZURE_AI_AGENT_ENDPOINT
                                                    with the trailing /api/projects/... segment
                                                    stripped.
    AZURE_OPENAI_EMBEDDING_MODEL         (optional) Embedding model deployment name.
                                                    Defaults to text-embedding-3-small.
    AZURE_CHAT_MODEL                     (optional) Chat model deployment name.
                                                    Defaults to gpt-5-mini.
    AZURE_AI_SEARCH_INDEX                (optional) Search index name.
                                                    Defaults to <SOLUTION_SUFFIX>-documents.
    AZURE_AI_SEARCH_ONBOARDING_INDEX     (optional) Customer onboarding search index name.
                                                    Defaults to <SOLUTION_SUFFIX>-onboarding-documents.
    AZURE_AI_AGENT_MODEL_DEPLOYMENT_NAME (optional) Chat model deployment name for the agent.
                                                    Falls back to AZURE_CHAT_MODEL, then gpt-5-mini.
    KB_MCP_CONNECTION_NAME               (optional) Project connection name for the Knowledge Base MCP tool.
                                                    Defaults to <SOLUTION_SUFFIX>-kb-mcp-connection.
    KB_ONBOARDING_MCP_CONNECTION_NAME    (optional) Project connection name for the onboarding Knowledge Base MCP tool.
                                                    Defaults to <SOLUTION_SUFFIX>-onboarding-kb-mcp-connection.
    AZURE_CONTAINER_REGISTRY_NAME        (optional) Azure Container Registry name. When set, the
                                                    hosted Foundry agent is built and deployed as a
                                                    final step. When unset, that step is skipped.
    HOSTED_AGENT_IMAGE_TAG               (optional) Tag applied to the hosted agent image.
                                                    Defaults to "latest".
    HOSTED_AGENT_CPU                     (optional) Hosted agent CPU allocation. Defaults to "0.5".
    HOSTED_AGENT_MEMORY                  (optional) Hosted agent memory allocation. Defaults to "1.0Gi".
"""

import logging
import os
import sys
from datetime import datetime
from typing import NoReturn

# Add infra/scripts/ to path so the fabric package and its modules can be imported
sys.path.append(os.path.dirname(__file__))

from common.logging_config import setup_logging

# Configure logging before any other imports so that library modules
# (fabric_api, graph_api, helpers.*) inherit the root logger's settings.
# The log level can be set via ``azd env set LOG_LEVEL DEBUG``.
setup_logging()

# Module-level logger for this entry-point script.  All log calls below
# use this logger; the level and handler are inherited from setup_logging().
logger = logging.getLogger(__name__)

from common.config import REPO_ROOT, SOLUTION_NAME, default_workspace_name
from common.env_utils import (
    get_required_env_var,
    parse_workspace_administrators,
    set_azd_env_var,
)
from common.step_printer import print_step, print_steps_summary
from fabric.fabric_api import create_fabric_client, create_workspace_fabric_client
from fabric.graph_api import create_graph_client
from fabric.step_notebook_installer import (
    INSTALLER_NOTEBOOK_NAME,
    get_notebook_path,
    run_installer_notebook,
    upload_installer_notebook,
)
from fabric.step_workspace_setup import setup_workspace
from fabric.step_workspace_admins import setup_workspace_administrators
from foundry.agent_api import ONBOARDING_AGENT_NAME
from foundry.step_agent_setup import setup_agent
from foundry.step_knowledge_base import setup_knowledge_base
from foundry.step_onboarding_agent_setup import setup_onboarding_agent
from foundry.step_pipeline_agents_setup import setup_pipeline_agents
from hosted.step_hosted_agent_deploy import deploy_hosted_agent


# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

ALL_DEPLOYMENT_STEPS = [
    "setup_knowledge_base",
    "setup_agent",
    "setup_onboarding_agent",
    "setup_pipeline_agents",
    "setup_workspace",
    "setup_administrators",
    "upload_installer",
    "run_installer",
    "deploy_hosted_agent",
]


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------


def main() -> None:
    """Orchestrate the minimal three-step solution installation."""

    # ------------------------------------------------------------------
    # Configuration from environment variables
    # ------------------------------------------------------------------
    capacity_name = get_required_env_var("AZURE_FABRIC_CAPACITY_NAME")
    subscription_id = get_required_env_var("AZURE_SUBSCRIPTION_ID")
    resource_group = get_required_env_var("AZURE_RESOURCE_GROUP")
    solution_suffix = get_required_env_var("SOLUTION_SUFFIX")
    workspace_name = os.getenv(
        "FABRIC_WORKSPACE_NAME", default_workspace_name(solution_suffix)
    )
    workspace_administrators = parse_workspace_administrators(
        get_required_env_var("AZURE_FABRIC_CAPACITY_ADMINISTRATORS"),
        os.getenv("FABRIC_WORKSPACE_ADMINISTRATORS"),
    )
    github_token = os.getenv("GITHUB_TOKEN")

    notebook_path = get_notebook_path()

    # Foundry / AI Search configuration (required — main.bicep outputs)
    search_endpoint = get_required_env_var("AZURE_AI_SEARCH_ENDPOINT")
    blob_endpoint = get_required_env_var("AZURE_STORAGE_BLOB_ENDPOINT")
    agent_endpoint = get_required_env_var("AZURE_AI_AGENT_ENDPOINT")
    ai_service_name = get_required_env_var("AI_SERVICE_NAME")
    ai_project_name = get_required_env_var("AZURE_AI_PROJECT_NAME")

    # AZURE_AI_ENDPOINT is not a bicep output: derive it from
    # AZURE_AI_AGENT_ENDPOINT (strip /api/projects/...) when the override
    # variable is not set.
    ai_endpoint = (
        os.getenv("AZURE_AI_ENDPOINT")
        or agent_endpoint.split("/api/projects")[0]
    )
    embedding_model = os.getenv("AZURE_OPENAI_EMBEDDING_MODEL", "text-embedding-3-small")
    chat_model = os.getenv("AZURE_CHAT_MODEL", "gpt-5-mini")
    search_index_name = os.getenv("AZURE_AI_SEARCH_INDEX", f"{solution_suffix}-documents")
    blob_container_name = f"{solution_suffix}-documents"
    knowledge_base_name = f"{solution_suffix}-kb"
    knowledge_source_name = f"{solution_suffix}-ks"

    # Customer onboarding knowledge base (separate index/KB, own document set)
    onboarding_search_index_name = os.getenv(
        "AZURE_AI_SEARCH_ONBOARDING_INDEX", f"{solution_suffix}-onboarding-documents"
    )
    onboarding_blob_container_name = f"{solution_suffix}-onboarding-documents"
    onboarding_knowledge_base_name = f"{solution_suffix}-onboarding-kb"
    onboarding_knowledge_source_name = f"{solution_suffix}-onboarding-ks"

    # Agent model selection
    agent_model = (
        os.getenv("AZURE_CHAT_MODEL")
        or os.getenv("AZURE_AI_AGENT_MODEL_DEPLOYMENT_NAME", "gpt-5-mini")
    )
    kb_mcp_connection_name = os.getenv("KB_MCP_CONNECTION_NAME", f"{solution_suffix}-kb-mcp-connection")
    onboarding_kb_mcp_connection_name = os.getenv(
        "KB_ONBOARDING_MCP_CONNECTION_NAME", f"{solution_suffix}-onboarding-kb-mcp-connection"
    )

    # ------------------------------------------------------------------
    # Startup banner
    # ------------------------------------------------------------------
    logger.info(f"🏭 {SOLUTION_NAME} – Solution Installer")
    logger.info("=" * 60)
    logger.info(f"Capacity:           {capacity_name}")
    logger.info(f"Subscription:       {subscription_id}")
    logger.info(f"Resource Group:     {resource_group}")
    logger.info(f"Workspace:          {workspace_name}")
    logger.info(f"Solution Suffix:    {solution_suffix}")
    logger.info(f"Installer Notebook: {notebook_path}")
    logger.info(f"GitHub Token:       {'***' if github_token else 'Not set'}")
    if workspace_administrators:
        logger.info(f"Administrators:     {', '.join(workspace_administrators)}")
    logger.info(f"Search Endpoint:    {search_endpoint}")
    logger.info(f"Search Index:       {search_index_name}")
    logger.info(f"Knowledge Base:     {knowledge_base_name}")
    logger.info(f"Onboarding Index:   {onboarding_search_index_name}")
    logger.info(f"Onboarding KB:      {onboarding_knowledge_base_name}")
    logger.info(f"Agent Endpoint:     {agent_endpoint}")
    logger.info(f"Agent Model:        {agent_model}")
    logger.info(f"KB MCP Connection:  {kb_mcp_connection_name}")
    logger.info(f"Start time:         {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    logger.info("=" * 60)

    # ------------------------------------------------------------------
    # Authenticate API clients
    # ------------------------------------------------------------------
    logger.info("\n🔐 Authenticating clients…")
    try:
        fabric_client = create_fabric_client()
        logger.info("   Fabric API client authenticated")
    except Exception as exc:
        logger.error(f"Failed to authenticate Fabric API client: {exc}")
        sys.exit(1)

    try:
        graph_client = create_graph_client()
        logger.info("   Graph API client authenticated")
    except Exception as exc:
        logger.error(f"Failed to authenticate Graph API client: {exc}")
        sys.exit(1)

    executed_steps: list = []
    failed_steps: list = []
    warnings_collected: list = []

    def _abort(step_name: str, error: Exception) -> NoReturn:
        """Record the failure, log a summary, and exit."""
        logger.error(f"Exception while executing {step_name}: {error}")
        failed_steps.append({"step": step_name, "error": str(error)})
        completed = {s for s in executed_steps} | {s["step"] for s in failed_steps}
        uncompleted = [s for s in ALL_DEPLOYMENT_STEPS if s not in completed]
        print_steps_summary(SOLUTION_NAME, solution_suffix, executed_steps, failed_steps, uncompleted)
        if warnings_collected:
            logger.warning(f"\nWarnings during deployment ({len(warnings_collected)}):")
            for _i, _w in enumerate(warnings_collected, 1):
                logger.warning(f"   {_i}. {_w['step']}: {_w['error']}")
                if _w.get("guidance"):
                    logger.warning(f"      {_w['guidance']}")
        sys.exit(1)

    def _warn_step(step_name: str, error: Exception, guidance: str | None = None) -> None:
        """Record a non-fatal step failure and let the deployment continue.

        Used by best-effort steps (e.g. setup_agent) where the underlying
        platform API is occasionally flaky and returns transient errors that
        should not abort the deployment.  The warning is shown again in the
        final summary so it is not lost in the scrollback.

        Args:
            step_name: Name of the step that failed.
            error: The exception raised by the step.
            guidance: Optional step-specific message (e.g. how to verify the
                resource was created). When provided, it is logged after the
                error and included in the final summary so users still see it
                even if it scrolled off-screen during deployment.
        """
        logger.warning(f"Step '{step_name}' reported an error: {error}")
        if guidance:
            logger.warning(f"   {guidance}")
        warnings_collected.append(
            {"step": step_name, "error": str(error), "guidance": guidance or ""}
        )
        executed_steps.append(f"{step_name} (with warnings)")

    # ------------------------------------------------------------------
    # Step 1 – Set up AI Search knowledge base (Foundry IQ)
    # ------------------------------------------------------------------
    print_step(1, 9, "Setting up AI Search knowledge base and Foundry IQ",
               search_endpoint=search_endpoint,
               index=search_index_name,
               knowledge_base=knowledge_base_name)
    try:
        setup_knowledge_base(
            solution_name=SOLUTION_NAME,
            search_endpoint=search_endpoint,
            blob_endpoint=blob_endpoint,
            ai_endpoint=ai_endpoint,
            search_index_name=search_index_name,
            blob_container_name=blob_container_name,
            knowledge_source_name=knowledge_source_name,
            knowledge_base_name=knowledge_base_name,
            embedding_model=embedding_model,
            chat_model=chat_model,
        )
        logger.info("   Setting up customer onboarding knowledge base…")
        setup_knowledge_base(
            solution_name=f"{SOLUTION_NAME} Onboarding",
            search_endpoint=search_endpoint,
            blob_endpoint=blob_endpoint,
            ai_endpoint=ai_endpoint,
            search_index_name=onboarding_search_index_name,
            blob_container_name=onboarding_blob_container_name,
            knowledge_source_name=onboarding_knowledge_source_name,
            knowledge_base_name=onboarding_knowledge_base_name,
            embedding_model=embedding_model,
            chat_model=chat_model,
            docs_subdir="documents_onboarding",
        )
        logger.info("Successfully completed: setup_knowledge_base")
        executed_steps.append("setup_knowledge_base")
    except Exception as exc:
        _abort("setup_knowledge_base", exc)

    # ------------------------------------------------------------------
    # Step 2 – Create AI Foundry agent (Knowledge Base MCP tool)
    #
    # Best-effort: the Foundry agents API is platform-dependent and
    # occasionally returns transient errors (e.g. ``(NotFound) Project not
    # found``) even when the underlying agent is created.  We log any
    # exception as a warning, record it for the final summary, and continue
    # with the deployment.
    # ------------------------------------------------------------------
    print_step(2, 9, "Creating AI Foundry agent with Knowledge Base MCP tool",
               agent_endpoint=agent_endpoint,
               knowledge_base=knowledge_base_name,
               connection=kb_mcp_connection_name)
    try:
        setup_agent(
            solution_name=SOLUTION_NAME,
            agent_endpoint=agent_endpoint,
            agent_model=agent_model,
            search_endpoint=search_endpoint,
            search_index_name=search_index_name,
            knowledge_base_name=knowledge_base_name,
            kb_mcp_connection_name=kb_mcp_connection_name,
            subscription_id=subscription_id,
            resource_group=resource_group,
            ai_service_name=ai_service_name,
            ai_project_name=ai_project_name,
        )
        logger.info("   Creating onboarding agent with Knowledge Base MCP tool…")
        setup_agent(
            solution_name=f"{SOLUTION_NAME} Onboarding",
            agent_endpoint=agent_endpoint,
            agent_model=agent_model,
            search_endpoint=search_endpoint,
            search_index_name=onboarding_search_index_name,
            knowledge_base_name=onboarding_knowledge_base_name,
            kb_mcp_connection_name=onboarding_kb_mcp_connection_name,
            subscription_id=subscription_id,
            resource_group=resource_group,
            ai_service_name=ai_service_name,
            ai_project_name=ai_project_name,
            agent_name=ONBOARDING_AGENT_NAME,
            scenario_desc="Guiding new-client onboarding: entity resolution, KYC/AML screening, document validation and account setup.",
        )
        logger.info("Successfully completed: setup_agent")
        executed_steps.append("setup_agent")
    except Exception as exc:
        _warn_step(
            "setup_agent",
            exc,
            guidance=(
                "This is often caused by a transient platform-level issue "
                "with the AI Foundry agents API and may not indicate a real "
                "failure. Please open the AI Foundry project and verify "
                "whether the agent was created. If it was not, re-run the "
                "deployment."
            ),
        )

    # ------------------------------------------------------------------
    # Step 3 – Create OnboardingAgent (Knowledge Base MCP tool)
    #
    # Best-effort, same rationale as setup_agent above.
    # ------------------------------------------------------------------
    print_step(3, 9, "Creating OnboardingAgent with Knowledge Base MCP tool",
               agent_endpoint=agent_endpoint,
               knowledge_base=knowledge_base_name,
               connection=kb_mcp_connection_name)
    try:
        setup_onboarding_agent(
            solution_name=SOLUTION_NAME,
            agent_endpoint=agent_endpoint,
            agent_model=agent_model,
            search_endpoint=search_endpoint,
            knowledge_base_name=knowledge_base_name,
            kb_mcp_connection_name=kb_mcp_connection_name,
            subscription_id=subscription_id,
            resource_group=resource_group,
            ai_service_name=ai_service_name,
            ai_project_name=ai_project_name,
        )
        logger.info("Successfully completed: setup_onboarding_agent")
        executed_steps.append("setup_onboarding_agent")
    except Exception as exc:
        _warn_step(
            "setup_onboarding_agent",
            exc,
            guidance=(
                "This is often caused by a transient platform-level issue "
                "with the AI Foundry agents API and may not indicate a real "
                "failure. Please open the AI Foundry project and verify "
                "whether the agent was created. If it was not, re-run the "
                "deployment."
            ),
        )

    # ------------------------------------------------------------------
    # Step 4 – Create onboarding-form pipeline agents (Intake, Orchestrator,
    # Opportunity, Insight, Crm, Lego)
    #
    # Best-effort, same rationale as setup_agent above.
    # ------------------------------------------------------------------
    print_step(4, 9, "Creating onboarding-form pipeline agents", agent_endpoint=agent_endpoint)
    try:
        setup_pipeline_agents(
            solution_name=SOLUTION_NAME,
            agent_endpoint=agent_endpoint,
            agent_model=agent_model,
        )
        logger.info("Successfully completed: setup_pipeline_agents")
        executed_steps.append("setup_pipeline_agents")
    except Exception as exc:
        _warn_step(
            "setup_pipeline_agents",
            exc,
            guidance=(
                "This is often caused by a transient platform-level issue "
                "with the AI Foundry agents API and may not indicate a real "
                "failure. Please open the AI Foundry project and verify "
                "whether the agents were created. If not, re-run the "
                "deployment."
            ),
        )

    # ------------------------------------------------------------------
    # Step 5 – Set up Fabric workspace
    # ------------------------------------------------------------------
    print_step(5, 9, "Setting up Fabric workspace and capacity assignment",
               capacity_name=capacity_name, workspace_name=workspace_name)
    try:
        workspace_id = setup_workspace(
            fabric_client=fabric_client,
            capacity_name=capacity_name,
            workspace_name=workspace_name,
            subscription_id=subscription_id,
            resource_group=resource_group,
        )
        logger.info("Successfully completed: setup_workspace")
        executed_steps.append("setup_workspace")
        # Persist for downstream automation (e.g. CI deployment summary).
        set_azd_env_var("FABRIC_WORKSPACE_ID", workspace_id)
        set_azd_env_var("FABRIC_WORKSPACE_NAME", workspace_name)
    except Exception as exc:
        _abort("setup_workspace", exc)

    # Workspace-scoped client required for all subsequent steps
    logger.info("\nCreating workspace-scoped Fabric API client…")
    try:
        workspace_client = create_workspace_fabric_client(workspace_id)
        logger.info("   Workspace client authenticated")
    except Exception as exc:
        _abort("create_workspace_client", exc)

    # ------------------------------------------------------------------
    # Step 5 – Configure workspace administrators
    # ------------------------------------------------------------------
    admin_display = ", ".join(workspace_administrators) if workspace_administrators else "None"
    print_step(6, 9, "Configuring workspace administrators",
               workspace_id=workspace_id, administrators=admin_display)
    try:
        setup_workspace_administrators(
            workspace_client=workspace_client,
            fabric_admins=workspace_administrators or [],
            graph_client=graph_client,
        )
        logger.info("Successfully completed: setup_administrators")
        executed_steps.append("setup_administrators")
    except Exception as exc:
        _abort("setup_administrators", exc)

    # ------------------------------------------------------------------
    # Step 6 – Upload installer notebook
    # ------------------------------------------------------------------
    print_step(7, 9, "Uploading installer notebook",
               notebook=INSTALLER_NOTEBOOK_NAME)
    try:
        notebook_id = upload_installer_notebook(workspace_client, notebook_path, github_token=github_token)
        logger.info("Successfully completed: upload_installer")
        executed_steps.append("upload_installer")
    except Exception as exc:
        _abort("upload_installer", exc)

    # ------------------------------------------------------------------
    # Step 7 – Run installer notebook
    # ------------------------------------------------------------------
    print_step(8, 9, "Running installer notebook",
               notebook_id=notebook_id)
    try:
        run_installer_notebook(workspace_client, notebook_id)
        logger.info("Successfully completed: run_installer")
        executed_steps.append("run_installer")
    except Exception as exc:
        _abort("run_installer", exc)

    # ------------------------------------------------------------------
    # Step 8 – Deploy hosted Foundry agent (optional)
    #
    # Requires an Azure Container Registry (AZURE_CONTAINER_REGISTRY_NAME),
    # which is not a main.bicep output. When it is not set the step is
    # skipped so azd up still succeeds. Best-effort otherwise: a failure is
    # recorded as a warning and does not abort the deployment.
    # ------------------------------------------------------------------
    container_registry_name = os.getenv("AZURE_CONTAINER_REGISTRY_NAME")
    print_step(9, 9, "Deploying hosted Foundry agent",
               container_registry=container_registry_name or "Not set (skipped)")
    if not container_registry_name:
        logger.info(
            "   AZURE_CONTAINER_REGISTRY_NAME not set — skipping hosted agent deploy."
        )
    else:
        try:
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
                image_tag=os.getenv("HOSTED_AGENT_IMAGE_TAG", "latest"),
                source_dir=os.path.join(REPO_ROOT, "src", "hosted"),
                cpu=os.getenv("HOSTED_AGENT_CPU", "0.5"),
                memory=os.getenv("HOSTED_AGENT_MEMORY", "1.0Gi"),
            )
            logger.info("Successfully completed: deploy_hosted_agent")
            executed_steps.append("deploy_hosted_agent")
        except Exception as exc:
            _warn_step(
                "deploy_hosted_agent",
                exc,
                guidance=(
                    "Verify the container registry exists and the image built "
                    "successfully. You can re-run infra/scripts/hosted/"
                    "deploy_hosted_agent.py to retry."
                ),
            )

    # ------------------------------------------------------------------
    # Success summary
    # ------------------------------------------------------------------
    workspace_url = (
        f"https://app.fabric.microsoft.com/groups/{workspace_id}?experience=fabric-developer"
    )

    print_steps_summary(SOLUTION_NAME, solution_suffix, executed_steps, failed_steps, [])

    logger.info(f"\n{'='*60}")
    logger.info(f"🎉 {SOLUTION_NAME.upper()} INSTALLATION COMPLETE!")
    logger.info(f"{'='*60}")
    logger.info(f"Completed:  {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    logger.info(f"Suffix:     {solution_suffix}")
    logger.info(f"Workspace:  {workspace_name}")
    logger.info(f"URL:        {workspace_url}")
    logger.info(f"{'='*60}")

    if warnings_collected:
        logger.warning(f"\nWarnings during deployment ({len(warnings_collected)}):")
        for _i, _w in enumerate(warnings_collected, 1):
            logger.warning(f"   {_i}. {_w['step']}: {_w['error']}")
            if _w.get("guidance"):
                logger.warning(f"      {_w['guidance']}")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        logger.warning("\n\nInstallation interrupted by user")
        sys.exit(1)
    except Exception as exc:
        logger.error(f"\n\nUnexpected error: {exc}")
        sys.exit(1)
