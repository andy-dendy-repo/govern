// Territory borders and the overlay. Borders: a dashed line of small raised blocks in the owner's seat colour along each border stretch, dashed so a
// border never reads as a river. Both lie on the ground's slopes (ground.js), not on a flat tile top. Built from a BoardScene's borders.
import {
    BoxGeometry, BufferGeometry, DoubleSide, Float32BufferAttribute, Group, Mesh, MeshBasicMaterial, MeshLambertMaterial,
} from '../../lib/three/three.module.js';
import { instances } from './shapes.js';
import { corners as hexCorners, groundOf, PLATEAU, TOP_JITTER } from './ground.js';

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
    const ground = groundOf(scene);
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
            const [x, z] = [border.x1 + dx * share, border.z1 + dz * share];
            dashes.push({
                x, y: ground.surfaceAt(x, z, border.height) + LIFT, z,
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

// A tint is a hex fan draped on the ground: the plateau's ring and the tint's own rim, each corner and edge middle at the
// ground's height under it.
function buildTints(tiles, palette, ground) {
    const positions = [];
    const colours = [];
    tiles.forEach(tile => {
        const { colour, alpha } = palette.tint(tile.tint);
        const lift = LIFT + TINT_LIFT * 0.3;
        const point = (share, k, middle) => {
            const [ax, az] = hexCorners[k];
            const [bx, bz] = middle ? hexCorners[(k + 1) % 6] : hexCorners[k];
            const [x, z] = [tile.x + (ax + bx) / 2 * share, tile.z + (az + bz) / 2 * share];
            return [x, (ground ? ground.surfaceAt(x, z, tile.height) : tile.height) + lift, z];
        };
        const triangle = (a, b, c) => {
            positions.push(...a, ...b, ...c);
            for (let v = 0; v < 3; v++) {
                colours.push(colour.r, colour.g, colour.b, alpha);
            }
        };
        const centre = point(0, 0, false);
        for (let k = 0; k < 6; k++) {
            const next = (k + 1) % 6;
            const [inA, inM, inB] = [point(PLATEAU, k, false), point(PLATEAU, k, true), point(PLATEAU, next, false)];
            const [outA, outM, outB] = [point(TINT_RADIUS, k, false), point(TINT_RADIUS, k, true), point(TINT_RADIUS, next, false)];
            triangle(centre, inB, inM);
            triangle(centre, inM, inA);
            triangle(inA, inM, outM);
            triangle(inA, outM, outA);
            triangle(inM, inB, outB);
            triangle(inM, outB, outM);
        }
    });
    const geometry = new BufferGeometry();
    geometry.setAttribute('position', new Float32BufferAttribute(positions, 3));
    geometry.setAttribute('color', new Float32BufferAttribute(colours, 4));
    const mesh = new Mesh(geometry, tints);
    mesh.renderOrder = 2;
    return mesh;
}

function buildRing(tile, palette, ground) {
    const colour = palette.token(tile.tint);
    const length = RING_RADIUS;
    const sides = hexCorners.map(([ax, az], k) => {
        const [bx, bz] = hexCorners[(k + 1) % 6];
        const [x, z] = [tile.x + (ax + bx) / 2 * RING_RADIUS, tile.z + (az + bz) / 2 * RING_RADIUS];
        return {
            x, y: (ground ? ground.surfaceAt(x, z, tile.height) : tile.height) + LIFT, z,
            sx: length, sy: 0.05, sz: RING_WIDTH, turn: Math.atan2(-(bz - az), bx - ax), colour,
        };
    });
    const mesh = instances(block, ring, sides);
    mesh.castShadow = false;
    return mesh;
}

// Builds the overlay over a scene's ground, or null when it is empty.
export function buildOverlay(overlay, palette, scene) {
    if (!overlay || (!overlay.tiles.length && !overlay.selected)) {
        return null;
    }
    const ground = scene ? groundOf(scene) : null;
    const group = new Group();
    if (overlay.tiles.length) {
        group.add(buildTints(overlay.tiles, palette, ground));
    }
    if (overlay.selected) {
        group.add(buildRing(overlay.selected, palette, ground));
    }
    return group;
}

// Frees the overlay's shared materials when a board goes away.
function disposeOverlayShared() {
    tints.dispose();
    ring.dispose();
}
