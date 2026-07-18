import * as React from 'react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { ChevronLeft20Regular } from '@fluentui/react-icons'
import Link from 'next/link'

interface HeaderAction {
  label: string
  href?: string
  onClick?: () => void
  icon?: React.ComponentType<{ className?: string }>
}

interface PageHeaderProps {
  title: string
  description?: string
  status?: {
    label: string
    variant: 'success' | 'warning' | 'danger' | 'info'
  }
  primaryAction?: HeaderAction
  secondaryAction?: HeaderAction
  backButton?: {
    href: string
    label?: string
  }
  className?: string
}

const statusVariants = {
  success: 'bg-status-success/15 text-status-success ring-status-success/40',
  warning: 'bg-status-warning/15 text-status-warning ring-status-warning/40',
  danger: 'bg-status-danger/15 text-status-danger ring-status-danger/40',
  info: 'bg-status-info/15 text-status-info ring-status-info/40',
}

function HeaderActionButton({ action, variant = 'default' }: { action?: HeaderAction; variant?: 'default' | 'outline' }) {
  if (!action) return null

  const content = (
    <>
      {action.icon && <action.icon className="mr-2 h-4 w-4" />}
      {action.label}
    </>
  )

  if (action.href) {
    return (
      <Button asChild size="lg" variant={variant}>
        <Link href={action.href}>{content}</Link>
      </Button>
    )
  }

  return (
    <Button size="lg" variant={variant} onClick={action.onClick}>
      {content}
    </Button>
  )
}

export function PageHeader({ title, description, status, primaryAction, secondaryAction, backButton, className }: PageHeaderProps) {
  return (
    <div className={cn('rounded-2xl border border-glass-border bg-glass-surface px-6 py-6 shadow-md backdrop-blur-surface', className)}>
      {backButton && (
        <div className="mb-4">
          <Link
            href={backButton.href}
            className="inline-flex items-center gap-2 text-sm text-fg-muted transition-colors duration-fast hover:text-fg-default"
          >
            <ChevronLeft20Regular className="h-4 w-4" />
            {backButton.label || 'Back'}
          </Link>
        </div>
      )}

      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0 flex-1">
          <div className="mb-2 flex items-center gap-3">
            <h1 className="truncate text-3xl font-semibold tracking-tight text-fg-default">{title}</h1>
            {status && (
              <span className={cn('inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-wide ring-1', statusVariants[status.variant])}>
                {status.label}
              </span>
            )}
          </div>
          {description && <p className="text-lg leading-relaxed text-fg-muted">{description}</p>}
        </div>

        {(primaryAction || secondaryAction) && (
          <div className="flex shrink-0 gap-3">
            <HeaderActionButton action={secondaryAction} variant="outline" />
            <HeaderActionButton action={primaryAction} />
          </div>
        )}
      </div>
    </div>
  )
}
