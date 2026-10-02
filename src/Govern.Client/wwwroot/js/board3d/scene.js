// One 3D board: the WebGL renderer in its host element, the light, the camera and the gestures (gestures.js). It draws on
// demand (after a scene, a camera move or a resize), and every frame only while an animation plays (motion.js).
import {
    DirectionalLight, Group, HemisphereLight, NeutralToneMapping, PCFShadowMap, Raycaster, Scene, Vector2, WebGLRenderer,
} from '../../lib/three/three.module.js';
import { CameraRig } from './camera.js';
import { Gestures } from './gestures.js';
import { Palette } from './palette.js';
import { Labels } from './labels.js';
import { buildBorders, buildOverlay, disposeShared as disposeBorders } from './overlays.js';
import { Motion } from './motion.js';
import { buildPieces, disposeShared as disposePieces, loadFigures } from './pieces.js';
import { buildFeatures, disposeShared as disposeFeatures } from './models.js';
import { buildRivers, disposeShared as disposeRivers } from './rivers.js';
import { disposeLayer } from './shapes.js';
import { buildTerrain, disposeShared as disposeGround, disposeTerrain } from './terrain.js';

// The layers drawn on the ground besides the terrain and the pieces, each built from its own part of the scene.
const builders = { rivers: buildRivers, features: buildFeatures, borders: buildBorders };

// What each layer is built from, as text to compare: a layer is built again only when its text changes. The terrain also reads
// the features (no trees under a building), and the features the tiles they stand on.
function layerKeys(scene) {
    const tiles = new Map(scene.tiles.map(tile => [`${tile.q},${tile.r}`, tile]));
    const under = (scene.features ?? []).map(feature => tiles.get(`${feature.q},${feature.r}`));
    return {
        terrain: JSON.stringify([scene.tiles, scene.features]),
        rivers: JSON.stringify([scene.rivers, scene.bridges]),
        features: JSON.stringify([scene.features, under]),
        borders: JSON.stringify(scene.borders),
        pieces: JSON.stringify(scene.pieces),
    };
}


export class Board3D {
    constructor(host, dotnet) {
        this.host = host;
        this.dotnet = dotnet;
        this.renderer = new WebGLRenderer({ antialias: true, alpha: true });
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        this.renderer.shadowMap.enabled = true;
        this.renderer.shadowMap.type = PCFShadowMap;
        this.renderer.toneMapping = NeutralToneMapping;
        this.canvas = this.renderer.domElement;
        this.canvas.className = 'board3d-canvas';
        host.append(this.canvas);

        this.rig = new CameraRig();
        this.world = new Scene();
        this.sky = new HemisphereLight();
        this.sun = new DirectionalLight();
        this.sun.castShadow = true;
        this.sun.shadow.mapSize.set(2048, 2048);
        this.sun.shadow.bias = -0.0005;
        this.world.add(this.sky, this.sun, this.sun.target);
        this.raycaster = new Raycaster();
        this.scene = null;
        this.terrain = null;
        this.layers = null;
        this.pieces = null;
        this.overlay = null;
        this.overlayData = null;
        this.labels = new Labels(host);
        this.drawn = {};
        this.keys = null;
        this.pieceByUnit = new Map();
        this.motion = new Motion(this);
        this.frame = 0;

        this.listen();
        this.resize();
        // Units show only their base until the baked characters arrive; then the pieces are built again.
        loadFigures().then(loaded => loaded && !this.disposed && this.rebuildPieces()).catch(error => console.error(error));
    }

    listen() {
        this.gestures = new Gestures(this);
        this.resizeObserver = new ResizeObserver(() => this.resize());
        this.resizeObserver.observe(this.host);
        this.theme = window.matchMedia('(prefers-color-scheme: dark)');
        this.onTheme = () => this.rebuild();
        this.theme.addEventListener('change', this.onTheme);
    }

    // Draws a new scene, building again only the layers whose part of the scene changed (a move usually changes the pieces
    // and the tiles coming into sight, not the rivers or the borders).
    setScene(scene) {
        const previous = this.scene;
        this.scene = scene;
        this.rig.setBounds(scene.map);
        this.pieceByUnit = new Map((scene.pieces ?? []).filter(piece => piece.unit != null).map(piece => [piece.unit, piece]));
        const sameMap = previous && JSON.stringify(previous.map) === JSON.stringify(scene.map);
        this.rebuild(sameMap ? 'changed' : 'all');
    }

    // Animates the scene (BoardAnimationService's motion); the scene itself stays as it is.
    setMotion(motion) {
        this.motion.set(motion, performance.now());
        this.requestRender();
    }

    // A unit's piece in the scene, by its id.
    pieceOf(unit) {
        return this.pieceByUnit.get(unit);
    }

    palette() {
        return new Palette(this.host);
    }

    // Builds the pieces again, with the units the motion moves as actors of their own, placed where they are drawn.
    rebuildPieces() {
        if (!this.layers || !this.scene) {
            return;
        }
        this.replaceLayer('pieces', this.palette());
        this.requestRender();
    }

    // Draws the player's current choice (lens, highlights, selection) over the scene; the map stays as it is.
    setOverlay(overlay) {
        this.overlayData = overlay;
        this.labels.setOverlay(overlay);
        this.drawOverlay(new Palette(this.host));
        this.requestRender();
    }

    drawOverlay(palette) {
        if (this.overlay) {
            this.world.remove(this.overlay);
            disposeLayer(this.overlay);
        }
        this.overlay = buildOverlay(this.overlayData, palette);
        if (this.overlay) {
            this.world.add(this.overlay);
        }
    }

    // Builds the layers again: 'all' (a new map, a theme change, the baked figures arriving) or only those whose part of the
    // scene changed since they were built. Records the time each layer took, for the bench.
    rebuild(which = 'all') {
        if (!this.scene) {
            return;
        }
        const began = performance.now();
        const keys = layerKeys(this.scene);
        const all = which === 'all' || !this.layers;
        const changed = Object.keys(keys).filter(name => all || keys[name] !== this.keys?.[name]);
        this.keys = keys;
        const keyed = performance.now() - began;
        const palette = new Palette(this.host);
        if (all) {
            this.light(palette);
            this.clear();
            this.layers = new Group();
            this.world.add(this.layers);
        }
        const timings = { keys: keyed };
        // The banners first, so the motion re-applied with the new pieces moves the new banners.
        if (changed.includes('pieces')) {
            this.labels.setPieces(this.scene.pieces);
        }
        for (const name of changed) {
            const start = performance.now();
            this.replaceLayer(name, palette);
            timings[name] = performance.now() - start;
        }
        if (all) {
            this.drawOverlay(palette);
        }
        this.requestRender();
        this.built = { layers: timings, total: performance.now() - began };
    }

    // The sky and the sun in the theme's colours, the sun placed over the map.
    light(palette) {
        this.sky.color = palette.token('--gv-board3d-sky');
        this.sky.groundColor = palette.token('--gv-board3d-ground');
        this.sky.intensity = 1.6;
        this.sun.color = palette.token('--gv-board3d-sun');
        this.sun.intensity = 2.2;
        this.placeSun();
    }

    // Builds one layer of the scene again in place of the one drawn.
    replaceLayer(name, palette) {
        if (name === 'terrain') {
            if (this.terrain) {
                this.world.remove(this.terrain.group);
                disposeTerrain(this.terrain.group);
            }
            this.terrain = buildTerrain(this.scene, palette);
            this.world.add(this.terrain.group);
            return;
        }
        const old = name === 'pieces' ? this.pieces?.group : this.drawn[name];
        if (old) {
            this.layers.remove(old);
            disposeLayer(old);
        }
        if (name === 'pieces') {
            this.pieces = buildPieces(this.scene, palette, this.motion.moving);
            this.pieces.group.userData.layer = name;
            this.layers.add(this.pieces.group);
            this.motion.reapply();
            return;
        }
        const layer = builders[name](this.scene, palette);
        this.drawn[name] = layer;
        if (layer) {
            layer.userData.layer = name;
            this.layers.add(layer);
        }
    }

    // Removes and frees the ground and the layers on it.
    clear() {
        if (this.terrain) {
            this.world.remove(this.terrain.group);
            disposeTerrain(this.terrain.group);
            this.terrain = null;
        }
        if (this.layers) {
            this.world.remove(this.layers);
            disposeLayer(this.layers);
            this.layers = null;
            this.pieces = null;
            this.drawn = {};
        }
    }

    // The sun shines from the south-west, high, and its shadow covers the whole map.
    placeSun() {
        const { left, top, width, height } = this.scene.map;
        const [x, z] = [left + width / 2, top + height / 2];
        const reach = Math.max(width, height) / 2 + 2;
        this.sun.position.set(x - reach * 0.6, reach * 1.4, z + reach * 0.8);
        this.sun.target.position.set(x, 0, z);
        const shadow = this.sun.shadow.camera;
        shadow.left = shadow.bottom = -reach * 1.3;
        shadow.right = shadow.top = reach * 1.3;
        shadow.near = 0.1;
        shadow.far = reach * 4;
        shadow.updateProjectionMatrix();
    }

    fit(rect) {
        this.rig.fit(rect);
        this.requestRender();
    }

    focus(x, z) {
        this.rig.focus(x, z);
        this.requestRender();
    }

    zoom(factor) {
        this.rig.zoomAt({ x: 0, y: 0 }, factor);
        this.requestRender();
    }

    resize() {
        const width = Math.max(this.host.clientWidth, 1);
        const height = Math.max(this.host.clientHeight, 1);
        this.renderer.setSize(width, height, false);
        this.rig.setAspect(width / height);
        this.requestRender();
    }

    requestRender() {
        if (!this.frame) {
            this.frame = requestAnimationFrame(() => {
                this.frame = 0;
                const animating = this.motion.tick(performance.now());
                this.renderer.render(this.world, this.rig.camera);
                this.labels.place(this.rig.camera, this.canvas.clientWidth, this.canvas.clientHeight);
                if (animating) {
                    this.requestRender();
                }
            });
        }
    }

    // A click picks what is under the pointer (a piece, else the ground) and tells .NET which tile it is on.
    click(at) {
        if (!this.terrain || !this.scene) {
            return;
        }
        this.raycaster.setFromCamera(new Vector2(at.x, at.y), this.rig.camera);
        const hit = this.raycaster.intersectObjects([this.terrain.pick, ...(this.pieces?.pickable ?? [])], false)[0];
        if (!hit) {
            return;
        }
        const tile = hit.object.userData.tiles
            ? hit.object.userData.tiles[hit.instanceId]
            : this.scene.tiles[this.terrain.pick.userData.faceTile[hit.faceIndex]];
        this.dotnet.invokeMethodAsync('OnTileClicked', tile.q, tile.r);
    }

    dispose() {
        this.disposed = true;
        cancelAnimationFrame(this.frame);
        this.gestures.dispose();
        this.resizeObserver.disconnect();
        this.theme.removeEventListener('change', this.onTheme);
        this.motion.dispose();
        this.clear();
        if (this.overlay) {
            disposeLayer(this.overlay);
        }
        this.labels.dispose();
        for (const disposeShared of [disposeGround, disposeRivers, disposeFeatures, disposeBorders, disposePieces]) {
            disposeShared();
        }
        this.sun.dispose();
        this.renderer.dispose();
        this.renderer.forceContextLoss();
        this.canvas.remove();
    }
}
