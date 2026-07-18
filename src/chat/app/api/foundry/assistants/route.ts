import { NextRequest, NextResponse } from 'next/server'
import { foundryFetch, getFoundryAssistantsApiVersion } from '@/lib/server/azure'

export const dynamic = 'force-dynamic'
export const revalidate = 0

export async function GET() {
  try {
    const response = await foundryFetch('/assistants', {}, getFoundryAssistantsApiVersion())
    const data = await response.json().catch(() => ({}))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}

export async function POST(request: NextRequest) {
  try {
    const body = await request.json()
    const response = await foundryFetch('/assistants', {
      method: 'POST',
      body: JSON.stringify(body),
    }, getFoundryAssistantsApiVersion())
    const data = await response.json().catch(() => ({}))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}
