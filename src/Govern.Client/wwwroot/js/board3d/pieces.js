// Units and cities: one small low-poly model per PieceModel (Features/Games/Enums/PieceModel), built from simple shapes and
// coloured by seat, enemy pieces a little darker. Each part remembers its tile, so clicking a piece clicks its tile. Built from a
// BoardScene's pieces.
import {
    BoxGeometry, ConeGeometry, CylinderGeometry, Group, IcosahedronGeometry, MeshLambertMaterial, Vector3,
} from '../../lib/three/three.module.js';
import { instances } from './shapes.js';

// Shapes standing on y = 0 (the wheel's axle lies along x, centred on its height); never disposed until the board goes away.
const shapes = {
    box: new BoxGeometry(1, 1, 1).translate(0, 0.5, 0),
    post: new CylinderGeometry(0.5, 0.5, 1, 6).translate(0, 0.5, 0),
    body: new CylinderGeometry(0.32, 0.5, 1, 6).translate(0, 0.5, 0),
    cone: new ConeGeometry(0.5, 1, 6).translate(0, 0.5, 0),
    roof: new ConeGeometry(Math.SQRT1_2, 1, 4).rotateY(Math.PI / 4).translate(0, 0.5, 0),
    head: new IcosahedronGeometry(0.5, 0).translate(0, 0.5, 0),
    disc: new CylinderGeometry(0.5, 0.5, 1, 12).translate(0, 0.5, 0),
    wheel: new CylinderGeometry(0.5, 0.5, 1, 8).rotateZ(Math.PI / 2),
};
const solid = new MeshLambertMaterial({ flatShading: true });

// Pieces are drawn this much larger than the model space below, which is about 0.5 across.
const PIECE_SCALE = 1.6;

// A standing figure: a base in seat colour, a body and a head; units add what they carry.
const figure = c => [
    ['disc', 0, 0, 0, 0.44, 0.03, 0.44, c.base],
    ['body', 0, 0.03, 0, 0.18, 0.28, 0.18, c.seat],
    ['head', 0, 0.31, 0, 0.12, 0.12, 0.12, c.skin],
];

// The parts of each model: [shape, x, y, z, width, height, depth, colour], z towards the camera.
const models = {
    soldier: c => [...figure(c),
        ['box', -0.12, 0.06, 0.02, 0.03, 0.18, 0.14, c.dark],
        ['box', 0.12, 0.1, 0.02, 0.025, 0.22, 0.025, c.metal],
    ],
    spearman: c => [...figure(c),
        ['post', 0.12, 0.03, 0, 0.02, 0.56, 0.02, c.timber],
        ['cone', 0.12, 0.59, 0, 0.05, 0.1, 0.05, c.metal],
    ],
    archer: c => [...figure(c),
        ['box', 0.12, 0.08, 0.02, 0.02, 0.36, 0.04, c.timber],
        ['post', -0.05, 0.14, -0.1, 0.05, 0.18, 0.05, c.timber],
    ],
    builder: c => [...figure(c),
        ['post', 0.12, 0.03, 0, 0.02, 0.3, 0.02, c.timber],
        ['box', 0.12, 0.31, 0, 0.14, 0.03, 0.03, c.metal],
    ],
    horseman: c => [
        ['disc', 0, 0, 0, 0.5, 0.03, 0.5, c.base],
        ...[[-0.05, -0.1], [0.05, -0.1], [-0.05, 0.1], [0.05, 0.1]].map(([x, z]) => ['post', x, 0.03, z, 0.04, 0.13, 0.04, c.horse]),
        ['box', 0, 0.15, 0, 0.14, 0.12, 0.34, c.horse],
        ['box', 0, 0.22, 0.17, 0.08, 0.16, 0.08, c.horse],
        ['body', 0, 0.27, -0.02, 0.13, 0.2, 0.13, c.seat],
        ['head', 0, 0.47, -0.02, 0.1, 0.1, 0.1, c.skin],
    ],
    settler: c => [
        ['disc', 0, 0, 0, 0.5, 0.03, 0.5, c.base],
        ...[[-0.13, -0.1], [0.13, -0.1], [-0.13, 0.1], [0.13, 0.1]].map(([x, z]) => ['wheel', x, 0.08, z, 0.03, 0.11, 0.11, c.timber]),
        ['box', 0, 0.07, 0, 0.22, 0.08, 0.32, c.timber],
        ['box', 0, 0.15, 0, 0.21, 0.14, 0.28, c.canvas],
        ['box', 0, 0.29, 0, 0.22, 0.03, 0.29, c.seat],
    ],
    city: c => [
        ['disc', 0, 0, 0, 0.95, 0.03, 0.95, c.ground],
        ...[0, 1, 2, 3, 4].flatMap(k => {
            const angle = Math.PI / 2.5 * k + 0.4;
            const [x, z] = [Math.cos(angle) * 0.3, Math.sin(angle) * 0.3];
            return [['box', x, 0.03, z, 0.15, 0.12, 0.13, c.wall], ['roof', x, 0.15, z, 0.18, 0.09, 0.16, c.seat]];
        }),
        ['post', 0, 0.03, 0, 0.16, 0.4, 0.16, c.wall],
        ['cone', 0, 0.43, 0, 0.22, 0.18, 0.22, c.seat],
    ],
};

function colours(piece, palette) {
    const seat = palette.token(`--gv-seat-${piece.seat}`).multiplyScalar(piece.own ? 1 : 0.8);
    const wall = palette.token('--gv-board3d-wall');
    return {
        seat, wall,
        base: seat.clone().multiplyScalar(0.75),
        dark: seat.clone().multiplyScalar(0.6),
        ground: wall.clone().multiplyScalar(0.8),
        skin: palette.token('--gv-board3d-figure'),
        horse: palette.token('--gv-board3d-horse'),
        timber: palette.token('--gv-bridge'),
        metal: palette.token('--gv-board3d-rock'),
        canvas: palette.token('--gv-board3d-canvas'),
    };
}

// Builds the pieces of a scene: the group to add and the meshes a click can hit (`userData.tiles`: the tile per instance).
export function buildPieces(scene, palette) {
    const parts = new Map();
    for (const piece of scene.pieces ?? []) {
        const build = models[piece.model] ?? models.soldier;
        for (const [shape, x, y, z, sx, sy, sz, colour] of build(colours(piece, palette))) {
            if (!parts.has(shape)) {
                parts.set(shape, []);
            }
            parts.get(shape).push({
                x: piece.x + x * PIECE_SCALE, y: piece.height + y * PIECE_SCALE, z: piece.z + z * PIECE_SCALE,
                sx: sx * PIECE_SCALE, sy: sy * PIECE_SCALE, sz: sz * PIECE_SCALE, colour, tile: { q: piece.q, r: piece.r },
            });
        }
    }
    const group = new Group();
    const pickable = [];
    for (const [shape, items] of parts) {
        const mesh = instances(shapes[shape], solid, items);
        mesh.userData.tiles = items.map(item => item.tile);
        group.add(mesh);
        pickable.push(mesh);
    }
    return { group, pickable };
}

// The point above a piece where its banner floats, in board units.
export function bannerAnchor(piece) {
    return new Vector3(piece.x, piece.height + (piece.kind === 'city' ? 0.85 : 0.95), piece.z);
}

// Frees the shared shapes and material when a board goes away (see terrain.js disposeShared).
export function disposeShared() {
    for (const resource of [...Object.values(shapes), solid]) {
        resource.dispose();
    }
}
