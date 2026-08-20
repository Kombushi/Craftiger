import type { BomResponse, ItemRef } from '../types'

const messages: Record<string, (name: string, item: ItemRef | undefined) => string> = {
  pin_unknown: (name) => `The pinned recipe for ${name} no longer exists — pin ignored.`,
  pin_illegal: (name) => `The pinned recipe for ${name} is not legal in this garage — pin ignored.`,
  pin_cycle: (name) => `The pin on ${name} would loop the chain without end — pin ignored.`,
  unreachable_target: (name, item) =>
    item?.uncraftable
      ? `${name} is uncraftable — nothing in the pack produces it.`
      : `${name} cannot be crafted with this garage.`,
  unreachable_input: (name, item) =>
    item?.uncraftable
      ? `${name} is needed but nothing in the pack produces it — the chain is incomplete.`
      : `${name} is needed but cannot be crafted — the chain is incomplete.`,
  loop_unseeded: (name) =>
    `${name} is made in a loop that nothing outside it can start — the plan assumes a first unit exists.`,
}

export function Warnings({ bom }: { bom: BomResponse }) {
  if (bom.warnings.length === 0) {
    return null
  }
  return (
    <ul className="warnings">
      {bom.warnings.map((warning, index) => {
        const item = bom.items[warning.itemId]
        const name = item?.name ?? warning.itemId
        const text = messages[warning.kind]?.(name, item) ?? `${warning.kind}: ${name}`
        return (
          <li key={index} className="warning-row">
            {text}
          </li>
        )
      })}
    </ul>
  )
}