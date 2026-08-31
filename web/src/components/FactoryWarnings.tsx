import { useStore } from '../storeContext'
import type { FactoryResponse } from '../types'

const messages: Record<string, (name: string) => string> = {
  target_unknown: (name) => `${name} is not in this pack's item set — target ignored.`,
  unreachable_target: (name) => `${name} cannot be made with this garage.`,
  pin_unknown: (name) => `The pinned recipe for ${name} no longer exists — pin ignored.`,
  pin_illegal: (name) => `The pinned recipe for ${name} is not legal in this garage — pin ignored.`,
  pin_conflict: (name) => `The pin on ${name} removes every viable route — clear it to solve.`,
  no_generator: () => 'No garage-legal generator can serve the energy target.',
  consume_shortfall: (name) => `The plan cannot absorb ${name} at the requested rate.`,
  infeasible_item: (name) => `${name} cannot be balanced at the requested rates.`,
  infeasible_energy: () => 'The energy balance cannot be met at the requested rates.',
  infeasible: () => 'The plan is infeasible, and no single item takes the blame.',
  free_lunch: () => 'A free-producing cycle survived into the model — a data defect, not a plan.',
  timeout: () => 'The solve hit its time budget — try again, or simplify the targets.',
  solver_error: () => 'The solver failed — try again.',
  routes_pruned: () => 'Routes priced far off the optimum were left out of the model.',
}

/** Informational rather than alarming rows. */
const informational = new Set(['routes_pruned'])

export function FactoryWarnings({ plan }: { plan: FactoryResponse }) {
  const { openDetail } = useStore()
  if (plan.warnings.length === 0) {
    return null
  }
  return (
    <ul className="warnings">
      {plan.warnings.map((warning, index) => {
        const name = plan.items[warning.itemId]?.name ?? warning.itemId
        const text = messages[warning.kind]?.(name) ?? `${warning.kind}: ${name}`
        return (
          <li
            key={index}
            className={`warning-row${informational.has(warning.kind) ? ' warning-info' : ''}${warning.itemId !== '' ? ' warning-clickable' : ''}`}
            onClick={warning.itemId !== '' ? () => openDetail(warning.itemId) : undefined}
          >
            {text}
          </li>
        )
      })}
    </ul>
  )
}
