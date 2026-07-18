import { NextRequest, NextResponse } from 'next/server'
import { normalizeSearchPayload, searchFetch } from '@/lib/server/azure'

export const dynamic = 'force-dynamic'
export const revalidate = 0

interface RouteContext {
  params: Promise<{ id: string }> | { id: string }
}

export async function GET(_request: NextRequest, context: RouteContext) {
  try {
    const params = context.params instanceof Promise ? await context.params : context.params
    const response = await searchFetch(`/agents/${encodeURIComponent(params.id)}`)
    const data = await response.json().catch(() => ({}))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}

export async function PUT(request: NextRequest, context: RouteContext) {
  try {
    const params = context.params instanceof Promise ? await context.params : context.params
    const body = normalizeSearchPayload(await request.json())
    let headers: HeadersInit = {}
    if (typeof body['@odata.etag'] === 'string') {
      headers = { 'If-Match': body['@odata.etag'] }
    }
    const response = await searchFetch(`/agents/${encodeURIComponent(params.id)}`, {
      method: 'PUT',
      headers,
      body: JSON.stringify(body),
    })
    const text = await response.text()
    const data = text ? JSON.parse(text) : { success: response.ok }
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}

export async function DELETE(_request: NextRequest, context: RouteContext) {
  try {
    const params = context.params instanceof Promise ? await context.params : context.params
    const response = await searchFetch(`/agents/${encodeURIComponent(params.id)}`, { method: 'DELETE' })
    const text = await response.text()
    const data = text ? JSON.parse(text) : { success: response.ok }
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}
