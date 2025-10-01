/**
 * Custom OpenLayers controls for VintageAtlas
 */
import Control from 'ol/control/Control';
import ScaleLine from 'ol/control/ScaleLine';

/**
 * Create scale line control
 */
export function createScaleLineControl() {
  return new ScaleLine({
    units: 'metric',
    bar: true,
    steps: 4,
    text: true,
    minWidth: 140,
    className: 'ol-scale-line'
  });
}

/**
 * Create custom screenshot control
 */
export class ScreenshotControl extends Control {
  constructor(opt_options?: any) {
    const options = opt_options || {};

    const button = document.createElement('button');
    button.innerHTML = '📷';
    button.title = 'Take screenshot';
    button.className = 'screenshot-control-button';

    const element = document.createElement('div');
    element.className = 'screenshot-control ol-unselectable ol-control';
    element.appendChild(button);

    super({
      element: element,
      target: options.target
    });

    button.addEventListener('click', this.handleScreenshot.bind(this), false);
  }

  handleScreenshot() {
    const map = this.getMap();
    if (!map) return;

    map.once('rendercomplete', () => {
      const mapCanvas = document.createElement('canvas');
      const size = map.getSize();
      if (!size) return;

      mapCanvas.width = size[0];
      mapCanvas.height = size[1];
      const mapContext = mapCanvas.getContext('2d');
      if (!mapContext) return;

      // Get all canvas elements from map layers
      const canvases = map.getViewport().querySelectorAll('.ol-layer canvas, canvas.ol-layer');
      
      canvases.forEach((canvas) => {
        const htmlCanvas = canvas as HTMLCanvasElement;
        if (htmlCanvas.width > 0) {
          const opacity = (canvas.parentNode as HTMLElement)?.style.opacity || '1';
          mapContext.globalAlpha = opacity === '' ? 1 : Number(opacity);
          
          const transform = htmlCanvas.style.transform;
          if (transform) {
            // Parse transform matrix
            const matrix = transform
              .match(/^matrix\(([^)]*)\)$/)?.[1]
              ?.split(',')
              .map(Number);
            
            if (matrix && matrix.length === 6) {
              mapContext.setTransform(matrix[0], matrix[1], matrix[2], matrix[3], matrix[4], matrix[5]);
            }
          }
          
          mapContext.drawImage(htmlCanvas, 0, 0);
        }
      });

      mapContext.globalAlpha = 1;
      mapContext.setTransform(1, 0, 0, 1, 0, 0);

      // Download as PNG
      mapCanvas.toBlob((blob) => {
        if (blob) {
          const url = URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = `vintageatlas-map-${Date.now()}.png`;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          URL.revokeObjectURL(url);
        }
      });
    });

    map.renderSync();
  }
}

/**
 * Create custom coordinates control
 */
export class CoordinatesControl extends Control {
  private coordsElement: HTMLDivElement;

  constructor(opt_options?: any) {
    const options = opt_options || {};

    const element = document.createElement('div');
    element.className = 'coordinates-control ol-unselectable ol-control';

    const coordsDiv = document.createElement('div');
    coordsDiv.className = 'coordinates-display';
    coordsDiv.innerHTML = 'X: 0, Z: 0';
    element.appendChild(coordsDiv);

    super({
      element: element,
      target: options.target
    });

    this.coordsElement = coordsDiv;
  }
}

/**
 * Create fullscreen control
 */
export class FullscreenControl extends Control {
  private isFullscreen: boolean = false;

  constructor(opt_options?: any) {
    const options = opt_options || {};

    const button = document.createElement('button');
    button.innerHTML = '⛶';
    button.title = 'Toggle fullscreen';
    button.className = 'fullscreen-control-button';

    const element = document.createElement('div');
    element.className = 'fullscreen-control ol-unselectable ol-control';
    element.appendChild(button);

    super({
      element: element,
      target: options.target
    });

    button.addEventListener('click', this.handleFullscreen.bind(this), false);

    // Listen for fullscreen changes
    document.addEventListener('fullscreenchange', () => {
      this.isFullscreen = !!document.fullscreenElement;
      button.innerHTML = this.isFullscreen ? '🗗' : '⛶';
    });
  }

  handleFullscreen() {
    if (!document.fullscreenElement) {
      document.documentElement.requestFullscreen();
    } else {
      if (document.exitFullscreen) {
        document.exitFullscreen();
      }
    }
  }
}

