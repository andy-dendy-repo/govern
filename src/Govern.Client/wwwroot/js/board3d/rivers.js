// Rivers and bridges: each river edge is a gently winding ribbon of water over a slightly wider band of bank, darker at its
// sides than in its middle, with a few light ripples on visible rivers, and round ends so a river's edges join at the corners;
// each bridged edge gets a plank deck with rails, sloping between the two tiles. Built from a BoardScene's rivers and bridges.
import {
    BoxGeometry, BufferGeometry, CircleGeometry, DoubleSide, Float32BufferAttribute, Group, Mesh, MeshLambertMaterial,
    MeshStandardMaterial, Quaternion, Vector3,
} from '../../lib/three/three.module.js';
import { instances } from './shapes.js';
import { hash, TOP_JITTER } from './terrain.js';

// The water sits just above the highest a tile top can vary (terrain.js), so it never flickers against the ground; the bank just
// under it, and the ripples on it, are kept apart from it by polygon offsets.
const RIVER_LIFT = TOP_JITTER + 0.002;
const BANK_LIFT = TOP_JITTER + 0.001;
const BANK = 0.035;
// Pieces per edge, how far an edge's middle may wind off the straight line (at most this share of its width), how much wider it
// gets there, and how dark the water is at its sides.
const SEGMENTS = 8;
const WIND = 0.35;
const SWELL = 0.15;
const SIDE_SHADE = 0.75;
const DECK_WIDTH = 0.24;
const DECK_THICKNESS = 0.035;
const DECK_LIFT = 0.03;
const RAIL_HEIGHT = 0.05;

// A disc whose rim is as dark as the ribbon's sides, so a river's round ends match its water.
const disc = new CircleGeometry(0.5, 16).rotateX(-Math.PI / 2);
disc.setAttribute('color', new Float32BufferAttribute(Array.from({ length: disc.attributes.position.count }, (_, i) => {
    const shade = i === 0 ? 1 : SIDE_SHADE;
    return [shade, shade, shade];
}).flat(), 3));
const plain = new CircleGeometry(0.5, 16).rotateX(-Math.PI / 2);
const plank = new BoxGeometry(1, 1, 1);
const surface = new MeshStandardMaterial({ vertexColors: true, roughness: 0.3, metalness: 0, side: DoubleSide, polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -2 });
const ends = new MeshStandardMaterial({ vertexColors: true, roughness: 0.3, metalness: 0, side: DoubleSide, polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -2 });
const bank = new MeshLambertMaterial({ vertexColors: true, side: DoubleSide });
const banks = new MeshLambertMaterial({ side: DoubleSide });
const ripple = new MeshStandardMaterial({ roughness: 0.15, metalness: 0, side: DoubleSide, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -6 });
const wood = new MeshLambertMaterial({ flatShading: true });

// The water's colour: deep rivers darker, remembered ones in their explored shade. The token names are written out, so
// GameUiTests.Board3DScripts_ReadOnlyTokensThatExist sees every one.
function riverColour(river, palette) {
    const visible = river.visibility === 'visible';
    if (river.deep) {
        return palette.token(visible ? '--gv-river-deep' : '--gv-river-deep-explored');
    }
    return palette.token(visible ? '--gv-river' : '--gv-river-explored');
}

// The points along an edge: its line, bent to one side by a little (the same bend every time, from the edge's middle), and the
// half-width there.
function course(river) {
    const length = Math.hypot(river.x2 - river.x1, river.z2 - river.z1) || 1;
    const [dx, dz] = [(river.x2 - river.x1) / length, (river.z2 - river.z1) / length];
    const [mx, mz] = [Math.round((river.x1 + river.x2) * 8), Math.round((river.z1 + river.z2) * 8)];
    const bend = (hash(mx, mz, 7) - 0.5) * 2 * WIND * river.width;
    const wobble = (hash(mx, mz, 8) - 0.5) * WIND * river.width;
    return Array.from({ length: SEGMENTS + 1 }, (_, i) => {
        const t = i / SEGMENTS;
        const offset = bend * Math.sin(Math.PI * t) + wobble * Math.sin(2 * Math.PI * t);
        return {
            x: river.x1 + (river.x2 - river.x1) * t - dz * offset,
            z: river.z1 + (river.z2 - river.z1) * t + dx * offset,
            half: river.width / 2 * (1 + SWELL * Math.sin(Math.PI * t)),
            nx: -dz,
            nz: dx,
        };
    });
}

// A ribbon along the points at height y, `extra` wider on each side, coloured `middle` along its centre and `side` at its edges.
function ribbon(points, y, extra, middle, side, positions, colours) {
    const at = (point, sign) => [point.x + point.nx * (point.half + extra) * sign, y, point.z + point.nz * (point.half + extra) * sign];
    const centre = point => [point.x, y, point.z];
    const push = (vertices, shades) => {
        positions.push(...vertices.flat());
        for (const shade of shades) {
            colours.push(shade.r, shade.g, shade.b);
        }
    };
    for (let i = 0; i < points.length - 1; i++) {
        const [p, q] = [points[i], points[i + 1]];
        for (const sign of [1, -1]) {
            push([at(p, sign), centre(p), centre(q)], [side, middle, middle]);
            push([at(p, sign), centre(q), at(q, sign)], [side, middle, side]);
        }
    }
}

function mesh(positions, colours, material) {
    const geometry = new BufferGeometry();
    geometry.setAttribute('position', new Float32BufferAttribute(positions, 3));
    geometry.setAttribute('color', new Float32BufferAttribute(colours, 3));
    geometry.computeVertexNormals();
    const built = new Mesh(geometry, material);
    built.receiveShadow = true;
    return built;
}

function buildWater(rivers, palette) {
    const water = { positions: [], colours: [] };
    const shore = { positions: [], colours: [] };
    const joints = [];
    const shoreJoints = [];
    const ripples = [];
    for (const river of rivers) {
        const colour = riverColour(river, palette);
        const sides = colour.clone().multiplyScalar(SIDE_SHADE);
        const land = palette.faded('--gv-board3d-riverbank', river);
        const points = course(river);
        ribbon(points, river.height + RIVER_LIFT, 0, colour, sides, water.positions, water.colours);
        ribbon(points, river.height + BANK_LIFT, BANK, land, land, shore.positions, shore.colours);
        for (const [x, z] of [[river.x1, river.z1], [river.x2, river.z2]]) {
            joints.push({ x, y: river.height + RIVER_LIFT, z, sx: river.width, sy: 1, sz: river.width, colour });
            shoreJoints.push({ x, y: river.height + BANK_LIFT, z, sx: river.width + BANK * 2, sy: 1, sz: river.width + BANK * 2, colour: land });
        }
        if (river.visibility === 'visible') {
            const light = colour.clone().offsetHSL(0, -0.1, 0.22);
            const turn = -Math.atan2(river.z2 - river.z1, river.x2 - river.x1);
            for (let k = 0; k < 2; k++) {
                const point = points[2 + k * 4];
                const shift = (hash(Math.round(point.x * 8), Math.round(point.z * 8), 9 + k) - 0.5) * point.half;
                ripples.push({
                    x: point.x + point.nx * shift, y: river.height + RIVER_LIFT, z: point.z + point.nz * shift,
                    sx: river.width * 0.9, sy: 1, sz: river.width * 0.12, turn, colour: light,
                });
            }
        }
    }
    const layers = [mesh(water.positions, water.colours, surface), mesh(shore.positions, shore.colours, bank)];
    for (const [shape, material, items] of [[disc, ends, joints], [plain, banks, shoreJoints], [plain, ripple, ripples]]) {
        if (items.length) {
            const instanced = instances(shape, material, items);
            instanced.castShadow = false;
            layers.push(instanced);
        }
    }
    return layers;
}

function buildBridges(bridges, palette) {
    const deck = palette.token('--gv-bridge');
    const rail = palette.token('--gv-bridge-outline');
    const parts = [];
    const along = new Vector3(1, 0, 0);
    for (const bridge of bridges) {
        const from = new Vector3(bridge.x1, bridge.y1 + DECK_LIFT, bridge.z1);
        const to = new Vector3(bridge.x2, bridge.y2 + DECK_LIFT, bridge.z2);
        const direction = to.clone().sub(from);
        const length = direction.length();
        const rotation = new Quaternion().setFromUnitVectors(along, direction.normalize());
        const middle = from.clone().add(to).multiplyScalar(0.5);
        parts.push({ x: middle.x, y: middle.y, z: middle.z, sx: length, sy: DECK_THICKNESS, sz: DECK_WIDTH, rotation, colour: deck });
        const side = new Vector3(-direction.z, 0, direction.x).normalize().multiplyScalar(DECK_WIDTH / 2);
        for (const sign of [1, -1]) {
            parts.push({
                x: middle.x + side.x * sign, y: middle.y + RAIL_HEIGHT / 2, z: middle.z + side.z * sign,
                sx: length, sy: RAIL_HEIGHT, sz: 0.025, rotation, colour: rail,
            });
        }
    }
    return instances(plank, wood, parts);
}

// Builds the rivers and bridges of a scene, or null when it has none.
export function buildRivers(scene, palette) {
    const group = new Group();
    if (scene.rivers?.length) {
        group.add(...buildWater(scene.rivers, palette));
    }
    if (scene.bridges?.length) {
        group.add(buildBridges(scene.bridges, palette));
    }
    return group.children.length ? group : null;
}

// Frees the shared shapes and materials when a board goes away (see terrain.js disposeShared).
export function disposeShared() {
    for (const resource of [disc, plain, plank, surface, ends, bank, banks, ripple, wood]) {
        resource.dispose();
    }
}
