#!/usr/bin/env python3

import os
import sys
from pathlib import Path

from dotenv import load_dotenv


AGENT_ROOT = Path(__file__).resolve().parent.parent
REPO_ROOT = AGENT_ROOT.parents[1]
INFRA_SCRIPTS = REPO_ROOT / "infra" / "scripts"
sys.path.insert(0, str(INFRA_SCRIPTS))

from common.env import load_all_env
from common.env_utils import get_required_env_var
from common.logging_config import setup_logging
from foundry.agent_api import (
    WORKIQ_MAIL_SERVER_LABEL,
    WORKIQ_MAIL_SERVER_URL,
    WORKIQ_SERVER_LABEL,
    WORKIQ_SERVER_URL,
    build_fabric_iq_tool,
)
from hosted.step_hosted_agent_deploy import deploy_hosted_agent


def main() -> None:
    setup_logging()
    load_all_env()
    load_dotenv(AGENT_ROOT / ".env", override=False)

    solution_suffix = get_required_env_var("SOLUTION_SUFFIX")
    agent_endpoint = os.getenv("AZURE_AI_AGENT_ENDPOINT") or os.getenv(
        "FOUNDRY_PROJECT_ENDPOINT"
    )
    if not agent_endpoint:
        raise EnvironmentError(
            "AZURE_AI_AGENT_ENDPOINT or FOUNDRY_PROJECT_ENDPOINT must be set"
        )

    agent_model = os.getenv("AZURE_AI_MODEL_DEPLOYMENT_NAME") or os.getenv(
        "AZURE_CHAT_MODEL", "gpt-5.6-sol"
    )
    knowledge_base_name = f"{solution_suffix}-kb"
    toolbox_name = os.getenv("TOOLBOX_NAME", "kyc-toolbox")

    extra_tools = []
    # Published together with the KB tool by deploy_hosted_agent (toolbox
    # versions are immutable, so all tools must go in a single publish).
    extra_tools.append(
        {
            "type": "mcp",
            "server_label": WORKIQ_MAIL_SERVER_LABEL,
            "server_url": WORKIQ_MAIL_SERVER_URL,
            "require_approval": "never",
            "project_connection_id": WORKIQ_MAIL_SERVER_LABEL,
        }
    )
    for kb_label, kb_path in (
        ("kb-crm-foundry-iq-6ys34", "crm-foundry-iq"),
        ("kb-crm-fabric-iq-6ys34", "crm-fabric-iq"),
        ("kb-crm-web-iq-6ys34", "crm-web-iq"),
    ):
        extra_tools.append(
            {
                "type": "mcp",
                "server_label": kb_label,
                "server_url": (
                    "https://srch-ciquocsj.search.windows.net/knowledgebases/"
                    f"{kb_path}/mcp?api-version=2026-05-01-preview"
                ),
                "require_approval": "never",
                "project_connection_id": kb_label,
            }
        )
    extra_tools.append(
        {
            "type": "mcp",
            "server_label": WORKIQ_SERVER_LABEL,
            "server_url": WORKIQ_SERVER_URL,
            "require_approval": {"never": {"tool_names": ["ask"]}},
            "project_connection_id": WORKIQ_SERVER_LABEL,
        }
    )
    fabric_iq_connection_id = os.getenv("FABRIC_IQ_CONNECTION_ID")
    if fabric_iq_connection_id:
        extra_tools.append(
            build_fabric_iq_tool(
                project_connection_id=fabric_iq_connection_id,
                server_url=os.getenv("FABRIC_IQ_SERVER_URL"),
            )
        )

    deploy_hosted_agent(
        agent_name="hosted-agent-kyc",
        agent_endpoint=agent_endpoint,
        agent_model=agent_model,
        search_endpoint=get_required_env_var("AZURE_AI_SEARCH_ENDPOINT"),
        knowledge_base_name=knowledge_base_name,
        kb_mcp_connection_name=os.getenv(
            "KB_MCP_CONNECTION_NAME", f"{knowledge_base_name}-mcp-connection"
        ),
        subscription_id=get_required_env_var("AZURE_SUBSCRIPTION_ID"),
        resource_group=get_required_env_var("AZURE_RESOURCE_GROUP"),
        ai_service_name=get_required_env_var("AI_SERVICE_NAME"),
        ai_project_name=get_required_env_var("AZURE_AI_PROJECT_NAME"),
        container_registry_name=get_required_env_var(
            "AZURE_CONTAINER_REGISTRY_NAME"
        ),
        source_dir=str(AGENT_ROOT),
        cpu=os.getenv("HOSTED_AGENT_CPU", "0.5"),
        memory=os.getenv("HOSTED_AGENT_MEMORY", "1.0Gi"),
        toolbox_name=toolbox_name,
        extra_tools=extra_tools,
    )


if __name__ == "__main__":
    main()