import { NextRequest, NextResponse } from 'next/server'
import { normalizeSearchPayload, searchFetch } from '@/lib/server/azure'

export async function POST(request: NextRequest) {
  try {
    const body = normalizeSearchPayload(await request.json())
    const response = await searchFetch(`/knowledgebases/${encodeURIComponent(body.name)}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    })
    const text = await response.text()
    const data = text ? JSON.parse(text) : { success: response.ok, name: body.name }
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}
