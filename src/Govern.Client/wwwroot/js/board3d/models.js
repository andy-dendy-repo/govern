// Improvements and buildings: one small low-poly model per FeatureModel (Features/Games/Enums/FeatureModel), built from boxes,
// pyramids, cylinders and a dome, standing in its tile's upper-left corner. A building's roof or flag takes its owner's seat
// colour; a building a city is still producing is drawn see-through. Built from a BoardScene's features.
import {
    BoxGeometry, ConeGeometry, CylinderGeometry, Group, MeshLambertMaterial, SphereGeometry, Vector3,
} from '../../lib/three/three.module.js';
import { hash, tileSurface } from './terrain.js';
import { instances } from './shapes.js';

// Unit shapes, each standing on y = 0 (the log lies along x, centred); never disposed until the board goes away.
const shapes = {
    box: new BoxGeometry(1, 1, 1).translate(0, 0.5, 0),
    pyramid: new ConeGeometry(Math.SQRT1_2, 1, 4).rotateY(Math.PI / 4).translate(0, 0.5, 0),
    post: new CylinderGeometry(0.5, 0.5, 1, 6).translate(0, 0.5, 0),
    log: new CylinderGeometry(0.5, 0.5, 1, 6).rotateZ(Math.PI / 2).translate(0, 0.5, 0),
    mound: new ConeGeometry(0.5, 1, 6).translate(0, 0.5, 0),
    dome: new SphereGeometry(0.5, 10, 5, 0, Math.PI * 2, 0, Math.PI / 2),
};
// The model space below is drawn this much larger, so a model reads from the default zoom.
const MODEL_SCALE = 1.6;
const solid = new MeshLambertMaterial({ flatShading: true });
const ghost = new MeshLambertMaterial({ flatShading: true, transparent: true, opacity: 0.45, depthWrite: false });

// The parts of each model: [shape, x, y, z, width, height, depth, colour], in a model space about 0.45 across (before
// MODEL_SCALE) centred on the feature's spot, z towards the camera.
const models = {
    field: c => [
        ['box', 0, 0, 0, 0.46, 0.02, 0.36, c.field],
        ...[-0.12, -0.04, 0.04, 0.12].map(z => ['box', 0, 0, z, 0.44, 0.03, 0.035, c.furrow]),
    ],
    camp: c => [
        ['box', 0.09, 0, -0.04, 0.16, 0.12, 0.14, c.wall],
        ['pyramid', 0.09, 0.12, -0.04, 0.2, 0.08, 0.18, c.roof],
        ['log', -0.1, 0, 0.04, 0.26, 0.06, 0.06, c.timber],
        ['log', -0.1, 0, 0.11, 0.26, 0.06, 0.06, c.timber],
        ['log', -0.1, 0.05, 0.075, 0.26, 0.06, 0.06, c.timber],
    ],
    mine: c => [
        ['mound', 0, 0, -0.02, 0.4, 0.22, 0.36, c.rock],
        ['box', 0, 0, 0.15, 0.08, 0.1, 0.03, c.shaft],
        ['box', -0.055, 0, 0.17, 0.03, 0.12, 0.03, c.timber],
        ['box', 0.055, 0, 0.17, 0.03, 0.12, 0.03, c.timber],
        ['box', 0, 0.12, 0.17, 0.15, 0.03, 0.04, c.timber],
    ],
    tents: c => [
        ['pyramid', -0.1, 0, 0.06, 0.16, 0.16, 0.16, c.canvas],
        ['pyramid', 0.1, 0, 0.07, 0.16, 0.16, 0.16, c.canvas],
        ['pyramid', 0, 0, -0.1, 0.18, 0.18, 0.18, c.canvas],
        ['post', 0.17, 0, -0.12, 0.014, 0.32, 0.014, c.timber],
        ['box', 0.22, 0.24, -0.12, 0.1, 0.06, 0.012, c.roof],
    ],
    hall: c => [
        ['box', 0, 0, 0, 0.38, 0.03, 0.3, c.base],
        ['box', 0, 0.03, -0.04, 0.3, 0.16, 0.18, c.wall],
        ...[-0.13, -0.045, 0.045, 0.13].map(x => ['post', x, 0.03, 0.1, 0.03, 0.16, 0.03, c.wall]),
        ['pyramid', 0, 0.19, 0, 0.38, 0.12, 0.3, c.roof],
    ],
    dome: c => [
        ['box', 0, 0, 0, 0.32, 0.03, 0.32, c.base],
        ['box', 0, 0.03, 0, 0.28, 0.13, 0.28, c.wall],
        ['dome', 0, 0.16, 0, 0.26, 0.26, 0.26, c.roof],
    ],
    stall: c => [
        ['box', 0, 0, -0.02, 0.26, 0.1, 0.14, c.timber],
        ['post', -0.14, 0, -0.1, 0.02, 0.2, 0.02, c.timber],
        ['post', 0.14, 0, -0.1, 0.02, 0.2, 0.02, c.timber],
        ['box', 0, 0.2, -0.03, 0.34, 0.025, 0.22, c.roof],
        ['box', 0.14, 0, 0.13, 0.08, 0.08, 0.08, c.crate],
        ['box', 0.04, 0, 0.15, 0.07, 0.07, 0.07, c.crate],
    ],
    pier: c => [
        ['box', -0.08, 0, -0.06, 0.18, 0.14, 0.16, c.wall],
        ['pyramid', -0.08, 0.14, -0.06, 0.22, 0.1, 0.2, c.roof],
        ['box', 0.1, 0.02, 0.06, 0.1, 0.025, 0.36, c.timber],
        ['post', 0.06, 0, 0.2, 0.025, 0.05, 0.025, c.timber],
        ['post', 0.14, 0, 0.2, 0.025, 0.05, 0.025, c.timber],
    ],
};

function colours(feature, palette) {
    const wall = palette.token('--gv-board3d-wall');
    const timber = palette.token('--gv-bridge');
    const field = palette.token('--gv-board3d-field');
    const rock = palette.token('--gv-board3d-rock');
    return {
        wall, timber, field, rock,
        base: wall.clone().multiplyScalar(0.85),
        furrow: field.clone().multiplyScalar(0.72),
        shaft: rock.clone().multiplyScalar(0.3),
        crate: timber.clone().multiplyScalar(1.2),
        canvas: palette.token('--gv-board3d-canvas'),
        roof: feature.seat > 0 ? palette.token(`--gv-seat-${feature.seat}`) : palette.token('--gv-board3d-roof'),
    };
}

// Builds the improvements and buildings of a scene, or null when it has none.
export function buildFeatures(scene, palette) {
    if (!scene.features?.length) {
        return null;
    }
    const tiles = new Map(scene.tiles.map(tile => [`${tile.q},${tile.r}`, tile]));
    const parts = new Map();
    const up = new Vector3(0, 1, 0);
    for (const feature of scene.features) {
        const tile = tiles.get(`${feature.q},${feature.r}`);
        const top = tile ? tileSurface(tile) : feature.height;
        const turn = (hash(feature.q, feature.r, 80) - 0.5) * 0.6;
        const build = models[feature.model] ?? (feature.seat > 0 ? models.hall : models.field);
        for (const [shape, x, y, z, sx, sy, sz, colour] of build(colours(feature, palette))) {
            const offset = new Vector3(x, 0, z).multiplyScalar(MODEL_SCALE).applyAxisAngle(up, turn);
            const key = `${shape}:${feature.planned}`;
            if (!parts.has(key)) {
                parts.set(key, []);
            }
            parts.get(key).push({
                x: feature.x + offset.x, y: top + y * MODEL_SCALE, z: feature.z + offset.z,
                sx: sx * MODEL_SCALE, sy: sy * MODEL_SCALE, sz: sz * MODEL_SCALE, turn, colour,
            });
        }
    }
    const group = new Group();
    for (const [key, items] of parts) {
        const [shape, planned] = key.split(':');
        const mesh = instances(shapes[shape], planned === 'true' ? ghost : solid, items);
        mesh.castShadow = planned !== 'true';
        group.add(mesh);
    }
    return group;
}

// Frees the shared shapes and materials when a board goes away (see terrain.js disposeShared).
export function disposeShared() {
    for (const resource of [...Object.values(shapes), solid, ghost]) {
        resource.dispose();
    }
}
