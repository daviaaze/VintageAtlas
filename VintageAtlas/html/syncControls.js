/**
 * VintageAtlas Sync Controls
 * Provides UI for toggling auto-export and historical tracking
 */

class SyncControls {
    constructor(apiBaseUrl = '') {
        this.apiBaseUrl = apiBaseUrl;
        this.config = null;
        this.updateInterval = null;
    }

    /**
     * Initialize sync controls and attach to container
     * @param {string|HTMLElement} container - Container element or selector
     */
    async init(container) {
        const element = typeof container === 'string' 
            ? document.querySelector(container)
            : container;
            
        if (!element) {
            console.error('[SyncControls] Container not found');
            return;
        }

        // Create control panel HTML
        element.innerHTML = this.createControlsHTML();
        
        // Attach event listeners
        this.attachEventListeners(element);
        
        // Load initial config
        await this.loadConfig();
        
        // Start auto-refresh
        this.startAutoRefresh();
    }

    /**
     * Create controls HTML structure
     */
    createControlsHTML() {
        return `
            <div class="sync-controls-panel">
                <h3><i class="fas fa-cog"></i> Server Controls</h3>
                
                <div class="control-section">
                    <div class="control-item">
                        <label class="toggle-label">
                            <input type="checkbox" id="auto-export-toggle" class="toggle-input">
                            <span class="toggle-slider"></span>
                            <span class="toggle-text">
                                <i class="fas fa-map"></i> Auto Map Export
                            </span>
                        </label>
                        <div class="control-info">
                            <span id="export-status" class="status-text">Loading...</span>
                        </div>
                    </div>

                    <div class="control-item">
                        <label class="toggle-label">
                            <input type="checkbox" id="historical-tracking-toggle" class="toggle-input">
                            <span class="toggle-slider"></span>
                            <span class="toggle-text">
                                <i class="fas fa-database"></i> Historical Tracking
                            </span>
                        </label>
                        <div class="control-info">
                            <span id="tracking-status" class="status-text">Loading...</span>
                        </div>
                    </div>
                </div>

                <div class="control-section">
                    <button id="manual-export-btn" class="btn btn-primary">
                        <i class="fas fa-download"></i> Export Now
                    </button>
                    
                    <button id="save-config-btn" class="btn btn-secondary">
                        <i class="fas fa-save"></i> Save to Disk
                    </button>
                </div>

                <div class="control-info-panel">
                    <div class="info-item">
                        <span class="info-label">Export Interval:</span>
                        <span id="export-interval" class="info-value">-</span>
                    </div>
                    <div class="info-item">
                        <span class="info-label">Last Export:</span>
                        <span id="last-export" class="info-value">-</span>
                    </div>
                    <div class="info-item">
                        <span class="info-label">Status:</span>
                        <span id="export-active-status" class="info-value">-</span>
                    </div>
                </div>

                <div id="control-message" class="control-message" style="display: none;"></div>
            </div>
        `;
    }

    /**
     * Attach event listeners to controls
     */
    attachEventListeners(container) {
        // Auto-export toggle
        const autoExportToggle = container.querySelector('#auto-export-toggle');
        autoExportToggle?.addEventListener('change', () => this.handleToggle('autoExportMap', autoExportToggle.checked));

        // Historical tracking toggle
        const historicalToggle = container.querySelector('#historical-tracking-toggle');
        historicalToggle?.addEventListener('change', () => this.handleToggle('historicalTracking', historicalToggle.checked));

        // Manual export button
        const exportBtn = container.querySelector('#manual-export-btn');
        exportBtn?.addEventListener('click', () => this.triggerExport());

        // Save config button
        const saveBtn = container.querySelector('#save-config-btn');
        saveBtn?.addEventListener('click', () => this.saveConfig());
    }

    /**
     * Load config from API
     */
    async loadConfig() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/api/config`);
            if (!response.ok) throw new Error('Failed to load config');

            this.config = await response.json();
            this.updateUI();
        } catch (error) {
            console.error('[SyncControls] Error loading config:', error);
            this.showMessage('Failed to load configuration', 'error');
        }
    }

    /**
     * Update UI with current config
     */
    updateUI() {
        if (!this.config) return;

        // Update toggles
        const autoExportToggle = document.querySelector('#auto-export-toggle');
        const historicalToggle = document.querySelector('#historical-tracking-toggle');

        if (autoExportToggle) autoExportToggle.checked = this.config.autoExportMap;
        if (historicalToggle) historicalToggle.checked = this.config.historicalTracking;

        // Update status texts
        const exportStatus = document.querySelector('#export-status');
        const trackingStatus = document.querySelector('#tracking-status');

        if (exportStatus) {
            exportStatus.textContent = this.config.autoExportMap ? 'Enabled' : 'Disabled';
            exportStatus.className = `status-text ${this.config.autoExportMap ? 'status-enabled' : 'status-disabled'}`;
        }

        if (trackingStatus) {
            trackingStatus.textContent = this.config.historicalTracking ? 'Enabled' : 'Disabled';
            trackingStatus.className = `status-text ${this.config.historicalTracking ? 'status-enabled' : 'status-disabled'}`;
        }

        // Update info panel
        const intervalEl = document.querySelector('#export-interval');
        const lastExportEl = document.querySelector('#last-export');
        const activeStatusEl = document.querySelector('#export-active-status');

        if (intervalEl) {
            const minutes = Math.floor(this.config.exportIntervalMs / 60000);
            intervalEl.textContent = `${minutes} minutes`;
        }

        if (lastExportEl) {
            if (this.config.lastExportTime > 0) {
                const elapsed = Date.now() - this.config.lastExportTime;
                const minutes = Math.floor(elapsed / 60000);
                lastExportEl.textContent = minutes === 0 ? 'Just now' : `${minutes}m ago`;
            } else {
                lastExportEl.textContent = 'Never';
            }
        }

        if (activeStatusEl) {
            activeStatusEl.textContent = this.config.isExporting ? 'Exporting...' : 'Idle';
            activeStatusEl.className = `info-value ${this.config.isExporting ? 'status-active' : ''}`;
        }

        // Disable manual export button if already exporting
        const exportBtn = document.querySelector('#manual-export-btn');
        if (exportBtn) {
            exportBtn.disabled = this.config.isExporting;
            exportBtn.innerHTML = this.config.isExporting 
                ? '<i class="fas fa-spinner fa-spin"></i> Exporting...'
                : '<i class="fas fa-download"></i> Export Now';
        }
    }

    /**
     * Handle toggle change
     */
    async handleToggle(key, value) {
        try {
            const response = await fetch(`${this.apiBaseUrl}/api/config`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ [key]: value })
            });

            if (!response.ok) throw new Error('Failed to update config');

            this.config = await response.json();
            this.updateUI();
            this.showMessage(`${key === 'autoExportMap' ? 'Auto-export' : 'Historical tracking'} ${value ? 'enabled' : 'disabled'}`, 'success');
        } catch (error) {
            console.error('[SyncControls] Error updating config:', error);
            this.showMessage('Failed to update setting', 'error');
            await this.loadConfig(); // Revert UI
        }
    }

    /**
     * Trigger manual export
     */
    async triggerExport() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/api/export`, {
                method: 'POST'
            });

            const result = await response.json();

            if (!result.success) {
                this.showMessage(result.message, 'warning');
                return;
            }

            this.showMessage('Export started successfully', 'success');
            await this.loadConfig(); // Refresh status
        } catch (error) {
            console.error('[SyncControls] Error triggering export:', error);
            this.showMessage('Failed to start export', 'error');
        }
    }

    /**
     * Save config to disk
     */
    async saveConfig() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/api/config`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ saveToDisk: true })
            });

            if (!response.ok) throw new Error('Failed to save config');

            this.config = await response.json();
            this.updateUI();
            this.showMessage('Configuration saved to disk', 'success');
        } catch (error) {
            console.error('[SyncControls] Error saving config:', error);
            this.showMessage('Failed to save configuration', 'error');
        }
    }

    /**
     * Show message to user
     */
    showMessage(text, type = 'info') {
        const messageEl = document.querySelector('#control-message');
        if (!messageEl) return;

        messageEl.textContent = text;
        messageEl.className = `control-message control-message-${type}`;
        messageEl.style.display = 'block';

        setTimeout(() => {
            messageEl.style.display = 'none';
        }, 3000);
    }

    /**
     * Start auto-refresh
     */
    startAutoRefresh() {
        // Refresh every 5 seconds
        this.updateInterval = setInterval(() => {
            this.loadConfig();
        }, 5000);
    }

    /**
     * Stop auto-refresh
     */
    stopAutoRefresh() {
        if (this.updateInterval) {
            clearInterval(this.updateInterval);
            this.updateInterval = null;
        }
    }

    /**
     * Cleanup
     */
    destroy() {
        this.stopAutoRefresh();
    }
}

// Export for use in other scripts
if (typeof module !== 'undefined' && module.exports) {
    module.exports = SyncControls;
}

