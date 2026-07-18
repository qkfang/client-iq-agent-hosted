import { NextRequest, NextResponse } from 'next/server'
import { normalizeSearchPayload, searchFetch } from '@/lib/server/azure'

export const dynamic = 'force-dynamic'
export const revalidate = 0

export async function GET() {
  try {
    const response = await searchFetch('/knowledgesources')
    const data = await response.json().catch(() => ({}))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}

export async function PUT(request: NextRequest) {
  try {
    const body = normalizeSearchPayload(await request.json())
    const sourceName = body.name
    const response = await searchFetch(`/knowledgesources/${encodeURIComponent(sourceName)}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    })
    const data = await response.json().catch(() => ({}))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}
