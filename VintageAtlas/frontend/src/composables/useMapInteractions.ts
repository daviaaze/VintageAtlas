/**
 * Composable for managing OpenLayers interactions
 * Provides Select, Hover, and other interactions
 */
import { ref, onUnmounted, Ref } from 'vue';
import Select from 'ol/interaction/Select';
import { click, pointerMove } from 'ol/events/condition';
import { Style, Circle, Fill, Stroke } from 'ol/style';
import type { Map } from 'ol';
import type { FeatureLike } from 'ol/Feature';

export function useMapInteractions(map: Ref<Map | null>) {
  const selectInteraction = ref<Select | null>(null);
  const hoverInteraction = ref<Select | null>(null);
  const selectedFeature = ref<FeatureLike | null>(null);
  const hoveredFeature = ref<FeatureLike | null>(null);

  /**
   * Highlight style for selected features
   */
  const selectStyle = new Style({
    image: new Circle({
      radius: 8,
      fill: new Fill({
        color: 'rgba(255, 255, 0, 0.8)'
      }),
      stroke: new Stroke({
        color: '#ffff00',
        width: 3
      })
    }),
    stroke: new Stroke({
      color: '#ffff00',
      width: 3
    }),
    fill: new Fill({
      color: 'rgba(255, 255, 0, 0.3)'
    })
  });

  /**
   * Hover style for features
   */
  const hoverStyle = new Style({
    image: new Circle({
      radius: 7,
      fill: new Fill({
        color: 'rgba(0, 150, 255, 0.6)'
      }),
      stroke: new Stroke({
        color: '#0096ff',
        width: 2
      })
    }),
    stroke: new Stroke({
      color: '#0096ff',
      width: 2
    })
  });

  /**
   * Initialize select interaction
   */
  function initSelectInteraction(onSelect?: (feature: FeatureLike | null) => void) {
    if (!map.value) return;

    selectInteraction.value = new Select({
      condition: click,
      style: selectStyle,
      hitTolerance: 5
    });

    selectInteraction.value.on('select', (e) => {
      const selected = e.selected.length > 0 ? e.selected[0] : null;
      selectedFeature.value = selected;
      
      if (onSelect) {
        onSelect(selected);
      }
    });

    map.value.addInteraction(selectInteraction.value);
  }

  /**
   * Initialize hover interaction
   */
  function initHoverInteraction(onHover?: (feature: FeatureLike | null) => void) {
    if (!map.value) return;

    hoverInteraction.value = new Select({
      condition: pointerMove,
      style: hoverStyle,
      hitTolerance: 5
    });

    hoverInteraction.value.on('select', (e) => {
      const hovered = e.selected.length > 0 ? e.selected[0] : null;
      hoveredFeature.value = hovered;
      
      // Change cursor
      if (map.value) {
        map.value.getTargetElement().style.cursor = hovered ? 'pointer' : '';
      }
      
      if (onHover) {
        onHover(hovered);
      }
    });

    map.value.addInteraction(hoverInteraction.value);
  }

  /**
   * Clear selection
   */
  function clearSelection() {
    if (selectInteraction.value) {
      selectInteraction.value.getFeatures().clear();
      selectedFeature.value = null;
    }
  }

  /**
   * Cleanup
   */
  onUnmounted(() => {
    if (selectInteraction.value && map.value) {
      map.value.removeInteraction(selectInteraction.value);
    }
    if (hoverInteraction.value && map.value) {
      map.value.removeInteraction(hoverInteraction.value);
    }
  });

  return {
    selectInteraction,
    hoverInteraction,
    selectedFeature,
    hoveredFeature,
    initSelectInteraction,
    initHoverInteraction,
    clearSelection
  };
}

