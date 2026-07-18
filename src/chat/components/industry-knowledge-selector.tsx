'use client'

import { useState } from 'react'
import { motion } from 'framer-motion'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { BuildingFactory20Regular, HeartPulse20Regular, MoneyHand20Regular, People20Regular } from '@fluentui/react-icons'
import { useRouter } from 'next/navigation'
import { cn } from '@/lib/utils'

interface IndustryKnowledgeBase {
  id: string
  name: string
  industry: string
  description: string
  icon: React.ComponentType<{ className?: string }>
  iconBg: string
  iconColor: string
  agentName: string
}

const INDUSTRY_KNOWLEDGE_BASES: IndustryKnowledgeBase[] = [
  {
    id: 'finance-knowledge-base',
    name: 'Finance knowledge base',
    industry: 'Financial services',
    description: 'Market commentary, policy documents, and investment research.',
    icon: MoneyHand20Regular,
    iconBg: 'bg-green-500/10',
    iconColor: 'text-green-600 dark:text-green-500',
    agentName: 'market-knowledge-base',
  },
  {
    id: 'hr-knowledge-base',
    name: 'HR knowledge base',
    industry: 'Human resources',
    description: 'Benefits, policies, and employee enablement content.',
    icon: People20Regular,
    iconBg: 'bg-blue-500/10',
    iconColor: 'text-blue-600 dark:text-blue-500',
    agentName: 'hr-knowledge-base',
  },
  {
    id: 'healthcare-knowledge-base',
    name: 'Healthcare knowledge base',
    industry: 'Healthcare',
    description: 'Clinical guidance, safety references, and medication summaries.',
    icon: HeartPulse20Regular,
    iconBg: 'bg-rose-500/10',
    iconColor: 'text-rose-600 dark:text-rose-500',
    agentName: 'healthcare-knowledge-base',
  },
  {
    id: 'operations-knowledge-base',
    name: 'Operations knowledge base',
    industry: 'Operations',
    description: 'Playbooks, logistics plans, and operational readiness guidance.',
    icon: BuildingFactory20Regular,
    iconBg: 'bg-violet-500/10',
    iconColor: 'text-violet-600 dark:text-violet-500',
    agentName: 'operations-knowledge-base',
  },
]

export function IndustryKnowledgeSelector() {
  const router = useRouter()
  const [selectedKB, setSelectedKB] = useState<string | null>(null)

  const handleSelectKB = (kb: IndustryKnowledgeBase) => {
    setSelectedKB(kb.id)
    router.push(`/playground?agent=${encodeURIComponent(kb.agentName)}`)
  }

  return (
    <div className="min-h-[calc(100vh-7rem)] flex flex-col items-center justify-center p-6 bg-gradient-to-br from-bg-canvas via-bg-canvas to-accent-subtle/5">
      <div className="max-w-6xl w-full space-y-8">
        <motion.div initial={{ opacity: 0, y: -20 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.5 }} className="text-center space-y-3">
          <h1 className="text-4xl font-bold text-fg-default">Choose a scenario</h1>
          <p className="text-lg text-fg-muted max-w-2xl mx-auto">Select a sample knowledge base to explore retrieval, citations, and runtime controls.</p>
        </motion.div>

        <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-4">
          {INDUSTRY_KNOWLEDGE_BASES.map((kb, index) => {
            const Icon = kb.icon
            const isSelected = selectedKB === kb.id
            return (
              <motion.div key={kb.id} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.5, delay: index * 0.1 }}>
                <Card className={cn('h-full cursor-pointer transition-all duration-300 hover:shadow-xl hover:-translate-y-1', isSelected ? 'border-accent shadow-lg ring-2 ring-accent ring-offset-2 ring-offset-bg-canvas' : 'hover:border-accent/50')} onClick={() => handleSelectKB(kb)}>
                  <CardHeader className="space-y-4">
                    <div className={cn('flex h-16 w-16 items-center justify-center rounded-xl', kb.iconBg)}>
                      <Icon className={cn('h-8 w-8', kb.iconColor)} />
                    </div>
                    <div className="space-y-1">
                      <CardTitle className="text-xl">{kb.name}</CardTitle>
                      <div className="text-sm font-medium text-accent">{kb.industry}</div>
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <CardDescription className="text-sm leading-relaxed">{kb.description}</CardDescription>
                    <Button className="w-full">Open playground</Button>
                  </CardContent>
                </Card>
              </motion.div>
            )
          })}
        </div>
      </div>
    </div>
  )
}
