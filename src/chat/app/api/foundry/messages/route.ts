import { NextRequest, NextResponse } from 'next/server'
import { foundryFetch, getFoundryAssistantsApiVersion } from '@/lib/server/azure'

export async function GET(request: NextRequest) {
  try {
    const threadId = request.nextUrl.searchParams.get('threadId')
    if (!threadId) {
      return NextResponse.json({ error: 'threadId is required' }, { status: 400 })
    }
    const response = await foundryFetch(`/threads/${encodeURIComponent(threadId)}/messages`, {}, getFoundryAssistantsApiVersion())
    const data = await response.json().catch(() => ({}))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}

export async function POST(request: NextRequest) {
  try {
    const body = await request.json()
    const threadId = body.threadId
    const payload = { role: body.role || 'user', content: body.content }
    const response = await foundryFetch(`/threads/${encodeURIComponent(threadId)}/messages`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }, getFoundryAssistantsApiVersion())
    const data = await response.json().catch(() => ({}))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}
