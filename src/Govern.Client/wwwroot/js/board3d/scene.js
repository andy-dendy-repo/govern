// One 3D board: the WebGL renderer in its host element, the light, the camera, the gestures (gestures.js) and the keys that pan
// it (keys.js). It draws on demand (after a scene, a camera move or a resize), and every frame only while an animation plays
// (motion.js) or a pan key is held.
import {
    DirectionalLight, Group, HemisphereLight, NeutralToneMapping, PCFShadowMap, Raycaster, Scene, Vector2, WebGLRenderer,
} from '../../lib/three/three.module.js';
import { CameraRig } from './camera.js';
import { Gestures } from './gestures.js';
import { groundOf } from './ground.js';
import { KeyPan } from './keys.js';
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

// What the ground (ground.js) is built from, as text to compare: the tiles and the rivers' courses.
function groundKey(scene) {
    const courses = (scene.rivers ?? []).map(river => [river.x1, river.z1, river.x2, river.z2]);
    return JSON.stringify([scene.tiles, courses]);
}

// What each layer is built from, as text to compare: a layer is built again only when its text changes. The terrain also reads
// the features (no trees under a building), and the features the tiles they stand on.
function layerKeys(scene, ground) {
    const tiles = new Map(scene.tiles.map(tile => [`${tile.q},${tile.r}`, tile]));
    const under = (scene.features ?? []).map(feature => tiles.get(`${feature.q},${feature.r}`));
    // Rivers, borders and the features lie on the ground (ground.js), which follows the tiles and the rivers (a river cuts a
    // gorge through a region's relief), so they are built again with them.
    return {
        terrain: ground + JSON.stringify(scene.features),
        rivers: JSON.stringify([scene.rivers, scene.bridges]) + ground,
        features: JSON.stringify([scene.features, under]) + ground,
        borders: JSON.stringify(scene.borders) + ground,
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
        this.hovered = null;
        this.hoverAt = null;
        this.hoverFrame = 0;
        this.lost = false;

        this.listen();
        this.resize();
        // Units show a stand-in body until the baked characters arrive; then the pieces are built again.
        loadFigures().then(loaded => loaded && !this.disposed && this.rebuildPieces()).catch(error => console.error(error));
    }

    listen() {
        this.gestures = new Gestures(this);
        this.keyPan = new KeyPan(this);
        // A lost context (a phone backgrounding the tab, a GPU reset) stops drawing until the browser gives it back; Three.js
        // then uploads everything again, and the board draws the scene it kept.
        this.onContextLost = event => {
            event.preventDefault();
            this.lost = true;
            cancelAnimationFrame(this.frame);
            this.frame = 0;
            this.dotnet.invokeMethodAsync('OnContextLost');
        };
        this.onContextRestored = () => {
            this.lost = false;
            this.rebuild();
            this.requestRender();
            this.dotnet.invokeMethodAsync('OnContextRestored');
        };
        this.canvas.addEventListener('webglcontextlost', this.onContextLost);
        this.canvas.addEventListener('webglcontextrestored', this.onContextRestored);
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
        this.groundKey = groundKey(scene);
        // The same tiles and rivers as before: the scene keeps the earlier tile list (equal, item for item), so the ground built
        // for it (cached per tile list) serves again and a view that only moves pieces doesn't build it anew.
        if (previous && this.groundKey === this.previousGroundKey) {
            scene.tiles = previous.tiles;
        }
        this.previousGroundKey = this.groundKey;
        this.lift(scene.pieces);
        // The overlay comes again after every new view (GameBoard), which places its labels on the new ground; until then its
        // marks keep to it.
        this.placeMarks(this.overlayData);
        this.rig.setBounds(scene.map);
        this.pieceByUnit = new Map((scene.pieces ?? []).filter(piece => piece.unit != null).map(piece => [piece.unit, piece]));
        const sameMap = previous && JSON.stringify(previous.map) === JSON.stringify(scene.map);
        this.rebuild(sameMap ? 'changed' : 'all');
    }

    // Animates the scene (BoardAnimationService's motion); the scene itself stays as it is.
    setMotion(motion) {
        this.lift(motion?.positions);
        this.lift((motion?.ghosts ?? []).map(ghost => ghost.piece));
        this.lift(motion?.effects);
        this.motion.set(motion, performance.now());
        this.requestRender();
    }

    // Puts the overlay's tiles and edges (where the lens's labels stand) on the ground under them, which a region's relief may
    // lift; the same every time, so it can run again with a new scene.
    placeMarks(overlay) {
        if (!this.scene || !overlay) {
            return;
        }
        const ground = groundOf(this.scene);
        for (const mark of [...(overlay.tiles ?? []), ...(overlay.edges ?? [])]) {
            mark.height = ground.surfaceAt(mark.x, mark.z, mark.height);
        }
    }

    // Lifts things placed by the scene's tile heights (pieces, the motion's places) by the relief the ground's regions add
    // where they stand (ground.js), so a unit on a range's shelf stands on it rather than in it.
    lift(items) {
        if (!this.scene || !items?.length) {
            return;
        }
        const ground = groundOf(this.scene);
        for (const item of items) {
            // From the scene's own height every time, so lifting the same item again changes nothing.
            item.base ??= item.height;
            item.height = item.base + ground.reliefAt(item.x, item.z);
        }
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
        this.placeMarks(overlay);
        this.labels.setOverlay(overlay);
        this.drawOverlay(new Palette(this.host));
        this.requestRender();
    }

    drawOverlay(palette) {
        if (this.overlay) {
            this.world.remove(this.overlay);
            disposeLayer(this.overlay);
        }
        this.overlay = buildOverlay(this.overlayData, palette, this.scene);
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
        const keys = layerKeys(this.scene, this.groundKey ?? groundKey(this.scene));
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

    // Keeps a page element (the attack forecast) next to a tile; null lets go of it.
    anchor(element, q, r) {
        const tile = element && this.scene?.tiles.find(candidate => candidate.q === q && candidate.r === r);
        if (tile) {
            this.labels.setAnchor(element, tile.x, groundOf(this.scene).surfaceAt(tile.x, tile.z, tile.height) + 0.4, tile.z);
        } else {
            this.labels.clearAnchor();
        }
        this.requestRender();
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
        if (!this.frame && !this.lost) {
            this.frame = requestAnimationFrame(() => {
                this.frame = 0;
                const now = performance.now();
                const panning = this.keyPan.tick(now);
                const animating = this.motion.tick(now) || panning;
                this.renderer.render(this.world, this.rig.camera);
                this.labels.place(this.rig.camera, this.canvas.clientWidth, this.canvas.clientHeight);
                if (animating) {
                    this.requestRender();
                }
            });
        }
    }

    // The tile under a point of the view (normalised device coordinates): the piece hit there, else the ground; null off the map.
    pick(at) {
        if (!this.terrain || !this.scene) {
            return null;
        }
        this.raycaster.setFromCamera(new Vector2(at.x, at.y), this.rig.camera);
        const hit = this.raycaster.intersectObjects([this.terrain.pick, ...(this.pieces?.pickable ?? [])], false)[0];
        if (!hit) {
            return null;
        }
        return hit.object.userData.tiles
            ? hit.object.userData.tiles[hit.instanceId]
            : this.scene.tiles[this.terrain.pick.userData.faceTile[hit.faceIndex]];
    }

    // A click tells .NET which tile it is on.
    click(at) {
        const tile = this.pick(at);
        if (tile) {
            this.dotnet.invokeMethodAsync('OnTileClicked', tile.q, tile.r);
        }
    }

    // The mouse or pen is over a point of the view; null when it left or a drag started. Picks at most once a frame, and tells
    // .NET only when the tile under it changes.
    hover(at) {
        this.hoverAt = at;
        if (at === null) {
            cancelAnimationFrame(this.hoverFrame);
            this.hoverFrame = 0;
            this.setHovered(null);
        } else if (!this.hoverFrame) {
            this.hoverFrame = requestAnimationFrame(() => {
                this.hoverFrame = 0;
                if (this.hoverAt) {
                    this.setHovered(this.pick(this.hoverAt));
                }
            });
        }
    }

    setHovered(tile) {
        const key = tile ? `${tile.q},${tile.r}` : null;
        if (key === this.hovered) {
            return;
        }
        this.hovered = key;
        if (tile) {
            this.dotnet.invokeMethodAsync('OnTileHovered', tile.q, tile.r);
        } else {
            this.setPointer(false);
            this.dotnet.invokeMethodAsync('OnHoverEnded');
        }
    }

    setPointer(actionable) {
        this.canvas.classList.toggle('actionable', actionable && this.hovered !== null);
    }

    dispose() {
        this.disposed = true;
        cancelAnimationFrame(this.frame);
        cancelAnimationFrame(this.hoverFrame);
        this.gestures.dispose();
        this.keyPan.dispose();
        this.canvas.removeEventListener('webglcontextlost', this.onContextLost);
        this.canvas.removeEventListener('webglcontextrestored', this.onContextRestored);
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
