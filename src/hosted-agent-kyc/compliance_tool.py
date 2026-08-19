"""Structured KYC/AML compliance case response tool for the hosted agent.

The response shape follows a Fenergo-style Client Lifecycle Management (CLM) case
record: separate KYC and AML assessment sections, a regulatory classification
block, and an overall case outcome with any outstanding follow-up items.
"""

from typing import Annotated, Literal

from agent_framework import tool

RiskRating = Literal["Low", "Medium", "High"]
AssessmentResult = Literal["Pass", "Conditional Pass", "Fail"]
ScreeningOutcome = Literal["Clear", "Potential Match", "Confirmed Match"]
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
    entity_name: Annotated[str, "Legal name of the client entity or individual under review"],
    entity_type: Annotated[str, "Entity type, e.g. Individual, Corporate, Trust, Partnership"],
    jurisdiction: Annotated[str, "Primary jurisdiction of incorporation or residence"],
    identity_verification_status: Annotated[
        Literal["Verified", "Partially Verified", "Not Verified"],
        "KYC identity verification outcome",
    ],
    beneficial_owners: Annotated[
        list[str],
        "Beneficial owners as 'name - ownership % - PEP status', e.g. 'Jane Doe - 40% - Not a PEP'",
    ],
    source_of_funds: Annotated[str, "Declared and verified source of funds"],
    source_of_wealth: Annotated[str, "Declared and verified source of wealth"],
    required_documents: Annotated[
        list[str],
        "KYC document checklist with status inline, e.g. 'Passport - Received', 'Proof of address - Outstanding'",
    ],
    kyc_risk_factors: Annotated[
        list[str],
        "KYC risk drivers considered, e.g. customer type, product, channel, geography",
    ],
    sanctions_screening: Annotated[ScreeningOutcome, "Sanctions list screening outcome"],
    sanctions_lists_checked: Annotated[
        list[str],
        "Sanctions/watch lists checked, e.g. OFAC, UN, EU, UK HMT, DFAT",
    ],
    pep_screening: Annotated[ScreeningOutcome, "Politically Exposed Person screening outcome"],
    adverse_media_screening: Annotated[
        Literal["Clear", "Findings Identified"],
        "Adverse media screening outcome",
    ],
    transaction_monitoring_flags: Annotated[
        list[str],
        "AML transaction monitoring alerts; leave empty if none were raised",
    ],
    regulatory_classifications: Annotated[
        list[str],
        "Regulatory classifications, e.g. 'FATCA: US Person', 'CRS: Reportable - Ireland'",
    ],
    overall_risk_rating: Annotated[RiskRating, "Combined KYC/AML risk rating, based on knowledge base criteria"],
    final_assessment_result: Annotated[AssessmentResult, "Overall case outcome"],
    follow_up_items: Annotated[
        list[str],
        "Outstanding items to close out despite the overall outcome, e.g. minor document renewals",
    ],
    workflow_status: Annotated[WorkflowStatus, "Current case workflow status"],
    next_review_date: Annotated[str, "Date of the next scheduled periodic review, e.g. '2027-08-19'"],
    next_actions: Annotated[list[str], "Concrete next steps for the compliance analyst or client"],
) -> dict[str, object]:
    """Render a KYC/AML answer as a Fenergo-style CLM compliance case response."""
    return {
        "summary": summary.strip(),
        "entity_details": {
            "entity_name": entity_name,
            "entity_type": entity_type,
            "jurisdiction": jurisdiction,
        },
        "kyc_assessment": {
            "identity_verification_status": identity_verification_status,
            "beneficial_owners": beneficial_owners,
            "source_of_funds": source_of_funds,
            "source_of_wealth": source_of_wealth,
            "required_documents": required_documents,
            "risk_factors": kyc_risk_factors,
        },
        "aml_assessment": {
            "sanctions_screening": sanctions_screening,
            "sanctions_lists_checked": sanctions_lists_checked,
            "pep_screening": pep_screening,
            "adverse_media_screening": adverse_media_screening,
            "transaction_monitoring_flags": transaction_monitoring_flags,
        },
        "regulatory_classifications": regulatory_classifications,
        "final_assessment": {
            "overall_risk_rating": overall_risk_rating,
            "result": final_assessment_result,
            "follow_up_items": follow_up_items,
        },
        "case_management": {
            "workflow_status": workflow_status,
            "next_review_date": next_review_date,
            "next_actions": next_actions,
        },
    }
