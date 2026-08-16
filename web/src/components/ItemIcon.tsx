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
  const x = (atlasIdx % cols) * atlas.cell * scale
  const y = Math.floor(atlasIdx / cols) * atlas.cell * scale
  return (
    <span
      className="icon"
      style={{
        width: size,
        height: size,
        backgroundImage: 'url(/atlas.webp)',
        backgroundPosition: `${-x}px ${-y}px`,
        backgroundSize: `${atlas.width * scale}px ${atlas.height * scale}px`,
      }}
    />
  )
}