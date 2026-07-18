import {
  ClientSecretCredential,
  DefaultAzureCredential,
  TokenCredential,
} from '@azure/identity'

const SEARCH_SCOPE = 'https://search.azure.com/.default'
const FOUNDRY_SCOPE = 'https://ai.azure.com/.default'
const MANAGEMENT_SCOPE = 'https://management.azure.com/.default'

const DEFAULT_SEARCH_API_VERSION = '2025-11-01-preview'
const DEFAULT_FOUNDRY_API_VERSION = '2025-05-01'
const DEFAULT_FOUNDRY_ASSISTANTS_API_VERSION = '2025-05-01'

let credential: TokenCredential | null = null

function getCredential(): TokenCredential {
  if (credential) return credential

  const method = process.env.AZURE_AUTH_METHOD || 'default'
  const tenantId = process.env.AZURE_TENANT_ID

  if (method === 'service-principal') {
    const clientId = process.env.AZURE_CLIENT_ID
    const clientSecret = process.env.AZURE_CLIENT_SECRET
    credential =
      tenantId && clientId && clientSecret
        ? new ClientSecretCredential(tenantId, clientId, clientSecret)
        : new DefaultAzureCredential(tenantId ? { tenantId } : {})
  } else if (method === 'managed-identity') {
    const managedIdentityClientId = process.env.AZURE_MANAGED_IDENTITY_CLIENT_ID
    // Include tenant id in the managed identity token request, matching the web/func app
    // (DefaultAzureCredentialOptions.TenantId). The JS ManagedIdentityCredential has no
    // tenantId option, so DefaultAzureCredential is used to carry both tenant and client id.
    credential = new DefaultAzureCredential({
      ...(tenantId ? { tenantId } : {}),
      ...(managedIdentityClientId ? { managedIdentityClientId } : {}),
    })
  } else {
    credential = new DefaultAzureCredential(tenantId ? { tenantId } : {})
  }

  return credential
}

const tokenCache = new Map<string, { token: string; expiresOn: number }>()

async function getToken(scope: string): Promise<string> {
  const now = Date.now()
  const cached = tokenCache.get(scope)
  if (cached && cached.expiresOn - now > 5 * 60 * 1000) {
    return cached.token
  }

  const result = await getCredential().getToken(scope)
  if (!result) {
    throw new Error(`Failed to acquire token for scope ${scope}`)
  }

  tokenCache.set(scope, {
    token: result.token,
    expiresOn: result.expiresOnTimestamp ?? now + 60 * 60 * 1000,
  })
  return result.token
}

function appendApiVersion(url: string, apiVersion: string): string {
  return `${url}${url.includes('?') ? '&' : '?'}api-version=${apiVersion}`
}

function trimTrailingSlash(value: string): string {
  return value.replace(/\/$/, '')
}

function getSearchEndpoint(): string {
  const endpoint = process.env.AZURE_AI_SEARCH_ENDPOINT || process.env.AZURE_SEARCH_ENDPOINT
  if (!endpoint) {
    throw new Error('AZURE_AI_SEARCH_ENDPOINT is not configured')
  }
  return trimTrailingSlash(endpoint)
}

function getFoundryEndpoint(): string {
  const endpoint =
    process.env.FOUNDRY_PROJECT_ENDPOINT ||
    process.env.AZURE_AI_PROJECT_ENDPOINT ||
    process.env.AZURE_AI_AGENT_ENDPOINT
  if (!endpoint) {
    throw new Error('FOUNDRY_PROJECT_ENDPOINT is not configured')
  }
  return trimTrailingSlash(endpoint)
}

export function getFoundryAssistantsApiVersion(): string {
  return process.env.FOUNDRY_ASSISTANTS_API_VERSION || DEFAULT_FOUNDRY_ASSISTANTS_API_VERSION
}

async function authorizedFetch(
  url: string,
  scope: string,
  init: RequestInit,
  apiKey?: string
): Promise<Response> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...((init.headers as Record<string, string> | undefined) || {}),
  }

  if (apiKey) {
    headers['api-key'] = apiKey
  } else {
    headers['Authorization'] = `Bearer ${await getToken(scope)}`
  }

  return fetch(url, { ...init, headers, cache: 'no-store' })
}

export async function searchFetch(path: string, init: RequestInit = {}): Promise<Response> {
  const apiVersion = process.env.AZURE_SEARCH_API_VERSION || DEFAULT_SEARCH_API_VERSION
  const url = appendApiVersion(`${getSearchEndpoint()}${path}`, apiVersion)
  return authorizedFetch(url, SEARCH_SCOPE, init, process.env.AZURE_SEARCH_API_KEY)
}

export async function foundryFetch(
  path: string,
  init: RequestInit = {},
  apiVersion?: string
): Promise<Response> {
  const version = apiVersion || process.env.FOUNDRY_API_VERSION || DEFAULT_FOUNDRY_API_VERSION
  const url = appendApiVersion(`${getFoundryEndpoint()}${path}`, version)
  return authorizedFetch(url, FOUNDRY_SCOPE, init)
}

export async function managementFetch(url: string, init: RequestInit = {}): Promise<Response> {
  return authorizedFetch(url, MANAGEMENT_SCOPE, init)
}

export function normalizeSearchPayload<T>(payload: T): T {
  if (payload === null || typeof payload !== 'object') {
    return payload
  }

  if (Array.isArray(payload)) {
    return payload.map((item) => normalizeSearchPayload(item)) as unknown as T
  }

  const result: Record<string, unknown> = {}
  for (const [key, value] of Object.entries(payload as Record<string, unknown>)) {
    if (value === undefined) continue
    result[key] = normalizeSearchPayload(value)
  }
  return result as T
}
