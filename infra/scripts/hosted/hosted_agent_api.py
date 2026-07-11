"""
Deployment operations for the hosted Foundry agent in ``src/hosted``.

Provides functions for:
- Building and pushing the agent's container image with ``az acr build``.
- Creating or replacing a hosted AI Foundry agent version wired up to the
  Knowledge Base MCP tool.
"""

import logging
import os
import shutil
import subprocess

# Module-level logger — inherits configuration from the root logger set up
# by setup_logging() in the entry-point scripts.
logger = logging.getLogger(__name__)

HOSTED_AGENT_NAME = "hosted-chat-agent"


def build_and_push_image(
    registry_name: str, image_name: str, image_tag: str, source_dir: str
) -> str:
    """Build the agent container image and push it to Azure Container Registry.

    Uses ``az acr build`` so no local Docker daemon is required.

    Args:
        registry_name: Azure Container Registry name (without domain suffix).
        image_name: Repository name for the image.
        image_tag: Tag to apply to the built image.
        source_dir: Directory containing the Dockerfile and agent source
            (``src/hosted``).

    Returns:
        Fully qualified image reference (``<registry>.azurecr.io/<name>:<tag>``).
    """
    image_ref = f"{image_name}:{image_tag}"
    logger.info(f"   Building and pushing image '{image_ref}' to '{registry_name}'…")
    az_cmd = shutil.which("az") or "az"
    # Force UTF-8 I/O so the Azure CLI can stream build logs on Windows
    # consoles that default to a non-UTF-8 code page (e.g. cp1252).
    az_env = {**os.environ, "PYTHONIOENCODING": "utf-8", "PYTHONUTF8": "1"}
    # ``--no-logs`` skips streaming the remote build log, which the Azure CLI
    # renders through colorama and crashes on Windows when the log contains
    # characters outside the console code page (cp1252). The command still
    # waits for the build to finish and returns a non-zero exit code on failure.
    process = subprocess.Popen(
        [
            az_cmd,
            "acr",
            "build",
            "--registry",
            registry_name,
            "--image",
            image_ref,
            "--no-logs",
            source_dir,
        ],
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
        env=az_env,
    )
    for line in process.stdout:
        logger.info("      %s", line.rstrip())
    return_code = process.wait()
    if return_code != 0:
        raise subprocess.CalledProcessError(return_code, process.args)
    return f"{registry_name}.azurecr.io/{image_ref}"


def create_or_update_hosted_agent(
    project_client,
    agent_name: str,
    image: str,
    cpu: str,
    memory: str,
    mcp_endpoint: str,
    connection_name: str,
    environment_variables: dict,
):
    """Create or replace a hosted AI Foundry agent with the Knowledge Base MCP tool.

    If an agent with the same name already exists it is deleted before the
    new version is created.

    Args:
        project_client: Authenticated ``AIProjectClient`` (created with
            ``allow_preview=True``).
        agent_name: Name for the hosted agent resource.
        image: Fully qualified container image reference.
        cpu: CPU allocation for the hosted agent (e.g. ``"0.5"``).
        memory: Memory allocation for the hosted agent (e.g. ``"1.0Gi"``).
        mcp_endpoint: Full MCP endpoint URL for the Knowledge Base.
        connection_name: Project connection name registered for the MCP tool.
        environment_variables: Environment variables to set in the container.

    Returns:
        The created hosted agent version.
    """
    from azure.ai.projects.models import (
        ContainerConfiguration,
        HostedAgentDefinition,
        MCPTool,
        ProtocolVersionRecord,
    )

    try:
        existing = project_client.agents.get(agent_name)
    except Exception:
        existing = None  # agent does not yet exist
    if existing:
        project_client.agents.delete(agent_name)
        logger.debug(f"      Deleted existing hosted agent '{agent_name}'")

    mcp_tool = MCPTool(
        server_label="knowledge-base",
        server_url=mcp_endpoint,
        require_approval="never",
        allowed_tools=["knowledge_base_retrieve"],
        project_connection_id=connection_name,
    )

    agent_definition = HostedAgentDefinition(
        cpu=cpu,
        memory=memory,
        container_configuration=ContainerConfiguration(image=image),
        protocol_versions=[
            ProtocolVersionRecord(protocol="responses", version="2.0.0")
        ],
        tools=[mcp_tool],
        environment_variables=environment_variables,
    )

    return project_client.agents.create_version(
        agent_name=agent_name,
        definition=agent_definition,
    )
