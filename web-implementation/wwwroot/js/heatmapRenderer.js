/**
 * Heatmap Renderer for Pressure Data
 * Renders 32x32 pressure matrices on HTML5 Canvas
 * Author: SID:2412494
 */

// Module state - stores instances by canvas ID
const heatmapInstances = new Map();

/**
 * Initialize a heatmap renderer on a canvas element
 * @param {string} canvasId - The ID of the canvas element
 * @param {number} minValue - Minimum pressure value (from appsettings)
 * @param {number} maxValue - Maximum pressure value (from appsettings)
 * @param {number} cellSize - Size of each cell in pixels (default 10)
 * @returns {boolean} - True if initialization succeeded
 */
window.heatmapRenderer = {
    // 7 discrete colors from blue (low) through yellow to red (high)
    discreteColors: [
        { r: 0,   g: 0,   b: 255 },   // Blue (lowest)
        { r: 0,   g: 128, b: 255 },   // Cyan-Blue
        { r: 0,   g: 255, b: 255 },   // Cyan
        { r: 0,   g: 255, b: 128 },   // Cyan-Green
        { r: 255, g: 255, b: 0   },   // Yellow
        { r: 255, g: 128, b: 0   },   // Orange
        { r: 255, g: 0,   b: 0   }    // Red (highest)
    ],

    /**
     * Initialize a heatmap instance
     * @param {string} canvasId - Canvas element ID
     * @param {number} minValue - Min pressure value from appsettings
     * @param {number} maxValue - Max pressure value from appsettings
     * @param {number} cellSize - Pixel size per cell (default 10)
     */
    initialize: function(canvasId, minValue, maxValue, cellSize = 10) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error(`Canvas element '${canvasId}' not found`);
            return false;
        }

        const ctx = canvas.getContext('2d');
        if (!ctx) {
            console.error('Could not get 2D context from canvas');
            return false;
        }

        // Set canvas size for 32x32 grid
        canvas.width = 32 * cellSize;
        canvas.height = 32 * cellSize;

        // Store instance configuration
        heatmapInstances.set(canvasId, {
            canvas: canvas,
            ctx: ctx,
            minValue: minValue,
            maxValue: maxValue,
            cellSize: cellSize,
            range: maxValue - minValue
        });

        // Initialize with empty grid
        ctx.fillStyle = '#000000';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        return true;
    },

    /**
     * Render pressure data using 7 discrete colors
     * @param {string} canvasId - Canvas element ID
     * @param {number[]} values - Array of 1024 integers (32x32 grid, row-major)
     */
    renderDiscrete: function(canvasId, values) {
        const instance = heatmapInstances.get(canvasId);
        if (!instance) {
            console.error(`Heatmap instance '${canvasId}' not initialized`);
            return;
        }

        const { ctx, minValue, range, cellSize } = instance;
        const numColors = this.discreteColors.length;

        for (let i = 0; i < 1024; i++) {
            const row = Math.floor(i / 32);
            const col = i % 32;

            // Normalize value to 0-1 range, clamped
            const normalized = Math.max(0, Math.min(1, (values[i] - minValue) / range));

            // Map to discrete color index (0-6)
            const colorIndex = Math.min(numColors - 1, Math.floor(normalized * numColors));
            const color = this.discreteColors[colorIndex];

            ctx.fillStyle = `rgb(${color.r}, ${color.g}, ${color.b})`;
            ctx.fillRect(col * cellSize, row * cellSize, cellSize, cellSize);
        }
    },

    /**
     * Render pressure data using smooth gradient between colors
     * @param {string} canvasId - Canvas element ID
     * @param {number[]} values - Array of 1024 integers (32x32 grid, row-major)
     */
    renderGradient: function(canvasId, values) {
        const instance = heatmapInstances.get(canvasId);
        if (!instance) {
            console.error(`Heatmap instance '${canvasId}' not initialized`);
            return;
        }

        const { ctx, minValue, range, cellSize } = instance;
        const colors = this.discreteColors;
        const numSegments = colors.length - 1;

        for (let i = 0; i < 1024; i++) {
            const row = Math.floor(i / 32);
            const col = i % 32;

            // Normalize value to 0-1 range, clamped
            const normalized = Math.max(0, Math.min(1, (values[i] - minValue) / range));

            // Determine which segment of the gradient we're in
            const scaledPos = normalized * numSegments;
            const segmentIndex = Math.min(numSegments - 1, Math.floor(scaledPos));
            const segmentProgress = scaledPos - segmentIndex;

            // Interpolate between adjacent colors
            const color1 = colors[segmentIndex];
            const color2 = colors[segmentIndex + 1];

            const r = Math.round(color1.r + (color2.r - color1.r) * segmentProgress);
            const g = Math.round(color1.g + (color2.g - color1.g) * segmentProgress);
            const b = Math.round(color1.b + (color2.b - color1.b) * segmentProgress);

            ctx.fillStyle = `rgb(${r}, ${g}, ${b})`;
            ctx.fillRect(col * cellSize, row * cellSize, cellSize, cellSize);
        }
    },

    /**
     * Dispose of a heatmap instance
     * @param {string} canvasId - Canvas element ID
     */
    dispose: function(canvasId) {
        if (heatmapInstances.has(canvasId)) {
            const instance = heatmapInstances.get(canvasId);
            // Clear the canvas
            instance.ctx.clearRect(0, 0, instance.canvas.width, instance.canvas.height);
            heatmapInstances.delete(canvasId);
        }
    }
};
