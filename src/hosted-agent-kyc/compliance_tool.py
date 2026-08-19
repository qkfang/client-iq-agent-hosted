"""Structured KYC/AML compliance case response tool for the hosted agent."""

from typing import Annotated, Literal

from agent_framework import tool

RiskRating = Literal["Low", "Medium", "High"]
WorkflowStatus = Literal[
    "Pending Review",
    "Approved",
    "Escalated",
    "Rejected",
    "Additional Information Required",
]


@tool
def build_compliance_response(
    summary: Annotated[str, "Plain-language answer to the compliance question or case"],
    risk_rating: Annotated[RiskRating, "Overall KYC/AML risk rating, based on knowledge base criteria"],
    required_documents: Annotated[
        list[str],
        "Documents needed to complete or support the case, e.g. proof of identity, proof of address, source of funds",
    ],
    screening_findings: Annotated[
        list[str],
        "Sanctions, PEP, or adverse media screening findings; leave empty if none were identified",
    ],
    workflow_status: Annotated[WorkflowStatus, "Current case workflow status"],
    next_actions: Annotated[list[str], "Concrete next steps for the compliance analyst or client"],
) -> dict[str, str | list[str]]:
    """Render a KYC/AML answer as a standard compliance case response."""
    return {
        "summary": summary.strip(),
        "risk_rating": risk_rating,
        "required_documents": required_documents,
        "screening_findings": screening_findings or ["No adverse findings identified."],
        "workflow_status": workflow_status,
        "next_actions": next_actions,
    }
