// Colours for the 3D board, read from the design tokens in css/tokens.css (the only place colour values are written), so
// light and dark come from the same tokens as the rest of the app. Read again when the system theme changes.
import { Color, SRGBColorSpace } from '../../lib/three/three.module.js';

export class Palette {
    constructor(element) {
        this.style = getComputedStyle(element);
        this.cache = new Map();
    }

    // A token as a Three.js colour; a missing token gives a neutral colour so a new terrain still draws.
    token(name) {
        let colour = this.cache.get(name);
        if (!colour) {
            const value = this.style.getPropertyValue(name).trim();
            colour = new Color();
            if (value) {
                colour.setStyle(value);
            } else {
                colour.setScalar(0.5);
            }
            this.cache.set(name, colour);
        }
        return colour.clone();
    }

    // A colour token that may carry transparency (the overlay and lens tints are tokens with an alpha): its colour and its alpha.
    tint(name) {
        const value = this.style.getPropertyValue(name).trim();
        const rgba = /^rgba?\(\s*([\d.]+)[\s,]+([\d.]+)[\s,]+([\d.]+)(?:[\s,/]+([\d.]+%?))?\s*\)$/.exec(value);
        if (!rgba) {
            return { colour: this.token(name), alpha: 1 };
        }
        const colour = new Color().setRGB(+rgba[1] / 255, +rgba[2] / 255, +rgba[3] / 255, SRGBColorSpace);
        const alpha = rgba[4] === undefined ? 1 : rgba[4].endsWith('%') ? parseFloat(rgba[4]) / 100 : +rgba[4];
        return { colour, alpha };
    }

    // A tile's terrain colour (faded towards parchment when only remembered), or the colour under the mist.
    terrain(tile) {
        return tile.terrain ? this.faded(`--gv-terrain-${tile.terrain}`, tile) : this.token('--gv-board3d-unexplored');
    }

    // A board colour faded like a remembered tile when it is only explored.
    faded(name, tile) {
        const colour = this.token(name);
        return tile.visibility === 'visible' ? colour : colour.lerp(this.token('--gv-board3d-memory'), 0.45);
    }
}
