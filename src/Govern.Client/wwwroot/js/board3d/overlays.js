// Territory borders: a dashed line of small raised blocks in the owner's seat colour along each border stretch, dashed so a
// border never reads as a river. Built from a BoardScene's borders.
import { BoxGeometry, MeshLambertMaterial } from '../../lib/three/three.module.js';
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
}
