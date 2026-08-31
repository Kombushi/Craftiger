import type {
  BomResponse,
  FactoryResponse,
  GarageState,
  ItemDetail,
  ItemSummary,
  ListResponse,
  MetaResponse,
  SolveResponse,
} from './types'

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, init)
  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`
    try {
      const body = (await response.json()) as { error?: string }
      if (body.error) {
        message = body.error
      }
    } catch {
      // Non-JSON error bodies keep the status text.
    }
    throw new ApiError(response.status, message)
  }
  return (await response.json()) as T
}

function post<T>(path: string, body: unknown): Promise<T> {
  return request<T>(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
}

export const getMeta = () => request<MetaResponse>('/api/meta')

export const solve = (garage: GarageState, b: number, weights: Record<string, number>) =>
  post<SolveResponse>('/api/solve', { garage, b, weights })

export const search = (q: string, solveId: string | null) =>
  request<ItemSummary[]>(
    `/api/search?q=${encodeURIComponent(q)}${solveId ? `&solveId=${solveId}` : ''}`,
  )

export const list = (solveId: string, page: number, pageSize: number, hideUnreachable: boolean) =>
  request<ListResponse>(
    `/api/list?solveId=${solveId}&page=${page}&pageSize=${pageSize}&hideUnreachable=${hideUnreachable}`,
  )

export const itemDetail = (itemId: string, solveId: string) =>
  request<ItemDetail>(`/api/item/${encodeURIComponent(itemId)}?solveId=${solveId}`)

export const machinesFor = (targetIds: string[], deep = false) =>
  request<string[]>(
    `/api/machines?targets=${encodeURIComponent(targetIds.join(','))}${deep ? '&deep=true' : ''}`,
  )

export const bom = (
  solveId: string,
  targets: { itemId: string; count: number }[],
  pins: Record<string, string>,
) => post<BomResponse>('/api/bom', { solveId, targets, pins })

export interface FactorySolveTarget {
  kind: 'produce' | 'consume' | 'energy'
  itemId?: string
  rate: number
  generatorTier?: number
}

export const factorySolve = (
  garage: GarageState,
  b: number,
  weights: Record<string, number>,
  targets: FactorySolveTarget[],
  priority: string[],
  pins: Record<string, string>,
  mobFarms: boolean,
  bredSeeds: boolean,
) =>
  post<FactoryResponse>('/api/factory/solve', {
    garage, b, weights, targets, priority, pins, mobFarms, bredSeeds,
  })