// Rivers and bridges: a strip of water along each river edge, with round ends so a river's edges join at the corners, and a
// plank deck with rails across each bridged edge, sloping between the two tiles. Built from a BoardScene's rivers and bridges.
import {
    BoxGeometry, BufferGeometry, CircleGeometry, DoubleSide, Float32BufferAttribute, Group, Mesh, MeshLambertMaterial,
    MeshStandardMaterial, Quaternion, Vector3,
} from '../../lib/three/three.module.js';
import { instances } from './shapes.js';
import { TOP_JITTER } from './terrain.js';

// The water sits just above the highest a tile top can vary (terrain.js), so it never flickers against the ground.
const RIVER_LIFT = TOP_JITTER + 0.002;
const DECK_WIDTH = 0.24;
const DECK_THICKNESS = 0.035;
const DECK_LIFT = 0.03;
const RAIL_HEIGHT = 0.05;

const disc = new CircleGeometry(0.5, 12).rotateX(-Math.PI / 2);
const plank = new BoxGeometry(1, 1, 1);
const surface = new MeshStandardMaterial({ vertexColors: true, roughness: 0.3, metalness: 0, side: DoubleSide });
const ends = new MeshStandardMaterial({ roughness: 0.3, metalness: 0, side: DoubleSide });
const wood = new MeshLambertMaterial({ flatShading: true });

// The water's colour: deep rivers darker, remembered ones in their explored shade (as the 2D board's .river rules).
function riverColour(river, palette) {
    const name = river.deep ? '--gv-river-deep' : '--gv-river';
    return palette.token(river.visibility === 'visible' ? name : `${name}-explored`);
}

function buildWater(rivers, palette) {
    const positions = [];
    const colours = [];
    const corners = [];
    for (const river of rivers) {
        const colour = riverColour(river, palette);
        const y = river.height + RIVER_LIFT;
        const length = Math.hypot(river.x2 - river.x1, river.z2 - river.z1) || 1;
        const [nx, nz] = [-(river.z2 - river.z1) / length * river.width / 2, (river.x2 - river.x1) / length * river.width / 2];
        const a = [river.x1 + nx, y, river.z1 + nz];
        const b = [river.x1 - nx, y, river.z1 - nz];
        const c = [river.x2 - nx, y, river.z2 - nz];
        const d = [river.x2 + nx, y, river.z2 + nz];
        positions.push(...a, ...b, ...c, ...a, ...c, ...d);
        for (let i = 0; i < 6; i++) {
            colours.push(colour.r, colour.g, colour.b);
        }
        for (const [x, z] of [[river.x1, river.z1], [river.x2, river.z2]]) {
            corners.push({ x, y, z, sx: river.width, sy: 1, sz: river.width, colour });
        }
    }
    const geometry = new BufferGeometry();
    geometry.setAttribute('position', new Float32BufferAttribute(positions, 3));
    geometry.setAttribute('color', new Float32BufferAttribute(colours, 3));
    geometry.computeVertexNormals();
    const strips = new Mesh(geometry, surface);
    strips.receiveShadow = true;
    const joints = instances(disc, ends, corners);
    joints.castShadow = false;
    return [strips, joints];
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
    for (const resource of [disc, plank, surface, ends, wood]) {
        resource.dispose();
    }
}
