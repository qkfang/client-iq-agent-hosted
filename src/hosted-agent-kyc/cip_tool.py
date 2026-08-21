"""Deterministic CIP rule evaluation for the Asia (Hong Kong) rule set.

The decision tree and the risk-score bands are held as data so the walk is
reproducible: the model supplies the reasoning narrative around the result it
gets back, it does not invent the outcome. Node ids match the rule ids in the
tracking app's rulebook (CIP-Q2, CIP-Q7, ...), so every node the walk visits
can be reported straight back with submit_policy_check.
"""

from typing import Annotated, Literal

from agent_framework import tool

JURISDICTION = "HK"

SCHEDULES = {
    "4.2": "Public Listed Company (Approved Exchanges)",
    "4.3": "Public Listed Company (Non-Approved Exchanges)",
    "4.6": "Partnerships",
    "4.7": "Sovereign Government / Local Government / Supranational Body / Central Bank",
    "4.8": "Regulated Entity & Branches (Approved Regulators)",
    "4.9": "Regulated Entity & Branches (Cross-border Correspondent Relationship)",
    "4.10": "Regulated Entity & Branches (Non-Approved Regulators)",
    "4.11": "Regulated Entity & Branches (Non-Approved Regulators, Cross-border Correspondent)",
    "4.12": "Fund with Regulated Investment Manager (Approved Regulator)",
    "4.13": "Fund with Regulated Investment Manager (Non-Approved Regulator)",
}

ENTRY_POINTS = {
    "regulated financial institution": "Q2",
    "public listed company": "Q7",
    "fund": "Q15",
    "fund / collective investment scheme": "Q15",
    "pension scheme": "Q20",
    "government / wholly state-owned entity": "Q23",
    "partnership": "SCHEDULE:4.6",
}

NODES = {
    "Q2": {
        "rule_id": "CIP-Q2",
        "question": "Is the relationship a cross-border correspondent relationship?",
        "input": "cross_border_correspondent",
        "yes": "Q3",
        "no": "Q8",
    },
    "Q3": {
        "rule_id": "CIP-Q3",
        "question": "Is the regulator on the AR list, or the regulator of a FATF member country not Extreme on the JRL?",
        "input": "regulator_approved",
        "yes": "SCHEDULE:4.9",
        "no": "SCHEDULE:4.11",
    },
    "Q7": {
        "rule_id": "CIP-Q7",
        "question": "Is the entity regulated?",
        "input": "is_regulated",
        "yes": "Q2",
        "no": "Q11",
    },
    "Q8": {
        "rule_id": "CIP-Q8",
        "question": "Is the regulator on the AR list, or the regulator of a FATF member country not Extreme on the JRL?",
        "input": "regulator_approved",
        "yes": "SCHEDULE:4.8",
        "no": "SCHEDULE:4.10",
    },
    "Q11": {
        "rule_id": "CIP-Q11",
        "question": "Is the entity listed on an AE-list exchange, or a FATF-member exchange of a country not Very High to Extreme on the JRL?",
        "input": "exchange_approved",
        "yes": "SCHEDULE:4.2",
        "no": "SCHEDULE:4.3",
    },
    "Q15": {
        "rule_id": "CIP-Q15",
        "question": "Is the fund's investment manager regulated?",
        "input": "fund_manager_regulated",
        "yes": "Q16",
        "no": "SELECT_BY_ENTITY_TYPE",
    },
    "Q16": {
        "rule_id": "CIP-Q16",
        "question": "Is the investment manager, or the fund administrator conducting KYC, regulated by an AR-list regulator?",
        "input": "fund_manager_regulator_approved",
        "yes": "SCHEDULE:4.12",
        "no": "SCHEDULE:4.13",
    },
    "Q20": {
        "rule_id": "CIP-Q20",
        "question": "Is the entity a government pension scheme?",
        "input": "government_pension_scheme",
        "yes": "SCHEDULE:4.7",
        "no": "Q15",
    },
    "Q23": {
        "rule_id": "CIP-Q23",
        "question": "Is the entity a wholly state-owned entity?",
        "input": "wholly_state_owned",
        "yes": "Q24",
        "no": "SCHEDULE:4.7",
    },
    "Q24": {
        "rule_id": "CIP-Q24",
        "question": "Is the wholly state-owned entity regulated?",
        "input": "is_regulated",
        "yes": "Q2",
        "no": "SELECT_BY_ENTITY_TYPE",
    },
}

RISK_CATEGORIES = [
    ("Ownership Type Risk", "ownership_type_score", 15),
    ("Listed Entity Risk", "listed_entity_score", 2),
    ("Regulated Status Risk", "regulated_status_score", 2),
    ("Industry Risk", "industry_score", 65),
    ("Product Risk", "product_score", 15),
]

LOW_BAND_MAX = 19
MEDIUM_BAND_MAX = 49


@tool
def evaluate_cip_decision_tree(
    entity_type: Annotated[
        str,
        "Regulated Financial Institution, Public Listed Company, Fund / Collective Investment Scheme, "
        "Pension Scheme, Government / Wholly State-Owned Entity or Partnership",
    ],
    is_regulated: Annotated[bool, "Is the entity under regulatory oversight?"] = False,
    regulator_approved: Annotated[
        bool,
        "Is the regulator on the Approved Regulator list, or the regulator of a FATF member country not Extreme on the JRL?",
    ] = False,
    cross_border_correspondent: Annotated[
        bool, "Is the relationship a cross-border correspondent relationship?"
    ] = False,
    exchange_approved: Annotated[
        bool,
        "Is the listing on an Approved Exchange, or a FATF-member exchange of a country not Very High to Extreme on the JRL?",
    ] = False,
    fund_manager_regulated: Annotated[bool, "Is the fund's investment manager regulated?"] = False,
    fund_manager_regulator_approved: Annotated[
        bool, "Is the investment manager or KYC-conducting administrator regulated by an AR-list regulator?"
    ] = False,
    government_pension_scheme: Annotated[bool, "Is the entity a government pension scheme?"] = False,
    wholly_state_owned: Annotated[bool, "Is the entity a wholly state-owned entity?"] = False,
) -> dict[str, object]:
    """Walk the Hong Kong CIP schedule decision tree and return the schedule plus the node trace."""
    answers = {
        "is_regulated": is_regulated,
        "regulator_approved": regulator_approved,
        "cross_border_correspondent": cross_border_correspondent,
        "exchange_approved": exchange_approved,
        "fund_manager_regulated": fund_manager_regulated,
        "fund_manager_regulator_approved": fund_manager_regulator_approved,
        "government_pension_scheme": government_pension_scheme,
        "wholly_state_owned": wholly_state_owned,
    }

    node = ENTRY_POINTS.get(entity_type.strip().lower())
    if node is None:
        return {
            "jurisdiction": JURISDICTION,
            "error": f"Unknown entity type '{entity_type}'. Expected one of: {', '.join(sorted(ENTRY_POINTS))}.",
        }

    trace: list[dict[str, object]] = []
    visited: set[str] = set()

    while node.startswith("Q"):
        if node in visited:
            break
        visited.add(node)

        spec = NODES[node]
        answer = bool(answers[spec["input"]])
        trace.append(
            {
                "step": len(trace) + 1,
                "rule_id": spec["rule_id"],
                "node": node,
                "question": spec["question"],
                "answer": "Yes" if answer else "No",
            }
        )
        node = spec["yes"] if answer else spec["no"]

    if not node.startswith("SCHEDULE:"):
        return {
            "jurisdiction": JURISDICTION,
            "entity_type": entity_type,
            "trace": trace,
            "skipped_rules": _skipped(trace),
            "conclusion": "The tree resolves by entity type; select the schedule matching the underlying entity type.",
        }

    clause = node.split(":", 1)[1]
    return {
        "jurisdiction": JURISDICTION,
        "entity_type": entity_type,
        "clause_number": clause,
        "clause_name": SCHEDULES[clause],
        "trace": trace,
        "skipped_rules": _skipped(trace),
        "conclusion": f"Apply '{clause} {SCHEDULES[clause]}' CIP Schedule",
        "sources": ["HK CIP Schedule decision tree", "Approved Regulator list", "Approved Exchange list"],
    }


@tool
def calculate_risk_score(
    ownership_type_score: Annotated[int, "Ownership Type Risk score, max 15"],
    listed_entity_score: Annotated[int, "Listed Entity Risk score, max 2; negative when the exchange is approved"],
    regulated_status_score: Annotated[int, "Regulated Status Risk score, max 2; negative when the regulator is approved"],
    industry_score: Annotated[int, "Industry Risk score, max 65"],
    product_score: Annotated[int, "Product Risk score, max 15"],
    reasons: Annotated[
        list[str],
        "One reason per category, in the order ownership, listed entity, regulated status, industry, product",
    ],
) -> dict[str, object]:
    """Total the five risk categories and return the rating band with per-category detail."""
    scores = {
        "ownership_type_score": ownership_type_score,
        "listed_entity_score": listed_entity_score,
        "regulated_status_score": regulated_status_score,
        "industry_score": industry_score,
        "product_score": product_score,
    }

    categories = []
    for index, (name, key, maximum) in enumerate(RISK_CATEGORIES):
        score = scores[key]
        categories.append(
            {
                "name": name,
                "score": score,
                "max": maximum,
                "indicator": _indicator(score, maximum),
                "reason": reasons[index] if index < len(reasons) else "",
            }
        )

    total = sum(scores.values())
    rating: Literal["Low", "Medium", "High"] = (
        "Low" if total <= LOW_BAND_MAX else "Medium" if total <= MEDIUM_BAND_MAX else "High"
    )

    return {
        "risk_rating": rating,
        "total_score": total,
        "max_possible_score": sum(maximum for _, _, maximum in RISK_CATEGORIES),
        "categories": categories,
    }


def _indicator(score: int, maximum: int) -> str:
    if score <= 0 or maximum == 0:
        return "green"
    ratio = score / maximum
    return "green" if ratio <= 0.34 else "amber" if ratio <= 0.66 else "red"


def _skipped(trace: list[dict[str, object]]) -> list[str]:
    """Decision-tree rules the walk never reached; report them as Not Applicable."""
    visited = {step["rule_id"] for step in trace}
    return [spec["rule_id"] for spec in NODES.values() if spec["rule_id"] not in visited]
