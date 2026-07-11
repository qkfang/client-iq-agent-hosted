#!/usr/bin/env python3
"""
Onboarding-form pipeline agents setup step for the Microsoft IQ deployment.

Creates the AI Foundry agents that process an incoming onboarding form once
it lands in blob storage: IntakeAgent extracts a structured JSON summary,
OrchestratorAgent routes that summary to a domain agent, and the domain
agents (OpportunityAgent, InsightAgent, CrmAgent, LegoAgent) act on it.
OnboardingAgent itself is created separately by
``step_onboarding_agent_setup.py``.
"""

import logging

from foundry.agent_api import (
    CRM_AGENT_NAME,
    INSIGHT_AGENT_NAME,
    INTAKE_AGENT_NAME,
    LEGO_AGENT_NAME,
    OPPORTUNITY_AGENT_NAME,
    ORCHESTRATOR_AGENT_NAME,
    build_domain_agent_instructions,
    build_intake_agent_instructions,
    build_orchestrator_agent_instructions,
    create_agent_client,
    create_or_update_agent,
)

# Module-level logger — inherits configuration from the root logger set up
# by setup_logging() in the entry-point scripts.
logger = logging.getLogger(__name__)

# Domain agents invoked by OrchestratorAgent, keyed by agent name, with a
# short description of what each one is responsible for.
_DOMAIN_AGENT_ROLES = {
    OPPORTUNITY_AGENT_NAME: "review and qualify sales opportunities, upsells, and renewals",
    INSIGHT_AGENT_NAME: "summarize reporting and analytics requests raised in onboarding forms",
    CRM_AGENT_NAME: "apply and confirm customer relationship data updates or corrections",
    LEGO_AGENT_NAME: "identify and compose reusable onboarding workflow building blocks",
}


def setup_pipeline_agents(
    *,
    solution_name: str,
    agent_endpoint: str,
    agent_model: str,
) -> None:
    """Create or update the onboarding-form pipeline agents.

    Creates IntakeAgent, OrchestratorAgent, and the domain agents
    (OpportunityAgent, InsightAgent, CrmAgent, LegoAgent). None of these
    agents require the Knowledge Base MCP tool — they operate purely on the
    JSON handed between pipeline stages.

    Args:
        solution_name: Overall generic name for the project/solution.
        agent_endpoint: Azure AI Project endpoint URL.
        agent_model: Chat model deployment name for the agents.
    """
    logger.info("   Initialising AI Project client…")
    _agent_client = create_agent_client(agent_endpoint)

    with _agent_client:
        logger.info(f"   Creating agent '{INTAKE_AGENT_NAME}'…")
        _intake = create_or_update_agent(
            project_client=_agent_client,
            agent_name=INTAKE_AGENT_NAME,
            model=agent_model,
            instructions=build_intake_agent_instructions(solution_name),
            tools=[],
        )
        logger.info(f"   Agent '{_intake.name}' ready (id: {_intake.id})")

        logger.info(f"   Creating agent '{ORCHESTRATOR_AGENT_NAME}'…")
        _orchestrator = create_or_update_agent(
            project_client=_agent_client,
            agent_name=ORCHESTRATOR_AGENT_NAME,
            model=agent_model,
            instructions=build_orchestrator_agent_instructions(solution_name),
            tools=[],
        )
        logger.info(f"   Agent '{_orchestrator.name}' ready (id: {_orchestrator.id})")

        for agent_name, agent_role in _DOMAIN_AGENT_ROLES.items():
            logger.info(f"   Creating agent '{agent_name}'…")
            _domain_agent = create_or_update_agent(
                project_client=_agent_client,
                agent_name=agent_name,
                model=agent_model,
                instructions=build_domain_agent_instructions(solution_name, agent_role),
                tools=[],
            )
            logger.info(f"   Agent '{_domain_agent.name}' ready (id: {_domain_agent.id})")
