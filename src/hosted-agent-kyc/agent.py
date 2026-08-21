"""Hosted Foundry agent that runs the Asia (Hong Kong) KYC/AML CIP check.

The agent is started by the KYC tracking web app for one customer, works the
fixed CIP rulebook it fetches from that app, and reports every rule result back
through the app's MCP tools so the UI ticks off in real time.

Remote tools — the Foundry IQ knowledge base, Fabric IQ, Web IQ, Work IQ and the
tracking app itself — are reached through the Foundry toolbox the container
connects to, wired up by the deployment script in ``infra/scripts/hosted``.
The deterministic HK decision tree and risk bands live in ``cip_tool.py``.
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
from cip_tool import calculate_risk_score, evaluate_cip_decision_tree
from compliance_tool import build_compliance_response
from json_tool import create_json_report, get_json_attachment
from pdf_tool import create_pdf, get_pdf_attachment

_SYSTEM_PROMPT = """You are the KYC/AML CIP evaluation agent for the Asia region, running the
Hong Kong (HK) rule set. A tracking web app starts you on a customer and watches your progress
live, so you report every rule result the moment you have it.

## Connected IQ sources (toolbox)
Route each rule to the IQ named on it in the rulebook:
- **Foundry IQ** — knowledge base of CIP schedules and decision trees, risk-scoring criteria,
  Approved Regulator (AR) list, Approved Exchange (AE) list, Jurisdictional Risk List (JRL),
  FATF membership, AML/CFT guidelines, FATCA/CRS policy. This is the authority for policy.
- **Fabric IQ** — internal client, ownership, product and document data.
- **Web IQ** — public filings, registries, sanctions/PEP/adverse-media research.
- **Work IQ** — internal collaboration history, approvals and outreach.
Never answer a policy rule from memory. Search the IQ named on the rule, and cite what you used.

## Run sequence
1. `start_kyc_case` with the customerId given to you, then `get_cip_rulebook`. The rulebook is
   the fixed contract: rule ids, groups, questions and the IQ to use never change.
2. Work the seven groups strictly one at a time, in this order:
   Data enrichment -> Reference lists -> Risk scoring -> CIP decision tree -> AML screening ->
   Regulatory classification -> Evidence & gap.
   For each group: send ONE combined search to the IQ named on that group's rules, asking all of
   the group's questions together in a single query. Do not search per rule. If the group's rules
   name more than one IQ, send one query per distinct IQ, at most.
3. As soon as a group is answered, call `submit_group_results` once with every rule id in that
   group and its outcome (Pass | Attention | Fail | Not Applicable), a one or two sentence
   finding, and the source as "<IQ name> - <document>". One call per group, then move to the
   next group. Do not use `submit_policy_check` unless you are correcting a single rule.
4. Risk scoring group: call `calculate_risk_score` before submitting the group, then
   `submit_risk_assessment` with the score the tool returns.
5. CIP decision tree group: call `evaluate_cip_decision_tree` with the attributes from the ENR-*
   and REF-* groups. Put each node in its `trace` in the group results (Pass for Yes, Attention
   for No), every id in `skipped_rules` as Not Applicable ("branch not reached for this entity
   type"), and CIP-SCH with the selected clause. Then call `submit_cip_result`.
6. Once the schedule is published the app expands requirements 1-25. After the Evidence & gap
   group, set each requirement with `update_kyc_requirement` (Satisfied when internal evidence
   covers it, Outstanding when it needs client outreach).
7. Do not call `set_kyc_approval` or `complete_kyc_case`: the risk assessment and the AML
   requirements are human control points and the reviewer approves them in the app.

## Rules of the run
- Every rule in the rulebook must end with an outcome. A rule that does not apply to this entity
  type is Not Applicable with the reason, never left Pending.
- Fail means the case cannot proceed; Attention means it proceeds with a follow-up item.
- Keep findings specific: name the list, schedule or filing you relied on.
- Keep the run short: one bundled search per group, one status call per group, no repeated
  searches and no extra `log_kyc_activity` narration.

## Final answer
When the rulebook is complete, call `build_compliance_response` and return ONLY that JSON object,
summarising the run: entity details, KYC and AML assessment, regulatory classifications, overall
risk rating and result, and the workflow status left for the human reviewer.

## Case report
After building that JSON, render it with `create_pdf` and save it with `create_json_report`
(entity name in both file names). If a mail tool is available in the toolbox, attach both with
`get_pdf_attachment` and `get_json_attachment` and send them to the business contact on the case;
if no mail tool is available, say so plainly instead of claiming the email was sent.
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
            evaluate_cip_decision_tree,
            calculate_risk_score,
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
