// Units and cities: one small low-poly model per PieceModel (Features/Games/Enums/PieceModel). Units are characters baked from
// the CC0 KayKit Adventurers pack (wwwroot/models/units, made by tools/unit-models) standing on a base in seat colour; the
// horse and the city are built from simple shapes. Enemy pieces are a little darker. Each part remembers its tile, so clicking
// a piece clicks its tile. Built from a BoardScene's pieces; a unit that animates is built on its own (an actor) so it can move.
import {
    BoxGeometry, BufferAttribute, BufferGeometry, ConeGeometry, CylinderGeometry, Group, MeshLambertMaterial,
    Vector3,
} from '../../lib/three/three.module.js';
import { instances } from './shapes.js';

// Shapes standing on y = 0; never disposed until the board goes away.
const shapes = {
    box: new BoxGeometry(1, 1, 1).translate(0, 0.5, 0),
    post: new CylinderGeometry(0.5, 0.5, 1, 6).translate(0, 0.5, 0),
    body: new CylinderGeometry(0.32, 0.5, 1, 6).translate(0, 0.5, 0),
    cone: new ConeGeometry(0.5, 1, 6).translate(0, 0.5, 0),
    roof: new ConeGeometry(Math.SQRT1_2, 1, 4).rotateY(Math.PI / 4).translate(0, 0.5, 0),
    disc: new CylinderGeometry(0.5, 0.5, 1, 12).translate(0, 0.5, 0),
};
const solid = new MeshLambertMaterial({ flatShading: true });
// The baked characters carry their own colours, which the instance colour multiplies (white, darker for an enemy, or the seat
// colour for a seat part).
const painted = new MeshLambertMaterial({ flatShading: true, vertexColors: true });

// The baked characters, by file name, each split into its own colours (`name`) and its seat parts (`name:seat`); filled once
// by loadFigures, and empty until then (units then show only their base).
const FIGURES = ['soldier', 'spearman', 'archer', 'builder', 'settler', 'scout', 'rider'];
const figures = new Map();
let loading = null;

// Pieces are drawn this much larger than the model space below, which is about 0.5 across.
const PIECE_SCALE = 1.6;

// How tall a standing character is in the model space below.
const FIGURE_HEIGHT = 0.5;

// A character from its baked file standing on a base in seat colour.
const standing = (c, name) => [
    ['disc', 0, 0, 0, 0.4, 0.03, 0.4, c.base],
    ...character(c, name, 0, 0.03, 0),
];

// The two parts of a baked character: its own colours and its seat parts.
const character = (c, name, x, y, z) => [
    [name, x, y, z, FIGURE_HEIGHT, FIGURE_HEIGHT, FIGURE_HEIGHT, c.tint],
    [`${name}:seat`, x, y, z, FIGURE_HEIGHT, FIGURE_HEIGHT, FIGURE_HEIGHT, c.seat],
];

// Parts turned about the model's centre by `angle` (a quarter turn shows a horse side on).
const turned = (angle, parts) => parts.map(([shape, x, y, z, sx, sy, sz, colour, turn = 0]) => [
    shape, x * Math.cos(angle) + z * Math.sin(angle), y, z * Math.cos(angle) - x * Math.sin(angle), sx, sy, sz, colour, turn + angle,
]);

// A ship side on: a hull with a raised prow and stern, a mast with its sail in seat colour (along the hull, so it faces the
// camera), and `rows` rows of oars a side. Ships float on water, so they have no base.
const ship = (c, rows) => turned(Math.PI / 2, [
    ['box', 0, 0, 0, 0.2, 0.08, 0.6, c.hull],
    ['box', 0, 0.06, 0.28, 0.12, 0.1, 0.08, c.hull],
    ['box', 0, 0.06, -0.28, 0.12, 0.07, 0.08, c.hull],
    ['post', 0, 0.08, 0.04, 0.025, 0.42, 0.025, c.wall],
    ['box', 0, 0.2, 0.04, 0.02, 0.24, 0.3, c.seat],
    ...[-0.16, -0.04, 0.08, 0.2].flatMap(z => Array.from({ length: rows }, (_, row) => [-1, 1].map(side =>
        ['box', side * 0.14, 0.02 + row * 0.03, z, 0.14, 0.012, 0.018, c.wall]))).flat(),
]);

// The parts of each model: [shape or figure, x, y, z, width, height, depth, colour, optional turn], z towards the camera.
const models = {
    soldier: c => [...standing(c, 'soldier')],
    spearman: c => [...standing(c, 'spearman')],
    archer: c => [...standing(c, 'archer')],
    builder: c => [...standing(c, 'builder')],
    settler: c => [...standing(c, 'settler')],
    scout: c => [...standing(c, 'scout')],
    // The settler's figure with a pack of goods on its back.
    trader: c => [
        ...standing(c, 'settler'),
        ['box', 0, 0.2, -0.13, 0.17, 0.17, 0.1, c.wall],
        ['box', 0, 0.37, -0.13, 0.13, 0.05, 0.08, c.seat],
    ],
    galley: c => [...ship(c, 1)],
    // Two rows of oars a side, with a small archer at the stern facing the camera (the hull runs along x once turned).
    bireme: c => [
        ...ship(c, 2),
        ['archer', -0.17, 0.08, 0, 0.26, 0.26, 0.26, c.tint],
        ['archer:seat', -0.17, 0.08, 0, 0.26, 0.26, 0.26, c.seat],
    ],
    horseman: c => [
        ['disc', 0, 0, 0, 0.5, 0.03, 0.5, c.base],
        ...turned(Math.PI / 2, [
            ...[[-0.06, -0.17], [0.06, -0.17], [-0.06, 0.16], [0.06, 0.16]].map(([x, z]) => ['post', x, 0.03, z, 0.045, 0.23, 0.045, c.horse]),
            ['body', 0, 0.24, 0, 0.19, 0.15, 0.46, c.horse],
            ['box', 0, 0.24, -0.01, 0.16, 0.14, 0.48, c.horse],
            ['box', 0, 0.32, 0.23, 0.085, 0.24, 0.1, c.horse],
            ['box', 0, 0.48, 0.29, 0.08, 0.08, 0.19, c.horse],
            ['box', 0, 0.4, 0.19, 0.03, 0.2, 0.08, c.mane],
            ['box', 0, 0.18, -0.25, 0.04, 0.18, 0.04, c.mane],
            ...character(c, 'rider', 0, 0.27, -0.02),
        ]),
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
        tint: palette.token('--gv-board3d-figure').multiplyScalar(piece.own ? 1 : 0.8),
        base: seat.clone().multiplyScalar(0.75),
        ground: wall.clone().multiplyScalar(0.8),
        horse: palette.token('--gv-board3d-horse'),
        mane: palette.token('--gv-board3d-horse').multiplyScalar(0.55),
        hull: palette.token('--gv-board3d-horse').multiplyScalar(0.75),
    };
}

// The parts of a piece, placed relative to (ox, oy, oz): each a shape or figure with its place, size, colour and turn.
function partsOf(piece, palette, ox, oy, oz) {
    const build = models[piece.model] ?? models.soldier;
    const parts = [];
    for (const [shape, x, y, z, sx, sy, sz, colour, turn = 0] of build(colours(piece, palette))) {
        if (!shapes[shape] && !figures.has(shape)) {
            if (!FIGURES.includes(shape.replace(':seat', ''))) {
                throw new Error(`pieces.js has no shape or figure named ${shape}`);
            }
            continue;
        }
        parts.push({
            shape, x: piece.x - ox + x * PIECE_SCALE, y: piece.height - oy + y * PIECE_SCALE, z: piece.z - oz + z * PIECE_SCALE,
            sx: sx * PIECE_SCALE, sy: sy * PIECE_SCALE, sz: sz * PIECE_SCALE, colour, turn, tile: { q: piece.q, r: piece.r },
        });
    }
    return parts;
}

// One instanced mesh per shape for a list of parts, in a group; the meshes a click can hit carry `userData.tiles`.
function meshesOf(parts, materials) {
    const byShape = new Map();
    for (const part of parts) {
        if (!byShape.has(part.shape)) {
            byShape.set(part.shape, []);
        }
        byShape.get(part.shape).push(part);
    }
    const group = new Group();
    const pickable = [];
    for (const [shape, items] of byShape) {
        const mesh = shapes[shape] ? instances(shapes[shape], materials.solid, items) : instances(figures.get(shape), materials.painted, items);
        mesh.userData.tiles = items.map(item => item.tile);
        group.add(mesh);
        pickable.push(mesh);
    }
    return { group, pickable };
}

// Builds the pieces of a scene: the group to add and the meshes a click can hit (`userData.tiles`: the tile per instance).
// Units whose id is in `moving` are left out of the shared meshes and built as actors of their own (see buildActor), so the
// motion can move them one by one.
export function buildPieces(scene, palette, moving = new Set()) {
    const parts = [];
    const actors = new Map();
    for (const piece of scene.pieces ?? []) {
        if (piece.unit != null && moving.has(piece.unit)) {
            actors.set(piece.unit, buildActor(piece, palette));
        } else {
            parts.push(...partsOf(piece, palette, 0, 0, 0));
        }
    }
    const { group, pickable } = meshesOf(parts, { solid, painted });
    for (const actor of actors.values()) {
        group.add(actor.group);
        pickable.push(...actor.pickable);
    }
    return { group, pickable, actors };
}

// One piece as a group of its own, standing at the group's origin, which is placed at the piece: moving or turning the group
// moves the piece. A fading actor (a destroyed unit's ghost) has its own see-through materials, freed by disposeActor.
export function buildActor(piece, palette, fading = false) {
    const materials = fading
        ? { solid: solid.clone(), painted: painted.clone(), own: true }
        : { solid, painted, own: false };
    if (fading) {
        materials.solid.transparent = materials.painted.transparent = true;
    }
    const { group, pickable } = meshesOf(partsOf(piece, palette, piece.x, piece.height, piece.z), materials);
    group.position.set(piece.x, piece.height, piece.z);
    // A fading ghost casts no shadow, which would stay dark while the model fades.
    group.traverse(object => { object.castShadow &&= !fading; });
    return { group, pickable, piece, materials };
}

// Frees an actor's instance buffers and, for a fading one, its own materials.
export function disposeActor(actor) {
    actor.group.traverse(object => object.isInstancedMesh && object.dispose());
    if (actor.materials.own) {
        actor.materials.solid.dispose();
        actor.materials.painted.dispose();
    }
}

// How high a banner floats over a piece, in board units: over a standing character's head, over a rider or over a city.
const BANNER_HEIGHTS = { horseman: 1.3, city: 0.85 };

// The point above a piece where its banner floats, in board units.
export function bannerAnchor(piece) {
    return new Vector3(piece.x, piece.height + (BANNER_HEIGHTS[piece.model] ?? 1.05), piece.z);
}

// Loads the baked characters (true when any arrived); a board rebuilds its pieces when they do. A file that fails to load
// leaves its units showing only their base, and the next board tries it again.
export function loadFigures() {
    loading ??= Promise.allSettled(FIGURES.filter(name => !figures.has(name)).map(async name => {
        const response = await fetch(new URL(`../../models/units/${name}.json`, import.meta.url));
        if (!response.ok) {
            throw new Error(`${name}.json: ${response.status}`);
        }
        const baked = await response.json();
        figures.set(name, geometry(baked.body));
        figures.set(`${name}:seat`, geometry(baked.seat));
    })).then(results => {
        if (results.some(result => result.status === 'rejected')) {
            loading = null;
        }
        return results.some(result => result.status === 'fulfilled');
    });
    return loading;
}

function geometry({ positions, colours, indices }) {
    const shape = new BufferGeometry();
    shape.setAttribute('position', new BufferAttribute(new Float32Array(positions), 3));
    shape.setAttribute('color', new BufferAttribute(new Uint8Array(colours), 3, true));
    shape.setIndex(indices);
    shape.computeVertexNormals();
    return shape;
}

// Frees the shared shapes, figures and materials when a board goes away (see terrain.js disposeShared); they upload again for
// the next board.
export function disposeShared() {
    for (const resource of [...Object.values(shapes), ...figures.values(), solid, painted]) {
        resource.dispose();
    }
}
