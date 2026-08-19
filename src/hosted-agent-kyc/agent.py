"""Hosted Foundry agent grounded with the Foundry IQ knowledge base.

This agent is designed for **hosted deployment** to Microsoft Foundry, where
the Foundry IQ knowledge base (built by this solution accelerator's
knowledge-base setup step) is declared in ``agent.manifest.yaml`` and wired
in as a Knowledge Base MCP tool by the deployment script in
``infra/scripts/hosted``.

The agent's intelligence lives in the system prompt below; the knowledge base
tool is injected at deploy time, not instantiated here.
"""

import os
from pathlib import Path

try:
    from dotenv import load_dotenv

    load_dotenv(dotenv_path=Path(__file__).with_name(".env"))
except ImportError:
    pass  # dotenv not needed in hosted deployment

from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import FoundryToolbox
from azure.identity import DefaultAzureCredential
from compliance_tool import build_compliance_response
from json_tool import create_json_report, get_json_attachment
from pdf_tool import create_pdf, get_pdf_attachment

_SYSTEM_PROMPT = """You are a KYC/AML compliance assistant with access to a knowledge base of policy,
risk-rating, and regulatory reference documents.

## Knowledge Base (Foundry IQ)
The knowledge base is automatically searched before answering. It contains KYC/AML policies,
risk-rating criteria, required-document rules, screening rules, and regulatory guidance
(e.g. FATCA, CRS, beneficial ownership, sanctions and PEP screening).

## Response Format
Every answer about a client, case, or compliance question must call build_compliance_response
and return ONLY the resulting JSON object, a Fenergo-style CLM case record shaped like:
{
  "summary": "plain-language answer",
  "entity_details": {"entity_name": "...", "entity_type": "...", "jurisdiction": "..."},
  "kyc_assessment": {
    "identity_verification_status": "Verified | Partially Verified | Not Verified",
    "beneficial_owners": ["..."],
    "source_of_funds": "...",
    "source_of_wealth": "...",
    "required_documents": ["..."],
    "risk_factors": ["..."]
  },
  "aml_assessment": {
    "sanctions_screening": "Clear | Potential Match | Confirmed Match",
    "sanctions_lists_checked": ["..."],
    "pep_screening": "Clear | Potential Match | Confirmed Match",
    "adverse_media_screening": "Clear | Findings Identified",
    "transaction_monitoring_flags": ["..."]
  },
  "regulatory_classifications": ["..."],
  "final_assessment": {
    "overall_risk_rating": "Low | Medium | High",
    "result": "Pass | Conditional Pass | Fail",
    "follow_up_items": ["..."]
  },
  "case_management": {
    "workflow_status": "Pending Review | Approved | Escalated | Rejected | Additional Information Required",
    "next_review_date": "YYYY-MM-DD",
    "next_actions": ["..."]
  }
}

Unless the knowledge base or case data clearly indicates a problem, default demo cases to
overall_risk_rating "Low" and result "Pass", with 2-3 minor follow_up_items (e.g. a document
renewal or a periodic review date) rather than leaving every field empty.

## PDF Report, JSON File, and Email
For every case response, after building the JSON above, always:
1. Call create_pdf to render it as a readable report (use the entity name in the title).
2. Call create_json_report to save the same JSON payload as a standalone file (same entity name
   in the file_name, different extension).
3. Call get_pdf_attachment and get_json_attachment, then send both attachments together in one
   email with the Work IQ Mail tool (mcp_MailTools_graph_mail_sendMail) to
   danielfang@MngEnvMCAP951655.onmicrosoft.com.
Do this automatically every time, without waiting for the user to ask for a PDF, a JSON file, or
an email. If no mail tool is available, say so plainly instead of claiming the email was sent.
"""


def _build_agent() -> Agent:
    project_endpoint = os.environ.get("FOUNDRY_PROJECT_ENDPOINT")
    model = os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME", "gpt-5.6-sol")

    if not project_endpoint:
        raise EnvironmentError(
            "FOUNDRY_PROJECT_ENDPOINT environment variable is not set. "
            "Copy .env.template to .env and fill in your Foundry project endpoint."
        )

    credential = DefaultAzureCredential()
    client = FoundryChatClient(
        project_endpoint=project_endpoint,
        model=model,
        credential=credential,
    )

    # Tools declared on the hosted agent version are not passed to the container,
    # so remote tools must be reached through a toolbox the container connects to.
    use_toolbox = os.environ.get("TOOLBOX_NAME") or os.environ.get("TOOLBOX_ENDPOINT")
    toolbox = [FoundryToolbox(credential=credential)] if use_toolbox else []

    # The Knowledge Base MCP tool is wired up by the deployment script in
    # infra/scripts/hosted, not instantiated here.
    return Agent(
        client=client,
        name="HostedChatAgent",
        instructions=_SYSTEM_PROMPT,
        tools=[
            build_compliance_response,
            create_pdf,
            get_pdf_attachment,
            create_json_report,
            get_json_attachment,
            *toolbox,
        ],
        context_providers=[],
    )


# Module-level export used by main.py.
agent = _build_agent()
