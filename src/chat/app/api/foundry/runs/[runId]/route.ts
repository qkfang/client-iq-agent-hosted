import { NextRequest, NextResponse } from 'next/server'
import { foundryFetch, getFoundryAssistantsApiVersion } from '@/lib/server/azure'

interface RouteContext {
  params: Promise<{ runId: string }> | { runId: string }
}

export async function GET(request: NextRequest, context: RouteContext) {
  try {
    const params = context.params instanceof Promise ? await context.params : context.params
    const threadId = request.nextUrl.searchParams.get('threadId')
    if (!threadId) {
      return NextResponse.json({ error: 'threadId is required' }, { status: 400 })
    }
    const response = await foundryFetch(`/threads/${encodeURIComponent(threadId)}/runs/${encodeURIComponent(params.runId)}`, {}, getFoundryAssistantsApiVersion())
    const data = await response.json().catch(() => ({}))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}
