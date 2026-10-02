// HTML over the canvas: a banner with the sprite symbol above every piece (with a health bar under a hurt unit's), city names,
// the active lens's labels and edge badges, and damage numbers while an attack plays. Built with createElement, createElementNS and textContent only (no HTML is
// parsed), placed by projecting their anchor points after every draw, and hidden from assistive technology, since the side
// panel says the same.
import { Vector3 } from '../../lib/three/three.module.js';
import { bannerAnchor } from './pieces.js';

const SVG = 'http://www.w3.org/2000/svg';

export class Labels {
    constructor(host) {
        this.layer = document.createElement('div');
        this.layer.className = 'board3d-labels';
        this.layer.setAttribute('aria-hidden', 'true');
        host.append(this.layer);
        this.pieces = [];
        this.marks = [];
        this.damage = new Map();
        this.point = new Vector3();
    }

    // How many labels there are (the bench reports it).
    get count() {
        return this.marks.length + this.pieces.length + this.damage.size;
    }

    // One banner per piece, and a name under each city.
    setPieces(pieces) {
        this.clear(this.pieces);
        this.pieces = [];
        for (const piece of pieces ?? []) {
            const anchor = bannerAnchor(piece);
            this.pieces.push({ element: this.banner(piece), anchor, base: anchor.clone(), unit: piece.unit });
            if (piece.name) {
                const name = this.element('div', 'board3d-city-name');
                name.textContent = piece.name;
                this.pieces.push({ element: name, anchor: new Vector3(piece.x, piece.height, piece.z + 0.75) });
            }
        }
    }

    // The lens's labels on its tiles and its badges on edges.
    setOverlay(overlay) {
        this.clear(this.marks);
        this.marks = [];
        for (const tile of overlay?.tiles ?? []) {
            if (tile.label) {
                const label = this.element('div', 'board3d-lens-label');
                label.textContent = tile.label;
                this.marks.push({ element: label, anchor: new Vector3(tile.x, tile.height + 0.05, tile.z) });
            }
        }
        for (const edge of overlay?.edges ?? []) {
            const badge = this.element('div', `board3d-edge-badge ${edge.mark}`);
            badge.textContent = edge.label;
            this.marks.push({ element: badge, anchor: new Vector3(edge.x, edge.height + 0.05, edge.z) });
        }
    }

    // Moves a unit's banner with its piece while it animates (offset from where the scene has it).
    follow(unit, dx, dy, dz) {
        for (const label of this.pieces) {
            if (label.unit === unit) {
                label.anchor.set(label.base.x + dx, label.base.y + dy, label.base.z + dz);
                label.placed = false;
            }
        }
    }

    // A damage number over a tile; its CSS animation floats it up and fades it.
    addDamage(key, text, x, height, z) {
        const label = this.element('div', 'board3d-damage');
        label.textContent = text;
        this.damage.set(key, { element: label, anchor: new Vector3(x, height + 0.9, z) });
    }

    // A page element (the attack forecast) kept next to a point of the board. Unlike the labels it takes clicks and is never
    // hidden: its CSS keeps it on the board, beside the point (--anchor-x, --anchor-y) on the side with more room (--anchor-flip).
    setAnchor(element, x, y, z) {
        this.anchor = { element, anchor: new Vector3(x, y, z) };
    }

    clearAnchor() {
        this.anchor = null;
    }

    removeDamage(key) {
        this.damage.get(key)?.element.remove();
        this.damage.delete(key);
    }

    banner(piece) {
        const banner = this.element('div', `board3d-banner seat-${piece.seat} ${piece.own ? 'own' : 'enemy'} ${piece.kind}`);
        const icon = document.createElementNS(SVG, 'svg');
        icon.setAttribute('class', 'board3d-banner-icon');
        icon.setAttribute('viewBox', '-12 -12 24 24');
        const use = document.createElementNS(SVG, 'use');
        use.setAttribute('href', `#${piece.icon}`);
        icon.append(use);
        banner.append(icon);
        if (piece.health < 1) {
            const track = document.createElement('div');
            track.className = 'board3d-health';
            const fill = document.createElement('div');
            fill.className = piece.health * 3 <= 1 ? 'board3d-health-fill low' : 'board3d-health-fill';
            fill.style.width = `${Math.round(Math.max(piece.health, 0) * 100)}%`;
            track.append(fill);
            banner.append(track);
        }
        return banner;
    }

    element(tag, className) {
        const element = document.createElement(tag);
        element.className = className;
        this.layer.append(element);
        return element;
    }

    clear(labels) {
        for (const label of labels) {
            label.element.remove();
        }
    }

    // Moves every label over its anchor as the camera sees it; those behind the camera or off the canvas are hidden. When the
    // camera hasn't moved, only the labels that are new or follow a moving piece are placed, and the page is written to only
    // where a label's place or visibility changed.
    place(camera, width, height) {
        const view = `${camera.matrixWorld.elements.join()},${camera.projectionMatrix.elements.join()},${width},${height}`;
        const moved = view !== this.view;
        this.view = view;
        for (const label of [...this.marks, ...this.pieces, ...this.damage.values()]) {
            if (moved || label.placed !== true) {
                this.put(label, camera, width, height);
            }
        }
        if (this.anchor && (moved || this.anchor.placed !== true)) {
            this.putAnchor(this.anchor, camera, width, height);
        }
    }

    putAnchor(anchor, camera, width, height) {
        anchor.placed = true;
        this.point.copy(anchor.anchor).project(camera);
        const x = (this.point.x + 1) / 2 * width;
        const style = anchor.element.style;
        style.setProperty('--anchor-x', `${x.toFixed(1)}px`);
        style.setProperty('--anchor-y', `${((1 - this.point.y) / 2 * height).toFixed(1)}px`);
        style.setProperty('--anchor-flip', x > width * 0.55 ? '1' : '0');
    }

    put(label, camera, width, height) {
        label.placed = true;
        this.point.copy(label.anchor).project(camera);
        const visible = this.point.z < 1 && Math.abs(this.point.x) <= 1.1 && Math.abs(this.point.y) <= 1.1;
        if (label.element.hidden === visible) {
            label.element.hidden = !visible;
        }
        if (!visible) {
            return;
        }
        const transform = `translate(${((this.point.x + 1) / 2 * width).toFixed(1)}px, ${((1 - this.point.y) / 2 * height).toFixed(1)}px) translate(-50%, -50%)`;
        if (transform !== label.transform) {
            label.transform = transform;
            label.element.style.transform = transform;
        }
    }

    dispose() {
        this.layer.remove();
    }
}
