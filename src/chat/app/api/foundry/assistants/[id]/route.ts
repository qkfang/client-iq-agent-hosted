import { NextRequest, NextResponse } from 'next/server'
import { foundryFetch, getFoundryAssistantsApiVersion } from '@/lib/server/azure'

export const dynamic = 'force-dynamic'
export const revalidate = 0

interface RouteContext {
  params: Promise<{ id: string }> | { id: string }
}

export async function GET(_request: NextRequest, context: RouteContext) {
  try {
    const params = context.params instanceof Promise ? await context.params : context.params
    const response = await foundryFetch(`/assistants/${encodeURIComponent(params.id)}`, {}, getFoundryAssistantsApiVersion())
    const data = await response.json().catch(() => ({}))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}

export async function PUT(request: NextRequest, context: RouteContext) {
  try {
    const params = context.params instanceof Promise ? await context.params : context.params
    const body = await request.json()
    const response = await foundryFetch(`/assistants/${encodeURIComponent(params.id)}`, {
      method: 'POST',
      body: JSON.stringify(body),
    }, getFoundryAssistantsApiVersion())
    const data = await response.json().catch(() => ({}))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}

export async function DELETE(_request: NextRequest, context: RouteContext) {
  try {
    const params = context.params instanceof Promise ? await context.params : context.params
    const response = await foundryFetch(`/assistants/${encodeURIComponent(params.id)}`, {
      method: 'DELETE',
    }, getFoundryAssistantsApiVersion())
    const data = await response.json().catch(() => ({ success: response.ok }))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}
