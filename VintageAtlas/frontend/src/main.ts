import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { initOLConfig } from './utils/olMapConfig'

// Import global styles
import './style.css'

// Initialize map configuration from API before creating the app
async function initializeApp() {
  try {
    await initOLConfig()
  } catch (error) {
    // Show user-friendly error message
    document.body.innerHTML = `
      <div style="
        display: flex;
        align-items: center;
        justify-content: center;
        height: 100vh;
        font-family: system-ui, -apple-system, sans-serif;
        background: #1a1a1a;
        color: #fff;
        margin: 0;
        padding: 20px;
        box-sizing: border-box;
      ">
        <div style="
          max-width: 600px;
          text-align: center;
          padding: 40px;
          background: #2a2a2a;
          border-radius: 8px;
          border: 2px solid #dc3545;
        ">
          <h1 style="color: #dc3545; margin: 0 0 20px 0;">❌ Cannot Load Map Configuration</h1>
          <p style="font-size: 16px; line-height: 1.6; margin: 0 0 20px 0;">
            The VintageAtlas frontend could not fetch required configuration from the server.
          </p>
          <details style="text-align: left; margin: 20px 0;">
            <summary style="cursor: pointer; font-weight: bold; margin-bottom: 10px;">
              Error Details
            </summary>
            <pre style="
              background: #1a1a1a;
              padding: 15px;
              border-radius: 4px;
              overflow-x: auto;
              font-size: 12px;
              color: #ff6b6b;
            ">${error instanceof Error ? error.message : String(error)}</pre>
          </details>
          <p style="font-size: 14px; color: #999;">
            <strong>Troubleshooting:</strong><br>
            1. Ensure the VintageAtlas mod is running on the server<br>
            2. Check that <code>/api/map-config</code> endpoint is accessible<br>
            3. Check the browser console for more details
          </p>
        </div>
      </div>
    `;
    return; // Stop app initialization
  }

  // Create Vue app
  const app = createApp(App)

  // Set up Pinia for state management
  app.use(createPinia())

  // Set up Vue Router
  app.use(router)

  // Mount the app
  app.mount('#app')
}

// Start the app
initializeApp()
