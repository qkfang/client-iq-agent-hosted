import { redirect } from 'next/navigation'

interface PlaygroundPageProps {
  params: {
    agentId: string
  }
}

export default function PlaygroundPage({ params }: PlaygroundPageProps) {
  redirect(`/playground?agent=${encodeURIComponent(params.agentId)}`)
}
