// Territory borders and the overlay. Borders: a dashed line of small raised blocks in the owner's seat colour along each border stretch, dashed so a
// border never reads as a river. Built from a BoardScene's borders.
import {
    BoxGeometry, BufferGeometry, DoubleSide, Float32BufferAttribute, Group, Mesh, MeshBasicMaterial, MeshLambertMaterial,
} from '../../lib/three/three.module.js';
import { instances } from './shapes.js';
import { TOP_JITTER } from './terrain.js';

const DASH = 0.14;
const GAP = 0.08;
const DASH_WIDTH = 0.05;
const DASH_HEIGHT = 0.025;
// Above the highest a tile top can vary (terrain.js), so the dashes always show.
const LIFT = TOP_JITTER + 0.002;

const block = new BoxGeometry(1, 1, 1).translate(0, 0.5, 0);
const paint = new MeshLambertMaterial({ flatShading: true });

// Builds the borders of a scene, or null when it has none.
export function buildBorders(scene, palette) {
    if (!scene.borders?.length) {
        return null;
    }
    const dashes = [];
    for (const border of scene.borders) {
        const colour = palette.token(`--gv-seat-${border.seat}`);
        const [dx, dz] = [border.x2 - border.x1, border.z2 - border.z1];
        const length = Math.hypot(dx, dz);
        const count = Math.max(1, Math.floor((length + GAP) / (DASH + GAP)));
        const start = (length - (count * DASH + (count - 1) * GAP)) / 2 + DASH / 2;
        const turn = Math.atan2(-dz, dx);
        for (let i = 0; i < count; i++) {
            const share = (start + i * (DASH + GAP)) / length;
            dashes.push({
                x: border.x1 + dx * share, y: border.height + LIFT, z: border.z1 + dz * share,
                sx: DASH, sy: DASH_HEIGHT, sz: DASH_WIDTH, turn, colour,
            });
        }
    }
    const mesh = instances(block, paint, dashes);
    mesh.castShadow = false;
    return mesh;
}

// Frees the shared shape and material when a board goes away (see terrain.js disposeShared).
export function disposeShared() {
    block.dispose();
    paint.dispose();
    disposeOverlayShared();
}

// The overlay over the board (BoardOverlay): every tinted tile as a see-through hex just above its surface (later tiles
// slightly higher, so a highlight lies over a lens mark), and the selected tile ringed. Rebuilt alone when the player's
// choice changes.
const TINT_LIFT = 0.03;
const TINT_RADIUS = 0.9;
const RING_RADIUS = 0.93;
const RING_WIDTH = 0.07;
const tints = new MeshBasicMaterial({ vertexColors: true, transparent: true, depthWrite: false, side: DoubleSide });
const ring = new MeshLambertMaterial({ flatShading: true });

const hexCorners = Array.from({ length: 6 }, (_, k) => {
    const angle = Math.PI / 180 * (60 * k - 30);
    return [Math.cos(angle), Math.sin(angle)];
});

function buildTints(tiles, palette) {
    const positions = [];
    const colours = [];
    tiles.forEach(tile => {
        const { colour, alpha } = palette.tint(tile.tint);
        const y = tile.height + LIFT + TINT_LIFT * 0.3;
        for (let k = 0; k < 6; k++) {
            const [ax, az] = hexCorners[k];
            const [bx, bz] = hexCorners[(k + 1) % 6];
            positions.push(tile.x, y, tile.z, tile.x + bx * TINT_RADIUS, y, tile.z + bz * TINT_RADIUS, tile.x + ax * TINT_RADIUS, y, tile.z + az * TINT_RADIUS);
            for (let v = 0; v < 3; v++) {
                colours.push(colour.r, colour.g, colour.b, alpha);
            }
        }
    });
    const geometry = new BufferGeometry();
    geometry.setAttribute('position', new Float32BufferAttribute(positions, 3));
    geometry.setAttribute('color', new Float32BufferAttribute(colours, 4));
    const mesh = new Mesh(geometry, tints);
    mesh.renderOrder = 2;
    return mesh;
}

function buildRing(tile, palette) {
    const colour = palette.token(tile.tint);
    const length = RING_RADIUS;
    const sides = hexCorners.map(([ax, az], k) => {
        const [bx, bz] = hexCorners[(k + 1) % 6];
        return {
            x: tile.x + (ax + bx) / 2 * RING_RADIUS, y: tile.height + LIFT, z: tile.z + (az + bz) / 2 * RING_RADIUS,
            sx: length, sy: 0.05, sz: RING_WIDTH, turn: Math.atan2(-(bz - az), bx - ax), colour,
        };
    });
    const mesh = instances(block, ring, sides);
    mesh.castShadow = false;
    return mesh;
}

// Builds the overlay, or null when it is empty.
export function buildOverlay(overlay, palette) {
    if (!overlay || (!overlay.tiles.length && !overlay.selected)) {
        return null;
    }
    const group = new Group();
    if (overlay.tiles.length) {
        group.add(buildTints(overlay.tiles, palette));
    }
    if (overlay.selected) {
        group.add(buildRing(overlay.selected, palette));
    }
    return group;
}

// Frees the overlay's shared materials when a board goes away.
function disposeOverlayShared() {
    tints.dispose();
    ring.dispose();
}
