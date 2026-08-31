import type { RateUnit } from './types'

/** Compact badge text for slot counts: "12", "4.88", "1.2k", "3.4M". */
export function fmtCount(value: number): string {
  if (value >= 1_000_000) {
    return `${trim(value / 1_000_000, 1)}M`
  }
  if (value >= 10_000) {
    return `${trim(value / 1_000, 1)}k`
  }
  if (Number.isInteger(value)) {
    return String(value)
  }
  return value >= 100 ? String(Math.round(value)) : trim(value, 2)
}

export function fmtAmount(value: number, isFluid: boolean): string {
  return isFluid ? `${fmtCount(value)} mB` : `×${fmtCount(value)}`
}

export function fmtCost(value: number | null | undefined): string {
  if (value === null || value === undefined) {
    return '∞'
  }
  if (value === 0) {
    return '0'
  }
  if (value >= 1_000) {
    return Math.round(value).toLocaleString('en-US')
  }
  if (value >= 1) {
    return trim(value, 3)
  }
  return trim(value, 4)
}

export function fmtDuration(ticks: number): string {
  const seconds = ticks / 20
  if (seconds >= 3600) {
    return `${trim(seconds / 3600, 1)}h`
  }
  if (seconds >= 60) {
    return `${trim(seconds / 60, 1)}m`
  }
  return `${trim(seconds, 1)}s`
}

export function fmtRuns(runs: number): string {
  return `×${fmtCount(runs)}`
}

/** A whole amount of a stackable item as stacks — "5×64 + 15" — or null when stacks add
 * nothing: fluids and unstackable items carry no stack size, and under one stack the plain
 * count already says it all. */
export function fmtStacks(amount: number, maxStack: number | null | undefined): string | null {
  if (!maxStack || maxStack <= 1 || maxStack > 64 || amount < maxStack) {
    return null
  }
  const whole = Math.floor(amount)
  const stacks = Math.floor(whole / maxStack)
  const rest = whole - stacks * maxStack
  return rest > 0 ? `${stacks}×${maxStack} + ${rest}` : `${stacks}×${maxStack}`
}

export function fmtHeat(heat: number): string {
  return `${heat.toLocaleString('en-US')}K`
}

function trim(value: number, decimals: number): string {
  return value.toFixed(decimals).replace(/\.?0+$/, '')
}

const RATE_UNITS = {
  tick: { factor: 1 / 20, label: '/t' },
  second: { factor: 1, label: '/s' },
  minute: { factor: 60, label: '/min' },
} as const

/** Three significant digits: per-tick rates are tiny and per-minute rates large, so fixed decimals fit neither. */
function sig(value: number): string {
  if (value === 0) {
    return '0'
  }
  if (value >= 10_000) {
    return fmtCount(value)
  }
  if (value >= 100) {
    return String(Math.round(value))
  }
  return trim(value, Math.min(6, Math.max(0, 2 - Math.floor(Math.log10(value)))))
}

/** A per-second rate rendered in the display unit: "1.6/s", "0.533 mB/t", "96/min". */
export function fmtRate(perSecond: number, unit: RateUnit, isFluid = false): string {
  const { factor, label } = RATE_UNITS[unit]
  return `${sig(perSecond * factor)}${isFluid ? ' mB' : ''}${label}`
}

/** The bare converted number for slot badges; the unit lives in the tooltip and the global picker. */
export function fmtRateBadge(perSecond: number, unit: RateUnit): string {
  return sig(perSecond * RATE_UNITS[unit].factor)
}

export function fmtEuT(value: number): string {
  return `${value >= 100 ? Math.round(value).toLocaleString('en-US') : trim(value, 1)} EU/t`
}
/** Item name plus the display aliases unification merged away: "Tin Nugget (aka Tin Oreberry)". */
export function fmtAka(
  item: { name: string; aliases?: string[] | null } | undefined,
  fallback: string,
): string {
  if (!item) {
    return fallback
  }
  return item.aliases && item.aliases.length > 0
    ? `${item.name} (aka ${item.aliases.join(', ')})`
    : item.name
}
