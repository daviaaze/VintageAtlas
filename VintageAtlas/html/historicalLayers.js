// Historical Data Layers for WebCartographer
// Provides heatmaps, player paths, and time-based visualizations

(function() {
    if (typeof ol === 'undefined' || typeof map === 'undefined') {
        console.error('[historical] OpenLayers or map not found');
        return;
    }

    // Configuration
    const API_BASE = '/api/';
    const DEFAULT_HOURS = 24;
    const DEFAULT_GRID_SIZE = 32;

    // Sources and layers
    const heatmapSource = new ol.source.Vector();
    const playerPathSource = new ol.source.Vector();
    const deathMarkerSource = new ol.source.Vector();

    // State management
    let currentPlayerUid = null;
    let isPlaying = false;
    let playbackIndex = 0;
    let playbackPath = [];
    let playbackTimer = null;

    // Create heatmap layer
    const heatmapLayer = new ol.layer.Vector({
        zIndex: 999, // Below live layers but above map
        source: heatmapSource,
        visible: false,
        style: function(feature) {
            const count = feature.get('count') || 1;
            const maxCount = feature.get('maxCount') || 100;
            const intensity = Math.min(count / maxCount, 1);
            
            // Color gradient: blue (low) -> yellow -> red (high)
            let color;
            if (intensity < 0.5) {
                const t = intensity * 2;
                const r = Math.round(0 + (255 * t));
                const g = Math.round(100 + (155 * t));
                const b = Math.round(255 - (255 * t));
                color = `rgba(${r},${g},${b},0.6)`;
            } else {
                const t = (intensity - 0.5) * 2;
                const r = 255;
                const g = Math.round(255 - (100 * t));
                const b = 0;
                color = `rgba(${r},${g},${b},0.6)`;
            }

            return new ol.style.Style({
                fill: new ol.style.Fill({ color: color }),
                stroke: new ol.style.Stroke({ color: 'rgba(255,255,255,0.3)', width: 1 })
            });
        }
    });

    // Create player path layer
    const playerPathLayer = new ol.layer.Vector({
        zIndex: 1003, // Above live layers
        source: playerPathSource,
        visible: false,
        style: function(feature) {
            const isCurrentPos = feature.get('isCurrent');
            
            if (isCurrentPos) {
                // Current position marker
                return new ol.style.Style({
                    image: new ol.style.Circle({
                        radius: 8,
                        fill: new ol.style.Fill({ color: '#4a9eff' }),
                        stroke: new ol.style.Stroke({ color: '#ffffff', width: 3 })
                    })
                });
            } else {
                // Path line
                return new ol.style.Style({
                    stroke: new ol.style.Stroke({
                        color: '#4a9eff',
                        width: 3,
                        lineCap: 'round',
                        lineJoin: 'round'
                    })
                });
            }
        }
    });

    // Create death marker layer
    const deathMarkerLayer = new ol.layer.Vector({
        zIndex: 1004,
        source: deathMarkerSource,
        visible: false,
        style: function(feature) {
            return [
                new ol.style.Style({
                    image: new ol.style.Circle({
                        radius: 6,
                        fill: new ol.style.Fill({ color: '#ff0000' }),
                        stroke: new ol.style.Stroke({ color: '#ffffff', width: 2 })
                    })
                }),
                new ol.style.Style({
                    text: new ol.style.Text({
                        text: '💀',
                        font: 'bold 20px sans-serif',
                        textAlign: 'center',
                        textBaseline: 'middle',
                        offsetY: -15
                    })
                })
            ];
        }
    });

    // Add layers to map
    map.addLayer(heatmapLayer);
    map.addLayer(playerPathLayer);
    map.addLayer(deathMarkerLayer);

    // Create UI controls
    function createHistoricalUI() {
        const css = document.createElement('style');
        css.textContent = `
            #historical-ctrl {
                position: absolute;
                top: 70px;
                left: 10px;
                z-index: 5000;
                background: rgba(0,0,0,0.85);
                color: #fff;
                padding: 12px;
                border-radius: 8px;
                font: 13px/1.4 sans-serif;
                min-width: 300px;
                backdrop-filter: blur(6px);
                box-shadow: 0 4px 12px rgba(0,0,0,0.4);
                display: none;
            }
            #historical-ctrl.visible {
                display: block;
            }
            #historical-ctrl h3 {
                margin: 0 0 10px 0;
                font-size: 14px;
                color: #4a9eff;
                border-bottom: 1px solid rgba(74,158,255,0.3);
                padding-bottom: 8px;
            }
            #historical-ctrl label {
                display: block;
                margin: 8px 0 4px;
                font-size: 12px;
                color: #aaa;
            }
            #historical-ctrl input[type="range"],
            #historical-ctrl select {
                width: 100%;
                margin: 4px 0;
            }
            #historical-ctrl select {
                background: rgba(255,255,255,0.1);
                color: #fff;
                border: 1px solid rgba(255,255,255,0.2);
                border-radius: 4px;
                padding: 6px;
            }
            #historical-ctrl button {
                background: #4a9eff;
                border: none;
                color: #fff;
                padding: 8px 12px;
                border-radius: 4px;
                cursor: pointer;
                margin: 4px 4px 4px 0;
                font-size: 12px;
                transition: background 0.2s;
            }
            #historical-ctrl button:hover {
                background: #357abd;
            }
            #historical-ctrl button:disabled {
                background: #555;
                cursor: not-allowed;
                opacity: 0.5;
            }
            #historical-ctrl .controls {
                display: flex;
                gap: 4px;
                margin-top: 8px;
                flex-wrap: wrap;
            }
            #historical-ctrl .stat {
                font-size: 11px;
                color: #888;
                margin-top: 6px;
            }
            .historical-toggle {
                margin: 6px 0;
                padding: 4px 0;
            }
            .historical-toggle label {
                display: inline-block;
                margin-left: 6px;
                cursor: pointer;
            }
            #playback-progress {
                margin: 8px 0;
                display: none;
            }
            #playback-progress.active {
                display: block;
            }
            #toggle-historical {
                position: absolute;
                top: 130px;
                left: 10px;
                z-index: 5001;
                background: rgba(74,158,255,0.9);
                border: none;
                color: #fff;
                padding: 10px 14px;
                border-radius: 8px;
                cursor: pointer;
                font-weight: bold;
                font-size: 13px;
                box-shadow: 0 2px 8px rgba(0,0,0,0.3);
                transition: all 0.2s;
            }
            #toggle-historical:hover {
                background: rgba(74,158,255,1);
                transform: scale(1.05);
            }
        `;
        document.head.appendChild(css);

        // Toggle button
        const toggleBtn = document.createElement('button');
        toggleBtn.id = 'toggle-historical';
        toggleBtn.textContent = '📊 Historical Data';
        toggleBtn.setAttribute('aria-label', 'Toggle historical data panel');
        document.body.appendChild(toggleBtn);

        // Main control panel
        const div = document.createElement('div');
        div.id = 'historical-ctrl';
        div.setAttribute('role', 'region');
        div.setAttribute('aria-label', 'Historical data controls');
        
        div.innerHTML = `
            <h3>📊 Historical Data</h3>
            
            <div class="historical-toggle">
                <input type="checkbox" id="chk-heatmap" aria-label="Show activity heatmap">
                <label for="chk-heatmap">Activity Heatmap</label>
            </div>
            
            <div class="historical-toggle">
                <input type="checkbox" id="chk-player-path" aria-label="Show player path">
                <label for="chk-player-path">Player Path</label>
            </div>
            
            <label for="player-select">Player:</label>
            <select id="player-select" aria-label="Select player">
                <option value="">All Players</option>
            </select>
            
            <label for="time-range">Time Range: <span id="time-range-value">24 hours</span></label>
            <input type="range" id="time-range" min="1" max="168" value="24" step="1" 
                   aria-label="Time range in hours" aria-valuemin="1" aria-valuemax="168" aria-valuenow="24">
            
            <label for="grid-size">Grid Size: <span id="grid-size-value">32 blocks</span></label>
            <input type="range" id="grid-size" min="16" max="128" value="32" step="16"
                   aria-label="Heatmap grid size in blocks">
            
            <div class="controls">
                <button id="btn-load-heatmap">Load Heatmap</button>
                <button id="btn-load-path" disabled>Load Path</button>
                <button id="btn-play-path" disabled>▶ Play</button>
                <button id="btn-clear">Clear All</button>
            </div>
            
            <div id="playback-progress">
                <label for="playback-slider">Playback:</label>
                <input type="range" id="playback-slider" min="0" max="100" value="0" step="1">
                <div class="stat" id="playback-info">Ready</div>
            </div>
            
            <div class="stat" id="status-text">Ready</div>
        `;
        document.body.appendChild(div);

        // Event handlers
        toggleBtn.addEventListener('click', () => {
            div.classList.toggle('visible');
        });

        const chkHeatmap = div.querySelector('#chk-heatmap');
        const chkPath = div.querySelector('#chk-player-path');
        const playerSelect = div.querySelector('#player-select');
        const timeRange = div.querySelector('#time-range');
        const timeRangeValue = div.querySelector('#time-range-value');
        const gridSize = div.querySelector('#grid-size');
        const gridSizeValue = div.querySelector('#grid-size-value');
        const btnLoadHeatmap = div.querySelector('#btn-load-heatmap');
        const btnLoadPath = div.querySelector('#btn-load-path');
        const btnPlayPath = div.querySelector('#btn-play-path');
        const btnClear = div.querySelector('#btn-clear');
        const statusText = div.querySelector('#status-text');

        chkHeatmap.addEventListener('change', () => {
            heatmapLayer.setVisible(chkHeatmap.checked);
        });

        chkPath.addEventListener('change', () => {
            playerPathLayer.setVisible(chkPath.checked);
        });

        playerSelect.addEventListener('change', () => {
            currentPlayerUid = playerSelect.value || null;
            btnLoadPath.disabled = !currentPlayerUid;
        });

        timeRange.addEventListener('input', () => {
            const hours = parseInt(timeRange.value);
            timeRangeValue.textContent = hours === 1 ? '1 hour' : `${hours} hours`;
        });

        gridSize.addEventListener('input', () => {
            gridSizeValue.textContent = `${gridSize.value} blocks`;
        });

        btnLoadHeatmap.addEventListener('click', () => loadHeatmap());
        btnLoadPath.addEventListener('click', () => loadPlayerPath());
        btnPlayPath.addEventListener('click', () => togglePlayback());
        btnClear.addEventListener('click', () => clearAll());

        // Populate players from live data
        setInterval(updatePlayerList, 30000); // Every 30 seconds
        updatePlayerList();
    }

    async function updatePlayerList() {
        try {
            const response = await fetch(API_BASE + 'status');
            if (!response.ok) return;
            
            const data = await response.json();
            const playerSelect = document.getElementById('player-select');
            if (!playerSelect) return;

            const currentValue = playerSelect.value;
            const players = data.players || [];
            
            // Clear and repopulate
            playerSelect.innerHTML = '<option value="">All Players</option>';
            players.forEach(player => {
                const option = document.createElement('option');
                option.value = player.uid;
                option.textContent = player.name;
                playerSelect.appendChild(option);
            });

            // Restore selection if still valid
            if (currentValue && players.some(p => p.uid === currentValue)) {
                playerSelect.value = currentValue;
            }
        } catch (err) {
            console.error('[historical] Failed to update player list:', err);
        }
    }

    async function loadHeatmap() {
        const statusText = document.getElementById('status-text');
        const hours = parseInt(document.getElementById('time-range').value);
        const gridSize = parseInt(document.getElementById('grid-size').value);
        const playerUid = currentPlayerUid;

        statusText.textContent = 'Loading heatmap...';
        
        try {
            let url = `${API_BASE}heatmap?hours=${hours}&gridSize=${gridSize}`;
            if (playerUid) {
                url += `&player=${encodeURIComponent(playerUid)}`;
            }

            const response = await fetch(url);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const data = await response.json();
            const heatmapData = data.heatmap || [];
            
            if (heatmapData.length === 0) {
                statusText.textContent = 'No heatmap data found for this time range';
                return;
            }

            // Find max count for normalization
            const maxCount = Math.max(...heatmapData.map(p => p.count));

            // Clear and add features
            heatmapSource.clear();
            heatmapData.forEach(point => {
                const feature = new ol.Feature({
                    geometry: new ol.geom.Polygon([[
                        [point.x, point.z],
                        [point.x + gridSize, point.z],
                        [point.x + gridSize, point.z + gridSize],
                        [point.x, point.z + gridSize],
                        [point.x, point.z]
                    ]]),
                    count: point.count,
                    maxCount: maxCount
                });
                heatmapSource.addFeature(feature);
            });

            heatmapLayer.setVisible(true);
            document.getElementById('chk-heatmap').checked = true;
            statusText.textContent = `Loaded ${heatmapData.length} heatmap cells (max: ${maxCount} visits)`;
        } catch (err) {
            console.error('[historical] Failed to load heatmap:', err);
            statusText.textContent = `Error loading heatmap: ${err.message}`;
        }
    }

    async function loadPlayerPath() {
        if (!currentPlayerUid) {
            alert('Please select a player first');
            return;
        }

        const statusText = document.getElementById('status-text');
        const hours = parseInt(document.getElementById('time-range').value);

        statusText.textContent = 'Loading player path...';

        try {
            const url = `${API_BASE}player-path?player=${encodeURIComponent(currentPlayerUid)}&hours=${hours}`;
            const response = await fetch(url);
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const data = await response.json();
            playbackPath = data.path || [];
            
            if (playbackPath.length === 0) {
                statusText.textContent = 'No path data found for this player';
                return;
            }

            // Create path line
            playerPathSource.clear();
            const coordinates = playbackPath.map(p => [p.x, p.z]);
            const lineFeature = new ol.Feature({
                geometry: new ol.geom.LineString(coordinates)
            });
            playerPathSource.addFeature(lineFeature);

            // Add end marker
            const lastPoint = playbackPath[playbackPath.length - 1];
            const markerFeature = new ol.Feature({
                geometry: new ol.geom.Point([lastPoint.x, lastPoint.z]),
                isCurrent: true
            });
            playerPathSource.addFeature(markerFeature);

            playerPathLayer.setVisible(true);
            document.getElementById('chk-player-path').checked = true;
            document.getElementById('btn-play-path').disabled = false;
            
            // Setup playback controls
            const playbackProgress = document.getElementById('playback-progress');
            playbackProgress.classList.add('active');
            const slider = document.getElementById('playback-slider');
            slider.max = playbackPath.length - 1;
            slider.value = playbackPath.length - 1;
            
            statusText.textContent = `Loaded path with ${playbackPath.length} points`;
        } catch (err) {
            console.error('[historical] Failed to load path:', err);
            statusText.textContent = `Error loading path: ${err.message}`;
        }
    }

    function togglePlayback() {
        const btnPlay = document.getElementById('btn-play-path');
        
        if (isPlaying) {
            // Stop playback
            isPlaying = false;
            btnPlay.textContent = '▶ Play';
            if (playbackTimer) {
                clearInterval(playbackTimer);
                playbackTimer = null;
            }
        } else {
            // Start playback
            isPlaying = true;
            btnPlay.textContent = '⏸ Pause';
            playbackIndex = 0;
            
            playbackTimer = setInterval(() => {
                if (playbackIndex >= playbackPath.length) {
                    togglePlayback(); // Stop at end
                    return;
                }
                
                updatePlaybackPosition(playbackIndex);
                playbackIndex++;
            }, 200); // 5 points per second
        }
    }

    function updatePlaybackPosition(index) {
        if (index < 0 || index >= playbackPath.length) return;
        
        const point = playbackPath[index];
        const slider = document.getElementById('playback-slider');
        const info = document.getElementById('playback-info');
        
        slider.value = index;
        
        // Update marker position
        const features = playerPathSource.getFeatures();
        const marker = features.find(f => f.get('isCurrent'));
        if (marker) {
            marker.setGeometry(new ol.geom.Point([point.x, point.z]));
        }
        
        // Center map on current position (optional)
        // map.getView().animate({ center: [point.x, point.z], duration: 200 });
        
        // Update info
        const percentage = ((index / playbackPath.length) * 100).toFixed(1);
        info.textContent = `Position ${index + 1}/${playbackPath.length} (${percentage}%)`;
    }

    // Manual playback slider control
    document.addEventListener('DOMContentLoaded', () => {
        const slider = document.getElementById('playback-slider');
        if (slider) {
            slider.addEventListener('input', (e) => {
                if (isPlaying) return; // Don't allow manual control during playback
                updatePlaybackPosition(parseInt(e.target.value));
            });
        }
    });

    function clearAll() {
        heatmapSource.clear();
        playerPathSource.clear();
        deathMarkerSource.clear();
        playbackPath = [];
        isPlaying = false;
        playbackIndex = 0;
        
        if (playbackTimer) {
            clearInterval(playbackTimer);
            playbackTimer = null;
        }
        
        document.getElementById('chk-heatmap').checked = false;
        document.getElementById('chk-player-path').checked = false;
        document.getElementById('btn-play-path').disabled = true;
        document.getElementById('playback-progress').classList.remove('active');
        document.getElementById('status-text').textContent = 'Cleared all data';
    }

    // Initialize UI when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', createHistoricalUI);
    } else {
        createHistoricalUI();
    }

    console.log('[WebCartographer] Historical layers initialized');
})();

