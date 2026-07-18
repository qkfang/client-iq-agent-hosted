import { NextRequest, NextResponse } from 'next/server'
import { searchFetch } from '@/lib/server/azure'

export const dynamic = 'force-dynamic'
export const revalidate = 0

interface RouteContext {
  params: Promise<{ id: string }> | { id: string }
}

export async function POST(request: NextRequest, context: RouteContext) {
  try {
    const params = context.params instanceof Promise ? await context.params : context.params
    const body = await request.json()
    const response = await searchFetch(`/knowledgebases/${encodeURIComponent(params.id)}/retrieve`, {
      method: 'POST',
      body: JSON.stringify(body),
    })
    const text = await response.text()
    const data = text ? JSON.parse(text) : {}
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}
