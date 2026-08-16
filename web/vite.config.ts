import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// The API serves solves and the icon atlas on 5111; the dev server proxies both.
export default defineConfig({
  plugins: [react()],
  server: {
    // A Windows-drive checkout under WSL2 delivers no inotify events; poll instead.
    watch: { usePolling: true, interval: 500 },
    proxy: {
      '/api': 'http://localhost:5111',
      '/atlas.webp': 'http://localhost:5111',
      '/atlas-offsets.json': 'http://localhost:5111',
    },
  },
})