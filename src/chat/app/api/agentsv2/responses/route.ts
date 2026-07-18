import { NextResponse } from 'next/server'
import { foundryFetch } from '@/lib/server/azure'

export async function POST(request: Request) {
  try {
    const body = await request.json()
    const response = await foundryFetch('/agents/responses', {
      method: 'POST',
      body: JSON.stringify(body),
    })
    const data = await response.json().catch(async () => ({ error: await response.text() }))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}
