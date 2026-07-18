import { NextResponse } from 'next/server'
import { managementFetch } from '@/lib/server/azure'

export async function GET() {
  try {
    const resourceId = process.env.AI_FOUNDRY_RESOURCE_ID
    const projectName = process.env.AZURE_AI_PROJECT_NAME
    if (!resourceId || !projectName) {
      return NextResponse.json({ error: 'AI_FOUNDRY_RESOURCE_ID and AZURE_AI_PROJECT_NAME are required' }, { status: 500 })
    }

    const url = `https://management.azure.com${resourceId}/projects/${projectName}/connections?api-version=2025-04-01-preview`
    const response = await managementFetch(url)
    const data = await response.json().catch(async () => ({ error: await response.text() }))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}
