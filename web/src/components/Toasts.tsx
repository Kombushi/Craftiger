import { useEffect } from 'react'
import { useStore } from '../storeContext'
import type { Toast } from '../storeContext'

const TOAST_MS = 6000

export function Toasts() {
  const { toasts, dismissToast } = useStore()
  if (toasts.length === 0) {
    return null
  }
  return (
    <div className="toasts">
      {toasts.map((toast) => (
        <ToastRow key={toast.id} toast={toast} dismiss={dismissToast} />
      ))}
    </div>
  )
}

function ToastRow({ toast, dismiss }: { toast: Toast; dismiss: (id: number) => void }) {
  useEffect(() => {
    const timer = window.setTimeout(() => dismiss(toast.id), TOAST_MS)
    return () => window.clearTimeout(timer)
  }, [toast.id, dismiss])
  return (
    <button type="button" className="toast" title="Dismiss" onClick={() => dismiss(toast.id)}>
      {toast.message}
    </button>
  )
}
