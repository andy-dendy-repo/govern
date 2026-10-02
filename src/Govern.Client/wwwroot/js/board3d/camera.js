// The board's camera: a north-up perspective camera looking at a point on the ground. It tilts down further as it zooms
// out, and panning grabs the ground under the pointer.
import { MathUtils, PerspectiveCamera, Plane, Raycaster, Vector2, Vector3 } from '../../lib/three/three.module.js';

const FIELD_OF_VIEW = 35;
const MIN_DISTANCE = 5;
const MAX_DISTANCE = 70;
const PITCH_NEAR = MathUtils.degToRad(38);
const PITCH_FAR = MathUtils.degToRad(60);
const ground = new Plane(new Vector3(0, 1, 0), 0);
const pointer = new Vector2();

export class CameraRig {
    constructor() {
        this.camera = new PerspectiveCamera(FIELD_OF_VIEW, 1, 0.1, 500);
        this.target = new Vector3();
        this.distance = 16;
        this.bounds = { left: 0, top: 0, width: 1, height: 1 };
        this.raycaster = new Raycaster();
        this.update();
    }

    get pitch() {
        const t = (this.distance - MIN_DISTANCE) / (MAX_DISTANCE - MIN_DISTANCE);
        return MathUtils.lerp(PITCH_NEAR, PITCH_FAR, Math.sqrt(MathUtils.clamp(t, 0, 1)));
    }

    setAspect(aspect) {
        this.camera.aspect = aspect;
        this.camera.updateProjectionMatrix();
    }

    // The map's rectangle (x and depth); the camera's target stays inside it.
    setBounds(bounds) {
        this.bounds = bounds;
        this.update();
    }

    update() {
        const { left, top, width, height } = this.bounds;
        this.distance = MathUtils.clamp(this.distance, MIN_DISTANCE, MAX_DISTANCE);
        this.target.x = MathUtils.clamp(this.target.x, left, left + width);
        this.target.z = MathUtils.clamp(this.target.z, top, top + height);
        const pitch = this.pitch;
        this.camera.position.set(
            this.target.x,
            this.target.y + this.distance * Math.sin(pitch),
            this.target.z + this.distance * Math.cos(pitch));
        this.camera.lookAt(this.target);
        this.camera.updateMatrixWorld();
    }

    // Shows a rectangle of the board (x and depth) as large as the view allows.
    fit(rect) {
        this.target.set(rect.left + rect.width / 2, 0, rect.top + rect.height / 2);
        const tan = Math.tan(MathUtils.degToRad(FIELD_OF_VIEW / 2));
        const byWidth = (rect.width / 2) / (tan * this.camera.aspect);
        // The tilt shortens the map's depth on screen by about sin(pitch); the pitch depends on the distance, so settle it.
        for (let i = 0; i < 3; i++) {
            const byHeight = (rect.height * Math.sin(this.pitch) / 2) / tan;
            this.distance = MathUtils.clamp(Math.max(byHeight, byWidth) * 1.05, MIN_DISTANCE, MAX_DISTANCE);
        }
        this.update();
    }

    focus(x, z) {
        this.target.x = x;
        this.target.z = z;
        this.update();
    }

    // The ground point under a position in normalised device coordinates (-1 to 1), or null above the horizon.
    groundAt(ndc) {
        this.raycaster.setFromCamera(pointer.set(ndc.x, ndc.y), this.camera);
        return this.raycaster.ray.intersectPlane(ground, new Vector3());
    }

    // Moves the camera so the ground under `from` ends up under `to`.
    pan(from, to) {
        const a = this.groundAt(from);
        const b = this.groundAt(to);
        if (a && b) {
            this.target.add(a.sub(b));
            this.update();
        }
    }

    // Zooms by a factor (above 1 zooms in) keeping the ground under `at` in place.
    zoomAt(at, factor) {
        const before = this.groundAt(at);
        this.distance /= factor;
        this.update();
        const after = this.groundAt(at);
        if (before && after) {
            this.target.add(before.sub(after));
            this.update();
        }
    }

    // Two fingers moved: the ground under their old midpoint (`from`) ends up under the new one (`to`), and the view zooms by
    // `factor` around it (above 1 when the fingers spread).
    pinch(from, to, factor) {
        this.pan(from, to);
        this.zoomAt(to, factor);
    }
}
