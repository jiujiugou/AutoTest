import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

const apiBaseUrl = process.env.VITE_API_BASE_URL || 'http://localhost:5033'

export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      '/api': {
        target: apiBaseUrl,
        changeOrigin: true
      },
      '/hubs': {
        target: apiBaseUrl,
        changeOrigin: true,
        ws: true
      }
    }
  }
})
