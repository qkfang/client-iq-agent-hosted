"""Clone the OnboardingChatAgent Foundry agent into numbered copies."""

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

ENDPOINT = "https://aisa-ciquocsj.services.ai.azure.com/api/projects/aifp-ciquocsj"
SOURCE_AGENT = "OnboardingChatAgent"
COPY_COUNT = 5


def main() -> None:
    client = AIProjectClient(endpoint=ENDPOINT, credential=DefaultAzureCredential())

    source = client.agents.get(SOURCE_AGENT)
    definition = source.versions.latest.definition

    for i in range(1, COPY_COUNT + 1):
        name = f"{SOURCE_AGENT}{i}"
        client.agents.create_version(agent_name=name, definition=definition)
        print(f"Created {name}")


if __name__ == "__main__":
    main()
