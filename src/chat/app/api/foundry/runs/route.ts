import { NextRequest, NextResponse } from 'next/server'
import { foundryFetch, getFoundryAssistantsApiVersion } from '@/lib/server/azure'

export async function POST(request: NextRequest) {
  try {
    const body = await request.json()
    const threadId = body.threadId
    const payload = { assistant_id: body.assistant_id || body.assistantId, instructions: body.instructions }
    const response = await foundryFetch(`/threads/${encodeURIComponent(threadId)}/runs`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }, getFoundryAssistantsApiVersion())
    const data = await response.json().catch(() => ({}))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}
