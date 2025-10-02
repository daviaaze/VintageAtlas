/**
 * Composable for managing OpenLayers Overlays
 * Provides a better way to display feature info popups
 */
import { ref, onUnmounted, Ref } from 'vue';
import Overlay from 'ol/Overlay';
import type { Map } from 'ol';
import type { Coordinate } from 'ol/coordinate';

export function useMapOverlay(map: Ref<Map | null>) {
  const overlayElement = ref<HTMLElement | null>(null);
  const overlay = ref<Overlay | null>(null);
  const position = ref<Coordinate | undefined>(undefined);
  const content = ref<any>(null);

  /**
   * Initialize the overlay
   */
  function initOverlay(element: HTMLElement) {
    if (!map.value) return;

    overlayElement.value = element;
    
    overlay.value = new Overlay({
      element: element,
      autoPan: {
        animation: {
          duration: 250
        }
      },
      positioning: 'bottom-center',
      stopEvent: false,
      offset: [0, -10]
    });

    map.value.addOverlay(overlay.value);
  }

  /**
   * Show overlay at specific position with content
   */
  function showOverlay(coordinate: Coordinate, featureData: any) {
    if (!overlay.value) return;
    
    position.value = coordinate;
    content.value = featureData;
    overlay.value.setPosition(coordinate);
  }

  /**
   * Hide the overlay
   */
  function hideOverlay() {
    if (!overlay.value) return;
    
    position.value = undefined;
    content.value = null;
    overlay.value.setPosition(undefined);
  }

  /**
   * Cleanup
   */
  onUnmounted(() => {
    if (overlay.value && map.value) {
      map.value.removeOverlay(overlay.value);
    }
  });

  return {
    overlayElement,
    position,
    content,
    initOverlay,
    showOverlay,
    hideOverlay
  };
}

