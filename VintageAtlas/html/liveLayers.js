// Direct connection version (no PHP proxy)
// Connects directly to Vintage Story VintageAtlas API

function pickAnimalEmoji(str){
  if (!str) return '';
  const s = String(str).toLowerCase();

  // Birds
  if (s.includes('henpoult') || s.includes('pullet')) return '🐥';
  if (s.includes('chicken-baby')) return '🐔';
  if (s.includes('chick')) return '🐥';
  if (s.includes('rooster')) return '🐓';
  if (s.includes('hen')) return '🐔';
  if (s.includes('duck')) return '🦆';
  if (s.includes('owl')) return '🦉';
  if (s.includes('robin')) return '🐦';
  if (s.includes('waxwing')) return '🐦';
  if (s.includes('sparrow') || s.includes('house-sparrow')) return '🐦';
  if (s.includes('swan')) return '🦢';

  // Mammals
  if (s.includes('raccoon')) return '🦝';
  if (s.includes('wolf')) return '🐺';
  if (s.includes('fox')) return '🦊';
  if (s.includes('hare')) return '🐇';
  if (s.includes('goat')) return '🐐';
  if (s.includes('bear')) return '🐻';
  if (s.includes('boar')) return '🐗';
  if (s.includes('deer')) return '🦌';
  if (s.includes('chipmunk') || s.includes('squirrel')) return '🐿️';
  if (s.includes('fieldmouse') || s.includes('field-mouse') || s.includes('field mouse')) return '🐭';
  if (s.includes('hedgehog')) return '🦔';
  if (s.includes('yak')) return '🐂';
  if (s.includes('crow')) return '🐦‍⬛';

  // Invertebrates
  if (s.includes('snail')) return '🐌';
  if (s.includes('crab')) return '🦀';

  // Fish
  if (s.includes('salmon')) return '🐟';

  // Other
  if (s.includes('strawdummy') || s.includes('straw')) return '🧍‍♂️';
  if (s.includes('animal')) return '🐾';
  return '❓';
}

(function(){
  if (typeof ol === 'undefined' || typeof map === 'undefined') {
    console.error('[liveLayers-direct] OpenLayers or map not found');
    return;
  }

  // --- Config ---
  // Detect base path from <base> tag for nginx sub-path support
  function getBasePath() {
    const baseEl = document.querySelector('base[href]');
    if (baseEl) {
      const href = baseEl.getAttribute('href');
      // Ensure it ends with / and starts with /
      if (href && href !== '__BASE_PATH__') {
        return href.endsWith('/') ? href : href + '/';
      }
    }
    return '/';
  }
  
  const BASE_PATH = getBasePath();
  
  // For integrated server, we use relative API path
  // The mod serves the web UI and API from the same server
  const SERVERS_JSON = null; // Not used in integrated mode
  const INTEGRATED_API_URL = BASE_PATH + 'api/status'; // Respects base path for nginx

  // --- Sources & Layers ---
  const playersSource = new ol.source.Vector();
  const animalsSource = new ol.source.Vector();
  const spawnSource   = new ol.source.Vector();

  // --- Font / util ---
  function getLabelFontPx() {
    const base = parseInt(localStorage.labelSize || 10, 10);
    return isFinite(base) ? base : 12;
  }
  function getLabelFont(mult = 0) {
    const px = Math.max(6, getLabelFontPx() + mult);
    return 'bold ' + px + 'px sans-serif';
  }
  function lineHeight(mult=1.35){ return Math.round(getLabelFontPx() * mult); }

  function clamp(n, a, b){ return Math.max(a, Math.min(b, n)); }
  function pct(cur, max){ cur = Number(cur) || 0; max = Number(max) || 1; return clamp((cur/max)*100, 0, 100); }
  function lerp(a, b, t){ return a + (b - a) * t; }
  function colorRedGreen(percent){
    const t = clamp(percent/100, 0, 1);
    const r = Math.round(lerp(255, 20, t));
    const g = Math.round(lerp(20, 200, t));
    const b = Math.round(lerp(20, 20, t));
    return 'rgb(' + r + ',' + g + ',' + b + ')';
  }
  function tempColorCss(t){
    if (!isFinite(t)) return '#FFFFFF';
    if (t <= -10) return '#4aa3ff';
    if (t <=   5) return '#5cb3ff';
    if (t <=  18) return '#2ecc71';
    if (t <=  28) return '#f1c40f';
    if (t <=  35) return '#e67e22';
    return '#e74c3c';
  }

  // --- Toggles (localStorage) ---
  const SHOW_PLAYER_STATS_KEY = 'showPlayerStats';
  const SHOW_ANIMAL_HP_KEY    = 'showAnimalStats';
  const SHOW_COORDS_KEY       = 'showCoords';
  const SHOW_ANIMAL_ENV_KEY   = 'showAnimalEnv';
  const SHOW_PLAYERS_KEY      = 'showPlayers';
  const SHOW_ANIMALS_KEY      = 'showAnimals';

  function getBool(key, def){ try { return JSON.parse(localStorage.getItem(key) || String(def)); } catch(e){ return def; } }
  function setBool(key, val){ localStorage.setItem(key, JSON.stringify(!!val)); }

  // Defaults
  const showPlayersDefault    = true;
  const showAnimalsDefault    = true;
  const showStatsDefault      = false;
  const showAnimalDefault     = false;
  const showCoordsDefault     = false;
  const showAnimalEnvDefault  = false;

  // --- UI controls ---
  function ensureUi(){
    if (document.getElementById('livectrl')) return;
    const css = document.createElement('style');
    css.textContent = `
      #livectrl{position:absolute;top:10px;left:10px;z-index:5000;background:rgba(0,0,0,.7);color:#fff;
        padding:8px 12px;border-radius:12px;font:13px/1.4 sans-serif;display:flex;gap:12px;align-items:center;
        backdrop-filter: blur(4px);box-shadow:0 4px 12px rgba(0,0,0,.3);transition:all .3s ease;}
      #livectrl label{display:flex;align-items:center;gap:6px;cursor:pointer;user-select:none;
        padding:4px 6px;border-radius:6px;transition:background .2s ease;}
      #livectrl label:hover{background:rgba(255,255,255,.1);}
      #livectrl label:focus-within{background:rgba(255,255,255,.15);outline:2px solid rgba(255,255,255,.5);outline-offset:2px;}
      #livectrl input[type="checkbox"]{width:18px;height:18px;cursor:pointer;accent-color:#4a9eff;}
      #livectrl input[type="checkbox"]:focus{outline:2px solid #4a9eff;outline-offset:2px;}
      #livectrl .sep{opacity:.35;margin:0 4px;}
      #livectrl .status{color:#8f8;font-size:14px;cursor:help;padding:4px 8px;border-radius:6px;
        transition:all .2s ease;min-width:20px;text-align:center;}
      #livectrl .status:hover{background:rgba(255,255,255,.1);}
      #livectrl .status.error{color:#f88;}
      #livectrl .status.warning{color:#fd3;}
      #livectrl .status.reconnecting{color:#f90;animation:pulse 1s ease-in-out infinite;}
      @keyframes pulse{0%,100%{opacity:1;}50%{opacity:.5;}}
      
      /* Mobile responsiveness */
      @media (max-width: 768px) {
        #livectrl{flex-wrap:wrap;max-width:calc(100vw - 20px);font-size:12px;padding:10px;gap:8px;top:5px;left:5px;}
        #livectrl label{padding:6px 8px;min-height:36px;}
        #livectrl input[type="checkbox"]{width:20px;height:20px;}
        #livectrl .status{font-size:16px;min-width:24px;padding:6px 10px;}
      }
      
      @media (max-width: 480px) {
        #livectrl{font-size:11px;padding:8px;gap:6px;}
        #livectrl .sep{display:none;}
        #livectrl label{padding:8px;min-height:40px;flex:0 0 calc(50% - 3px);}
      }
      
      /* High contrast mode support */
      @media (prefers-contrast: high) {
        #livectrl{background:rgba(0,0,0,.9);border:2px solid #fff;}
        #livectrl .status{font-weight:bold;}
      }
      
      /* Reduced motion support */
      @media (prefers-reduced-motion: reduce) {
        #livectrl{transition:none;}
        #livectrl label{transition:none;}
        #livectrl .status{transition:none;}
        .status.reconnecting{animation:none;}
      }
      
      /* Help button */
      .help-btn{background:rgba(74,158,255,.2);border:1px solid rgba(74,158,255,.5);color:#4a9eff;
        border-radius:50%;width:24px;height:24px;cursor:pointer;font-weight:bold;font-size:14px;
        transition:all .2s ease;padding:0;margin-left:4px;}
      .help-btn:hover{background:rgba(74,158,255,.4);transform:scale(1.1);}
      .help-btn:focus{outline:2px solid #4a9eff;outline-offset:2px;}
      
      /* Help dialog */
      .help-dialog{position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);
        background:rgba(0,0,0,.95);color:#fff;padding:24px;border-radius:12px;
        max-width:500px;width:calc(100vw - 40px);box-shadow:0 8px 32px rgba(0,0,0,.5);
        z-index:10000;backdrop-filter:blur(8px);border:2px solid rgba(255,255,255,.1);}
      .help-dialog h3{margin:0 0 16px;color:#4a9eff;font-size:20px;}
      .help-dialog .shortcut{display:flex;justify-content:space-between;padding:8px 0;
        border-bottom:1px solid rgba(255,255,255,.1);}
      .help-dialog .shortcut:last-child{border-bottom:none;}
      .help-dialog .key{background:rgba(74,158,255,.2);padding:4px 8px;border-radius:4px;
        font-family:monospace;font-size:13px;color:#4a9eff;}
      .help-dialog .close-btn{background:#4a9eff;border:none;color:#fff;padding:10px 20px;
        border-radius:6px;cursor:pointer;margin-top:16px;width:100%;font-weight:bold;
        font-size:14px;transition:background .2s ease;}
      .help-dialog .close-btn:hover{background:#357abd;}
      .help-dialog .close-btn:focus{outline:2px solid #fff;outline-offset:2px;}
      .help-overlay{position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,.7);
        z-index:9999;backdrop-filter:blur(2px);}
      
      @media (max-width: 480px) {
        .help-dialog{padding:20px;max-width:none;}
        .help-dialog h3{font-size:18px;}
        .help-btn{width:28px;height:28px;font-size:16px;}
      }
    `;
    document.head.appendChild(css);
    const div = document.createElement('div');
    div.id = 'livectrl';
    div.setAttribute('role', 'toolbar');
    div.setAttribute('aria-label', 'Live map controls');
    div.innerHTML = `
      <span class="status" id="liveStatus" role="status" aria-live="polite" aria-atomic="true">●</span>
      <label title="Toggle player layer visibility">
        <input type="checkbox" id="chkPlayers" aria-label="Show players on map">
        Players
      </label>
      <label title="Show player statistics (HP, hunger, temperature, body temperature)">
        <input type="checkbox" id="chkPlayerStats" aria-label="Show player stats">
        Player stats
      </label>
      <span class="sep" aria-hidden="true">|</span>
      <label title="Toggle animal layer visibility">
        <input type="checkbox" id="chkAnimals" aria-label="Show animals on map">
        Animals
      </label>
      <label title="Show animal health bars">
        <input type="checkbox" id="chkAnimalHP" aria-label="Show animal HP">
        Animal HP
      </label>
      <label title="Show animal environment data (temperature, rain, wind)">
        <input type="checkbox" id="chkAnimalEnv" aria-label="Show animal environment">
        Animal env
      </label>
      <span class="sep" aria-hidden="true">|</span>
      <label title="Show coordinates relative to spawn point">
        <input type="checkbox" id="chkCoords" aria-label="Show coordinates">
        Coordinates
      </label>
      <button id="helpButton" class="help-btn" aria-label="Show keyboard shortcuts" title="Keyboard shortcuts">
        ?
      </button>
    `;
    document.body.appendChild(div);

    // Init states
    const chkPly = div.querySelector('#chkPlayers');
    const chkP   = div.querySelector('#chkPlayerStats');
    const chkAni = div.querySelector('#chkAnimals');
    const chkA   = div.querySelector('#chkAnimalHP');
    const chkE   = div.querySelector('#chkAnimalEnv');
    const chkC   = div.querySelector('#chkCoords');

    chkPly.checked = getBool(SHOW_PLAYERS_KEY, showPlayersDefault);
    chkP.checked   = getBool(SHOW_PLAYER_STATS_KEY, showStatsDefault);
    chkAni.checked = getBool(SHOW_ANIMALS_KEY, showAnimalsDefault);
    chkA.checked   = getBool(SHOW_ANIMAL_HP_KEY, showAnimalDefault);
    chkE.checked   = getBool(SHOW_ANIMAL_ENV_KEY, showAnimalEnvDefault);
    chkC.checked   = getBool(SHOW_COORDS_KEY, showCoordsDefault);

    // Handlers
    chkPly.addEventListener('change', () => {
      setBool(SHOW_PLAYERS_KEY, chkPly.checked);
      if (typeof playersLayer !== 'undefined') playersLayer.setVisible(chkPly.checked);
      playersSource.clear(true);
    });
    chkP.addEventListener('change', () => {
      setBool(SHOW_PLAYER_STATS_KEY, chkP.checked);
      playersSource.changed();
    });
    chkAni.addEventListener('change', () => {
      setBool(SHOW_ANIMALS_KEY, chkAni.checked);
      if (typeof animalsLayer !== 'undefined') animalsLayer.setVisible(chkAni.checked);
      animalsSource.clear(true);
    });
    chkA.addEventListener('change', () => {
      setBool(SHOW_ANIMAL_HP_KEY, chkA.checked);
      animalsSource.changed();
    });
    chkE.addEventListener('change', () => {
      setBool(SHOW_ANIMAL_ENV_KEY, chkE.checked);
      animalsSource.changed();
    });
    chkC.addEventListener('change', () => {
      setBool(SHOW_COORDS_KEY, chkC.checked);
      playersSource.changed();
      animalsSource.changed();
    });
  }
  ensureUi();
  
  // Help dialog functionality
  function showHelpDialog() {
    const overlay = document.createElement('div');
    overlay.className = 'help-overlay';
    overlay.setAttribute('role', 'presentation');
    
    const dialog = document.createElement('div');
    dialog.className = 'help-dialog';
    dialog.setAttribute('role', 'dialog');
    dialog.setAttribute('aria-labelledby', 'help-title');
    dialog.setAttribute('aria-modal', 'true');
    
    dialog.innerHTML = `
      <h3 id="help-title">⌨️ Keyboard Shortcuts</h3>
      <div class="shortcut">
        <span>Toggle Players</span>
        <kbd class="key">Alt + P</kbd>
      </div>
      <div class="shortcut">
        <span>Toggle Player Stats</span>
        <kbd class="key">Alt + S</kbd>
      </div>
      <div class="shortcut">
        <span>Toggle Animals</span>
        <kbd class="key">Alt + A</kbd>
      </div>
      <div class="shortcut">
        <span>Toggle Animal HP</span>
        <kbd class="key">Alt + H</kbd>
      </div>
      <div class="shortcut">
        <span>Toggle Animal Environment</span>
        <kbd class="key">Alt + E</kbd>
      </div>
      <div class="shortcut">
        <span>Toggle Coordinates</span>
        <kbd class="key">Alt + C</kbd>
      </div>
      <button class="close-btn" id="closeHelp">Got it!</button>
    `;
    
    document.body.appendChild(overlay);
    document.body.appendChild(dialog);
    
    // Focus management
    const closeBtn = dialog.querySelector('#closeHelp');
    closeBtn.focus();
    
    function closeDialog() {
      document.body.removeChild(overlay);
      document.body.removeChild(dialog);
      document.getElementById('helpButton')?.focus();
    }
    
    closeBtn.addEventListener('click', closeDialog);
    overlay.addEventListener('click', closeDialog);
    
    // Escape key to close
    const escHandler = (e) => {
      if (e.key === 'Escape') {
        closeDialog();
        document.removeEventListener('keydown', escHandler);
      }
    };
    document.addEventListener('keydown', escHandler);
  }
  
  // Attach help button event
  setTimeout(() => {
    const helpBtn = document.getElementById('helpButton');
    if (helpBtn) {
      helpBtn.addEventListener('click', showHelpDialog);
    }
  }, 100);
  
  // Keyboard navigation
  document.addEventListener('keydown', (e) => {
    // Skip if user is typing in an input
    if (e.target.tagName === 'INPUT' && e.target.type === 'text') return;
    
    // Keyboard shortcuts (Alt+Key to avoid conflicts)
    if (e.altKey) {
      const shortcuts = {
        'p': 'chkPlayers',      // Alt+P: Toggle players
        's': 'chkPlayerStats',  // Alt+S: Toggle player stats
        'a': 'chkAnimals',      // Alt+A: Toggle animals
        'h': 'chkAnimalHP',     // Alt+H: Toggle animal HP
        'e': 'chkAnimalEnv',    // Alt+E: Toggle animal environment
        'c': 'chkCoords'        // Alt+C: Toggle coordinates
      };
      
      const elementId = shortcuts[e.key.toLowerCase()];
      if (elementId) {
        e.preventDefault();
        const checkbox = document.getElementById(elementId);
        if (checkbox) {
          checkbox.checked = !checkbox.checked;
          checkbox.dispatchEvent(new Event('change'));
          // Announce to screen readers
          const label = checkbox.parentElement.textContent.trim();
          announceToScreenReader(`${label} ${checkbox.checked ? 'enabled' : 'disabled'}`);
        }
      }
    }
  });
  
  // Screen reader announcements
  function announceToScreenReader(message) {
    const announcement = document.createElement('div');
    announcement.setAttribute('role', 'status');
    announcement.setAttribute('aria-live', 'polite');
    announcement.className = 'sr-only';
    announcement.textContent = message;
    document.body.appendChild(announcement);
    setTimeout(() => document.body.removeChild(announcement), 1000);
  }
  
  // Add screen-reader-only class
  const srOnlyStyle = document.createElement('style');
  srOnlyStyle.textContent = `
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0,0,0,0);
      white-space: nowrap;
      border-width: 0;
    }
  `;
  document.head.appendChild(srOnlyStyle);

  let spawnTempC = null;
  let spawnRain1 = false;
  let spawnRainVal = 0;

  // --- Spawn layer ---  
  const SPAWN_EMOJI = '📍';
  
  const spawnLayer = new ol.layer.Vector({
    zIndex: 1001,
    source: spawnSource,
    style: function(feature){
      const styles = [];
      styles.push(new ol.style.Style({
        text: new ol.style.Text({
          text: SPAWN_EMOJI,
          font: getLabelFont(+8),
          textAlign: 'center',
          textBaseline: 'middle',
          offsetX: 0,
          offsetY: 0
        })
      }));
      styles.push(new ol.style.Style({
        text: new ol.style.Text({
          text: 'Spawn',
          font: getLabelFont(0),
          textAlign: 'center',
          textBaseline: 'bottom',
          offsetY: -40,
          backgroundFill: new ol.style.Fill({ color: 'rgba(0,0,0,0.45)' }),
          padding: [2,4,2,4],
          fill: new ol.style.Fill({ color: '#FFFFFF' }),
          stroke: new ol.style.Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
        })
      }));
      if (Number.isFinite(spawnTempC)) {
        styles.push(new ol.style.Style({
          text: new ol.style.Text({
            text: '🌡️ ' + spawnTempC.toFixed(1) + '°C',
            font: getLabelFont(-2),
            textAlign: 'center',
            textBaseline: 'top',
            offsetY: +16,
            backgroundFill: new ol.style.Fill({ color: 'rgba(0,0,0,0.45)' }),
            padding: [1,3,1,3],
            fill: new ol.style.Fill({ color: '#FFFFFF' }),
            stroke: new ol.style.Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
          })
        }));
      }
      return styles;
    }
  });

  // [Rest of the styling code - same as original liveLayers.js]
  const BAR_LEN_PLAYER = 16;
  const BAR_LEN_ANIMAL = 8;
  const BAR_BG_CHAR = '░';
  const BAR_FILL_CHAR = '█';
  const BAR_BG_COLOR = '#aaaaaa';

  function pushBarStyles(styles, baseOffsetY, row, params){
    const { colorFill, labelPrefix, cur, max, barLen, fontDelta, leftOffset } = params;
    const safeMax = (Number(max) > 0) ? Number(max) : 1;
    const percent = pct(cur, safeMax);
    const countFill = Math.round(clamp(percent,0,100) / 100 * barLen);

    const bgBar = BAR_BG_CHAR.repeat(barLen);
    const fgBar = BAR_FILL_CHAR.repeat(countFill);

    const curStr = String(Math.round(Number(cur)||0));
    const maxStr = String(Math.round(Number(max)||0));
    const midText = curStr + '/' + maxStr;
    const innerPad = Math.max(0, Math.floor((barLen - midText.length)/2));
    const midLine = labelPrefix + ' ' + ' '.repeat(innerPad) + midText;

    styles.push(new ol.style.Style({
      text: new ol.style.Text({
        text: labelPrefix + ' ' + bgBar,
        font: getLabelFont(fontDelta),
        textAlign: 'left', textBaseline: 'top',
        offsetX: leftOffset,
        offsetY: baseOffsetY + row * lineHeight(),
        backgroundFill: new ol.style.Fill({ color: 'rgba(0,0,0,0.45)' }),
        padding: [2,4,2,4],
        fill: new ol.style.Fill({ color: BAR_BG_COLOR }),
        stroke: new ol.style.Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
      })
    }));

    styles.push(new ol.style.Style({
      text: new ol.style.Text({
        text: labelPrefix + ' ' + fgBar,
        font: getLabelFont(fontDelta),
        textAlign: 'left', textBaseline: 'top',
        offsetX: leftOffset,
        offsetY: baseOffsetY + row * lineHeight(),
        backgroundFill: new ol.style.Fill({ color: 'rgba(0,0,0,0.0)' }),
        padding: [2,4,2,4],
        fill: new ol.style.Fill({ color: colorFill }),
        stroke: new ol.style.Stroke({ color: 'rgba(0,0,0,0.0)', width: 0 })
      })
    }));

    styles.push(new ol.style.Style({
      text: new ol.style.Text({
        text: midLine,
        font: getLabelFont(fontDelta),
        textAlign: 'left', textBaseline: 'top',
        offsetX: leftOffset,
        offsetY: baseOffsetY + row * lineHeight(),
        backgroundFill: new ol.style.Fill({ color: 'rgba(0,0,0,0.0)' }),
        padding: [2,4,2,4],
        fill: new ol.style.Fill({ color: '#FFFFFF' }),
        stroke: new ol.style.Stroke({ color: 'rgba(0,0,0,0.0)', width: 0 })
      })
    }));
  }

  // Player and Animal layer styling (using same code as original)
  const playersLayer = new ol.layer.Vector({
    zIndex: 1002,
    source: playersSource,
    style: function(feature){
      const showGroup = getBool(SHOW_PLAYERS_KEY, showPlayersDefault);
      if (!showGroup) return [];

      const showStats  = getBool(SHOW_PLAYER_STATS_KEY, showStatsDefault);
      const showCoords = getBool(SHOW_COORDS_KEY, showCoordsDefault);

      const name = feature.get('name') || 'Player';
      const hp = Number(feature.get('hp') || 0);
      const hpMax = Number(feature.get('hpMax') || 1);
      const hungry = Number(feature.get('hunger') || 0);
      const hungryMax = Number(feature.get('hungerMax') || 1);
      const temp = feature.get('temp');
      const btemp = feature.get('bodytemp');
      const rx = feature.get('rx'), ry = feature.get('ry'), rz = feature.get('rz');

      const markerColor = colorRedGreen(pct(hp, hpMax));
      const tColor = tempColorCss(temp);

      const styles = [];
      let row = 0;
      const baseOffX = 12;
      function baseOffsetY(lineCount){ return - (lineCount * lineHeight() + 12); }

      let totalLines = 1;
      if (showStats) totalLines += 4;
      if (showCoords) totalLines += 1;

      styles.push(new ol.style.Style({
        image: new ol.style.Circle({
          radius: 5,
          fill: new ol.style.Fill({ color: markerColor }),
          stroke: new ol.style.Stroke({ color: '#FFFFFF', width: 2 })
        })
      }));
      styles.push(new ol.style.Style({
        text: new ol.style.Text({
          text: '🧍',
          font: getLabelFont(+6),
          textAlign: 'center',
          textBaseline: 'middle',
          offsetX: 0, offsetY: 0
        })
      }));

      styles.push(new ol.style.Style({
        text: new ol.style.Text({
          text: name,
          font: getLabelFont(0),
          textAlign: 'left', textBaseline: 'top',
          offsetX: baseOffX,
          offsetY: baseOffsetY(totalLines) + row * lineHeight(),
          backgroundFill: new ol.style.Fill({ color: 'rgba(0,0,0,0.45)' }),
          padding: [2,4,2,4],
          fill: new ol.style.Fill({ color: '#FFFFFF' }),
          stroke: new ol.style.Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
        })
      }));
      row++;

      if (showStats){
        pushBarStyles(styles, baseOffsetY(totalLines), row, {
          colorFill: '#ff4d4f', labelPrefix: '❤️', cur: hp, max: hpMax,
          barLen: BAR_LEN_PLAYER, fontDelta: -3, leftOffset: baseOffX
        }); row++;

        pushBarStyles(styles, baseOffsetY(totalLines), row, {
          colorFill: '#2ecc71', labelPrefix: '🍗', cur: hungry, max: hungryMax,
          barLen: BAR_LEN_PLAYER, fontDelta: -3, leftOffset: baseOffX
        }); row++;

        const tStr = isFinite(temp) ? (temp.toFixed(1) + '°C') : '—';
        styles.push(new ol.style.Style({
          text: new ol.style.Text({
            text: '🌡️ ' + tStr,
            font: getLabelFont(0),
            textAlign: 'left', textBaseline: 'top',
            offsetX: baseOffX,
            offsetY: baseOffsetY(totalLines) + row * lineHeight(),
            backgroundFill: new ol.style.Fill({ color: 'rgba(0,0,0,0.45)' }),
            padding: [2,4,2,4],
            fill: new ol.style.Fill({ color: tColor }),
            stroke: new ol.style.Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
          })
        })); row++;

        const btVal = isFinite(btemp) ? Math.min(37, Number(btemp)) : null;
        const btStr = (btVal === null) ? '—' : (btVal.toFixed(1) + '°C');
        styles.push(new ol.style.Style({
          text: new ol.style.Text({
            text: '🧍 ' + btStr,
            font: getLabelFont(0),
            textAlign: 'left', textBaseline: 'top',
            offsetX: baseOffX,
            offsetY: baseOffsetY(totalLines) + row * lineHeight(),
            backgroundFill: new ol.style.Fill({ color: 'rgba(0,0,0,0.45)' }),
            padding: [2,4,2,4],
            fill: new ol.style.Fill({ color: '#FFFFFF' }),
            stroke: new ol.style.Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
          })
        })); row++;
      }

      if (showCoords){
        const coordText = (isFinite(rx) && isFinite(ry) && isFinite(rz))
          ? ('X: ' + Math.round(rx) + '   Z: ' + Math.round(rz) + '   Y: ' + Math.round(ry))
          : '';
        if (coordText){
          styles.push(new ol.style.Style({
            text: new ol.style.Text({
              text: coordText,
              font: getLabelFont(0),
              textAlign: 'left', textBaseline: 'top',
              offsetX: baseOffX,
              offsetY: baseOffsetY(totalLines) + row * lineHeight(),
              backgroundFill: new ol.style.Fill({ color: 'rgba(0,0,0,0.45)' }),
              padding: [2,4,2,4],
              fill: new ol.style.Fill({ color: '#FFFFFF' }),
              stroke: new ol.style.Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
            })
          }));
        }
      }

      return styles;
    }
  });

  const animalsLayer = new ol.layer.Vector({
    zIndex: 990,
    source: animalsSource,
    style: function(feature){
      const showGroup = getBool(SHOW_ANIMALS_KEY, showAnimalsDefault);
      if (!showGroup) return [];

      const showHP = getBool(SHOW_ANIMAL_HP_KEY, showAnimalDefault);
      const showCoords = getBool(SHOW_COORDS_KEY, showCoordsDefault);
      const showEnv = getBool(SHOW_ANIMAL_ENV_KEY, showAnimalEnvDefault);

      const type = feature.get('type') || '';
      const label = feature.get('label') || type || 'Animal';
      const labelText = label;

      const hp = Number(feature.get('hp') || 0);
      const hpMax = Number(feature.get('hpMax') || 0);
      const hasHP = isFinite(hp) && isFinite(hpMax) && hpMax > 0;

      const aTemp = feature.get('atemp');
      const aRain = feature.get('arain');
      const windP = feature.get('awindp');
      const hasEnv = (aTemp !== null && aTemp !== undefined) || (aRain !== null && aRain !== undefined) || (windP !== null && windP !== undefined);

      const rx = feature.get('rx'), ry = feature.get('ry'), rz = feature.get('rz');

      const styles = [];
      let row = 0;
      const baseOffX = 10;
      function baseOffsetY(lineCount){ return - (lineCount * lineHeight() + 10); }

      let totalLines = 1;
      if (showHP && hasHP) totalLines += 1;
      if (showEnv && hasEnv) totalLines += 1;
      if (showCoords) totalLines += 1;

      styles.push(new ol.style.Style({
        image: new ol.style.Circle({
          radius: 4,
          fill: new ol.style.Fill({ color: '#8B5A2B' }),
          stroke: new ol.style.Stroke({ color: '#FFFFFF', width: 1 })
        })
      }));

      const iconEmoji = pickAnimalEmoji(label || type) || '❓';
      styles.push(new ol.style.Style({
        text: new ol.style.Text({
          text: iconEmoji,
          font: getLabelFont(+6),
          textAlign: 'center',
          textBaseline: 'middle',
          offsetX: 0, offsetY: 0
        })
      }));

      styles.push(new ol.style.Style({
        text: new ol.style.Text({
          text: labelText,
          font: getLabelFont(0),
          textAlign: 'left', textBaseline: 'top',
          offsetX: baseOffX,
          offsetY: baseOffsetY(totalLines) + row * lineHeight(),
          backgroundFill: new ol.style.Fill({ color: 'rgba(0,0,0,0.35)' }),
          padding: [1,3,1,3],
          fill: new ol.style.Fill({ color: '#FFFFFF' }),
          stroke: new ol.style.Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
        })
      }));
      row++;

      if (showHP && hasHP){
        pushBarStyles(styles, baseOffsetY(totalLines), row, {
          colorFill: '#ff4d4f', labelPrefix: '❤️', cur: hp, max: hpMax,
          barLen: BAR_LEN_ANIMAL, fontDelta: -4, leftOffset: baseOffX
        }); row++;
      }

      if (showEnv && hasEnv){
        const tStr = isFinite(aTemp) ? (aTemp.toFixed(1) + '°C') : '—';
        const tColor = tempColorCss(aTemp);
        const rainIc = Number(aRain ?? 0) > 0 ? ' 🌧️' : '';
        const windIc = isFinite(windP) ? (' 🌬️ ' + Math.round(windP) + '%') : '';
        styles.push(new ol.style.Style({
          text: new ol.style.Text({
            text: '🌡️ ' + tStr + rainIc + windIc,
            font: getLabelFont(0),
            textAlign: 'left', textBaseline: 'top',
            offsetX: baseOffX,
            offsetY: baseOffsetY(totalLines) + row * lineHeight(),
            backgroundFill: new ol.style.Fill({ color: 'rgba(0,0,0,0.35)' }),
            padding: [1,3,1,3],
            fill: new ol.style.Fill({ color: tColor }),
            stroke: new ol.style.Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
          })
        }));
        row++;
      }

      if (showCoords){
        const coordText = (isFinite(rx) && isFinite(ry) && isFinite(rz))
          ? ('X: ' + Math.round(rx) + '   Z: ' + Math.round(rz) + '   Y: ' + Math.round(ry))
          : '';
        if (coordText){
          styles.push(new ol.style.Style({
            text: new ol.style.Text({
              text: coordText,
              font: getLabelFont(0),
              textAlign: 'left', textBaseline: 'top',
              offsetX: baseOffX,
              offsetY: baseOffsetY(totalLines) + row * lineHeight(),
              backgroundFill: new ol.style.Fill({ color: 'rgba(0,0,0,0.35)' }),
              padding: [1,3,1,3],
              fill: new ol.style.Fill({ color: '#FFFFFF' }),
              stroke: new ol.style.Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
            })
          }));
        }
      }

      return styles;
    }
  });

  // Add layers
  map.addLayer(animalsLayer);
  map.addLayer(spawnLayer);
  map.addLayer(playersLayer);

  playersLayer.setVisible(getBool(SHOW_PLAYERS_KEY, showPlayersDefault));
  animalsLayer.setVisible(getBool(SHOW_ANIMALS_KEY, showAnimalsDefault));

  // --- Data fetchers ---
  async function fetchServers(){
    // In integrated mode, return the local API endpoint
    return [INTEGRATED_API_URL];
  }

  async function fetchServer(url){
    const res = await fetch(url, { cache: 'no-store' });
    if (!res.ok) throw new Error('Server HTTP ' + res.status);
    return await res.json();
  }

  function clearSources(){
    playersSource.clear(true);
    animalsSource.clear(true);
    spawnSource.clear(true);
  }

  // Debounce utility for performance
  function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
      const later = () => {
        clearTimeout(timeout);
        func(...args);
      };
      clearTimeout(timeout);
      timeout = setTimeout(later, wait);
    };
  }

  function updateStatusIndicator(status, message){
    const el = document.getElementById('liveStatus');
    if (!el) return;
    
    // status can be: 'ok', 'error', 'warning', 'reconnecting', 'loading'
    const statusConfig = {
      ok: { icon: '●', title: 'Connected - Live data updating', className: 'status' },
      error: { icon: '✕', title: message || 'Connection failed - Check server status', className: 'status error' },
      warning: { icon: '⚠', title: message || 'Connection issue - Retrying...', className: 'status warning' },
      reconnecting: { icon: '↻', title: message || 'Reconnecting...', className: 'status reconnecting' },
      loading: { icon: '⏳', title: 'Loading data...', className: 'status reconnecting' }
    };
    
    const config = statusConfig[status] || statusConfig.error;
    el.className = config.className;
    el.textContent = config.icon;
    el.title = config.title;
    
    // Update aria-label for screen readers
    el.setAttribute('aria-label', config.title);
  }

  function updateFromData(d){
    if (!d) return;
    clearSources();

    const sp = d.spawnPoint || { x: 0, y: 0, z: 0 };
    const spawnX = Number(sp.x || 0);
    const spawnZ = Number(sp.z || 0);

    // Temperature
    let t = null;
    try{
      if (d && typeof d === 'object'){
        if (Number.isFinite(d.spawnTemperature)) t = Number(d.spawnTemperature);
        else if (d.weather && Number.isFinite(d.weather.temperature)) t = Number(d.weather.temperature);
      }
    }catch(e){}
    spawnTempC = (Number.isFinite(t) ? Number(t) : null);

    // Date/time
    try{
      const dt = d && d.date ? d.date : null;
      if (dt){
        const pad2 = (n) => String(Math.trunc(Number(n)||0)).padStart(2,'0');
        const pad4 = (n) => String(Math.trunc(Number(n)||0)).padStart(4,'0');
        const y = pad4(dt.year);
        const mo = pad2(dt.month);
        const da = pad2(dt.day);
        const hh = pad2(dt.hour);
        const mi = pad2(dt.minute);
        const el = document.getElementById('serverDateTop');
        if (el) el.textContent = `${y},${mo},${da} ${hh}:${mi}`;
      }
    }catch(e){}

    spawnSource.addFeature(new ol.Feature({ geometry: new ol.geom.Point([0, 0]) }));

    const wantPlayers = getBool(SHOW_PLAYERS_KEY, showPlayersDefault);
    const wantAnimals = getBool(SHOW_ANIMALS_KEY, showAnimalsDefault);

    // Players
    if (wantPlayers){
      const players = Array.isArray(d.players) ? d.players : [];
      try{
        const names = players.map(p => String(p.name || 'Player')).filter(Boolean);
        const elp = document.getElementById('onlinePlayersTop');
        if (elp){
          const esc = (s)=>s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
          if (names.length === 0){
            elp.innerHTML = '<div class="hdr">Online players</div><div class="empty">— none —</div>';
          }else{
            elp.innerHTML = '<div class="hdr">Online players ('+names.length+')</div><ul>' + names.map(n=>'<li>'+esc(n)+'</li>').join('') + '</ul>';
          }
        }
      }catch(e){}
    
      for (const p of players){
        const coords = p.coordinates || {};
        const WX = Number(coords.x ?? 0);
        const WZ = Number(coords.z ?? 0);
        const WY = Number(coords.y ?? 0);
        const RX = WX - spawnX;
        const RZ = WZ - spawnZ;

        const health = p.health || {};
        const hunger = p.hunger || {};

        playersSource.addFeature(new ol.Feature({
          geometry: new ol.geom.Point([RX, -RZ]),
          name: p.name || 'Player',
          rx: RX, ry: WY, rz: RZ,
          hp: Number(health.current ?? 0),
          hpMax: Number(health.max ?? 1),
          hunger: Number(hunger.current ?? 0),
          hungerMax: Number(hunger.max ?? 1),
          temp: Number.isFinite(p.temperature) ? Number(p.temperature) : null,
          bodytemp: Number.isFinite(p.bodyTemp) ? Number(p.bodyTemp) : null
        }));
      }
    }

    // Animals
    if (wantAnimals){
      const animals = Array.isArray(d.animals) ? d.animals : [];
      for (const a of animals){
        const coords = a.coordinates || {};
        const WX = Number(coords.x ?? 0);
        const WZ = Number(coords.z ?? 0);
        const WY = Number(coords.y ?? 0);

        const RX = WX - spawnX;
        const RZ = WZ - spawnZ;

        const health = a.health || {};
        const wind = a.wind || {};

        animalsSource.addFeature(new ol.Feature({
          geometry: new ol.geom.Point([RX, -RZ]),
          type: a.type || '',
          label: a.name || (a.type || ''),
          rx: RX, ry: WY, rz: RZ,
          hp: Number(health.current ?? NaN),
          hpMax: Number(health.max ?? NaN),
          atemp: Number.isFinite(a.temperature) ? Number(a.temperature) : null,
          arain: Number.isFinite(a.rainfall) ? Number(a.rainfall) : null,
          awindp: Number.isFinite(wind.percent) ? Math.round(wind.percent) : null
        }));
      }
    }
  }

  let activeServer = null;
  let retryCount = 0;
  let maxRetries = 3;
  let retryTimeout = null;
  let isOnline = navigator.onLine;
  
  // Monitor online/offline status
  window.addEventListener('online', () => {
    isOnline = true;
    console.log('[liveLayers] Network online - resuming updates');
    updateStatusIndicator('warning', 'Network restored - Reconnecting...');
    retryCount = 0;
    refresh();
  });
  
  window.addEventListener('offline', () => {
    isOnline = false;
    console.log('[liveLayers] Network offline');
    updateStatusIndicator('error', 'No internet connection');
  });

  async function refresh(){
    // Don't try to refresh if offline
    if (!isOnline) {
      updateStatusIndicator('error', 'No internet connection');
      return;
    }

    try{
      // Show loading indicator
      updateStatusIndicator('loading', 'Fetching live data...');
      
      if (!activeServer){
        const servers = await fetchServers();
        activeServer = servers.length ? servers[0] : null;
        console.log('[liveLayers-direct] Using server:', activeServer);
      }
      if (!activeServer) {
        updateStatusIndicator('error', 'No server configured');
        return;
      }
      
      const payload = await fetchServer(activeServer);
      
      // Validate response
      if (!payload || typeof payload !== 'object') {
        throw new Error('Invalid server response');
      }
      
      updateFromData(payload);
      updateStatusIndicator('ok');
      
      // Reset retry count on success
      retryCount = 0;
      
      // Announce update to screen readers (debounced)
      const announceUpdate = debounce(() => {
        const playerCount = Array.isArray(payload.players) ? payload.players.length : 0;
        const animalCount = Array.isArray(payload.animals) ? payload.animals.length : 0;
        if (playerCount > 0 || animalCount > 0) {
          announceToScreenReader(`Map updated: ${playerCount} players, ${animalCount} animals`);
        }
      }, 5000);
      announceUpdate();
    }catch(err){
      console.error('[liveLayers-direct] refresh failed', err);
      
      // Determine error type and message
      let errorMessage = 'Connection error';
      if (err.message === 'Server HTTP 503') {
        errorMessage = 'Server starting up - Retrying...';
        updateStatusIndicator('warning', errorMessage);
      } else if (err.message === 'Server HTTP 500') {
        errorMessage = 'Server error - Please check logs';
        updateStatusIndicator('error', errorMessage);
      } else if (err.message.includes('NetworkError') || err.message.includes('Failed to fetch')) {
        errorMessage = 'Cannot reach server - Retrying...';
        updateStatusIndicator('reconnecting', errorMessage);
      } else {
        updateStatusIndicator('error', errorMessage + ': ' + err.message);
      }
      
      // Retry with exponential backoff
      if (retryCount < maxRetries) {
        retryCount++;
        const backoffTime = Math.min(1000 * Math.pow(2, retryCount), 30000); // Max 30s
        console.log(`[liveLayers-direct] Retry ${retryCount}/${maxRetries} in ${backoffTime}ms`);
        
        updateStatusIndicator('reconnecting', `Retrying in ${Math.round(backoffTime/1000)}s... (${retryCount}/${maxRetries})`);
        
        if (retryTimeout) clearTimeout(retryTimeout);
        retryTimeout = setTimeout(() => {
          refresh();
        }, backoffTime);
      } else {
        // Max retries reached, wait for next interval
        updateStatusIndicator('error', 'Connection failed - Will retry in 15s');
        retryCount = 0; // Reset for next interval
      }
    }
  }

  // Initial run + schedule
  refresh();
  const refreshInterval = setInterval(() => {
    // Only refresh if we're not in the middle of retrying
    if (!retryTimeout) {
      refresh();
    }
  }, 15000);

})();

