// HTML over the canvas: a banner with the sprite symbol above every piece (with a health bar under a hurt unit's), city names,
// and the active lens's labels and edge badges. Built with createElement, createElementNS and textContent only (no HTML is
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
        this.point = new Vector3();
    }

    // One banner per piece, and a name under each city.
    setPieces(pieces) {
        this.clear(this.pieces);
        this.pieces = [];
        for (const piece of pieces ?? []) {
            this.pieces.push({ element: this.banner(piece), anchor: bannerAnchor(piece) });
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

    // Moves every label over its anchor as the camera sees it; those behind the camera or off the canvas are hidden.
    place(camera, width, height) {
        for (const label of [...this.marks, ...this.pieces]) {
            this.point.copy(label.anchor).project(camera);
            const visible = this.point.z < 1 && Math.abs(this.point.x) <= 1.1 && Math.abs(this.point.y) <= 1.1;
            label.element.hidden = !visible;
            if (visible) {
                const x = (this.point.x + 1) / 2 * width;
                const y = (1 - this.point.y) / 2 * height;
                label.element.style.transform = `translate(${x.toFixed(1)}px, ${y.toFixed(1)}px) translate(-50%, -50%)`;
            }
        }
    }

    dispose() {
        this.layer.remove();
    }
}
