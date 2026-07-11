# Intelligent Client Fulfilment & KYC Onboarding (on Dynamics 365)

Ready to trade, faster: resolve the entity, pre-populate from what the firm already knows, screen the outside world, decide & set up.

A companion draw.io diagram is available at [use-case-architecture.drawio](use-case-architecture.drawio).

## Business process — Front → Middle → Back office

| # | Stage | Office | What happens |
|---|-------|--------|--------------|
| 1 | Initiate & resolve entity | Front | An RM raises a new-client onboarding case in Dynamics 365. A Copilot Studio / Foundry agent resolves the legal entity and maps its affiliate and related-party structure. Web IQ verifies the entity against external registries; Work IQ checks whether the firm already has a relationship with the entity or its affiliates. |
| 2 | Pre-populate from the estate | Front → Middle | Work IQ mines the firm's own Microsoft 365 estate — existing client folders, correspondence, briefing notes and prior KYC held on affiliated entities — to pre-fill the D365 case and draft onboarding documentation, respecting permissions and sensitivity labels. The client is asked only for what the firm does not already hold. |
| 3 | Screen & financial review | Middle | Web IQ pulls fresh external data — ultimate beneficial ownership, sanctions / PEP, adverse media, corporate filings and financials. An Azure AI Foundry model scores KYC / AML risk and runs the financial review, with Foundry IQ grounding every check in the firm's KYC policy. |
| 4 | Collect & validate documents | Middle | The agent requests only the missing documents. Azure AI Foundry Document Intelligence OCRs and validates the KYC documents against the policy schema and deduplicates forwarded chains; exceptions are escalated to an analyst (human-in-the-loop). |
| 5 | Decide & set up in D365 | Middle → Back | The KYC decision is made and orchestrated across Dynamics 365, onboarding, legal and compliance. Client, legal-entity, affiliate and account records are created. Foundry IQ makes every decision cited, permission-aware and audit-ready. |
| 6 | Ready to transact & learn | Back | The client is marked ready to trade and the regulatory record is filed. Fabric IQ models the client entity and learns onboarding cycle time and straight-through-onboarding rate, tuning the process; Microsoft 365 Copilot gives the RM and Ops a live view. |

## The Microsoft intelligence layer

The engine powering every stage — continuous improvement means outcomes tune the next decision.

| Intelligence layer | How it is showcased in this use case |
|--------------------|--------------------------------------|
| Data tier — OneLake + D365 | Dynamics 365 / Dataverse is the CRM system of record for the client, legal-entity and affiliate records; OneLake unifies the supporting data across the multi-cloud estate. |
| Work IQ | Mines the firm's own M365 estate — correspondence, briefing notes, prior KYC on affiliates — to pre-populate the case and documents, so the firm never re-asks for what it already holds (permission- and sensitivity-aware). |
| Web IQ | Brings fresh external intelligence on the new customer — company registries, UBO, affiliates, sanctions / PEP, adverse media and filings. |
| AI (Foundry) | Scores KYC / AML risk, resolves entities and their affiliates, and reads and validates documents (Document Intelligence). |
| Agents | Orchestrate the onboarding end to end — entity resolution, pre-population, targeted client outreach, cross-platform setup and escalation. |
| Foundry IQ | Grounds every check in the firm's KYC / AML policy and makes each decision cited, permission-aware and audit-ready. |
| Fabric IQ | Models the client / legal-entity ontology and learns onboarding cycle time and straight-through rate to tune the process. |

## Security & governance foundation

Microsoft Entra · Microsoft Purview · Microsoft Defender · Microsoft Sentinel · Microsoft Intune.

Runs across Azure · AWS · GCP (multi-cloud, no data migration).

## Data tier (shared reality)

Dynamics 365 / Dataverse is the CRM system of record; Fabric OneLake + Shortcuts unify supporting data across the estate.
