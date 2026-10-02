// One 3D board: the WebGL renderer in its host element, the light, the camera and the pointer. It draws on demand (after a
// scene, a camera move or a resize), and every frame only while an animation plays (motion.js).
import {
    DirectionalLight, Group, HemisphereLight, NeutralToneMapping, PCFShadowMap, Raycaster, Scene, Vector2, WebGLRenderer,
} from '../../lib/three/three.module.js';
import { CameraRig } from './camera.js';
import { Palette } from './palette.js';
import { Labels } from './labels.js';
import { buildBorders, buildOverlay, disposeShared as disposeBorders } from './overlays.js';
import { Motion } from './motion.js';
import { buildPieces, disposeShared as disposePieces, loadFigures } from './pieces.js';
import { buildFeatures, disposeShared as disposeFeatures } from './models.js';
import { buildRivers, disposeShared as disposeRivers } from './rivers.js';
import { disposeLayer } from './shapes.js';
import { buildTerrain, disposeShared as disposeGround, disposeTerrain } from './terrain.js';

// How far, in pixels, a press may move and still count as a click (as the 2D board's GameBoard.DragThreshold).
const DRAG_THRESHOLD = 4;
const ZOOM_STEP = 1.2;

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
        this.pieceByUnit = new Map();
        this.motion = new Motion(this);
        this.frame = 0;

        this.listen();
        this.resize();
        // Units show only their base until the baked characters arrive; then the pieces are built again.
        loadFigures().then(loaded => loaded && !this.disposed && this.rebuild()).catch(error => console.error(error));
    }

    listen() {
        this.handlers = {
            pointerdown: event => this.onPointerDown(event),
            pointermove: event => this.onPointerMove(event),
            pointerup: event => this.onPointerUp(event),
            pointercancel: () => { this.press = null; },
            wheel: event => this.onWheel(event),
        };
        for (const [type, handler] of Object.entries(this.handlers)) {
            this.canvas.addEventListener(type, handler, { passive: type !== 'wheel' });
        }
        this.resizeObserver = new ResizeObserver(() => this.resize());
        this.resizeObserver.observe(this.host);
        this.theme = window.matchMedia('(prefers-color-scheme: dark)');
        this.onTheme = () => this.rebuild();
        this.theme.addEventListener('change', this.onTheme);
    }

    setScene(scene) {
        this.scene = scene;
        this.rig.setBounds(scene.map);
        this.labels.setPieces(scene.pieces);
        this.pieceByUnit = new Map((scene.pieces ?? []).filter(piece => piece.unit != null).map(piece => [piece.unit, piece]));
        this.rebuild();
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
        if (this.pieces) {
            this.layers.remove(this.pieces.group);
            disposeLayer(this.pieces.group);
        }
        this.pieces = buildPieces(this.scene, this.palette(), this.motion.moving);
        this.layers.add(this.pieces.group);
        this.motion.reapply();
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

    // Builds the ground again with the current theme's colours.
    rebuild() {
        if (!this.scene) {
            return;
        }
        const palette = new Palette(this.host);
        this.sky.color = palette.token('--gv-board3d-sky');
        this.sky.groundColor = palette.token('--gv-board3d-ground');
        this.sky.intensity = 1.6;
        this.sun.color = palette.token('--gv-board3d-sun');
        this.sun.intensity = 2.2;
        this.placeSun();
        this.clear();
        this.terrain = buildTerrain(this.scene, palette);
        this.world.add(this.terrain.group);
        this.layers = new Group();
        for (const layer of [buildRivers(this.scene, palette), buildFeatures(this.scene, palette), buildBorders(this.scene, palette)]) {
            if (layer) {
                this.layers.add(layer);
            }
        }
        this.pieces = buildPieces(this.scene, palette, this.motion.moving);
        this.layers.add(this.pieces.group);
        this.motion.reapply();
        this.world.add(this.layers);
        this.drawOverlay(palette);
        this.requestRender();
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

    // Normalised device coordinates (-1 to 1) of a pointer event.
    ndc(event) {
        const rect = this.canvas.getBoundingClientRect();
        return { x: ((event.clientX - rect.left) / rect.width) * 2 - 1, y: -((event.clientY - rect.top) / rect.height) * 2 + 1 };
    }

    // Only the primary pointer pans, so a second finger can't make the board jump.
    onPointerDown(event) {
        if (!event.isPrimary) {
            return;
        }
        this.press = { id: event.pointerId, x: event.clientX, y: event.clientY, last: this.ndc(event), dragged: false };
        this.canvas.setPointerCapture(event.pointerId);
    }

    onPointerMove(event) {
        const press = this.press;
        if (!press || event.pointerId !== press.id) {
            return;
        }
        press.dragged ||= Math.abs(event.clientX - press.x) + Math.abs(event.clientY - press.y) > DRAG_THRESHOLD;
        const at = this.ndc(event);
        if (press.dragged) {
            this.rig.pan(press.last, at);
            this.requestRender();
        }
        press.last = at;
    }

    onPointerUp(event) {
        const press = this.press;
        if (!press || event.pointerId !== press.id) {
            return;
        }
        this.press = null;
        if (!press.dragged) {
            this.click(this.ndc(event));
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

    // One mouse notch zooms by ZOOM_STEP; a trackpad's small deltas zoom smoothly.
    onWheel(event) {
        event.preventDefault();
        const pixels = event.deltaMode === 1 ? event.deltaY * 100 / 3 : event.deltaMode === 2 ? event.deltaY * 800 : event.deltaY;
        if (pixels !== 0) {
            this.rig.zoomAt(this.ndc(event), Math.pow(ZOOM_STEP, -Math.max(-3, Math.min(3, pixels / 100))));
            this.requestRender();
        }
    }

    dispose() {
        this.disposed = true;
        cancelAnimationFrame(this.frame);
        for (const [type, handler] of Object.entries(this.handlers)) {
            this.canvas.removeEventListener(type, handler);
        }
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
