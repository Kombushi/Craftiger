import type { BomResponse } from '../types'

const messages: Record<string, (name: string) => string> = {
  pin_unknown: (name) => `The pinned recipe for ${name} no longer exists — pin ignored.`,
  pin_illegal: (name) => `The pinned recipe for ${name} is not legal in this garage — pin ignored.`,
  pin_cycle: (name) => `The pin on ${name} would loop the chain — pin ignored.`,
  unreachable_target: (name) => `${name} cannot be crafted with this garage.`,
  unreachable_input: (name) => `${name} is needed but cannot be crafted — the chain is incomplete.`,
}

export function Warnings({ bom }: { bom: BomResponse }) {
  if (bom.warnings.length === 0) {
    return null
  }
  return (
    <ul className="warnings">
      {bom.warnings.map((warning, index) => {
        const name = bom.items[warning.itemId]?.name ?? warning.itemId
        const text = messages[warning.kind]?.(name) ?? `${warning.kind}: ${name}`
        return (
          <li key={index} className="warning-row">
            {text}
          </li>
        )
      })}
    </ul>
  )
}