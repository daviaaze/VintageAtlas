import type Map from 'ol/Map';
import { useMapStore } from '@/stores/map';

/**
 * Parse deep link parameters from URL (?x=, ?y=, ?zoom=)
 * WebCartographer convention: y is game Z, map coordinate uses inverted sign for display.
 */
export function getDeepLinkOverrides(): { center?: [number, number], zoom?: number } {
  try {
    const params = new URLSearchParams(window.location.search);
    const hasX = params.has('x');
    const hasY = params.has('y');
    const hasZoom = params.has('zoom');

    const overrides: { center?: [number, number], zoom?: number } = {};

    if (hasX || hasY) {
      const x = Number(params.get('x'));
      const yGame = Number(params.get('y'));
      if (Number.isFinite(x) && Number.isFinite(yGame)) {
        // Map coordinate system: use y = -gameZ to align with existing display logic
        overrides.center = [x, -yGame];
      }
    }
    if (hasZoom) {
      const z = Number(params.get('zoom'));
      if (Number.isFinite(z)) overrides.zoom = z;
    }

    return overrides;
  } catch {
    return {};
  }
}

/**
 * Start syncing map center/zoom to URL query parameters (replaceState, no reload)
 */
export function startDeepLinkSync(map: Map) {
  const store = useMapStore();

  const updateUrl = () => {
    const center = store.center;
    const zoom = store.zoom;
    if (!center) return;

    const x = Math.round(center[0]);
    const yGame = Math.round(-center[1]); // invert to game coordinates for URL
    const z = Math.round(zoom);

    const url = new URL(window.location.href);
    url.searchParams.set('x', String(x));
    url.searchParams.set('y', String(yGame));
    url.searchParams.set('zoom', String(z));
    window.history.replaceState({}, '', url.toString());
  };

  // Update on interactions
  map.on('moveend', updateUrl);
  map.getView().on('change:resolution', updateUrl);

  // Initial update
  updateUrl();
}
