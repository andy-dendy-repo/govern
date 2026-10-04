// The board's ground: one raised hex per tile (flat-shaded, with darker sides) whose top is cut into small facets shaded in soft
// patches that run on across tiles of the same terrain, water over lake tiles, and the low-poly dressing of each tile shape (clear
// of the tile's improvement or building): trees, hills, peaks, dunes, grass, flowers, stones, reeds and so on. Each shape comes in
// a few variants (conifers or broadleaf, a meadow or flowers…), picked per tile, so neighbours of one terrain don't look the same.
// A mist covers unexplored tiles and thins out towards the known land. Built from a BoardScene (Features/Games/Models/BoardScene).
import {
    BufferGeometry, ConeGeometry, CylinderGeometry, Float32BufferAttribute, Group, IcosahedronGeometry, InstancedMesh, Matrix4,
    Mesh, MeshBasicMaterial, MeshLambertMaterial, MeshStandardMaterial, Quaternion, Vector3,
} from '../../lib/three/three.module.js';

// Tiles nearly touch: a thin seam shows the hex grid without wide gaps. They stand on a ground at y = 0.
const TILE_RADIUS = 0.985;
const BASE = 0;
const WATER_LEVEL = 0.1;
const MIST_LEVEL = 0.16;
const MIST_DENSITY = 0.92;
export const TOP_JITTER = 0.02;
// How far a decoration keeps from an improvement or building on its tile.
const FEATURE_ROOM = 0.5;
// How far the facets inside a tile's top may dip below its rim (the rim stays level, so rivers and borders lie on it).
const FACET_DIP = 0.012;
// The size, in map units, of the coarse and fine colour patches on the ground.
const PATCH_COARSE = 0.9;
const PATCH_FINE = 0.32;

// Shared shapes, each with its base at y = 0; never disposed.
const foliage = new ConeGeometry(0.2, 0.5, 6).translate(0, 0.25, 0);
const trunk = new CylinderGeometry(0.04, 0.05, 0.14, 5).translate(0, 0.07, 0);
const hump = new IcosahedronGeometry(0.32, 0);
const peak = new ConeGeometry(0.42, 1, 5).translate(0, 0.5, 0);
const cap = new ConeGeometry(0.16, 0.38, 5).translate(0, 0.19, 0);
const crown = new IcosahedronGeometry(0.21, 0).translate(0, 0.18, 0);
const rock = new IcosahedronGeometry(0.09, 0).translate(0, 0.04, 0);
const tuft = new ConeGeometry(0.035, 0.13, 4).translate(0, 0.065, 0);
const flower = new IcosahedronGeometry(0.035, 0).translate(0, 0.05, 0);
const cactus = new CylinderGeometry(0.035, 0.042, 0.26, 6).translate(0, 0.13, 0);
const pad = new CylinderGeometry(0.075, 0.075, 0.008, 7);
const reed = new ConeGeometry(0.012, 0.24, 3).translate(0, 0.12, 0);
const solid = new MeshLambertMaterial({ vertexColors: true, flatShading: true });
const decoration = new MeshLambertMaterial({ flatShading: true });
const mist = new MeshBasicMaterial({ vertexColors: true, transparent: true, depthWrite: false });
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

function smooth(t) {
    return t * t * (3 - 2 * t);
}

// Smooth value noise in [0, 1) over the ground, one lattice value per `size` map units, so a patch runs on across tiles.
function noise(x, z, size, salt) {
    const [fx, fz] = [x / size, z / size];
    const [i, j] = [Math.floor(fx), Math.floor(fz)];
    const [u, v] = [smooth(fx - i), smooth(fz - j)];
    const top = hash(i, j, salt) + (hash(i + 1, j, salt) - hash(i, j, salt)) * u;
    const bottom = hash(i, j + 1, salt) + (hash(i + 1, j + 1, salt) - hash(i, j + 1, salt)) * u;
    return top + (bottom - top) * v;
}

// How much of the accent colour a point of the ground takes: coarse patches with fine grain over them.
function patch(x, z) {
    return noise(x, z, PATCH_COARSE, 91) * 0.7 + noise(x, z, PATCH_FINE, 92) * 0.3;
}

// The colour a terrain's patches lean towards: drier grass, darker undergrowth, scree, rippled sand, deeper lake bed.
const accents = {
    flat: [0.035, 0.05, 0.06],
    trees: [-0.01, 0, -0.07],
    hills: [0.02, -0.05, -0.07],
    peaks: [0, -0.05, 0.09],
    dunes: [-0.015, 0.05, -0.07],
    water: [0, 0, -0.08],
};

function accentOf(tile, colour) {
    const [h, sat, l] = accents[tile.terrain ? tile.shape : 'flat'] ?? accents.flat;
    return colour.clone().offsetHSL(h, sat, tile.terrain ? l : l / 3);
}

// The height of a tile's top: its relief's height, varied a little per tile (up to TOP_JITTER either way) so the ground isn't
// perfectly flat. Rivers and borders are lifted just above TOP_JITTER (rivers.js, overlays.js) so they never flicker.
export function tileTop(tile) {
    return tile.height + (hash(tile.q, tile.r, 1) - 0.5) * 2 * TOP_JITTER;
}

// Where things stand on a tile: its top, or the water over a lake.
export function tileSurface(tile) {
    return tile.shape === 'water' ? Math.max(tileTop(tile), WATER_LEVEL) : tileTop(tile);
}

class MeshBuilder {
    constructor() {
        this.positions = [];
        this.colours = [];
        this.faceTile = [];
    }

    // A triangle in one colour, or with a colour per corner (`colour` an array of three).
    triangle(a, b, c, colour, tile) {
        this.positions.push(...a, ...b, ...c);
        for (let i = 0; i < 3; i++) {
            const corner = Array.isArray(colour) ? colour[i] : colour;
            this.colours.push(corner.r, corner.g, corner.b);
        }
        this.faceTile.push(tile);
    }

    // A hex top cut into 24 facets: each of the six triangles in four, through the middles of its sides. The rim and the centre
    // stay at `y`; the points between dip a little, so the light picks out the facets. Every point leans from `colour` towards
    // `accent` by the ground's patches.
    texturedTop(x, z, y, radius, colour, accent, tile) {
        const point = (px, pz, dip) => {
            const position = [px, y - dip, pz];
            return { position, colour: colour.clone().lerp(accent, patch(px, pz)) };
        };
        const centre = point(x, z, 0);
        const rim = corners.map(([cx, cz]) => point(x + cx * radius, z + cz * radius, 0));
        const inner = corners.map(([cx, cz]) => {
            const [px, pz] = [x + cx * radius / 2, z + cz * radius / 2];
            return point(px, pz, FACET_DIP * noise(px, pz, PATCH_FINE, 93));
        });
        const middle = corners.map(([ax, az], k) => {
            const [bx, bz] = corners[(k + 1) % 6];
            return point(x + (ax + bx) / 2 * radius, z + (az + bz) / 2 * radius, 0);
        });
        const face = (p, q, r) => this.triangle(p.position, q.position, r.position, [p.colour, q.colour, r.colour], tile);
        for (let k = 0; k < 6; k++) {
            const [a, b] = [rim[k], rim[(k + 1) % 6]];
            const [ia, ib] = [inner[k], inner[(k + 1) % 6]];
            face(centre, ib, ia);
            face(ib, b, middle[k]);
            face(ia, middle[k], a);
            face(ib, middle[k], ia);
        }
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

// The k-th of `count` small things scattered over a tile (grass, flowers, stones): round the middle, at varied distances.
function scatter(tile, k, count, spread, salt) {
    const angle = (k / count + hash(tile.q, tile.r, salt + k) * 0.6 / count) * Math.PI * 2;
    const distance = spread * (0.25 + hash(tile.q, tile.r, salt + 50 + k) * 0.75);
    return [tile.x + Math.cos(angle) * distance, tile.z + Math.sin(angle) * distance];
}

// Which of a shape's `count` variants a tile shows.
function variant(tile, count) {
    return Math.floor(hash(tile.q, tile.r, 80) * count);
}

// Whether a decoration at (x, z) keeps clear of the improvement or building on its tile, if any.
function clear(out, tile, x, z) {
    const feature = out.features.get(`${tile.q},${tile.r}`);
    return !feature || Math.hypot(x - feature.x, z - feature.z) > FEATURE_ROOM;
}

// Small things scattered over a tile, each kept clear of its feature: `make(x, z, k)` returns the item or null.
function strew(out, list, tile, count, spread, salt, make) {
    for (let k = 0; k < count; k++) {
        const [x, z] = scatter(tile, k, count, spread, salt);
        if (clear(out, tile, x, z)) {
            const item = make(x, z, k);
            if (item) {
                list.push(item);
            }
        }
    }
}

function size(tile, salt, from, range) {
    return from + hash(tile.q, tile.r, salt) * range;
}

function grass(tile, top, palette, out, count, salt) {
    const colour = palette.terrain(tile).multiplyScalar(0.78);
    strew(out, out.tufts, tile, count, 0.7, salt, (x, z, k) => {
        const s = size(tile, salt + 100 + k, 0.7, 0.6);
        return { x, y: top - FACET_DIP, z, sx: s, sy: s, sz: s, turn: k, colour };
    });
}

function stones(tile, top, palette, out, count, salt) {
    const colour = palette.faded('--gv-board3d-rock', tile);
    strew(out, out.rocks, tile, count, 0.6, salt, (x, z, k) => {
        const s = size(tile, salt + 100 + k, 0.7, 0.8);
        return { x, y: top - FACET_DIP, z, sx: s * 1.2, sy: s * 0.8, sz: s, turn: hash(tile.q, tile.r, salt + 200 + k) * Math.PI, colour };
    });
}

// A tree standing on (x, z): a cone of needles, or a round broadleaf crown on a taller trunk.
function tree(tile, x, z, top, scale, broadleaf, leaves, wood, out, k) {
    const shade = leaves.clone().multiplyScalar(0.85 + hash(tile.q, tile.r, 40 + k) * 0.25);
    if (broadleaf) {
        out.trunks.push({ x, y: top, z, sx: scale, sy: scale * 1.5, sz: scale, colour: wood });
        out.crowns.push({ x, y: top + 0.14 * scale, z, sx: scale, sy: scale * 0.9, sz: scale, turn: k, colour: shade.offsetHSL(0.02, 0, 0.03) });
    } else {
        out.trunks.push({ x, y: top, z, sx: scale, sy: scale, sz: scale, colour: wood });
        out.trees.push({ x, y: top + 0.12 * scale, z, sx: scale, sy: scale, sz: scale, turn: k, colour: shade });
    }
}

const decorators = {
    // Plains: a meadow, flowers, bushes or a few stones, always with some grass.
    flat(tile, top, palette, out) {
        switch (variant(tile, 4)) {
            case 0:
                grass(tile, top, palette, out, 6, 300);
                break;
            case 1: {
                grass(tile, top, palette, out, 3, 300);
                const colours = [palette.faded('--gv-board3d-flower', tile), palette.faded('--gv-board3d-blossom', tile)];
                strew(out, out.flowers, tile, 7, 0.65, 400, (x, z, k) => ({ x, y: top - FACET_DIP, z, colour: colours[k % 2] }));
                break;
            }
            case 2: {
                grass(tile, top, palette, out, 3, 300);
                const colour = palette.terrain(tile).multiplyScalar(0.72);
                strew(out, out.hills, tile, 2, 0.55, 500, (x, z, k) => {
                    const s = size(tile, 510 + k, 0.28, 0.14);
                    return { x, y: top - 0.02, z, sx: s, sy: s * 0.8, sz: s, turn: k, colour };
                });
                break;
            }
            default:
                grass(tile, top, palette, out, 3, 300);
                stones(tile, top, palette, out, 2, 600);
        }
    },
    // Forest: conifers, broadleaf or mixed woods, three to five trees.
    trees(tile, top, palette, out) {
        const leaves = palette.terrain(tile);
        const wood = palette.faded('--gv-board3d-trunk', tile);
        const kind = variant(tile, 3);
        const count = 3 + Math.floor(hash(tile.q, tile.r, 81) * 3);
        for (let k = 0; k < count; k++) {
            const [x, z] = k === 0 ? [tile.x, tile.z] : scatter(tile, k - 1, count - 1, 0.55, 700);
            if (!clear(out, tile, x, z)) {
                continue;
            }
            const broadleaf = kind === 1 || (kind === 2 && k % 2 === 1);
            tree(tile, x, z, top, 0.8 + hash(tile.q, tile.r, 30 + k) * 0.45, broadleaf, leaves, wood, out, k);
        }
        grass(tile, top, palette, out, 2, 310);
    },
    // Hills: rolling humps, with a rocky outcrop or a few trees on some.
    hills(tile, top, palette, out) {
        const colour = palette.terrain(tile).multiplyScalar(1.08);
        const kind = variant(tile, 3);
        const humps = kind === 0 ? 3 : 2;
        for (let k = 0; k < humps; k++) {
            const [x, z] = spot(tile, k, 0.4);
            if (!clear(out, tile, x, z)) {
                continue;
            }
            const s = 0.8 + hash(tile.q, tile.r, 50 + k) * 0.4;
            out.hills.push({ x, y: top - 0.04, z, sx: s, sy: s * 0.55, sz: s, turn: k * 2, colour });
        }
        if (kind === 1) {
            stones(tile, top, palette, out, 3, 610);
        } else if (kind === 2) {
            const leaves = palette.faded('--gv-terrain-forest', tile);
            const wood = palette.faded('--gv-board3d-trunk', tile);
            const [x, z] = spot(tile, 2, 0.5);
            if (clear(out, tile, x, z)) {
                tree(tile, x, z, top, 0.7, false, leaves, wood, out, 5);
            }
        }
        grass(tile, top, palette, out, 2, 320);
    },
    // Mountains: a peak with a lower one, a range of three, or one great peak among boulders.
    peaks(tile, top, palette, out) {
        const rock = palette.terrain(tile);
        const snow = palette.faded('--gv-board3d-snow', tile);
        const kind = variant(tile, 3);
        const peaks = kind === 0 ? [[tile.x - 0.08, tile.z - 0.05, 1], [...spot(tile, 1, 0.45), 0.6]]
            : kind === 1 ? [[...spot(tile, 0, 0.35), 0.75], [...spot(tile, 1, 0.4), 0.6], [...spot(tile, 2, 0.35), 0.85]]
                : [[tile.x, tile.z, 1.2]];
        peaks.filter(([x, z]) => clear(out, tile, x, z)).forEach(([x, z, s], k) => {
            const height = s * (0.9 + hash(tile.q, tile.r, 60 + k) * 0.3);
            out.peaks.push({ x, y: top - 0.05, z, sx: s, sy: height, sz: s, turn: k, colour: rock });
            out.caps.push({ x, y: top - 0.05 + height * 0.62, z, sx: s, sy: height, sz: s, turn: k, colour: snow });
        });
        if (kind === 2) {
            stones(tile, top, palette, out, 3, 620);
        }
    },
    // Desert: dunes, some with a cactus or stones.
    dunes(tile, top, palette, out) {
        const colour = palette.terrain(tile).multiplyScalar(1.06);
        const kind = variant(tile, 3);
        const dunes = kind === 2 ? 3 : 2;
        for (let k = 0; k < dunes; k++) {
            const [x, z] = spot(tile, k, 0.35);
            if (!clear(out, tile, x, z)) {
                continue;
            }
            const long = kind === 2 ? 1 : 1.3;
            out.hills.push({ x, y: top - 0.06, z, sx: long, sy: 0.25, sz: 0.7, turn: hash(tile.q, tile.r, 70 + k) * Math.PI, colour });
        }
        if (kind === 1) {
            const green = palette.faded('--gv-board3d-cactus', tile);
            strew(out, out.cacti, tile, 2, 0.6, 800, (x, z, k) => {
                const s = size(tile, 810 + k, 0.7, 0.6);
                return { x, y: top - FACET_DIP, z, sx: s, sy: s, sz: s, colour: green };
            });
        } else if (kind === 2) {
            stones(tile, top, palette, out, 2, 630);
        }
    },
    // Lakes: open water, reeds along the shore, or lily pads.
    water(tile, top, palette, out) {
        out.water.hexTop(tile.x, tile.z, WATER_LEVEL, TILE_RADIUS, palette.terrain(tile), out.index);
        const kind = variant(tile, 3);
        if (kind === 1) {
            const colour = palette.faded('--gv-board3d-reed', tile);
            for (let cluster = 0; cluster < 2; cluster++) {
                const angle = (hash(tile.q, tile.r, 900 + cluster) + cluster / 2) * Math.PI * 2;
                const [cx, cz] = [tile.x + Math.cos(angle) * 0.68, tile.z + Math.sin(angle) * 0.68];
                for (let k = 0; k < 5; k++) {
                    const [dx, dz] = [(hash(tile.q, tile.r, 910 + cluster * 10 + k) - 0.5) * 0.22, (hash(tile.q, tile.r, 930 + cluster * 10 + k) - 0.5) * 0.22];
                    const s = size(tile, 950 + cluster * 10 + k, 0.7, 0.6);
                    out.reeds.push({ x: cx + dx, y: top, z: cz + dz, sx: s, sy: s, sz: s, colour });
                }
            }
        } else if (kind === 2) {
            const colour = palette.faded('--gv-terrain-forest', tile).offsetHSL(0.03, 0, 0.05);
            strew(out, out.pads, tile, 3, 0.6, 960, (x, z, k) => {
                const s = size(tile, 970 + k, 0.7, 0.6);
                return { x, y: WATER_LEVEL + 0.004, z, sx: s, sy: 1, sz: s, turn: k, colour };
            });
        }
    },
};

// The mist over unexplored tiles: one seamless sheet (a hex per tile, no gaps) whose corners turn clear where they touch a
// known tile, so the fog thins out softly at the edge of what the player has seen instead of ending in a hard line.
function buildMist(scene, colour) {
    const known = new Set();
    for (const tile of scene.tiles) {
        if (tile.terrain) {
            known.add(spotKey(tile.x, tile.z));
        }
    }
    // A corner touches a known tile when one of the three tiles around it is known: their centres are 1 unit away.
    const clear = (x, z) => [0, 1, 2, 3, 4, 5].some(k => known.has(spotKey(x + Math.cos(Math.PI / 3 * k - Math.PI / 2) * 1, z + Math.sin(Math.PI / 3 * k - Math.PI / 2) * 1)))
        || known.has(spotKey(x, z));
    const positions = [];
    const colours = [];
    const vertex = (x, z, alpha) => {
        positions.push(x, MIST_LEVEL, z);
        colours.push(colour.r, colour.g, colour.b, alpha);
    };
    for (const tile of scene.tiles) {
        if (tile.terrain) {
            continue;
        }
        const edge = corners.map(([cx, cz]) => clear(tile.x + cx, tile.z + cz) ? 0 : MIST_DENSITY);
        const middle = edge.every(alpha => alpha > 0) ? MIST_DENSITY : MIST_DENSITY * 0.55;
        for (let k = 0; k < 6; k++) {
            const [ax, az] = corners[k];
            const [bx, bz] = corners[(k + 1) % 6];
            vertex(tile.x, tile.z, middle);
            vertex(tile.x + bx, tile.z + bz, edge[(k + 1) % 6]);
            vertex(tile.x + ax, tile.z + az, edge[k]);
        }
    }
    if (!positions.length) {
        return null;
    }
    const geometry = new BufferGeometry();
    geometry.setAttribute('position', new Float32BufferAttribute(positions, 3));
    geometry.setAttribute('color', new Float32BufferAttribute(colours, 4));
    const sheet = new Mesh(geometry, mist);
    sheet.renderOrder = 1;
    return sheet;
}

// A position rounded to a grid fine enough to tell tile centres apart (centres are at least 1.7 units apart).
function spotKey(x, z) {
    return `${Math.round(x * 4)},${Math.round(z * 4)}`;
}

// Builds the ground of a scene. Returns the group to add and the mesh to pick tiles on (`userData.faceTile`: tile per face).
export function buildTerrain(scene, palette) {
    const ground = new MeshBuilder();
    const features = new Map((scene.features ?? []).map(feature => [`${feature.q},${feature.r}`, feature]));
    const out = {
        water: new MeshBuilder(), trees: [], crowns: [], trunks: [], hills: [], peaks: [], caps: [], rocks: [], tufts: [], flowers: [],
        cacti: [], pads: [], reeds: [], index: 0, features,
    };

    scene.tiles.forEach((tile, index) => {
        out.index = index;
        const top = tileTop(tile);
        const shade = 0.96 + hash(tile.q, tile.r, 2) * 0.08;
        const colour = tile.shape === 'water' ? palette.faded('--gv-board3d-seabed', tile) : palette.terrain(tile).multiplyScalar(shade);
        ground.texturedTop(tile.x, tile.z, top, TILE_RADIUS, colour, accentOf(tile, colour), index);
        ground.hexSides(tile.x, tile.z, BASE, top, TILE_RADIUS, colour.clone().multiplyScalar(0.7), index);
        if (!tile.terrain) {
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
    const fog = buildMist(scene, palette.token('--gv-board3d-mist'));
    if (fog) {
        group.add(fog);
    }
    const shapes = [
        [foliage, out.trees], [crown, out.crowns], [trunk, out.trunks], [hump, out.hills], [peak, out.peaks], [cap, out.caps],
        [rock, out.rocks], [tuft, out.tufts], [flower, out.flowers], [cactus, out.cacti], [pad, out.pads], [reed, out.reeds],
    ];
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
    for (const resource of [foliage, crown, trunk, hump, peak, cap, rock, tuft, flower, cactus, pad, reed, solid, decoration, water, mist]) {
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
