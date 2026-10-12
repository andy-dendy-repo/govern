// The shape of the ground as one continuous landscape: every tile keeps a level plateau in its middle (where pieces, features
// and most of its dressing stand) and slopes from there to its rim, where it meets its neighbours. Along a shared edge both tiles
// come to the same height, the mean of their two plateaus, and at a corner all the tiles around it meet at the mean of theirs,
// so a higher tile's slope runs out into its lower neighbour's instead of stepping down at the hex line, and no cliffs or seams
// are left between explored tiles. Where a lake takes part, the rim sinks a little under the water (SHORE_DEPTH), so
// the land runs down into the lake and the shoreline follows the slope instead of the hex line. An unexplored tile keeps out of it:
// it stays flat under the mist, and its known neighbours keep their own height on the edge they share with it.
// Over that base, terrain regions add relief of their own as fields over the whole map rather than shapes per tile, so a range,
// a stretch of downs or a sand sea runs on from tile to tile: mountains rise in ridged crests (and the hills beside them into
// foothills), hills roll, and dunes run in long crests across the wind. Each region's strength is the tile's own on its plateau
// and, on the rim, the mean of the tiles that meet there, so the relief runs out over the slope of a neighbouring terrain
// (nothing along the mist or the map's edge) and stays seamless where the region goes on; tiles deep inside a region rise higher
// than those at its edge.
// A level shelf round each tile's middle keeps the relief it has there (units, cities and improvements stand on it, lifted by
// it: reliefAt), and a river's edge cuts a gorge through it.
// Built per scene and cached, so the terrain, the rivers, the borders and the overlay drape over the same ground.

// The share of a tile's radius its level plateau covers; the band outside it is the slope.
export const PLATEAU = 0.65;
// The water's height over a lake (TerrainReliefs.WaterLevel), under every land terrain's relief height (a plateau's jitter
// may still take a low one a little under it, out of the water's reach).
export const WATER_LEVEL = 0.07;
// How far under the water a rim shared with a lake lies.
export const SHORE_DEPTH = 0.05;
const SQRT3 = Math.sqrt(3);
// Points along each edge of a tile's rim (every tile alike, so neighbours share them); a tile a region's relief rises on is cut
// into this many rings (terrain.js), and the overlay's tints follow the same rings.
export const RIM_STEPS = 6;
// The level shelf round a tile's middle (map units from it), where the regions' relief stays as it is at the middle, and how far
// out it has turned into the relief at each point.
export const SHELF = 0.32;
const SHELF_FULL = 0.62;
// A river's gorge: no relief within GORGE_FLOOR of its edge, full relief from GORGE_WIDTH (map units).
const GORGE_FLOOR = 0.1;
const GORGE_WIDTH = 0.42;
// The share of the range's relief a hill takes when it touches a mountain: the range's foothills.
const FOOTHILLS = 0.35;
// The direction the dunes' crests face (radians), the same over the whole map, and their spacing (map units).
const WIND = 0.55;
const DUNE_SPACING = 0.7;

// The corners of a hex around its centre: pointy top, corner k at 60k − 30 degrees, as in HexLayout. Edge k runs from corner k
// to corner k + 1, and the neighbour across it lies at 60k degrees.
export const corners = Array.from({ length: 6 }, (_, k) => {
    const angle = Math.PI / 180 * (60 * k - 30);
    return [Math.cos(angle), Math.sin(angle)];
});

const cache = new WeakMap();
// The axial steps to the neighbour across edges 0 to 5.
const STEPS = [[1, 0], [0, 1], [-1, 1], [-1, 0], [0, -1], [1, -1]];

// The ground of a scene (cached per tile list).
export function groundOf(scene) {
    let ground = cache.get(scene.tiles);
    if (!ground) {
        ground = new Ground(scene.tiles, scene.rivers ?? []);
        cache.set(scene.tiles, ground);
    }
    return ground;
}

// The height of a tile's surface without the per-tile jitter: the water over a lake, else its relief's height.
export function surface(tile) {
    return tile.shape === 'water' ? Math.max(tile.height, WATER_LEVEL) : tile.height;
}

export function smooth(t) {
    return t * t * (3 - 2 * t);
}

function between(value, from, to) {
    return smooth(Math.min(Math.max((value - from) / (to - from), 0), 1));
}

// Smooth value noise in [0, 1) over the ground, one lattice value per `size` map units, so a pattern runs on across tiles.
export function noise(x, z, size, salt) {
    const fx = x / size;
    const fz = z / size;
    const i = Math.floor(fx);
    const j = Math.floor(fz);
    const u = smooth(fx - i);
    const v = smooth(fz - j);
    const a = hash(i, j, salt);
    const c = hash(i, j + 1, salt);
    const top = a + (hash(i + 1, j, salt) - a) * u;
    const bottom = c + (hash(i + 1, j + 1, salt) - c) * u;
    return top + (bottom - top) * v;
}

// Noise folded into crests: 1 along the lines where the noise crosses its middle, falling away on both sides.
function ridged(x, z, size, salt) {
    return 1 - Math.abs(2 * noise(x, z, size, salt) - 1);
}

// The terrain regions and their relief (map units over the base ground at full strength). `weight` is how much a tile takes
// part (0: not at all); `deep` scales it by how far the tile lies from the region's edge (1, 2, 3 or more tiles in).
const REGIONS = [
    {
        // Mountain ranges: two sizes of ridged crests over a bulk that lifts the whole range.
        weight: (tile, ground) => tile.shape === 'peaks' ? 1 : tile.shape === 'hills' && ground.touches(tile, 'peaks') ? FOOTHILLS : 0,
        deep: [0.75, 1, 1.2],
        relief: (x, z) => 0.95 * (0.18 + 0.82 * (0.65 * ridged(x, z, 1.6, 201) ** 2.5 + 0.35 * ridged(x, z, 0.7, 202) ** 1.5)),
    },
    {
        // Downs: soft rolling swells.
        weight: tile => tile.shape === 'hills' ? 1 : 0,
        deep: [0.85, 1, 1.1],
        relief: (x, z) => 0.17 * (0.1 + 0.9 * (0.65 * noise(x, z, 1.3, 211) + 0.35 * noise(x, z, 0.55, 212))),
    },
    {
        // A sand sea: long crests across the wind, a gentle windward slope and a steep lee, wandering and growing and shrinking.
        weight: tile => tile.shape === 'dunes' ? 1 : 0,
        deep: [0.85, 1, 1.1],
        relief: (x, z) => {
            const along = (x * Math.cos(WIND) + z * Math.sin(WIND)) / DUNE_SPACING + 0.7 * noise(x, z, 1.9, 221);
            const f = along - Math.floor(along);
            const crest = f < 0.72 ? smooth(f / 0.72) : smooth((1 - f) / 0.28);
            return 0.16 * crest * (0.45 + 0.55 * noise(x, z, 2.3, 222));
        },
    },
];

class Ground {
    constructor(tiles, rivers) {
        this.tiles = new Map(tiles.map(tile => [key(tile.q, tile.r), tile]));
        // Each tile's six neighbours (undefined off the board), found once.
        this.neighbours = new Map(tiles.map(tile => [tile, STEPS.map(([dq, dr]) => this.tile(tile.q + dq, tile.r + dr))]));
        // Each tile's rim, worked out once: the heights of its six corners and six edge middles, and whether it touches water.
        this.rims = new Map(tiles.map(tile => [tile, {
            corners: [0, 1, 2, 3, 4, 5].map(k => rimHeight(this.around(tile, k))),
            edges: [0, 1, 2, 3, 4, 5].map(k => rimHeight(this.beside(tile, k))),
            wet: tile.shape === 'water' || [0, 1, 2, 3, 4, 5].some(k => this.joined(tile, k) && this.neighbour(tile, k).shape === 'water'),
        }]));
        this.fields = this.regionFields(tiles);
        this.gorges = this.riverGorges(tiles, rivers);
        this.middles = new Map();
    }

    // Each tile's part in each region it belongs to or borders: its strength, and the strength at its rim's corners and edge
    // middles (the mean of the tiles that meet there, so the relief runs out over the slope of a neighbour outside the region;
    // none where the mist or the map's edge takes part).
    regionFields(tiles) {
        const fields = new Map();
        for (const region of REGIONS) {
            const weights = new Map(tiles.filter(tile => tile.terrain).map(tile => [tile, region.weight(tile, this)]));
            const strength = this.strengths(tiles, weights, region.deep);
            const of = tile => strength.get(tile) ?? 0;
            const mean = list => list.reduce((sum, tile) => sum + of(tile), 0) / list.length;
            // The region's tiles and the explored tiles beside them, whose slopes its relief runs out over.
            const reached = new Set(strength.keys());
            for (const tile of strength.keys()) {
                for (const other of this.neighbours.get(tile)) {
                    if (other?.terrain) {
                        reached.add(other);
                    }
                }
            }
            for (const tile of reached) {
                const corners = [0, 1, 2, 3, 4, 5].map(k => {
                    const around = this.around(tile, k);
                    return around.length < 3 ? 0 : mean(around);
                });
                const edges = [0, 1, 2, 3, 4, 5].map(k => this.joined(tile, k) ? mean(this.beside(tile, k)) : 0);
                const list = fields.get(tile) ?? [];
                list.push({ relief: region.relief, own: of(tile), corners, edges });
                fields.set(tile, list);
            }
        }
        return fields;
    }

    // The strength of each tile in a region: its weight, scaled by how many tiles in from the region's edge it lies.
    strengths(tiles, weights, deep) {
        const depth = new Map();
        let ring = [];
        for (const [tile, weight] of weights) {
            if (weight > 0 && this.neighbours.get(tile).some(other => !(weights.get(other) > 0))) {
                depth.set(tile, 1);
                ring.push(tile);
            }
        }
        for (let level = 2; ring.length; level++) {
            const next = [];
            for (const tile of ring) {
                for (const other of this.neighbours.get(tile)) {
                    if (weights.get(other) > 0 && !depth.has(other)) {
                        depth.set(other, level);
                        next.push(other);
                    }
                }
            }
            ring = next;
        }
        const strength = new Map();
        for (const [tile, level] of depth) {
            strength.set(tile, weights.get(tile) * deep[Math.min(level, deep.length) - 1]);
        }
        return strength;
    }

    // The river edges near each tile (its own and its neighbours'), as segments, for the gorges they cut.
    riverGorges(tiles, rivers) {
        const own = new Map();
        for (const river of rivers) {
            const [mx, mz] = [(river.x1 + river.x2) / 2, (river.z1 + river.z2) / 2];
            const [dx, dz] = [river.x2 - river.x1, river.z2 - river.z1];
            const length = Math.hypot(dx, dz) || 1;
            for (const side of [1, -1]) {
                const tile = this.tileAt(mx - dz / length * 0.3 * side, mz + dx / length * 0.3 * side);
                if (tile) {
                    own.set(tile, [...(own.get(tile) ?? []), river]);
                }
            }
        }
        const gorges = new Map();
        for (const tile of tiles) {
            const near = new Set(own.get(tile) ?? []);
            for (const other of this.neighbours.get(tile)) {
                for (const river of own.get(other) ?? []) {
                    near.add(river);
                }
            }
            if (near.size) {
                gorges.set(tile, [...near]);
            }
        }
        return gorges;
    }

    // Whether a tile has a neighbour of a shape.
    touches(tile, shape) {
        return this.neighbours.get(tile).some(other => other?.terrain && other.shape === shape);
    }

    // Whether a region's relief rises on a tile's own plateau (the terrain draws it finer; on a neighbour outside the region the
    // relief only runs out over the slope, which its rim's points carry).
    rough(tile) {
        return this.fields.get(tile)?.some(field => field.own > 0) ?? false;
    }

    tile(q, r) {
        return this.tiles.get(key(q, r));
    }

    // The tile across edge k, or undefined off the board.
    neighbour(tile, k) {
        return this.neighbours.get(tile)[k];
    }

    // Whether a tile's rim joins its neighbour's on edge k: both explored. Otherwise the edge is a step with a side wall.
    joined(tile, k) {
        const other = this.neighbour(tile, k);
        return Boolean(tile.terrain && other?.terrain);
    }

    // The tiles that meet at corner k of a tile and take part in its height: the tile and its explored neighbours there
    // (edges k − 1 and k), or the tile alone when it is unexplored.
    around(tile, k) {
        if (!tile.terrain) {
            return [tile];
        }
        return [tile, this.neighbour(tile, (k + 5) % 6), this.neighbour(tile, k)].filter(other => other?.terrain);
    }

    // The tiles sharing edge k of a tile that take part in its height.
    beside(tile, k) {
        return this.joined(tile, k) ? [tile, this.neighbour(tile, k)] : [tile];
    }

    cornerHeight(tile, k) {
        return this.rims.get(tile).corners[k];
    }

    edgeHeight(tile, k) {
        return this.rims.get(tile).edges[k];
    }

    // Whether a tile is a lake or touches one: the ground there may lie under the water.
    wet(tile) {
        return this.rims.get(tile).wet;
    }

    // The tile whose area holds a point (the nearest centre).
    tileAt(x, z) {
        const r = z / 1.5;
        const q = x / SQRT3 - r / 2;
        let [rq, rr, rs] = [Math.round(q), Math.round(r), Math.round(-q - r)];
        const [dq, dr, ds] = [Math.abs(rq - q), Math.abs(rr - r), Math.abs(rs + q + r)];
        if (dq > dr && dq > ds) {
            rq = -rr - rs;
        } else if (dr > ds) {
            rr = -rq - rs;
        }
        return this.tile(rq, rr);
    }

    // The height of the ground under a point (`top` is the plateau's height, by default the tile's own, jitter included). Off the
    // board, `fallback`. The terrain mesh draws exactly this at its vertices; between them it is flat, so on a tile a region rises
    // on the mesh may lie a few hundredths off it (and the facets' small dip is left out).
    heightAt(x, z, top, fallback = 0) {
        const tile = this.tileAt(x, z);
        return tile ? this.heightIn(tile, x, z, top) : fallback;
    }

    // The height of the ground at a point of a tile: its plateau, slope and rim, and the regions' relief over them (`weights`
    // from weightsAt, when already worked out).
    heightIn(tile, x, z, top, weights = weightsAt(x - tile.x, z - tile.z)) {
        const rim = this.rims.get(tile);
        const base = weights.plateau * (top ?? tileLevel(tile)) + weights.corner * rim.corners[weights.c] + weights.middle * rim.edges[weights.k];
        return base + this.reliefIn(tile, x, z, weights);
    }

    // The regions' relief at a point of a tile (`weights` from weightsAt). On the shelf round the tile's middle it is the relief at
    // the middle, so the shelf stays level (units, cities and improvements stand on it); from there out to SHELF_FULL it turns
    // into the relief at the point itself, which goes on seamlessly into the neighbours.
    reliefIn(tile, x, z, weights = weightsAt(x - tile.x, z - tile.z)) {
        const fields = this.fields.get(tile);
        if (!fields) {
            return 0;
        }
        const middle = this.middleRelief(tile, fields);
        const out = between(Math.hypot(x - tile.x, z - tile.z), SHELF, SHELF_FULL);
        return out === 0 ? middle : middle + (this.reliefOf(tile, fields, x, z, weights) - middle) * out;
    }

    // The relief at a point of the board (0 off it), as reliefIn.
    reliefAt(x, z) {
        const tile = this.tileAt(x, z);
        return tile ? this.reliefIn(tile, x, z) : 0;
    }

    // The relief at a tile's middle, worked out once.
    middleRelief(tile, fields) {
        let middle = this.middles.get(tile);
        if (middle === undefined) {
            middle = this.reliefOf(tile, fields, tile.x, tile.z, weightsAt(0, 0));
            this.middles.set(tile, middle);
        }
        return middle;
    }

    // Each region's strength at a point times its shape there, out of the gorge along a river.
    reliefOf(tile, fields, x, z, weights) {
        let relief = 0;
        for (const field of fields) {
            const strength = weights.plateau * field.own + weights.corner * field.corners[weights.c] + weights.middle * field.edges[weights.k];
            if (strength > 0) {
                relief += strength * field.relief(x, z);
            }
        }
        return relief === 0 ? 0 : relief * this.gorge(tile, x, z);
    }

    // How much of the relief a river near a point leaves: none on its edge, all from GORGE_WIDTH away.
    gorge(tile, x, z) {
        const rivers = this.gorges.get(tile);
        if (!rivers) {
            return 1;
        }
        let nearest = Infinity;
        for (const river of rivers) {
            nearest = Math.min(nearest, distanceToSegment(x, z, river.x1, river.z1, river.x2, river.z2));
        }
        return between(nearest, GORGE_FLOOR, GORGE_WIDTH);
    }

    // How close a point lies to a river edge (map units), or Infinity when none is near.
    riverDistance(x, z) {
        const tile = this.tileAt(x, z);
        let nearest = Infinity;
        for (const river of (tile && this.gorges.get(tile)) ?? []) {
            nearest = Math.min(nearest, distanceToSegment(x, z, river.x1, river.z1, river.x2, river.z2));
        }
        return nearest;
    }

    // A value over a tile blended the way its heights are: `own` on the plateau, `atCorner(tiles)` and `atEdge(tiles)` from the
    // tiles that meet at the rim's corners and edge middles (`weights` from weightsAt).
    blend(tile, weights, own, atCorner, atEdge) {
        let value = weights.plateau * own;
        if (weights.corner > 0) {
            value += weights.corner * atCorner(this.around(tile, weights.c));
        }
        if (weights.middle > 0) {
            value += weights.middle * atEdge(this.beside(tile, weights.k));
        }
        return value;
    }

    // Where things lie at (x, z): the ground, or the water over a lake (a tint, a border or a river floats on it).
    surfaceAt(x, z, fallback = 0) {
        const height = this.heightAt(x, z, undefined, fallback);
        const tile = this.tileAt(x, z);
        return tile && this.wet(tile) ? Math.max(height, WATER_LEVEL) : height;
    }
}

// How a point (dx, dz from a tile's centre) is made of the tile's plateau, one rim corner `c` and the middle of rim edge `k`,
// on the triangles terrain.js cuts the slope into (corner side: plateau corner, plateau middle, rim middle | plateau corner, rim
// middle, rim corner; or the other: plateau middle, plateau corner, rim corner | plateau middle, rim corner, rim middle), and
// how far out it lies (`reach`: 0 in the middle, 1 on the rim).
export function weightsAt(dx, dz) {
    // The edge the point faces (edge k is centred on 60k degrees), which side of its middle, and how far out it is.
    const degrees = (Math.atan2(dz, dx) * 180 / Math.PI + 360) % 360;
    const k = Math.round(degrees / 60) % 6;
    const off = ((degrees - 60 * k + 540) % 360) - 180;
    const reach = Math.hypot(dx, dz) * Math.cos(off * Math.PI / 180) / (SQRT3 / 2);
    const c = off < 0 ? k : (k + 1) % 6;
    if (reach <= PLATEAU) {
        return { plateau: 1, corner: 0, middle: 0, c, k, reach };
    }
    const [cx, cz] = corners[c];
    const [mx, mz] = middleOf(k);
    const flatCorner = { x: cx * PLATEAU, z: cz * PLATEAU, role: 0 };
    const flatMiddle = { x: mx * PLATEAU, z: mz * PLATEAU, role: 0 };
    const corner = { x: cx, z: cz, role: 1 };
    const middle = { x: mx, z: mz, role: 2 };
    const point = { x: dx, z: dz };
    const first = off < 0 ? [flatCorner, flatMiddle, middle] : [flatMiddle, flatCorner, corner];
    const second = off < 0 ? [flatCorner, middle, corner] : [flatMiddle, corner, middle];
    let triangle = first;
    let weights = barycentric(point, first);
    if (!weights.every(weight => weight >= -1e-9)) {
        triangle = second;
        weights = barycentric(point, second);
    }
    const shares = [0, 0, 0];
    triangle.forEach((vertex, i) => {
        shares[vertex.role] += weights[i];
    });
    return { plateau: shares[0], corner: shares[1], middle: shares[2], c, k, reach };
}

// The distance from a point to a segment.
function distanceToSegment(x, z, x1, z1, x2, z2) {
    const [dx, dz] = [x2 - x1, z2 - z1];
    const t = Math.min(Math.max(((x - x1) * dx + (z - z1) * dz) / (dx * dx + dz * dz || 1), 0), 1);
    return Math.hypot(x - x1 - dx * t, z - z1 - dz * t);
}

// The points of a hexagonal ring round (x, z) at `share` of the radius, `steps` along each side, from corner 0 round.
export function ring(x, z, share, steps) {
    const points = [];
    for (let k = 0; k < 6; k++) {
        const [ax, az] = corners[k];
        const [bx, bz] = corners[(k + 1) % 6];
        for (let s = 0; s < steps; s++) {
            const t = s / steps;
            points.push([x + (ax + (bx - ax) * t) * share, z + (az + (bz - az) * t) * share]);
        }
    }
    return points;
}

// Triangles from a middle point to a ring round it.
export function fan(centre, points, face) {
    points.forEach((point, i) => face(centre, points[(i + 1) % points.length], point));
}

// Triangles between two rings round the same middle (both from corner 0 round), walking both and always stepping on the one
// that is behind, so rings of any two sizes join without gaps.
export function stitch(inner, outer, face) {
    let [i, j] = [0, 0];
    while (i < inner.length || j < outer.length) {
        const a = inner[i % inner.length];
        const b = outer[j % outer.length];
        if (j >= outer.length || (i < inner.length && (i + 1) / inner.length <= (j + 1) / outer.length)) {
            face(a, inner[(i + 1) % inner.length], b);
            i++;
        } else {
            face(a, outer[(j + 1) % outer.length], b);
            j++;
        }
    }
}

// A number per axial coordinate (maps are at most a few dozen tiles across), quicker to look up than text.
function key(q, r) {
    return (q + 1024) * 4096 + r + 1024;
}

// The middle of edge k of a hex of radius 1 around the origin.
function middleOf(k) {
    const [a, b] = [corners[k], corners[(k + 1) % 6]];
    return [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2];
}

// The weights of a point between a triangle's three corners (all ≥ 0 inside it).
function barycentric(p, [a, b, c]) {
    const det = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
    const wa = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.z - c.z)) / det;
    const wb = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.z - c.z)) / det;
    return [wa, wb, 1 - wa - wb];
}

// The height where tiles meet: the mean of their plateaus, so a higher tile's slope runs out into a lower neighbour's instead
// of stepping down at the hex line, and neighbours of one terrain meet level; under the water when one of them is a lake.
function rimHeight(tiles) {
    if (tiles.some(tile => tile.shape === 'water')) {
        return Math.min(...tiles.map(surface), WATER_LEVEL - SHORE_DEPTH);
    }
    return tiles.reduce((sum, tile) => sum + tileLevel(tile), 0) / tiles.length;
}

// A stable pseudo-random number in [0, 1) per tile and purpose, so a tile looks the same on every view.
export function hash(q, r, salt) {
    let h = Math.imul(q, 374761393) ^ Math.imul(r, 668265263) ^ Math.imul(salt, 2246822519);
    h = Math.imul(h ^ (h >>> 13), 1274126177);
    return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

// How far a tile's plateau may sit above or below its relief's height, so the ground isn't perfectly level.
export const TOP_JITTER = 0.02;

// The terrain shapes a region's relief rises on (REGIONS); their plateaus stay level with each other, so a region has no creases
// at its tiles' rims and its relief alone shapes it.
const REGION_SHAPES = new Set(['peaks', 'hills', 'dunes']);

// The height of a tile's plateau: its relief's height, varied a little per tile outside the regions.
export function tileLevel(tile) {
    return REGION_SHAPES.has(tile.shape) ? tile.height : tile.height + (hash(tile.q, tile.r, 1) - 0.5) * 2 * TOP_JITTER;
}
