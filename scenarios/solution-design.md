# End-to-End Intelligent Onboarding — Solution Design

A focused view of the components that make the client fulfilment & KYC onboarding demo work end to end, across Copilot Studio, Power Platform, Dynamics 365, Azure AI Foundry, Microsoft Fabric and the Intelligence (IQ) layer.

The diagram is available at [solution-design.drawio](solution-design.drawio).

## Layers & components

### 1. Users & experience
- **RM / Onboarding Analyst** — raises and reviews the onboarding case.
- **Microsoft 365 Copilot** — live view of onboarding status for the RM and Ops.
- **Copilot Studio Agent** — conversational front door for onboarding and targeted client outreach.

### 2. Engagement & business process — Power Platform
- **Dynamics 365** — the onboarding case and CRM system of record.
- **Dataverse** — client, legal-entity, affiliate and account records.
- **Power Automate** — flows, approvals and human-in-the-loop escalation.
- **Connectors** — bridge Power Platform to the agent orchestration layer.

### 3. Agent orchestration — Azure AI Foundry Agent Service
- **Onboarding Orchestrator Agent** — coordinates the workflow: intake, KYC/AML, entity resolution and financial review.
- **Client Intake · KYC/AML · Entity Resolution · Financial Review agents** — stage specialists invoked by the orchestrator.
- **MCP Tool Server** — exposes the D365, OneLake and web-search tools the agents call.
- **Foundry Models** — score KYC/AML risk.
- **Document Intelligence** — OCR and validate KYC documents against the policy schema.
- **Observability** — Application Insights / OpenTelemetry tracing across the app.

### 4. Intelligence layer (IQ)
- **Work IQ** — mines the Microsoft 365 estate to pre-populate the case (permission- and sensitivity-aware).
- **Web IQ** — fresh external data: UBO, sanctions / PEP, adverse media and filings.
- **Foundry IQ** — grounds every check in KYC policy; decisions are cited and auditable.
- **Fabric IQ** — models the client / legal-entity ontology and learns onboarding cycle time and straight-through rate.

### 5. Data tier — Microsoft Fabric
- **Fabric OneLake / Lakehouse** — unified supporting data and onboarding outcomes.
- **OneLake Shortcuts** — reach data across Azure, AWS and GCP with no data migration.
- **Fabric Data Agent / MCP** — query the lakehouse to ground decisions.

### 6. Security & governance foundation
Microsoft Entra · Microsoft Purview · Microsoft Defender · Microsoft Sentinel · Microsoft Intune.

## End-to-end flow

1. The RM starts an onboarding case through Copilot Studio / Dynamics 365.
2. Power Platform connectors hand off to the **Onboarding Orchestrator Agent** in Azure AI Foundry.
3. The orchestrator runs the stage agents, calling tools via the **MCP Tool Server** and grounding with the **IQ** layer (Work IQ, Web IQ, Foundry IQ).
4. Foundry models and Document Intelligence score risk and validate documents.
5. Decisions and records are written back to Dynamics 365 / Dataverse; supporting data and outcomes land in **Fabric OneLake**.
6. **Fabric IQ** models the client entity and learns cycle time, tuning the next onboarding — a continuous-improvement loop.
