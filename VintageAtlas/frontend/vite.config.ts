import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [vue()],
  base: '/',  // Serve from root path
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:42422',
        changeOrigin: true
      },
      '/data': {
        target: 'http://localhost:42422',
        changeOrigin: true
      },
      '/tiles': {
        target: 'http://localhost:42422',
        changeOrigin: true
      },
      '/rain-tiles': {
        target: 'http://localhost:42422',
        changeOrigin: true
      },
      '/temperature-tiles': {
        target: 'http://localhost:42422',
        changeOrigin: true
      }
    }
  },
  build: {
    outDir: '../html',
    emptyOutDir: false,  // Don't delete css/, data/, webfonts/ directories
    rollupOptions: {
      output: {
        // Keep assets organized in assets/ subdirectory
        assetFileNames: 'assets/[name]-[hash][extname]',
        chunkFileNames: 'assets/[name]-[hash].js',
        entryFileNames: 'assets/[name]-[hash].js'
      }
    }
  }
})
