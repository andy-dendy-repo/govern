// The board's ground: one raised hex per tile (flat-shaded, with darker sides), water over lake tiles, low-poly trees, hills,
// peaks and dunes per tile shape, and clouds over unexplored tiles. Built from a BoardScene (Features/Games/Models/BoardScene).
import {
    BufferGeometry, ConeGeometry, CylinderGeometry, Float32BufferAttribute, Group, IcosahedronGeometry, InstancedMesh, Matrix4,
    Mesh, MeshLambertMaterial, MeshStandardMaterial, Quaternion, Vector3,
} from '../../lib/three/three.module.js';

const TILE_RADIUS = 0.95;
const BASE = -0.4;
const WATER_LEVEL = 0.2;

// Shared shapes, each with its base at y = 0; never disposed.
const foliage = new ConeGeometry(0.2, 0.5, 6).translate(0, 0.25, 0);
const trunk = new CylinderGeometry(0.04, 0.05, 0.14, 5).translate(0, 0.07, 0);
const hump = new IcosahedronGeometry(0.32, 0);
const peak = new ConeGeometry(0.42, 1, 5).translate(0, 0.5, 0);
const cap = new ConeGeometry(0.16, 0.38, 5).translate(0, 0.19, 0);
const puff = new IcosahedronGeometry(0.42, 1);
const solid = new MeshLambertMaterial({ vertexColors: true, flatShading: true });
const decoration = new MeshLambertMaterial({ flatShading: true });
const water = new MeshStandardMaterial({ vertexColors: true, transparent: true, opacity: 0.72, roughness: 0.25, metalness: 0, depthWrite: false });

// The corners of a hex around its centre: pointy top, corner k at 60k − 30 degrees, as in HexLayout.
const corners = Array.from({ length: 6 }, (_, k) => {
    const angle = Math.PI / 180 * (60 * k - 30);
    return [Math.cos(angle), Math.sin(angle)];
});

// A stable pseudo-random number in [0, 1) per tile and purpose, so a tile looks the same on every view.
export function hash(q, r, salt) {
    let h = Math.imul(q, 374761393) ^ Math.imul(r, 668265263) ^ Math.imul(salt, 2246822519);
    h = Math.imul(h ^ (h >>> 13), 1274126177);
    return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

class MeshBuilder {
    constructor() {
        this.positions = [];
        this.colours = [];
        this.faceTile = [];
    }

    triangle(a, b, c, colour, tile) {
        this.positions.push(...a, ...b, ...c);
        for (let i = 0; i < 3; i++) {
            this.colours.push(colour.r, colour.g, colour.b);
        }
        this.faceTile.push(tile);
    }

    // A hex top at height y, radius `radius`, around (x, z).
    hexTop(x, z, y, radius, colour, tile) {
        for (let k = 0; k < 6; k++) {
            const [ax, az] = corners[k];
            const [bx, bz] = corners[(k + 1) % 6];
            this.triangle([x, y, z], [x + bx * radius, y, z + bz * radius], [x + ax * radius, y, z + az * radius], colour, tile);
        }
    }

    // The six sides of a hex prism from `bottom` up to `top`.
    hexSides(x, z, bottom, top, radius, colour, tile) {
        for (let k = 0; k < 6; k++) {
            const [ax, az] = corners[k];
            const [bx, bz] = corners[(k + 1) % 6];
            const a0 = [x + ax * radius, bottom, z + az * radius];
            const b0 = [x + bx * radius, bottom, z + bz * radius];
            const a1 = [x + ax * radius, top, z + az * radius];
            const b1 = [x + bx * radius, top, z + bz * radius];
            this.triangle(a0, b1, b0, colour, tile);
            this.triangle(a0, a1, b1, colour, tile);
        }
    }

    build(material) {
        const geometry = new BufferGeometry();
        geometry.setAttribute('position', new Float32BufferAttribute(this.positions, 3));
        geometry.setAttribute('color', new Float32BufferAttribute(this.colours, 3));
        geometry.computeVertexNormals();
        const mesh = new Mesh(geometry, material);
        mesh.userData.faceTile = Int32Array.from(this.faceTile);
        return mesh;
    }
}

// Many copies of one shape, each with its own place, scale, turn and colour.
function instanced(geometry, items) {
    const mesh = new InstancedMesh(geometry, decoration, Math.max(items.length, 1));
    mesh.count = items.length;
    const matrix = new Matrix4();
    const turn = new Quaternion();
    const up = new Vector3(0, 1, 0);
    items.forEach((item, i) => {
        turn.setFromAxisAngle(up, item.turn ?? 0);
        matrix.compose(new Vector3(item.x, item.y, item.z), turn, new Vector3(item.sx ?? 1, item.sy ?? 1, item.sz ?? 1));
        mesh.setMatrixAt(i, matrix);
        mesh.setColorAt(i, item.colour);
    });
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    return mesh;
}

// Offsets inside a tile for its decorations: spread around the middle, varied per tile.
function spot(tile, k, spread) {
    const angle = (k / 3 + hash(tile.q, tile.r, 10 + k) * 0.2) * Math.PI * 2;
    const distance = spread * (0.45 + hash(tile.q, tile.r, 20 + k) * 0.55);
    return [tile.x + Math.cos(angle) * distance, tile.z + Math.sin(angle) * distance];
}

const decorators = {
    flat() { },
    trees(tile, top, palette, out) {
        const leaves = palette.terrain(tile);
        const wood = palette.faded('--gv-board3d-trunk', tile);
        for (let k = 0; k < 4; k++) {
            const [x, z] = k === 3 ? [tile.x, tile.z] : spot(tile, k, 0.5);
            const size = 0.8 + hash(tile.q, tile.r, 30 + k) * 0.45;
            out.trunks.push({ x, y: top, z, sx: size, sy: size, sz: size, colour: wood });
            out.trees.push({ x, y: top + 0.12 * size, z, sx: size, sy: size, sz: size, turn: k, colour: leaves.clone().multiplyScalar(0.85 + hash(tile.q, tile.r, 40 + k) * 0.25) });
        }
    },
    hills(tile, top, palette, out) {
        const colour = palette.terrain(tile).multiplyScalar(1.08);
        for (let k = 0; k < 3; k++) {
            const [x, z] = spot(tile, k, 0.4);
            const size = 0.8 + hash(tile.q, tile.r, 50 + k) * 0.4;
            out.hills.push({ x, y: top - 0.04, z, sx: size, sy: size * 0.55, sz: size, turn: k * 2, colour });
        }
    },
    peaks(tile, top, palette, out) {
        const rock = palette.terrain(tile);
        const snow = palette.faded('--gv-board3d-snow', tile);
        const peaks = [[tile.x - 0.08, tile.z - 0.05, 1], [...spot(tile, 1, 0.45), 0.6]];
        peaks.forEach(([x, z, size], k) => {
            const height = size * (0.9 + hash(tile.q, tile.r, 60 + k) * 0.3);
            out.peaks.push({ x, y: top - 0.05, z, sx: size, sy: height, sz: size, turn: k, colour: rock });
            out.caps.push({ x, y: top - 0.05 + height * 0.62, z, sx: size, sy: height, sz: size, turn: k, colour: snow });
        });
    },
    dunes(tile, top, palette, out) {
        const colour = palette.terrain(tile).multiplyScalar(1.06);
        for (let k = 0; k < 2; k++) {
            const [x, z] = spot(tile, k, 0.35);
            out.hills.push({ x, y: top - 0.06, z, sx: 1.3, sy: 0.25, sz: 0.7, turn: hash(tile.q, tile.r, 70 + k) * Math.PI, colour });
        }
    },
    water(tile, top, palette, out) {
        out.water.hexTop(tile.x, tile.z, WATER_LEVEL, TILE_RADIUS, palette.terrain(tile), out.index);
    },
};

// Builds the ground of a scene. Returns the group to add and the mesh to pick tiles on (`userData.faceTile`: tile per face).
export function buildTerrain(scene, palette) {
    const ground = new MeshBuilder();
    const out = { water: new MeshBuilder(), trees: [], trunks: [], hills: [], peaks: [], caps: [], clouds: [], index: 0 };
    const cloud = palette.token('--gv-board3d-cloud');

    scene.tiles.forEach((tile, index) => {
        out.index = index;
        const top = tile.height + (hash(tile.q, tile.r, 1) - 0.5) * 0.04;
        const shade = 0.96 + hash(tile.q, tile.r, 2) * 0.08;
        const colour = tile.shape === 'water' ? palette.faded('--gv-board3d-seabed', tile) : palette.terrain(tile).multiplyScalar(shade);
        ground.hexTop(tile.x, tile.z, top, TILE_RADIUS, colour, index);
        ground.hexSides(tile.x, tile.z, BASE, top, TILE_RADIUS, colour.clone().multiplyScalar(0.7), index);
        if (!tile.terrain) {
            // One soft puff per tile, drifting off its centre, so the unknown reads as a sea of cloud rather than tiles.
            const [x, z] = spot(tile, 0, 0.3);
            const size = 1.6 + hash(tile.q, tile.r, 80) * 0.5;
            out.clouds.push({ x, y: 0.2 + hash(tile.q, tile.r, 81) * 0.2, z, sx: size, sy: 0.5, sz: size * 0.9, turn: hash(tile.q, tile.r, 82) * Math.PI, colour: cloud });
            return;
        }
        (decorators[tile.shape] ?? decorators.flat)(tile, top, palette, out);
    });

    const group = new Group();
    const pick = ground.build(solid);
    pick.receiveShadow = true;
    pick.castShadow = true;
    group.add(pick);
    if (out.water.positions.length) {
        const surface = out.water.build(water);
        surface.receiveShadow = true;
        group.add(surface);
    }
    const shapes = [[foliage, out.trees], [trunk, out.trunks], [hump, out.hills], [peak, out.peaks], [cap, out.caps], [puff, out.clouds]];
    for (const [geometry, items] of shapes) {
        if (items.length) {
            group.add(instanced(geometry, items));
        }
    }
    return { group, pick };
}

// Frees the shared shapes and materials when a board goes away. Three.js keeps a reference to the renderer in every shape
// and material it drew, so keeping them would keep the old renderer alive; the next board simply uploads them again.
export function disposeShared() {
    for (const resource of [foliage, trunk, hump, peak, cap, puff, solid, decoration, water]) {
        resource.dispose();
    }
}

// Frees what buildTerrain created (the shared shapes and materials stay).
export function disposeTerrain(group) {
    group.traverse(object => {
        if (object.isInstancedMesh) {
            object.dispose();
        } else if (object.isMesh) {
            object.geometry.dispose();
        }
    });
}
