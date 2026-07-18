import { NextRequest, NextResponse } from 'next/server'
import { searchFetch } from '@/lib/server/azure'

export const dynamic = 'force-dynamic'
export const revalidate = 0

interface RouteContext {
  params: Promise<{ sourceName: string }> | { sourceName: string }
}

export async function GET(_request: NextRequest, context: RouteContext) {
  try {
    const params = context.params instanceof Promise ? await context.params : context.params
    const response = await searchFetch(`/knowledgesources('${params.sourceName}')/status`)
    const data = await response.json().catch(() => ({}))
    return NextResponse.json(data, { status: response.status })
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Internal server error' }, { status: 500 })
  }
}
