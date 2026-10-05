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
// The water's height over a lake (TerrainReliefs.WaterLevel), under the surface of every land terrain.
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
        this.tiles = new Map(tiles.map(tile => [`${tile.q},${tile.r}`, tile]));
        // Each tile's six neighbours (undefined off the board), found once.
        this.neighbours = new Map(tiles.map(tile => [tile, STEPS.map(([dq, dr]) => this.tile(tile.q + dq, tile.r + dr))]));
    }

    tile(q, r) {
        return this.tiles.get(`${q},${r}`);
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
        return rimHeight(this.around(tile, k));
    }

    edgeHeight(tile, k) {
        return rimHeight(this.beside(tile, k));
    }

    // Whether a tile is a lake or touches one: the ground there may lie under the water.
    wet(tile) {
        return tile.shape === 'water' || [0, 1, 2, 3, 4, 5].some(k => this.joined(tile, k) && this.neighbour(tile, k).shape === 'water');
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

    // The height of the ground under a point, from the plateau out along the slope to the rim (`top` is the plateau's height,
    // by default the tile's own, jitter included). Off the board, `fallback`.
    heightAt(x, z, top, fallback = 0) {
        const tile = this.tileAt(x, z);
        if (!tile) {
            return fallback;
        }
        const level = top ?? tileLevel(tile);
        const [dx, dz] = [x - tile.x, z - tile.z];
        const distance = Math.hypot(dx, dz);
        if (distance < 1e-6) {
            return level;
        }
        // The edge the point faces (edge k is centred on 60k degrees) and how far out it is, as a share of the way to the rim.
        const degrees = (Math.atan2(dz, dx) * 180 / Math.PI + 360) % 360;
        const k = Math.round(degrees / 60) % 6;
        const off = degrees - 60 * k > 180 ? degrees - 60 * k - 360 : degrees - 60 * k;
        const share = distance * Math.cos(off * Math.PI / 180) / (SQRT3 / 2);
        if (share <= PLATEAU) {
            return level;
        }
        const along = Math.max(-1, Math.min(1, off / 30));
        const middle = this.edgeHeight(tile, k);
        const end = along < 0 ? this.cornerHeight(tile, k) : this.cornerHeight(tile, (k + 1) % 6);
        const rim = middle + (end - middle) * Math.abs(along);
        const t = Math.min(1, (share - PLATEAU) / (1 - PLATEAU));
        return level + (rim - level) * t;
    }

    // Where things lie at (x, z): the ground, or the water over a lake (a tint or a border floats on it).
    surfaceAt(x, z, fallback = 0) {
        const height = this.heightAt(x, z, undefined, fallback);
        const tile = this.tileAt(x, z);
        return tile && this.wet(tile) ? Math.max(height, WATER_LEVEL) : height;
    }
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

// How far a tile's plateau may sit above or below its relief's height, so the ground isn't perfectly level. Rivers and borders
// are lifted just above it (rivers.js, overlays.js) so they never flicker.
export const TOP_JITTER = 0.02;

// The height of a tile's plateau: its relief's height, varied a little per tile.
export function tileLevel(tile) {
    return tile.height + (hash(tile.q, tile.r, 1) - 0.5) * 2 * TOP_JITTER;
}
