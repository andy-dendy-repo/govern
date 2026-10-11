// The board's ground: one continuous, flat-shaded landscape (ground.js gives its shape: a level plateau per tile that slopes to
// meet its neighbours, with no seams between explored tiles, and the relief of the terrain regions over it: mountain ranges,
// rolling downs, dune seas). A smooth tile's top is cut into small facets, a tile a region rises on into a fine grid of rings
// that follows its relief; every tile's rim has the same points, shared with its neighbours. Its colour runs into its
// neighbours' at the rim, where the tiles around a point share one blended colour, and leans in soft patches that run on across
// the map, so terrains fade into each other instead of ending at a hex line; sand lines the slope down to a lake and snow lies
// over the snowline. Water lies over lake tiles, lighter in the shallows, and runs a little onto their shores. The woods are
// planted over the whole map at once (plantWoods), so neighbouring forest tiles make one wood with a ragged edge. Each tile
// carries the low-poly dressing of its shape (clear of its improvement or building, set on the ground under it): grass, flowers,
// stones, a range's crags, cacti, reeds and so on. Each shape comes in a few variants (a meadow or flowers…), picked per tile,
// so neighbours of one terrain don't look the same.
// A mist covers unexplored tiles and thins out towards the known land. Built from a BoardScene (Features/Games/Models/BoardScene).
import {
    BufferAttribute, BufferGeometry, ConeGeometry, CylinderGeometry, Float32BufferAttribute, Group, IcosahedronGeometry, InstancedMesh, Matrix4,
    Mesh, MeshBasicMaterial, MeshLambertMaterial, MeshStandardMaterial, Quaternion, Vector3,
} from '../../lib/three/three.module.js';
import {
    corners, fan, groundOf, hash, noise, PLATEAU, RIM_STEPS, ring, SHELF, smooth, stitch, tileLevel, WATER_LEVEL, weightsAt,
} from './ground.js';
import { massif, snowfield } from './mountains.js';

export { hash };

// Tiles meet edge to edge; the board stands on a ground at y = 0, which shows only at the map's edge and under the mist.
const TILE_RADIUS = 1;
const BASE = 0;
const MIST_LEVEL = 0.16;
const MIST_DENSITY = 0.92;
// The water's surface sits just above the waterline the shores come down to, so the two never flicker.
const WATER_LIFT = 0.003;
// How far past a lake's rim its water runs onto the land's slope (map units), in steps along each edge, waving at this size.
const SHORE_MIN = 0.03;
const SHORE_MAX = 0.24;
const SHORE_STEPS = 6;
const SHORE_WAVE = 0.45;
// Where the facets of a plateau bend: its inner ring, as a share of the radius.
const INNER = 0.35;
// The snowline over the ground (map units), how far it wanders and how soft it is; how much sand lies on a lake's shore.
const SNOW_LINE = 0.9;
const SNOW_RAGGED = 0.16;
const SNOW_BLEND = 0.06;
const BEACH = 0.8;
// How much lighter a lake is in its shallows; the height a crag's top must reach to carry snow.
const SHALLOWS = 0.07;
const CAP_LINE = 1.05;
// Where a range's crags may stand: tries round the shelf, and how far the range must lift the ground there (map units).
const SUMMIT_TRIES = 12;
const SUMMIT_RISE = 0.12;
// The woods: the grid trees are planted on (map units), the density a tree needs (plus up to WOOD_EDGE_WANDER, by noise, so
// the edge wavers), the clearing kept round a forest tile's middle (a share of the radius) and the room kept from a river.
const WOOD_CELL = 0.44;
const WOOD_EDGE = 0.36;
const WOOD_EDGE_WANDER = 0.3;
const CLEARING = 0.18;
const RIVER_ROOM = 0.14;
// The terrains a tree may stand on.
const WOODED = new Set(['trees', 'flat', 'hills']);
// The size, in map units, of the slow light and shade that runs over the whole ground, and how strong it is (either way).
const SHADE_SIZE = 3.2;
const SHADE = 0.05;
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

// The height of a tile's top (its plateau): see tileLevel in ground.js.
export function tileTop(tile) {
    return tileLevel(tile);
}

// Where things stand on a tile: its top, or the water over a lake.
export function tileSurface(tile) {
    return tile.shape === 'water' ? Math.max(tileTop(tile), WATER_LEVEL) : tileTop(tile);
}

// The colour of the ground at (x, z), shared by the tiles that meet there: each one's colour leaned towards its accent by the
// patches (world-space, so the same at that point for every tile), averaged, then lit or shaded by the slow shade.
function snap(value) {
    return Math.round(value * 4096) / 4096;
}

// Plain numbers ({ r, g, b }) rather than THREE.Color: a big map has a few hundred thousand of these.
function groundColour(x, z, looks) {
    return scaled(leaned(looks, patch(x, z)), shadeAt(x, z));
}

// The mean of some tiles' colours, each leaned towards its accent by `lean`.
function leaned(looks, lean) {
    let [r, g, b] = [0, 0, 0];
    for (const { colour, accent } of looks) {
        r += colour.r + (accent.r - colour.r) * lean;
        g += colour.g + (accent.g - colour.g) * lean;
        b += colour.b + (accent.b - colour.b) * lean;
    }
    return { r: r / looks.length, g: g / looks.length, b: b / looks.length };
}

// The slow light and shade over the ground at (x, z), as a factor.
function shadeAt(x, z) {
    return 1 + (noise(x, z, SHADE_SIZE, 94) - 0.5) * 2 * SHADE;
}

// Writes triangles straight into typed arrays that double when full: a big map has a few hundred thousand. `capacity` is the
// number of triangles to make room for at first.
class MeshBuilder {
    constructor(capacity = 1024) {
        this.count = 0;
        this.positions = new Float32Array(9 * capacity);
        this.colours = new Float32Array(9 * capacity);
        this.faceTile = new Int32Array(capacity);
    }

    // A triangle in one colour, or with a colour per corner (`colour` an array of three).
    triangle(a, b, c, colour, tile) {
        if (this.count === this.faceTile.length) {
            this.grow();
        }
        const at = this.count * 9;
        const p = this.positions;
        p[at] = a[0]; p[at + 1] = a[1]; p[at + 2] = a[2];
        p[at + 3] = b[0]; p[at + 4] = b[1]; p[at + 5] = b[2];
        p[at + 6] = c[0]; p[at + 7] = c[1]; p[at + 8] = c[2];
        const many = Array.isArray(colour);
        for (let i = 0; i < 3; i++) {
            const corner = many ? colour[i] : colour;
            this.colours[at + i * 3] = corner.r;
            this.colours[at + i * 3 + 1] = corner.g;
            this.colours[at + i * 3 + 2] = corner.b;
        }
        this.faceTile[this.count++] = tile;
    }

    grow() {
        const bigger = (array, Type) => {
            const next = new Type(array.length * 2);
            next.set(array);
            return next;
        };
        this.positions = bigger(this.positions, Float32Array);
        this.colours = bigger(this.colours, Float32Array);
        this.faceTile = bigger(this.faceTile, Int32Array);
    }

    // A tile's piece of the landscape (`look(tile)` gives a tile's colours; `shared` holds the rim vertices already worked out).
    // Its rim has RIM_STEPS points along each edge, whose heights and colours come from the tiles that meet there. Inside, a
    // smooth tile is a level plateau cut into facets (the inner ring dips a little, so the light picks them out) and the slope
    // from it to the rim; a tile a region's relief rises on (ground.js) is cut into a fine grid of rings that follows it.
    landTop(tile, ground, look, shared, index) {
        const [x, z] = [tile.x, tile.z];
        if (!tile.terrain) {
            // Unexplored: a plain flat hex under the mist; its explored neighbours wall their side down to the base.
            this.hexTop(x, z, tileLevel(tile), TILE_RADIUS, groundColour(x, z, [look(tile)]), index);
            return;
        }
        const point = (px, pz, dip = 0) => {
            const weights = weightsAt(px - x, pz - z);
            const y = ground.heightIn(tile, px, pz, undefined, weights) - dip;
            return { position: [px, y, pz], colour: colourAt(tile, px, pz, y, ground, look, weights) };
        };
        // A facet's small dip on the level middle of a tile (none on the rim, where rivers and borders lie).
        const facet = (px, pz) => point(px, pz, FACET_DIP * noise(px, pz, PATCH_FINE, 93));
        // Rim points are snapped to a fine grid and worked out once per place, so the tiles sharing one write exactly the same
        // vertex (no hairline cracks).
        const rim = ring(x, z, 1, RIM_STEPS).map(([px, pz]) => {
            const [sx, sz] = [snap(px), snap(pz)];
            const key = Math.round(sx * 4096) * 4194304 + Math.round(sz * 4096) + 1048576;
            let vertex = shared.get(key);
            if (!vertex) {
                vertex = point(sx, sz);
                shared.set(key, vertex);
            }
            return vertex;
        });
        const face = (p, q, r) => this.landTriangle(p, q, r, index);
        const centre = facet(x, z);
        let inner;
        if (ground.rough(tile)) {
            inner = ring(x, z, 1 / RIM_STEPS, 1).map(([px, pz]) => facet(px, pz));
            fan(centre, inner, face);
            for (let j = 2; j < RIM_STEPS; j++) {
                const share = j / RIM_STEPS;
                // (A ring's share of the radius is in map units too: the radius is 1.)
                const next = ring(x, z, share, j).map(([px, pz]) => share < SHELF ? facet(px, pz) : point(px, pz));
                stitch(inner, next, face);
                inner = next;
            }
        } else {
            const facets = ring(x, z, INNER, 1).map(([px, pz]) => facet(px, pz));
            fan(centre, facets, face);
            inner = ring(x, z, PLATEAU, 2).map(([px, pz]) => point(px, pz));
            stitch(facets, inner, face);
        }
        stitch(inner, rim, face);
        for (let k = 0; k < 6; k++) {
            if (!ground.joined(tile, k)) {
                this.edgeSide(Array.from({ length: RIM_STEPS + 1 }, (_, s) => rim[(k * RIM_STEPS + s) % rim.length]), index);
            }
        }
    }

    // A triangle of the land, turned to face up, with a colour per corner.
    landTriangle(p, q, r, index) {
        const [a, b, c] = [p.position, q.position, r.position];
        if ((b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2]) < 0) {
            this.triangle(a, c, b, [p.colour, r.colour, q.colour], index);
        } else {
            this.triangle(a, b, c, [p.colour, q.colour, r.colour], index);
        }
    }

    // The wall under a rim edge (its points from corner to corner) down to the base, where no explored neighbour meets it: at
    // the map's edge and along the mist.
    edgeSide(points, index) {
        const shade = point => ({ r: point.colour.r * 0.7, g: point.colour.g * 0.7, b: point.colour.b * 0.7 });
        for (let i = 0; i < points.length - 1; i++) {
            const [p, q] = [points[i], points[i + 1]];
            const [p0, q0] = [[p.position[0], BASE, p.position[2]], [q.position[0], BASE, q.position[2]]];
            this.triangle(p0, q.position, q0, [shade(p), shade(q), shade(q)], index);
            this.triangle(p0, p.position, q.position, [shade(p), shade(p), shade(q)], index);
        }
    }

    // The water's run past edge k of a hex at height y, onto the land's slope that dips under it: a strip whose outer line
    // wanders out between SHORE_MIN and SHORE_MAX (world-space noise, so it runs on smoothly round a corner into the next edge),
    // which gives the lake a wavy shoreline of its own instead of a hex line. At an end shared with another lake the strip closes
    // to the corner (`reach`), so no water lies there twice.
    skirt(x, z, y, k, reach, colour, tile) {
        const [ax, az] = corners[k];
        const [bx, bz] = corners[(k + 1) % 6];
        const point = i => {
            const t = i / SHORE_STEPS;
            const [px, pz] = [x + ax + (bx - ax) * t, z + az + (bz - az) * t];
            const ends = (i === 0 && !reach[0]) || (i === SHORE_STEPS && !reach[1]);
            const out = ends ? 0 : SHORE_MIN + (SHORE_MAX - SHORE_MIN) * noise(px, pz, SHORE_WAVE, 95);
            const [dx, dz] = [px - x, pz - z];
            const length = Math.hypot(dx, dz);
            return [[px, y, pz], [px + dx / length * out, y, pz + dz / length * out]];
        };
        for (let i = 0; i < SHORE_STEPS; i++) {
            const [[a, a1], [b, b1]] = [point(i), point(i + 1)];
            this.triangle(a, b, b1, colour, tile);
            this.triangle(a, b1, a1, colour, tile);
        }
    }

    // A hex top at height y, radius `radius`, around (x, z): one colour, or the middle's and one per corner (`rim`).
    hexTop(x, z, y, radius, colour, tile, rim) {
        for (let k = 0; k < 6; k++) {
            const [ax, az] = corners[k];
            const [bx, bz] = corners[(k + 1) % 6];
            const shades = rim ? [colour, rim[(k + 1) % 6], rim[k]] : colour;
            this.triangle([x, y, z], [x + bx * radius, y, z + bz * radius], [x + ax * radius, y, z + az * radius], shades, tile);
        }
    }

    // The mesh of the triangles written. `normals` false skips working them out for a flat-shaded material, which lights each
    // face by its own slope; it gets a plain upward normal instead (the shadow pass still reads one).
    build(material, normals = true) {
        const geometry = new BufferGeometry();
        geometry.setAttribute('position', new BufferAttribute(this.positions.slice(0, this.count * 9), 3));
        geometry.setAttribute('color', new BufferAttribute(this.colours.slice(0, this.count * 9), 3));
        if (normals) {
            geometry.computeVertexNormals();
        } else {
            const up = new Float32Array(this.count * 9);
            for (let i = 1; i < up.length; i += 3) {
                up[i] = 1;
            }
            geometry.setAttribute('normal', new BufferAttribute(up, 3));
        }
        const mesh = new Mesh(geometry, material);
        mesh.userData.faceTile = this.faceTile.slice(0, this.count);
        return mesh;
    }
}

// The colour of the ground at a point of a tile at height y: the tile's own on its plateau, blended at its rim with the tiles
// that meet there (each leaned towards its accent by the patches), sand on the slope down to a lake, and snow over the snowline.
function colourAt(tile, x, z, y, ground, look, weights) {
    const lean = patch(x, z);
    let colour = scaled(leaned([look(tile)], lean), weights.plateau);
    let beach = 0;
    const shore = tiles => tiles.some(other => other.shape === 'water') && tiles.some(other => other.shape !== 'water') ? 1 : 0;
    if (weights.corner > 0) {
        const around = ground.around(tile, weights.c);
        colour = added(colour, scaled(leaned(around.map(look), lean), weights.corner));
        beach += weights.corner * shore(around);
    }
    if (weights.middle > 0) {
        const beside = ground.beside(tile, weights.k);
        colour = added(colour, scaled(leaned(beside.map(look), lean), weights.middle));
        beach += weights.middle * shore(beside);
    }
    colour = scaled(colour, shadeAt(x, z));
    const paints = look(tile);
    if (beach > 0) {
        colour = mixed(colour, paints.beach, beach * BEACH);
    }
    const line = SNOW_LINE + (noise(x, z, 0.6, 96) - 0.5) * SNOW_RAGGED;
    if (y > line) {
        colour = mixed(colour, paints.snow, smooth(Math.min((y - line) / SNOW_BLEND, 1)));
    }
    return colour;
}

function scaled(colour, by) {
    return { r: colour.r * by, g: colour.g * by, b: colour.b * by };
}

function added(a, b) {
    return { r: a.r + b.r, g: a.g + b.g, b: a.b + b.b };
}

function mixed(a, b, t) {
    return { r: a.r + (b.r - a.r) * t, g: a.g + (b.g - a.g) * t, b: a.b + (b.b - a.b) * t };
}

// Many copies of one shape, each with its own place, scale, turn and colour.
function instanced(geometry, items, material = decoration) {
    const mesh = new InstancedMesh(geometry, material, Math.max(items.length, 1));
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

// How tall a tree stands for its width (its shapes squashed to this share of their height).
const TREE_HEIGHT = 0.8;

// A tree standing on (x, z): a cone of needles, or a round broadleaf crown on a taller trunk.
function tree(tile, x, z, top, scale, broadleaf, leaves, wood, out, k) {
    const shade = leaves.clone().multiplyScalar(0.85 + hash(tile.q, tile.r, 40 + k) * 0.25);
    const tall = scale * TREE_HEIGHT;
    if (broadleaf) {
        out.trunks.push({ x, y: top, z, sx: scale, sy: tall * 1.5, sz: scale, colour: wood });
        out.crowns.push({ x, y: top + 0.14 * tall, z, sx: scale, sy: tall * 0.9, sz: scale, turn: k, colour: shade.offsetHSL(0.02, 0, 0.03) });
    } else {
        out.trunks.push({ x, y: top, z, sx: scale, sy: tall, sz: scale, colour: wood });
        out.trees.push({ x, y: top + 0.12 * tall, z, sx: scale, sy: tall, sz: scale, turn: k, colour: shade });
    }
}

// Where a range's crag stands on a tile: its highest point round the shelf, if the range rises there well over the shelf.
function summits(tile, ground, out) {
    const level = tileLevel(tile) + ground.reliefIn(tile, tile.x, tile.z);
    const candidates = Array.from({ length: SUMMIT_TRIES }, (_, i) => {
        const angle = (i + hash(tile.q, tile.r, 520 + i) * 0.6) / SUMMIT_TRIES * Math.PI * 2;
        const distance = 0.5 + hash(tile.q, tile.r, 540 + i) * 0.18;
        const [x, z] = [tile.x + Math.cos(angle) * distance, tile.z + Math.sin(angle) * distance];
        return { x, z, angle, rise: ground.heightIn(tile, x, z) - level };
    }).filter(candidate => candidate.rise > SUMMIT_RISE && clear(out, tile, candidate.x, candidate.z));
    const highest = candidates.reduce((best, candidate) => (!best || candidate.rise > best.rise ? candidate : best), null);
    return highest ? [[highest.x, highest.z]] : [];
}

// The trees of every wood, planted over the whole map at once on a jittered grid rather than tile by tile, so neighbouring
// forest tiles make one wood with no gap at the hex line. A tree grows where the wood is dense enough (the forest's share,
// blended over the rims like the ground's colours) for the wavering line its edge follows, so the edge is ragged and a few
// trees stand out on the land beside it; never on a lake, a mountain or the sand, beside a river, on a building's spot or on a
// neighbour's shelf. Conifers and broadleaf grow in stands of their own that run on across tiles.
function plantWoods(scene, ground, palette, out) {
    const cells = new Set();
    for (const tile of scene.tiles) {
        if (tile.terrain && tile.shape === 'trees') {
            for (let i = Math.floor((tile.x - 1.2) / WOOD_CELL); i <= Math.floor((tile.x + 1.2) / WOOD_CELL); i++) {
                for (let j = Math.floor((tile.z - 1.2) / WOOD_CELL); j <= Math.floor((tile.z + 1.2) / WOOD_CELL); j++) {
                    cells.add(`${i},${j}`);
                }
            }
        }
    }
    const share = tiles => tiles.filter(tile => tile.shape === 'trees').length / tiles.length;
    for (const cell of cells) {
        const [i, j] = cell.split(',').map(Number);
        const [x, z] = [(i + 0.15 + hash(i, j, 730) * 0.7) * WOOD_CELL, (j + 0.15 + hash(i, j, 731) * 0.7) * WOOD_CELL];
        const tile = ground.tileAt(x, z);
        if (!tile?.terrain || !WOODED.has(tile.shape)) {
            continue;
        }
        const weights = weightsAt(x - tile.x, z - tile.z);
        const density = ground.blend(tile, weights, tile.shape === 'trees' ? 1 : 0, share, share);
        const own = tile.shape === 'trees';
        if (density < WOOD_EDGE + noise(x, z, 0.9, 732) * WOOD_EDGE_WANDER || weights.reach < (own ? CLEARING : SHELF)
            || !clear(out, tile, x, z) || ground.riverDistance(x, z) < RIVER_ROOM) {
            continue;
        }
        const leaves = palette.faded('--gv-terrain-forest', tile);
        const wood = palette.faded('--gv-board3d-trunk', tile);
        const scale = 0.45 + density * 0.25 + hash(i, j, 734) * 0.2;
        tree(tile, x, z, ground.heightIn(tile, x, z) - FACET_DIP, scale, noise(x, z, 2.6, 733) > 0.55, leaves, wood, out, i * 7 + j);
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
    // Forest: its trees are planted over the whole wood at once (plantWoods); a tile adds only grass under them.
    trees(tile, top, palette, out) {
        grass(tile, top, palette, out, 2, 310);
    },
    // Hills: the downs' swells are in the ground (ground.js); a tile adds a rocky outcrop or a tree on some.
    hills(tile, top, palette, out) {
        const kind = variant(tile, 3);
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
    // Mountains: the range itself is in the ground (ground.js); a crag (mountains.js) stands on its highest point round the
    // tile's shelf where it rises well over it, snow-capped when high enough. A lone mountain, which no range lifts, keeps a
    // massif of its own: one with a lower one, a group of three, or one great massif.
    peaks(tile, top, palette, out) {
        const rock = palette.terrain(tile);
        const snow = palette.faded('--gv-board3d-snow', tile);
        const ground = out.shape;
        let peaks;
        if (ground.touches(tile, 'peaks')) {
            peaks = summits(tile, ground, out).map(([x, z], k) => [x, z, 0.42 + hash(tile.q, tile.r, 56 + k) * 0.18]);
        } else {
            const kind = variant(tile, 3);
            peaks = kind === 0 ? [[tile.x - 0.08, tile.z - 0.05, 1], [...spot(tile, 1, 0.38), 0.6]]
                : kind === 1 ? [[...spot(tile, 0, 0.35), 0.75], [...spot(tile, 1, 0.4), 0.6], [...spot(tile, 2, 0.35), 0.85]]
                    : [[tile.x, tile.z, 1.15]];
        }
        peaks.filter(([x, z]) => clear(out, tile, x, z)).forEach(([x, z, s], k) => {
            const height = s * (0.8 + hash(tile.q, tile.r, 60 + k) * 0.3);
            const stretch = 0.85 + hash(tile.q, tile.r, 64 + k) * 0.3;
            const place = { x, y: top - 0.04, z, sx: s * stretch, sy: height, sz: s / stretch, turn: hash(tile.q, tile.r, 68 + k) * Math.PI * 2 };
            out.peaks.push({ ...place, colour: rock });
            if (ground.heightIn(tile, x, z) + height > CAP_LINE) {
                out.caps.push({ ...place, colour: snow });
            }
        });
        stones(tile, top, palette, out, 2, 620);
    },
    // Desert: the dunes are in the ground (ground.js); a tile adds a few cacti or stones on some.
    dunes(tile, top, palette, out) {
        const kind = variant(tile, 3);
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
        const surface = palette.terrain(tile);
        // Lighter in the shallows, where the shore's slope comes up under the water: at the corners the land takes part in.
        const shallow = surface.clone().offsetHSL(0, -0.04, SHALLOWS);
        const land = k => out.shape.around(tile, k).some(other => other.shape !== 'water');
        const rim = corners.map((_, k) => land(k) ? shallow : surface);
        out.water.hexTop(tile.x, tile.z, WATER_LEVEL + WATER_LIFT, TILE_RADIUS, surface, out.index, rim);
        // Past each shore the water runs on a little, in a wavy strip, over the land's slope, which dips under it there.
        for (let k = 0; k < 6; k++) {
            if (out.shape.joined(tile, k) && out.shape.neighbour(tile, k).shape !== 'water') {
                // An end of the strip reaches out only where the land goes on beside it (not another lake, the mist or the map's edge).
                const shore = j => out.shape.joined(tile, j) && out.shape.neighbour(tile, j).shape !== 'water';
                out.water.skirt(tile.x, tile.z, WATER_LEVEL + WATER_LIFT, k, [shore((k + 5) % 6), shore((k + 1) % 6)], shallow, out.index);
            }
        }
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
                return { x, y: WATER_LEVEL + WATER_LIFT + 0.004, z, sx: s, sy: 1, sz: s, turn: k, colour };
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

// The lists of dressing a tile adds to.
const dressing = {
    trees: 0, crowns: 0, trunks: 0, hills: 0, peaks: 0, caps: 0, rocks: 0, tufts: 0, flowers: 0, cacti: 0, pads: 0, reeds: 0,
};

// Lowers (or raises) what a tile's decorators placed past its plateau onto the slope under it, so nothing floats over a hillside
// or sinks into a rise. Water dressing (reeds, pads) floats and stays.
function settle(out, first, ground, tile) {
    const level = tileLevel(tile);
    for (const name of Object.keys(dressing)) {
        if (name === 'pads' || name === 'reeds') {
            continue;
        }
        const list = out[name];
        for (let i = first[name]; i < list.length; i++) {
            list[i].y += ground.heightAt(list[i].x, list[i].z, level, level) - level;
        }
    }
}

// Builds the ground of a scene. Returns the group to add and the mesh to pick tiles on (`userData.faceTile`: tile per face).
export function buildTerrain(scene, palette) {
    // A smooth tile takes 72 triangles, a rough one 216 (fewer than half are rough on most maps).
    const ground = new MeshBuilder(scene.tiles.length * 128);
    const features = new Map((scene.features ?? []).map(feature => [`${feature.q},${feature.r}`, feature]));
    const out = {
        water: new MeshBuilder(), trees: [], crowns: [], trunks: [], hills: [], peaks: [], caps: [], rocks: [], tufts: [], flowers: [],
        cacti: [], pads: [], reeds: [], index: 0, features,
    };

    const shape = groundOf(scene);
    const shared = new Map();
    const looks = new Map();
    const look = tile => {
        let colours = looks.get(tile);
        if (!colours) {
            const colour = tile.shape === 'water' ? palette.faded('--gv-board3d-seabed', tile) : palette.terrain(tile);
            colours = {
                colour,
                accent: accentOf(tile, colour),
                beach: palette.faded('--gv-board3d-beach', tile),
                snow: palette.faded('--gv-board3d-snow', tile),
            };
            looks.set(tile, colours);
        }
        return colours;
    };
    out.shape = shape;
    scene.tiles.forEach((tile, index) => {
        out.index = index;
        ground.landTop(tile, shape, look, shared, index);
        if (!tile.terrain) {
            return;
        }
        const first = Object.fromEntries(Object.keys(dressing).map(name => [name, out[name].length]));
        (decorators[tile.shape] ?? decorators.flat)(tile, tileTop(tile), palette, out);
        settle(out, first, shape, tile);
    });
    plantWoods(scene, shape, palette, out);

    const group = new Group();
    const pick = ground.build(solid, false);
    pick.receiveShadow = true;
    pick.castShadow = true;
    group.add(pick);
    if (out.water.count) {
        const surface = out.water.build(water);
        surface.receiveShadow = true;
        group.add(surface);
    }
    const fog = buildMist(scene, palette.token('--gv-board3d-mist'));
    if (fog) {
        group.add(fog);
    }
    const shapes = [
        [foliage, out.trees], [crown, out.crowns], [trunk, out.trunks], [hump, out.hills], [massif, out.peaks, solid], [snowfield, out.caps, solid],
        [rock, out.rocks], [tuft, out.tufts], [flower, out.flowers], [cactus, out.cacti], [pad, out.pads], [reed, out.reeds],
    ];
    for (const [geometry, items, material] of shapes) {
        if (items.length) {
            group.add(instanced(geometry, items, material));
        }
    }
    return { group, pick };
}

// Frees the shared shapes and materials when a board goes away. Three.js keeps a reference to the renderer in every shape
// and material it drew, so keeping them would keep the old renderer alive; the next board simply uploads them again.
export function disposeShared() {
    for (const resource of [foliage, crown, trunk, hump, massif, snowfield, rock, tuft, flower, cactus, pad, reed, solid, decoration, water, mist]) {
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
