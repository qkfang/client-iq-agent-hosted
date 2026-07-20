#!/bin/bash

# Configure data-plane RBAC for the deployed application identities.
# Grants the Web App and Function App managed identities access to Azure
# OpenAI (AI Services), Azure AI Search and Storage.
#
# Reads configuration from the azd environment (Bicep outputs), with plain
# environment-variable fallbacks so the script can also be run standalone.

set -e

echo "======================================"
echo "Configure RBAC Permissions"
echo "======================================"
echo ""

# Resolve configuration from azd-provided environment variables, with fallbacks.
RESOURCE_GROUP="${RESOURCE_GROUP:-$AZURE_RESOURCE_GROUP}"
SUBSCRIPTION_ID="${SUBSCRIPTION_ID:-${AZURE_SUBSCRIPTION_ID:-$(az account show --query id -o tsv)}}"
WEB_APP_NAME="${WEB_APP_NAME:-$AZURE_WEB_APP_NAME}"
FUNCTION_APP_NAME="${FUNCTION_APP_NAME:-$AZURE_FUNCTION_APP_NAME}"
AI_SERVICE_NAME="${AI_SERVICE_NAME}"
SEARCH_SERVICE_NAME="${SEARCH_SERVICE_NAME:-$AZURE_AI_SEARCH_NAME}"
STORAGE_ACCOUNT_NAME="${STORAGE_ACCOUNT_NAME:-$AZURE_STORAGE_ACCOUNT_NAME}"

if [ -z "$RESOURCE_GROUP" ]; then
    echo "Error: RESOURCE_GROUP (or AZURE_RESOURCE_GROUP) is not set."
    exit 1
fi

echo "Configuration:"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Web App: $WEB_APP_NAME"
echo "  Function App: $FUNCTION_APP_NAME"
echo ""

# Role definition IDs
COGNITIVE_SERVICES_USER="a97b65f3-24c7-4388-baec-2e87135dc908"
SEARCH_INDEX_DATA_CONTRIBUTOR="8ebe5a00-799e-43f5-93ac-243d3dce84a7"
STORAGE_BLOB_DATA_CONTRIBUTOR="ba92f5b4-2d11-453d-a403-e96b0029c9fe"

# Resource scopes
AI_SERVICE_SCOPE="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.CognitiveServices/accounts/$AI_SERVICE_NAME"
SEARCH_SCOPE="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Search/searchServices/$SEARCH_SERVICE_NAME"
STORAGE_SCOPE="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT_NAME"

assign_role() {
    # $1 = principalId, $2 = roleId, $3 = scope, $4 = description
    [ -z "$1" ] && return 0
    echo "  - $4"
    az role assignment create \
        --assignee-object-id "$1" \
        --assignee-principal-type ServicePrincipal \
        --role "$2" \
        --scope "$3" \
        --output none 2>/dev/null || echo "    (already exists or insufficient permissions)"
}

grant_app_access() {
    # $1 = app principalId, $2 = app label
    local principal="$1"
    local label="$2"
    if [ -z "$principal" ] || [ "$principal" = "null" ]; then
        echo "Skipping $label (no managed identity found)"
        return 0
    fi
    echo "Assigning roles to $label..."
    [ -n "$AI_SERVICE_NAME" ] && assign_role "$principal" "$COGNITIVE_SERVICES_USER" "$AI_SERVICE_SCOPE" "Cognitive Services User on $AI_SERVICE_NAME"
    [ -n "$SEARCH_SERVICE_NAME" ] && assign_role "$principal" "$SEARCH_INDEX_DATA_CONTRIBUTOR" "$SEARCH_SCOPE" "Search Index Data Contributor on $SEARCH_SERVICE_NAME"
    [ -n "$STORAGE_ACCOUNT_NAME" ] && assign_role "$principal" "$STORAGE_BLOB_DATA_CONTRIBUTOR" "$STORAGE_SCOPE" "Storage Blob Data Contributor on $STORAGE_ACCOUNT_NAME"
    echo ""
}

echo "[1/2] Configuring Web App identity..."
WEB_PRINCIPAL=""
if [ -n "$WEB_APP_NAME" ]; then
    WEB_PRINCIPAL=$(az webapp identity show -n "$WEB_APP_NAME" -g "$RESOURCE_GROUP" --query principalId -o tsv 2>/dev/null || echo "")
fi
grant_app_access "$WEB_PRINCIPAL" "Web App ($WEB_APP_NAME)"

echo "[2/2] Configuring Function App identity..."
FUNC_PRINCIPAL=""
if [ -n "$FUNCTION_APP_NAME" ]; then
    FUNC_PRINCIPAL=$(az functionapp identity show -n "$FUNCTION_APP_NAME" -g "$RESOURCE_GROUP" --query principalId -o tsv 2>/dev/null || echo "")
fi
grant_app_access "$FUNC_PRINCIPAL" "Function App ($FUNCTION_APP_NAME)"

echo "======================================"
echo "RBAC configuration complete."
echo "======================================"
