'use client'

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { Bot20Regular, Play20Regular, Add20Regular, ArrowClockwise20Regular } from '@fluentui/react-icons'
import { PageHeader } from '@/components/shared/page-header'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { EmptyState } from '@/components/shared/empty-state'
import { LoadingSkeleton } from '@/components/shared/loading-skeleton'
import { ErrorState } from '@/components/shared/error-state'

interface AssistantRecord {
  id: string
  name?: string
  model?: string
  tools?: unknown[]
}

export default function AgentsPage() {
  const [assistants, setAssistants] = useState<AssistantRecord[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const loadAssistants = async () => {
    try {
      setLoading(true)
      setError(null)
      const response = await fetch('/api/foundry/assistants', { cache: 'no-store' })
      if (!response.ok) {
        throw new Error('Failed to load agents')
      }
      const data = await response.json()
      setAssistants(data.data || data.value || [])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load agents')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadAssistants()
  }, [])

  if (loading) {
    return <LoadingSkeleton />
  }

  if (error) {
    return <ErrorState title="Failed to load agents" description={error} action={{ label: 'Try again', onClick: loadAssistants }} />
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Agents"
        description="Create and reuse Azure AI Foundry assistants connected to your knowledge bases."
        primaryAction={{ label: 'Open agent builder', href: '/agent-builder', icon: Add20Regular }}
        secondaryAction={{ label: 'Refresh', onClick: loadAssistants, icon: ArrowClockwise20Regular }}
      />

      {assistants.length === 0 ? (
        <EmptyState
          title="No agents yet"
          description="Create your first assistant in the agent builder."
          action={{ label: 'Create agent', onClick: () => { window.location.href = '/agent-builder' } }}
        />
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {assistants.map((assistant) => (
            <Card key={assistant.id} className="transition-shadow hover:shadow-lg">
              <CardHeader>
                <div className="flex items-center gap-3">
                  <div className="rounded-full bg-accent-subtle p-3 text-accent"><Bot20Regular /></div>
                  <div>
                    <CardTitle className="text-base">{assistant.name || assistant.id}</CardTitle>
                    <p className="text-sm text-fg-muted">{assistant.model || 'Configured in Foundry'}</p>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                <p className="text-sm text-fg-muted">{Array.isArray(assistant.tools) ? `${assistant.tools.length} tool${assistant.tools.length === 1 ? '' : 's'} configured` : 'Assistant ready for chat'}</p>
                <div className="flex gap-2">
                  <Button asChild size="sm">
                    <Link href={`/playground?agent=${encodeURIComponent(assistant.id)}`}>
                      <Play20Regular className="h-4 w-4" />
                      Chat
                    </Link>
                  </Button>
                  <Button asChild variant="outline" size="sm">
                    <Link href={`/agent-builder?assistantId=${encodeURIComponent(assistant.id)}&mode=playground`}>
                      Edit
                    </Link>
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
