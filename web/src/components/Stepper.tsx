interface Props {
  value: number
  min: number
  step?: number
  id?: string
  className?: string
  onChange: (value: number) => void
}

/** Number input with its own arrow pair — native spinners overlap right-aligned digits. */
export function Stepper({ value, min, step = 1, id, className, onChange }: Props) {
  const accept = (next: number) => {
    if (Number.isFinite(next) && next >= min) {
      onChange(next)
    }
  }
  const nudge = (direction: 1 | -1) => {
    accept(Math.round((value + direction * step) * 1000) / 1000)
  }
  return (
    <span className={className ? `stepper ${className}` : 'stepper'}>
      <input
        id={id}
        className="mono stepper-input"
        type="number"
        min={min}
        step={step}
        value={value}
        onChange={(event) => accept(Number(event.target.value))}
      />
      <span className="stepper-arrows">
        <button type="button" tabIndex={-1} aria-label="Increase" onClick={() => nudge(1)}>
          ▴
        </button>
        <button type="button" tabIndex={-1} aria-label="Decrease" onClick={() => nudge(-1)}>
          ▾
        </button>
      </span>
    </span>
  )
}
