// Bakes the CC0 KayKit Adventurers characters into the 3D board's unit models (see README.md): each unit is posed once from
// one of the pack's animations, its texture is read into vertex colours, and the result is written as a small static mesh
// (no skeleton, no animation, no texture) to src/Govern.Client/wwwroot/models/units/<model>.json.
import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { NodeIO } from '@gltf-transform/core';
import { getPixels } from 'ndarray-pixels';
import { AnimationMixer, Box3, Color, Vector3 } from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

const SOURCE = 'https://raw.githubusercontent.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0/main/addons/kaykit_character_pack_adventures/Characters/gltf';
const CACHE = new URL('./cache/', import.meta.url);
// The files the committed models were baked from; a different download stops the bake instead of changing the models.
const SHA256 = {
    Barbarian: 'cefc311a0e10c7858b6141f5ada7e33268727564fb8ac1347aab97d000669cc6',
    Knight: '60428e3abc09ba83e595d256e3af8c5c976b46cdae599f0802fc82b4a3445168',
    Mage: 'cf898585da33fab50c724d31605fb931eb2912e6d2280092141e98ca81ad507d',
    Rogue: 'e825437cd4d2ee9c1960b517a74a69101e33eb409ae7fa8cedc7134a998fbb7d',
};
const OUTPUT = new URL('../../src/Govern.Client/wwwroot/models/units/', import.meta.url);

// Per PieceModel: the character, the animation it stands in, the accessories it keeps (its own body parts always stay), the
// accessories it drops, the parts drawn in the owner's seat colour, and an optional spear made from its sword.
export const units = {
    soldier: { character: 'Knight', clip: 'Idle', keep: ['1H_Sword', 'Round_Shield'], seat: ['Round_Shield', 'Knight_Cape'] },
    spearman: { character: 'Knight', clip: 'Idle', keep: ['Rectangle_Shield'], seat: ['Rectangle_Shield', 'Knight_Cape'], spear: true },
    archer: { character: 'Rogue', clip: 'Idle', keep: ['1H_Crossbow'], seat: ['Rogue_Cape'] },
    builder: { character: 'Barbarian', clip: 'Idle', keep: ['1H_Axe'], drop: ['Barbarian_Round_Shield'], seat: ['Barbarian_Cape'] },
    settler: { character: 'Mage', clip: 'Idle', keep: ['2H_Staff'], seat: ['Mage_Hat', 'Mage_Cape'] },
    rider: { character: 'Knight', clip: 'Sit_Chair_Idle', keep: ['1H_Sword'], seat: ['Knight_Cape'] },
};

// The moment of the animation the pose is taken at, in seconds.
const POSE_AT = 0.4;

async function download(character) {
    const file = new URL(`${character}.glb`, CACHE);
    if (!existsSync(file)) {
        await mkdir(CACHE, { recursive: true });
        const response = await fetch(`${SOURCE}/${character}.glb`);
        if (!response.ok) {
            throw new Error(`${character}: ${response.status}`);
        }
        await writeFile(file, Buffer.from(await response.arrayBuffer()));
    }
    const bytes = await readFile(file);
    if (createHash('sha256').update(bytes).digest('hex') !== SHA256[character]) {
        throw new Error(`${character}.glb is not the file the models were baked from; delete cache/ or update SHA256`);
    }
    return bytes;
}

// Reads each primitive's texture at its texture coordinates into a COLOR_0 attribute and drops the textures, so the model
// loads without images.
async function textureToColours(bytes) {
    const io = new NodeIO();
    const document = await io.readBinary(new Uint8Array(bytes));
    const atlases = new Map();
    for (const mesh of document.getRoot().listMeshes()) {
        for (const primitive of mesh.listPrimitives()) {
            const texture = primitive.getMaterial()?.getBaseColorTexture();
            const uv = primitive.getAttribute('TEXCOORD_0');
            if (!texture || !uv) {
                continue;
            }
            if (!atlases.has(texture)) {
                atlases.set(texture, await getPixels(texture.getImage(), texture.getMimeType()));
            }
            const pixels = atlases.get(texture);
            const [width, height] = pixels.shape;
            const colours = new Float32Array(uv.getCount() * 3);
            const colour = new Color();
            for (let i = 0; i < uv.getCount(); i++) {
                const [u, v] = uv.getElement(i, []);
                const x = Math.min(width - 1, Math.max(0, Math.floor((u - Math.floor(u)) * width)));
                const y = Math.min(height - 1, Math.max(0, Math.floor((v - Math.floor(v)) * height)));
                colour.setRGB(pixels.get(x, y, 0) / 255, pixels.get(x, y, 1) / 255, pixels.get(x, y, 2) / 255, 'srgb');
                colours.set([colour.r, colour.g, colour.b], i * 3);
            }
            primitive.setAttribute('COLOR_0', document.createAccessor().setType('VEC3').setArray(colours).setBuffer(uv.getBuffer()));
        }
    }
    for (const texture of document.getRoot().listTextures()) {
        texture.dispose();
    }
    return io.writeBinary(document);
}

function parse(bytes) {
    const buffer = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
    return new Promise((resolve, reject) => new GLTFLoader().parse(buffer, '', resolve, reject));
}

// The posed, world-space triangles of one mesh: positions and colours, three vertices per triangle.
function triangles(mesh, grey) {
    const geometry = mesh.geometry;
    const index = geometry.index;
    const count = index ? index.count : geometry.attributes.position.count;
    const colours = geometry.attributes.color;
    const positions = [];
    const tints = [];
    const point = new Vector3();
    for (let k = 0; k < count; k++) {
        const i = index ? index.getX(k) : k;
        mesh.getVertexPosition(i, point).applyMatrix4(mesh.matrixWorld);
        positions.push(point.x, point.y, point.z);
        const [r, g, b] = colours ? [colours.getX(i), colours.getY(i), colours.getZ(i)] : [1, 1, 1];
        // Seat parts keep only their light and shade; the board multiplies them by the seat colour.
        const shade = Math.min(1, (0.2126 * r + 0.7152 * g + 0.0722 * b) * 2.2);
        tints.push(...(grey ? [shade, shade, shade] : [r, g, b]));
    }
    return { positions, tints };
}

// An upright spear held where the sword's grip is (its end nearest the body's axis), from just above the ground to well above
// the head, as triangles.
function spear(sword, body) {
    const { positions } = triangles(sword, false);
    const axis = body.getCenter(new Vector3());
    let grip = null;
    for (let i = 0; i < positions.length; i += 3) {
        const point = new Vector3().fromArray(positions, i);
        const reach = Math.hypot(point.x - axis.x, point.z - axis.z);
        if (!grip || reach < grip.reach) {
            grip = { point, reach };
        }
    }
    const height = body.max.y - body.min.y;
    const start = new Vector3(grip.point.x, body.min.y + height * 0.05, grip.point.z);
    const end = new Vector3(grip.point.x, body.min.y + height * 1.35, grip.point.z);
    return prism(start, end, height * 0.018, height * 0.04, height * 0.16);
}

// A six-sided shaft from start to end with a pointed head, in two colours (timber and iron), as triangles.
function prism(start, end, radius, headRadius, headLength) {
    const direction = end.clone().sub(start).normalize();
    const side = Math.abs(direction.y) < 0.9 ? new Vector3(0, 1, 0) : new Vector3(1, 0, 0);
    const u = side.clone().cross(direction).normalize();
    const v = direction.clone().cross(u).normalize();
    const ring = (centre, r) => Array.from({ length: 6 }, (_, k) => centre.clone()
        .addScaledVector(u, Math.cos(k * Math.PI / 3) * r).addScaledVector(v, Math.sin(k * Math.PI / 3) * r));
    const neck = end.clone().addScaledVector(direction, -headLength);
    const [bottom, top, head] = [ring(start, radius), ring(neck, radius), ring(neck, headRadius)];
    const timber = new Color(0.42, 0.24, 0.12);
    const iron = new Color(0.55, 0.58, 0.62);
    const positions = [];
    const tints = [];
    const add = (points, colour) => {
        for (const p of points) {
            positions.push(p.x, p.y, p.z);
            tints.push(colour.r, colour.g, colour.b);
        }
    };
    for (let k = 0; k < 6; k++) {
        const n = (k + 1) % 6;
        add([bottom[k], bottom[n], top[n], bottom[k], top[n], top[k]], timber);
        add([head[k], head[n], end], iron);
    }
    return { positions, tints };
}

function merge(parts) {
    return parts.reduce((all, part) => ({ positions: all.positions.concat(part.positions), tints: all.tints.concat(part.tints) }),
        { positions: [], tints: [] });
}

// Shares equal vertices, then rounds: positions to a thousandth of the model's height, colours to 1/255.
function compact({ positions, tints }) {
    const keys = new Map();
    const vertices = [];
    const colours = [];
    const indices = [];
    for (let i = 0; i < positions.length / 3; i++) {
        const p = positions.slice(i * 3, i * 3 + 3).map(value => Math.round(value * 1000) / 1000);
        const c = tints.slice(i * 3, i * 3 + 3).map(value => Math.round(value * 255));
        const key = `${p}|${c}`;
        if (!keys.has(key)) {
            keys.set(key, vertices.length / 3);
            vertices.push(...p);
            colours.push(...c);
        }
        indices.push(keys.get(key));
    }
    return { positions: vertices, colours, indices };
}

// The box around a character's own body (head, body, arms and legs, not what it wears or carries).
function bodyBox(meshes, character) {
    const box = new Box3();
    for (const mesh of meshes.filter(m => /_(Head|Body|ArmLeft|ArmRight|LegLeft|LegRight)$/.test(m.name) && m.name.startsWith(`${character}_`))) {
        const { positions } = triangles(mesh, false);
        for (let i = 0; i < positions.length; i += 3) {
            box.expandByPoint(new Vector3().fromArray(positions, i));
        }
    }
    return box;
}

// Moves the model so it stands on y = 0 with its body centred on the origin, sized by `scale`.
function normalise(parts, body, scale) {
    const floor = Math.min(...parts.flatMap(part => part.positions.filter((_, i) => i % 3 === 1)));
    const centre = body.getCenter(new Vector3());
    for (const part of parts) {
        for (let i = 0; i < part.positions.length; i += 3) {
            part.positions[i] = (part.positions[i] - centre.x) * scale;
            part.positions[i + 1] = (part.positions[i + 1] - floor) * scale;
            part.positions[i + 2] = (part.positions[i + 2] - centre.z) * scale;
        }
    }
}

// A character's meshes, posed at POSE_AT seconds into one of its animations.
async function pose(character, clipName) {
    const gltf = await parse(await textureToColours(await download(character)));
    const mixer = new AnimationMixer(gltf.scene);
    mixer.clipAction(gltf.animations.find(animation => animation.name === clipName)).play();
    mixer.update(POSE_AT);
    gltf.scene.updateMatrixWorld(true);
    const meshes = [];
    gltf.scene.traverse(object => object.isMesh && meshes.push(object));
    return meshes;
}

// The pack's characters share one rig, so every model is sized alike: the standing knight's body is one unit tall.
const reference = bodyBox(await pose('Knight', 'Idle'), 'Knight');
const scale = 1 / (reference.max.y - reference.min.y);

async function bake(name, unit) {
    const meshes = await pose(unit.character, unit.clip);
    const own = mesh => (mesh.name.startsWith(`${unit.character}_`) || unit.keep.includes(mesh.name)) && !unit.drop?.includes(mesh.name);
    const kept = meshes.filter(own);
    const body = bodyBox(meshes, unit.character);
    const plainParts = kept.filter(mesh => !unit.seat.includes(mesh.name)).map(mesh => triangles(mesh, false));
    const seatParts = kept.filter(mesh => unit.seat.includes(mesh.name)).map(mesh => triangles(mesh, true));
    if (unit.spear) {
        plainParts.push(spear(meshes.find(mesh => mesh.name === '1H_Sword'), body));
    }
    const parts = [merge(plainParts), merge(seatParts)];
    normalise(parts, body, scale);
    const [plain, seated] = parts.map(compact);
    await writeFile(new URL(`${name}.json`, OUTPUT), JSON.stringify({ source: `KayKit Adventurers ${unit.character} (CC0)`, body: plain, seat: seated }));
    console.log(`${name}: ${plain.indices.length / 3 + seated.indices.length / 3} triangles`);
}

await mkdir(OUTPUT, { recursive: true });
for (const [name, unit] of Object.entries(units)) {
    await bake(name, unit);
}
