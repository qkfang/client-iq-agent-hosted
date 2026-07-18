'use client'

export const dynamic = 'force-dynamic'

import { useState, useEffect, Suspense } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import { ChevronLeft20Regular, Database20Regular, Globe20Regular, CloudArrowUp20Regular, CheckmarkCircle20Filled, ErrorCircle20Filled } from '@fluentui/react-icons'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { createKnowledgeSource } from '@/lib/api'
import { LoadingSkeleton } from '@/components/shared/loading-skeleton'

type SourceType = 'indexedOneLake' | 'searchIndex' | 'azureBlob' | 'remoteSharePoint' | 'indexedSharePoint' | 'web'

interface QuickCreateConfig {
  sourceType: SourceType
  name: string
  connectionString: string
  containerName?: string
  folderPath?: string
  indexName?: string
  urls?: string[]
}

const SOURCE_TYPE_INFO = {
  azureBlob: {
    icon: CloudArrowUp20Regular,
    title: 'Azure Blob Storage',
    description: 'Connect to documents in Azure Storage',
    requiredFields: ['connectionString', 'containerName'],
    defaultValues: {
      containerName: 'kr-demos',
      folderPath: 'documents/',
      embeddingModel: 'text-embedding-3-small',
      completionModel: 'gpt-5-mini'
    }
  },
  searchIndex: {
    icon: Database20Regular,
    title: 'Azure AI Search Index',
    description: 'Connect to an existing search index',
    requiredFields: ['connectionString', 'indexName'],
    defaultValues: {
      indexName: 'documents-index'
    }
  },
  web: {
    icon: Globe20Regular,
    title: 'Web Sources',
    description: 'Crawl and index web pages',
    requiredFields: ['urls'],
    defaultValues: {
      urls: ['https://learn.microsoft.com/azure/search/', 'https://learn.microsoft.com/azure/ai-foundry/']
    }
  }
}

function QuickCreateKnowledgeSourcePageContent() {
  const router = useRouter()
  const searchParams = useSearchParams()
  const returnUrl = searchParams.get('returnUrl')

  const [step, setStep] = useState<'select' | 'configure' | 'creating' | 'complete'>('select')
  const [selectedType, setSelectedType] = useState<SourceType | null>(null)
  const [config, setConfig] = useState<QuickCreateConfig>({
    sourceType: 'azureBlob',
    name: '',
    connectionString: ''
  })
  const [creating, setCreating] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [createdSourceId, setCreatedSourceId] = useState<string | null>(null)

  useEffect(() => {
    if (selectedType) {
      const defaults = SOURCE_TYPE_INFO[selectedType].defaultValues
      setConfig(prev => ({
        ...prev,
        sourceType: selectedType,
        name: `${selectedType}-${Date.now()}`,
        ...defaults
      }))
    }
  }, [selectedType])

  const handleTypeSelect = (type: SourceType) => {
    setSelectedType(type)
    setStep('configure')
  }

  const validateConfig = () => {
    if (!config.name || !config.connectionString) {
      return false
    }

    const requiredFields = SOURCE_TYPE_INFO[config.sourceType].requiredFields
    for (const field of requiredFields) {
      if (!config[field as keyof QuickCreateConfig]) {
        return false
      }
    }

    return true
  }

  const handleCreate = async () => {
    if (!validateConfig()) {
      setError('Please fill in all required fields')
      return
    }

    setCreating(true)
    setStep('creating')
    setError(null)

    try {
      const payload = {
        name: config.name,
        kind: config.sourceType,
        description: `Quick-created from the builder`,
        ...buildSourceSpecificPayload()
      }

      const result = await createKnowledgeSource(payload)
      setCreatedSourceId(result.name || result.id)
      setStep('complete')

      setTimeout(() => {
        if (returnUrl) {
          router.push(returnUrl)
        } else {
          router.push('/knowledge-sources')
        }
      }, 2000)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create knowledge source')
      setStep('configure')
    } finally {
      setCreating(false)
    }
  }

  const buildSourceSpecificPayload = () => {
    const aoaiEndpoint = process.env.NEXT_PUBLIC_AZURE_OPENAI_ENDPOINT || ''
    // No apiKey here: the model resource requires Entra ID auth, so Search's
    // managed identity is used automatically when apiKey/authIdentity are omitted.

    switch (config.sourceType) {
      case 'azureBlob':
        return {
          azureBlobParameters: {
            connectionString: config.connectionString,
            containerName: config.containerName,
            folderPath: config.folderPath,
            embeddingModel: {
              name: 'text-embedding-3-small',
              kind: 'azureOpenAI',
              azureOpenAIParameters: {
                resourceUri: aoaiEndpoint,
                deploymentId: 'text-embedding-3-small',
                modelName: 'text-embedding-3-small'
              }
            },
            chatCompletionModel: {
              kind: 'azureOpenAI',
              azureOpenAIParameters: {
                resourceUri: aoaiEndpoint,
                deploymentId: 'gpt-5-mini',
                modelName: 'gpt-5-mini'
              }
            }
          }
        }
      case 'searchIndex':
        return {
          searchIndexParameters: {
            connectionString: config.connectionString,
            indexName: config.indexName
          }
        }
      case 'web':
        return {
          webParameters: {
            urls: config.urls,
            embeddingModel: {
              name: 'text-embedding-3-small',
              kind: 'azureOpenAI',
              azureOpenAIParameters: {
                resourceUri: aoaiEndpoint,
                deploymentId: 'text-embedding-3-small',
                modelName: 'text-embedding-3-small'
              }
            }
          }
        }
      default:
        return {}
    }
  }

  if (step === 'creating') {
    return (
      <div className="min-h-screen bg-bg-primary flex items-center justify-center">
        <Card className="p-8 max-w-md w-full">
          <div className="text-center space-y-4">
            <div className="animate-spin h-12 w-12 border-4 border-stroke-accent border-t-transparent rounded-full mx-auto" />
            <h2 className="text-lg font-semibold">Creating Knowledge Source</h2>
            <p className="text-sm text-fg-muted">Setting up {config.name}...</p>
          </div>
        </Card>
      </div>
    )
  }

  if (step === 'complete') {
    return (
      <div className="min-h-screen bg-bg-primary flex items-center justify-center">
        <Card className="p-8 max-w-md w-full">
          <div className="text-center space-y-4">
            <CheckmarkCircle20Filled className="h-12 w-12 text-fg-success mx-auto" />
            <h2 className="text-lg font-semibold">Knowledge Source Created!</h2>
            <p className="text-sm text-fg-muted">Redirecting back to the builder...</p>
          </div>
        </Card>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-bg-primary">
      <div className="border-b border-stroke-divider bg-bg-secondary">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => step === 'configure' ? setStep('select') : router.back()}
                className="gap-2"
              >
                <ChevronLeft20Regular className="h-4 w-4" />
                Back
              </Button>
              <h1 className="text-lg font-semibold text-fg-primary">Quick Create knowledge source</h1>
            </div>
          </div>
        </div>
      </div>

      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {step === 'select' && (
          <div className="space-y-6">
            <div>
              <h2 className="text-xl font-semibold mb-2">Select source type</h2>
              <p className="text-fg-muted">Choose how you want to connect your data</p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {Object.entries(SOURCE_TYPE_INFO).map(([type, info]) => {
                const Icon = info.icon
                return (
                  <Card
                    key={type}
                    className="p-6 cursor-pointer hover:border-stroke-accent transition-all"
                    onClick={() => handleTypeSelect(type as SourceType)}
                  >
                    <div className="space-y-3">
                      <Icon className="h-8 w-8 text-fg-accent" />
                      <h3 className="font-semibold">{info.title}</h3>
                      <p className="text-sm text-fg-muted">{info.description}</p>
                    </div>
                  </Card>
                )
              })}
            </div>
          </div>
        )}

        {step === 'configure' && selectedType && (
          <div className="space-y-6">
            <div>
              <h2 className="text-xl font-semibold mb-2">Configure {SOURCE_TYPE_INFO[selectedType].title}</h2>
              <p className="text-fg-muted">We've pre-filled smart defaults. Just add your connection details.</p>
            </div>

            {error && (
              <div className="flex items-center gap-2 p-4 bg-bg-error-subtle border border-stroke-error rounded-lg">
                <ErrorCircle20Filled className="h-5 w-5 text-fg-error" />
                <span className="text-sm text-fg-error">{error}</span>
              </div>
            )}

            <Card className="p-6 space-y-4">
              <div>
                <label className="text-sm font-medium text-fg-secondary">Name</label>
                <Input
                  value={config.name}
                  onChange={(e) => setConfig({ ...config, name: e.target.value })}
                  placeholder="Enter a name for this knowledge source"
                  className="mt-1"
                />
              </div>

              <div>
                <label className="text-sm font-medium text-fg-secondary">
                  Connection String <span className="text-fg-error">*</span>
                </label>
                <Input
                  value={config.connectionString}
                  onChange={(e) => setConfig({ ...config, connectionString: e.target.value })}
                  placeholder={selectedType === 'azureBlob'
                    ? "ResourceId=/subscriptions/.../storageAccounts/..."
                    : "https://your-search.search.windows.net"}
                  className="mt-1 font-mono text-sm"
                />
                <p className="text-xs text-fg-muted mt-1">
                  {selectedType === 'azureBlob'
                    ? "Use ResourceId format or standard connection string"
                    : "Your Azure AI Search endpoint URL"}
                </p>
              </div>

              {selectedType === 'azureBlob' && (
                <>
                  <div>
                    <label className="text-sm font-medium text-fg-secondary">
                      Container Name <span className="text-fg-error">*</span>
                    </label>
                    <Input
                      value={config.containerName}
                      onChange={(e) => setConfig({ ...config, containerName: e.target.value })}
                      placeholder="e.g., documents"
                      className="mt-1"
                    />
                  </div>
                  <div>
                    <label className="text-sm font-medium text-fg-secondary">Folder Path</label>
                    <Input
                      value={config.folderPath}
                      onChange={(e) => setConfig({ ...config, folderPath: e.target.value })}
                      placeholder="e.g., documents/labels/"
                      className="mt-1"
                    />
                  </div>
                </>
              )}

              {selectedType === 'searchIndex' && (
                <div>
                  <label className="text-sm font-medium text-fg-secondary">
                    Index Name <span className="text-fg-error">*</span>
                  </label>
                  <Input
                    value={config.indexName}
                    onChange={(e) => setConfig({ ...config, indexName: e.target.value })}
                    placeholder="e.g., documents-index"
                    className="mt-1"
                  />
                </div>
              )}

              {selectedType === 'web' && (
                <div>
                  <label className="text-sm font-medium text-fg-secondary">
                    URLs to Crawl <span className="text-fg-error">*</span>
                  </label>
                  <textarea
                    value={config.urls?.join('\n') || ''}
                    onChange={(e) => setConfig({ ...config, urls: e.target.value.split('\n').filter(Boolean) })}
                    placeholder="Enter one URL per line"
                    className="mt-1 w-full p-2 border rounded-lg text-sm h-32"
                  />
                </div>
              )}

              <div className="p-3 bg-bg-info-subtle border border-stroke-info rounded-lg">
                <p className="text-xs text-fg-info">
                  <strong>Smart defaults applied:</strong> The builder uses the deployed embedding and chat models from your environment settings.
                  These can be customized later if needed.
                </p>
              </div>
            </Card>

            <div className="flex justify-end gap-3">
              <Button
                variant="secondary"
                onClick={() => setStep('select')}
              >
                Back
              </Button>
              <Button
                onClick={handleCreate}
                disabled={creating || !validateConfig()}
              >
                Create knowledge source
              </Button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

export default function QuickCreateKnowledgeSourcePage() {
  return (
    <Suspense fallback={<LoadingSkeleton />}>
      <QuickCreateKnowledgeSourcePageContent />
    </Suspense>
  )
}