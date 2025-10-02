import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { initializeMapConfig } from './utils/mapConfig'

// Import global styles
import './style.css'

// Initialize map configuration from API before creating the app
async function initializeApp() {
  try {
    console.log('[VintageAtlas] Initializing map configuration from server...')
    await initializeMapConfig()
    console.log('[VintageAtlas] Map configuration loaded successfully')
  } catch (error) {
    console.warn('[VintageAtlas] Failed to load map config, using fallback values:', error)
  }

  // Create Vue app
  const app = createApp(App)

  // Set up Pinia for state management
  app.use(createPinia())

  // Set up Vue Router
  app.use(router)

  // Mount the app
  app.mount('#app')

  // Log app initialization for debugging
  console.log('[VintageAtlas] Application initialized with dynamic configuration')
}

// Start the app
initializeApp()
