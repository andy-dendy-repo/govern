// Territory borders and the overlay. Borders: a dashed line of small raised blocks in the owner's seat colour along each border stretch, dashed so a
// border never reads as a river. Both lie on the ground's slopes (ground.js), not on a flat tile top. Built from a BoardScene's borders.
import {
    BoxGeometry, BufferGeometry, DoubleSide, Float32BufferAttribute, Group, Mesh, MeshBasicMaterial, MeshLambertMaterial,
} from '../../lib/three/three.module.js';
import { instances } from './shapes.js';
import { fan, groundOf, RIM_STEPS, ring as hexRing, stitch } from './ground.js';

const DASH = 0.14;
const GAP = 0.08;
const DASH_WIDTH = 0.05;
const DASH_HEIGHT = 0.025;
// Just over the ground they are draped on (ground.js), so the dashes and tints never flicker against it.
const LIFT = 0.008;

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
const RING_HEIGHT = 0.05;
const tints = new MeshBasicMaterial({ vertexColors: true, transparent: true, depthWrite: false, side: DoubleSide });
const ring = new MeshLambertMaterial({ vertexColors: true, flatShading: true, side: DoubleSide });

// A tint is a hex draped on the ground: the rings the terrain cuts a rough tile into (terrain.js), out to the tint's own rim,
// each point at the ground's height under it, so it lies on a range's crests and a hill's swells instead of cutting into them.
function buildTints(tiles, palette, ground) {
    const positions = [];
    const colours = [];
    tiles.forEach(tile => {
        const { colour, alpha } = palette.tint(tile.tint);
        const lift = LIFT + TINT_LIFT * 0.3;
        const lay = ([x, z]) => [x, (ground ? ground.surfaceAt(x, z, tile.height) : tile.height) + lift, z];
        const face = (a, b, c) => {
            positions.push(...a, ...b, ...c);
            for (let v = 0; v < 3; v++) {
                colours.push(colour.r, colour.g, colour.b, alpha);
            }
        };
        let inner = hexRing(tile.x, tile.z, 1 / RIM_STEPS, 1).map(lay);
        fan(lay([tile.x, tile.z]), inner, face);
        for (let j = 2; j < RIM_STEPS; j++) {
            const next = hexRing(tile.x, tile.z, j / RIM_STEPS, j).map(lay);
            stitch(inner, next, face);
            inner = next;
        }
        stitch(inner, hexRing(tile.x, tile.z, TINT_RADIUS, RIM_STEPS).map(lay), face);
    });
    const geometry = new BufferGeometry();
    geometry.setAttribute('position', new Float32BufferAttribute(positions, 3));
    geometry.setAttribute('color', new Float32BufferAttribute(colours, 4));
    const mesh = new Mesh(geometry, tints);
    mesh.renderOrder = 2;
    return mesh;
}

// The selection ring: a raised band round the tile, draped over the ground (RIM_STEPS points along each side, each at the
// ground's height under it), so it hugs a hillside or a ridge instead of floating over it or cutting into it.
function buildRing(tile, palette, ground) {
    const colour = palette.token(tile.tint);
    const positions = [];
    const colours = [];
    const lay = ([x, z], raise) => [x, (ground ? ground.surfaceAt(x, z, tile.height) : tile.height) + LIFT + raise, z];
    const quad = (a, b, c, d) => {
        positions.push(...a, ...b, ...c, ...a, ...c, ...d);
        for (let v = 0; v < 6; v++) {
            colours.push(colour.r, colour.g, colour.b);
        }
    };
    const inner = hexRing(tile.x, tile.z, RING_RADIUS - RING_WIDTH / 2, RIM_STEPS);
    const outer = hexRing(tile.x, tile.z, RING_RADIUS + RING_WIDTH / 2, RIM_STEPS);
    inner.forEach((_, i) => {
        const n = (i + 1) % inner.length;
        const [i0, i1] = [lay(inner[i], RING_HEIGHT), lay(inner[n], RING_HEIGHT)];
        const [o0, o1] = [lay(outer[i], RING_HEIGHT), lay(outer[n], RING_HEIGHT)];
        quad(i0, o0, o1, i1);
        quad(o0, lay(outer[i], 0), lay(outer[n], 0), o1);
        quad(i1, lay(inner[n], 0), lay(inner[i], 0), i0);
    });
    const geometry = new BufferGeometry();
    geometry.setAttribute('position', new Float32BufferAttribute(positions, 3));
    geometry.setAttribute('color', new Float32BufferAttribute(colours, 3));
    geometry.computeVertexNormals();
    return new Mesh(geometry, ring);
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
