// The shape of the ground as one continuous landscape: every tile keeps a level plateau in its middle (where pieces, features
// and most of its dressing stand) and slopes from there to its rim, where it meets its neighbours. Along a shared edge both tiles
// come to the same height, the lower of their two surfaces (the height a river on that edge lies at, SceneLayerBuilder), and at
// a corner all the tiles around it meet at the lowest of their surfaces, so hills and mountains rise out of the plain and no cliffs
// or seams are left between explored tiles. Where a lake takes part, the rim sinks a little under the water (SHORE_DEPTH), so
// the land runs down into the lake and the shoreline follows the slope instead of the hex line. An unexplored tile keeps out of it:
// it stays flat under the mist, and its known neighbours keep their own height on the edge they share with it.
// Built per scene and cached, so the terrain, the borders and the overlay drape over the same ground.

// The share of a tile's radius its level plateau covers; the band outside it is the slope.
export const PLATEAU = 0.65;
// The water's height over a lake (TerrainReliefs.WaterLevel), under every land terrain's relief height (a plateau's jitter
// may still take a low one a little under it, out of the water's reach).
export const WATER_LEVEL = 0.07;
// How far under the water a rim shared with a lake lies.
export const SHORE_DEPTH = 0.05;
const SQRT3 = Math.sqrt(3);

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
        ground = new Ground(scene.tiles);
        cache.set(scene.tiles, ground);
    }
    return ground;
}

// The height of a tile's surface without the per-tile jitter: the water over a lake, else its relief's height.
export function surface(tile) {
    return tile.shape === 'water' ? Math.max(tile.height, WATER_LEVEL) : tile.height;
}

class Ground {
    constructor(tiles) {
        this.tiles = new Map(tiles.map(tile => [key(tile.q, tile.r), tile]));
        // Each tile's six neighbours (undefined off the board), found once.
        this.neighbours = new Map(tiles.map(tile => [tile, STEPS.map(([dq, dr]) => this.tile(tile.q + dq, tile.r + dr))]));
        // Each tile's rim, worked out once: the heights of its six corners and six edge middles, and whether it touches water.
        this.rims = new Map(tiles.map(tile => [tile, {
            corners: [0, 1, 2, 3, 4, 5].map(k => rimHeight(this.around(tile, k))),
            edges: [0, 1, 2, 3, 4, 5].map(k => rimHeight(this.beside(tile, k))),
            wet: tile.shape === 'water' || [0, 1, 2, 3, 4, 5].some(k => this.joined(tile, k) && this.neighbour(tile, k).shape === 'water'),
        }]));
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

    // The height of the ground under a point, exactly as the terrain mesh draws it (`top` is the plateau's height, by default
    // the tile's own, jitter included; the facets' small dip is left out). Off the board, `fallback`.
    heightAt(x, z, top, fallback = 0) {
        const tile = this.tileAt(x, z);
        if (!tile) {
            return fallback;
        }
        const level = top ?? tileLevel(tile);
        const [dx, dz] = [x - tile.x, z - tile.z];
        // The edge the point faces (edge k is centred on 60k degrees), which side of its middle, and how far out it is.
        const degrees = (Math.atan2(dz, dx) * 180 / Math.PI + 360) % 360;
        const k = Math.round(degrees / 60) % 6;
        const off = ((degrees - 60 * k + 540) % 360) - 180;
        if (Math.hypot(dx, dz) * Math.cos(off * Math.PI / 180) <= PLATEAU * SQRT3 / 2) {
            return level;
        }
        // The slope's two triangles on that side, as terrain.js cuts them: corner side (plateau corner, plateau middle, rim
        // middle | plateau corner, rim middle, rim corner), or the other (plateau middle, plateau corner, rim corner | plateau
        // middle, rim corner, rim middle).
        const rim = this.rims.get(tile);
        const next = (k + 1) % 6;
        const [c, m] = off < 0 ? [corners[k], middleOf(k)] : [corners[next], middleOf(k)];
        const corner = { x: c[0], z: c[1], y: rim.corners[off < 0 ? k : next] };
        const middle = { x: m[0], z: m[1], y: rim.edges[k] };
        const flatCorner = { x: c[0] * PLATEAU, z: c[1] * PLATEAU, y: level };
        const flatMiddle = { x: m[0] * PLATEAU, z: m[1] * PLATEAU, y: level };
        const point = { x: dx, z: dz };
        const first = off < 0 ? [flatCorner, flatMiddle, middle] : [flatMiddle, flatCorner, corner];
        const second = off < 0 ? [flatCorner, middle, corner] : [flatMiddle, corner, middle];
        const inFirst = barycentric(point, first);
        return inFirst.every(weight => weight >= -1e-9) ? blend(inFirst, first) : blend(barycentric(point, second), second);
    }

    // Where things lie at (x, z): the ground, or the water over a lake (a tint, a border or a river floats on it).
    surfaceAt(x, z, fallback = 0) {
        const height = this.heightAt(x, z, undefined, fallback);
        const tile = this.tileAt(x, z);
        return tile && this.wet(tile) ? Math.max(height, WATER_LEVEL) : height;
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

function blend(weights, points) {
    return weights[0] * points[0].y + weights[1] * points[1].y + weights[2] * points[2].y;
}

// The height where tiles meet: the lowest of their surfaces, or under the water when one of them is a lake.
function rimHeight(tiles) {
    const lowest = Math.min(...tiles.map(surface));
    return tiles.some(tile => tile.shape === 'water') ? Math.min(lowest, WATER_LEVEL - SHORE_DEPTH) : lowest;
}

// A stable pseudo-random number in [0, 1) per tile and purpose, so a tile looks the same on every view.
export function hash(q, r, salt) {
    let h = Math.imul(q, 374761393) ^ Math.imul(r, 668265263) ^ Math.imul(salt, 2246822519);
    h = Math.imul(h ^ (h >>> 13), 1274126177);
    return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

// How far a tile's plateau may sit above or below its relief's height, so the ground isn't perfectly level.
export const TOP_JITTER = 0.02;

// The height of a tile's plateau: its relief's height, varied a little per tile.
export function tileLevel(tile) {
    return tile.height + (hash(tile.q, tile.r, 1) - 0.5) * 2 * TOP_JITTER;
}
