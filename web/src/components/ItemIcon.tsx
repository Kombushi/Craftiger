import { useStore } from '../storeContext'

interface Props {
  atlasIdx: number
  size?: number
}

/** One sprite out of atlas.webp, scaled with hard pixel edges. */
export function ItemIcon({ atlasIdx, size = 32 }: Props) {
  const { meta } = useStore()
  const atlas = meta?.atlas
  if (!atlas || atlasIdx < 0) {
    return <span className="icon icon-fallback" style={{ width: size, height: size }} />
  }
  const cols = atlas.width / atlas.cell
  const scale = size / atlas.cell
  // One-texel crop per edge: zoom-level rounding otherwise samples the neighboring
  // atlas cell, drawing a stray line on edge-to-edge sprites such as fluids.
  const inset = scale
  const x = (atlasIdx % cols) * atlas.cell * scale
  const y = Math.floor(atlasIdx / cols) * atlas.cell * scale
  return (
    <span
      className="icon"
      style={{
        width: size,
        height: size,
        border: `${inset}px solid transparent`,
        backgroundClip: 'padding-box',
        backgroundImage: 'url(/atlas.webp)',
        backgroundPosition: `${-(x + inset)}px ${-(y + inset)}px`,
        backgroundSize: `${atlas.width * scale}px ${atlas.height * scale}px`,
      }}
    />
  )
}