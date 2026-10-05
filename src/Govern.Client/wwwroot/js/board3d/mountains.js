// The mountain shape the terrain dresses its peaks with: a craggy massif rather than a cone. It rises in irregular rings
// (ridges reach out, gullies cut in, the summit leans off the middle) to a top, and a snowfield covers its upper part down to a
// ragged snowline that follows the rock. Built once in code from a fixed seed; each tile turns and stretches its own peaks, so
// no two look alike. The rock's faces are shaded by height (darker at the foot, lighter scree near the top) through vertex
// colours, which the instance colour (the terrain's or the snow's token) multiplies. The snow is cut from the rock's own faces
// and lifted off them a little, so it follows every ridge and gully and never dips under the rock.
import { BufferGeometry, Float32BufferAttribute } from '../../lib/three/three.module.js';
import { hash } from './ground.js';

// Around the massif, and its rings from the foot up: the height of each ring and its radius (at most; the rock varies under it).
const SIDES = 9;
const RINGS = [[-0.1, 0.5], [0.28, 0.4], [0.55, 0.26], [0.78, 0.13]];
const SUMMIT = [0.04, 1, -0.03];
// Where the snowline lies between the third and fourth ring (a share of the way up, varied per column), and how far the snow
// stands off the rock so the two never flicker.
const SNOWLINE = [0, 0.7];
const SNOW_LIFT = 0.008;

function random(i, j, salt) {
    return hash(i, j, 1300 + salt);
}

// The rings' points: each column at its own angle and reach, ridges out and gullies in.
function rings() {
    return RINGS.map(([y, radius], j) => Array.from({ length: SIDES }, (_, i) => {
        const angle = (i + (random(i, j, 1) - 0.5) * 0.5) / SIDES * Math.PI * 2;
        const reach = radius * (i % 2 === 0 ? 0.85 + random(i, j, 2) * 0.3 : 0.62 + random(i, j, 3) * 0.3);
        const lift = j === 0 ? 0 : (random(i, j, 4) - 0.5) * 0.08;
        return [Math.cos(angle) * reach, y + lift, Math.sin(angle) * reach];
    }));
}

function lerp(a, b, t) {
    return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
}

function minus(a, b) {
    return [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
}

function normalOf([a, b, c]) {
    const [u, v] = [minus(b, a), minus(c, a)];
    const n = [u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]];
    const length = Math.hypot(...n) || 1;
    return [n[0] / length, n[1] / length, n[2] / length];
}

// The rock's triangles: between the rings, quads cut on the diagonal from each column's upper point to the next column's lower
// one; above the last ring, a fan to the summit.
function rockFaces(layers) {
    const faces = [];
    for (let j = 0; j < layers.length; j++) {
        for (let i = 0; i < SIDES; i++) {
            const next = (i + 1) % SIDES;
            if (j === layers.length - 1) {
                faces.push([layers[j][i], SUMMIT, layers[j][next]]);
            } else {
                faces.push([layers[j][i], layers[j + 1][i], layers[j][next]]);
                faces.push([layers[j][next], layers[j + 1][i], layers[j + 1][next]]);
            }
        }
    }
    return faces;
}

// The snow's triangles, each lying on one of the rock's: from the snowline (`share` of the way up each column's edge between the
// third and fourth rings, and on the quads' diagonals between them) to the summit.
function snowFaces(layers, share) {
    const [low, high] = [layers[2], layers[3]];
    const faces = [];
    for (let i = 0; i < SIDES; i++) {
        const next = (i + 1) % SIDES;
        const [line, nextLine] = [lerp(low[i], high[i], share[i]), lerp(low[next], high[next], share[next])];
        // Where the snowline crosses the diagonal from this column's upper point to the next column's lower one.
        const cross = lerp(low[next], high[i], 1 - (1 - share[i]) * (1 - share[next]));
        faces.push([line, high[i], cross], [cross, high[i], high[next]], [cross, high[next], nextLine]);
        faces.push([high[i], SUMMIT, high[next]]);
    }
    return faces;
}

// Moves each point of a shape off it by `lift`, along the shape's averaged normal there, so its faces stay joined.
function lifted(faces, lift) {
    const normals = new Map();
    const keyOf = point => point.map(value => Math.round(value * 1e5)).join();
    for (const face of faces) {
        const n = normalOf(face);
        for (const point of face) {
            const sum = normals.get(keyOf(point)) ?? [0, 0, 0];
            normals.set(keyOf(point), [sum[0] + n[0], sum[1] + n[1], sum[2] + n[2]]);
        }
    }
    return faces.map(face => face.map(point => {
        const n = normals.get(keyOf(point));
        const length = Math.hypot(...n) || 1;
        return [point[0] + n[0] / length * lift, point[1] + n[1] / length * lift, point[2] + n[2] / length * lift];
    }));
}

// A flat-shaded shape from its triangles; `shade(y, k)` colours the k-th face.
function shapeOf(faces, shade) {
    const positions = [];
    const colours = [];
    faces.forEach(([a, b, c], k) => {
        positions.push(...a, ...b, ...c);
        const value = shade((a[1] + b[1] + c[1]) / 3, k);
        colours.push(value, value, value, value, value, value, value, value, value);
    });
    const geometry = new BufferGeometry();
    geometry.setAttribute('position', new Float32BufferAttribute(positions, 3));
    geometry.setAttribute('color', new Float32BufferAttribute(colours, 3));
    geometry.computeVertexNormals();
    return geometry;
}

const rockRings = rings();

// The rock: darker at the foot, lighter towards the top, each face a little different.
export const massif = shapeOf(rockFaces(rockRings), (y, k) => 0.72 + y * 0.3 + (random(k, 0, 5) - 0.5) * 0.12);

// The snow: from a ragged line between the third and fourth rings up to the summit, lying just off the rock.
const snowline = Array.from({ length: SIDES }, (_, i) => SNOWLINE[0] + random(i, 9, 6) * (SNOWLINE[1] - SNOWLINE[0]));
export const snowfield = shapeOf(lifted(snowFaces(rockRings, snowline), SNOW_LIFT), (y, k) => 0.98 + (random(k, 1, 7) - 0.5) * 0.06);
