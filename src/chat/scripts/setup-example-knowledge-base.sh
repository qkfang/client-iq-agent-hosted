#!/bin/bash

# Create an example Knowledge Source, Knowledge Base and Knowledge Agent on the
# Azure AI Search + AI Foundry resources already deployed by infra/main.bicep,
# then run a retrieval query to verify the setup works end to end.
#
# Reuses the storage account, AI Search service and AI Foundry model deployments
# that are already provisioned for this repo (see infra/deploy_ai_foundry.bicep).
# Run this after `azd up` / the Bicep deployment completes and after
# `az login` against the target subscription.

set -e

echo "======================================"
echo "Azure AI Search - Example Knowledge Base Setup"
echo "======================================"
echo ""

# Resolve configuration from environment, falling back to Bicep deployment outputs.
if [ -n "$RESOURCE_GROUP" ] && [ -n "$DEPLOYMENT_NAME" ]; then
    echo "[0/6] Reading configuration from deployment outputs..."
    outputs=$(az deployment group show -g "$RESOURCE_GROUP" -n "$DEPLOYMENT_NAME" --query properties.outputs -o json)
    SEARCH_ENDPOINT=${SEARCH_ENDPOINT:-$(echo "$outputs" | jq -r '.AZURE_AI_SEARCH_ENDPOINT.value // empty')}
    STORAGE_ACCOUNT_NAME=${STORAGE_ACCOUNT_NAME:-$(echo "$outputs" | jq -r '.AZURE_STORAGE_ACCOUNT_NAME.value // empty')}
    AZURE_OPENAI_ENDPOINT=${AZURE_OPENAI_ENDPOINT:-$(echo "$outputs" | jq -r '.AZURE_OPENAI_ENDPOINT.value // empty')}
    EMBEDDING_MODEL=${EMBEDDING_MODEL:-$(echo "$outputs" | jq -r '.AZURE_OPENAI_EMBEDDING_MODEL.value // empty')}
    CHAT_MODEL=${CHAT_MODEL:-$(echo "$outputs" | jq -r '.AZURE_OPENAI_DEPLOYMENT_MODEL.value // empty')}
fi

# Fall back to azd-provided environment variables (Bicep outputs) when the
# deployment-output lookup above was skipped or returned nothing.
SEARCH_ENDPOINT=${SEARCH_ENDPOINT:-$AZURE_AI_SEARCH_ENDPOINT}
STORAGE_ACCOUNT_NAME=${STORAGE_ACCOUNT_NAME:-$AZURE_STORAGE_ACCOUNT_NAME}
EMBEDDING_MODEL=${EMBEDDING_MODEL:-$AZURE_OPENAI_EMBEDDING_MODEL}
CHAT_MODEL=${CHAT_MODEL:-$AZURE_OPENAI_DEPLOYMENT_MODEL}

STORAGE_CONTAINER_NAME=${STORAGE_CONTAINER_NAME:-knowledge-base-example}
KNOWLEDGE_SOURCE_NAME=${KNOWLEDGE_SOURCE_NAME:-hr-benefits-example}
KNOWLEDGE_BASE_NAME=${KNOWLEDGE_BASE_NAME:-hr-benefits-example}
KNOWLEDGE_AGENT_NAME=${KNOWLEDGE_AGENT_NAME:-hr-benefits-example-agent}
SEARCH_API_VERSION=${SEARCH_API_VERSION:-2025-11-01-preview}
EMBEDDING_MODEL=${EMBEDDING_MODEL:-text-embedding-3-small}
CHAT_MODEL=${CHAT_MODEL:-gpt-5-mini}

if [ -z "$SEARCH_ENDPOINT" ] || [ -z "$STORAGE_ACCOUNT_NAME" ] || [ -z "$AZURE_OPENAI_ENDPOINT" ]; then
    echo "Error: Required configuration not resolved."
    echo ""
    echo "Set the following environment variables, or provide RESOURCE_GROUP and"
    echo "DEPLOYMENT_NAME so they can be read from the Bicep deployment outputs:"
    echo "  export SEARCH_ENDPOINT='https://<search-service>.search.windows.net'"
    echo "  export STORAGE_ACCOUNT_NAME='<storage-account-name>'"
    echo "  export AZURE_OPENAI_ENDPOINT='https://<ai-account>.openai.azure.com/'"
    exit 1
fi

SUBSCRIPTION_ID=$(az account show --query id -o tsv)
RESOURCE_GROUP=${RESOURCE_GROUP:-$(az storage account show --name "$STORAGE_ACCOUNT_NAME" --query resourceGroup -o tsv)}
STORAGE_RESOURCE_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT_NAME"
SEARCH_TOKEN=$(az account get-access-token --resource https://search.azure.com --query accessToken -o tsv)

echo "Configuration:"
echo "  Search Endpoint: $SEARCH_ENDPOINT"
echo "  Storage Account: $STORAGE_ACCOUNT_NAME"
echo "  Container: $STORAGE_CONTAINER_NAME"
echo "  Embedding Model: $EMBEDDING_MODEL"
echo "  Chat Model: $CHAT_MODEL"
echo ""

# Step 1: Download a small, generic HR knowledge-base sample from the public
# Azure-Samples repo (fictional company data, no customer information).
echo "[1/6] Downloading example knowledge base documents..."
SAMPLE_BASE_URL="https://raw.githubusercontent.com/Azure-Samples/azure-search-openai-demo/main/data"
SAMPLE_FILES=("employee_handbook.pdf" "PerksPlus.pdf")

mkdir -p ./sample-data/knowledge-base-example
for file in "${SAMPLE_FILES[@]}"; do
    curl -fL "$SAMPLE_BASE_URL/$file" -o "./sample-data/knowledge-base-example/$file"
    echo "✓ Downloaded: $file"
done

# Step 2: Upload the documents to the deployed storage account.
echo ""
echo "[2/6] Uploading documents to Azure Blob Storage..."
az storage container create \
    --account-name "$STORAGE_ACCOUNT_NAME" \
    --name "$STORAGE_CONTAINER_NAME" \
    --auth-mode login \
    --output none

for file in "${SAMPLE_FILES[@]}"; do
    az storage blob upload \
        --account-name "$STORAGE_ACCOUNT_NAME" \
        --container-name "$STORAGE_CONTAINER_NAME" \
        --name "$file" \
        --file "./sample-data/knowledge-base-example/$file" \
        --auth-mode login \
        --overwrite \
        --output none
done
echo "✓ Uploaded ${#SAMPLE_FILES[@]} document(s) to $STORAGE_CONTAINER_NAME"

# Step 3: Create the Knowledge Source, pointing at the uploaded container and
# reusing the embedding/chat model deployments from the AI Foundry resource.
echo ""
echo "[3/6] Creating knowledge source '$KNOWLEDGE_SOURCE_NAME'..."
KNOWLEDGE_SOURCE_BODY=$(cat <<EOF
{
  "name": "$KNOWLEDGE_SOURCE_NAME",
  "kind": "azureBlob",
  "description": "Example HR knowledge base reusing the deployed storage account",
  "azureBlobParameters": {
    "connectionString": "ResourceId=$STORAGE_RESOURCE_ID",
    "containerName": "$STORAGE_CONTAINER_NAME",
    "embeddingModel": {
      "name": "$EMBEDDING_MODEL",
      "kind": "azureOpenAI",
      "azureOpenAIParameters": {
        "resourceUri": "$AZURE_OPENAI_ENDPOINT",
        "deploymentId": "$EMBEDDING_MODEL",
        "modelName": "$EMBEDDING_MODEL"
      }
    },
    "chatCompletionModel": {
      "kind": "azureOpenAI",
      "azureOpenAIParameters": {
        "resourceUri": "$AZURE_OPENAI_ENDPOINT",
        "deploymentId": "$CHAT_MODEL",
        "modelName": "$CHAT_MODEL"
      }
    }
  }
}
EOF
)

curl -fsS -X PUT "$SEARCH_ENDPOINT/knowledgesources/$KNOWLEDGE_SOURCE_NAME?api-version=$SEARCH_API_VERSION" \
    -H "Authorization: Bearer $SEARCH_TOKEN" \
    -H "Content-Type: application/json" \
    -d "$KNOWLEDGE_SOURCE_BODY" -o /dev/null
echo "✓ Created knowledge source: $KNOWLEDGE_SOURCE_NAME"

# Step 4: Create the Knowledge Base referencing the knowledge source above.
echo ""
echo "[4/6] Creating knowledge base '$KNOWLEDGE_BASE_NAME'..."
KNOWLEDGE_BASE_BODY=$(cat <<EOF
{
  "name": "$KNOWLEDGE_BASE_NAME",
  "description": "Example HR knowledge base for employee benefits questions",
  "retrievalInstructions": "Answer employee benefits and HR policy questions using the provided documents.",
  "knowledgeSources": [
    { "name": "$KNOWLEDGE_SOURCE_NAME" }
  ]
}
EOF
)

curl -fsS -X PUT "$SEARCH_ENDPOINT/knowledgebases/$KNOWLEDGE_BASE_NAME?api-version=$SEARCH_API_VERSION" \
    -H "Authorization: Bearer $SEARCH_TOKEN" \
    -H "Content-Type: application/json" \
    -d "$KNOWLEDGE_BASE_BODY" -o /dev/null
echo "✓ Created knowledge base: $KNOWLEDGE_BASE_NAME"

# Step 5: Create a Knowledge Agent that answers questions using the source.
echo ""
echo "[5/6] Creating knowledge agent '$KNOWLEDGE_AGENT_NAME'..."
KNOWLEDGE_AGENT_BODY=$(cat <<EOF
{
  "name": "$KNOWLEDGE_AGENT_NAME",
  "description": "Example agent that answers HR benefits questions",
  "models": [{
    "kind": "azureOpenAI",
    "azureOpenAIParameters": {
      "resourceUri": "$AZURE_OPENAI_ENDPOINT",
      "deploymentId": "$CHAT_MODEL",
      "modelName": "$CHAT_MODEL"
    }
  }],
  "knowledgeSources": [
    { "name": "$KNOWLEDGE_SOURCE_NAME", "includeReferences": true }
  ],
  "outputMode": "answerSynthesis"
}
EOF
)

curl -fsS -X PUT "$SEARCH_ENDPOINT/agents/$KNOWLEDGE_AGENT_NAME?api-version=$SEARCH_API_VERSION" \
    -H "Authorization: Bearer $SEARCH_TOKEN" \
    -H "Content-Type: application/json" \
    -d "$KNOWLEDGE_AGENT_BODY" -o /dev/null
echo "✓ Created knowledge agent: $KNOWLEDGE_AGENT_NAME"

# Step 6: Test the knowledge base with a sample retrieval query.
echo ""
echo "[6/6] Testing retrieval against the knowledge base..."
RETRIEVE_BODY='{
  "messages": [
    { "role": "user", "content": [{ "type": "text", "text": "What perks does PerksPlus cover?" }] }
  ]
}'

curl -fsS -X POST "$SEARCH_ENDPOINT/knowledgebases/$KNOWLEDGE_BASE_NAME/retrieve?api-version=$SEARCH_API_VERSION" \
    -H "Authorization: Bearer $SEARCH_TOKEN" \
    -H "Content-Type: application/json" \
    -d "$RETRIEVE_BODY"

echo ""
echo "✓ Retrieval test completed. Review the response above for a synthesized answer."
