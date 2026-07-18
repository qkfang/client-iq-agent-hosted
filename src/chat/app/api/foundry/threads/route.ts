import { NextResponse } from 'next/server'
import { foundryFetch, getFoundryAssistantsApiVersion } from '@/lib/server/azure'

export async function POST(request: Request) {
  try {
    const body = await request.json().catch(() => ({}))
    const response = await foundryFetch('/threads', {
      method: 'POST',
      body: JSON.stringify(body),
    }, getFoundryAssistantsApiVersion())
    const data = await response.json().catch(() => ({}))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}
