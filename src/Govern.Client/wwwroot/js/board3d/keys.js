// The board's keyboard: W, A, S and D pan the camera while held, north, west, south and east on the map,
// faster the further out the camera is; Shift doubles the speed. Keys typed into a field (a city's name) or with Ctrl, Alt or
// Cmd are left alone, and letting go of the window stops the pan.

// Map units per second at a camera distance of 1, so the view crosses about its own width in a second and a half.
const SPEED = 0.6;
// A frame longer than this (a background tab) doesn't jump the camera.
const MAX_STEP = 0.1;
// By physical key, so the same keys pan on any keyboard layout. The arrow keys stay with the page's controls (lists, sliders).
const directions = { KeyW: [0, -1], KeyS: [0, 1], KeyA: [-1, 0], KeyD: [1, 0] };

// Whether the key press belongs to something else on the page: a text field, a select, or editable text.
function typing(target) {
    return target instanceof HTMLElement && (target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName));
}

export class KeyPan {
    // `board` gives the camera rig, requestRender() and hover(ndc).
    constructor(board) {
        this.board = board;
        this.held = new Set();
        this.fast = false;
        this.last = 0;
        this.handlers = {
            keydown: event => this.down(event),
            keyup: event => this.up(event),
            blur: () => this.release(),
        };
        for (const [type, handler] of Object.entries(this.handlers)) {
            window.addEventListener(type, handler);
        }
    }

    get active() {
        return this.held.size > 0;
    }

    down(event) {
        this.fast = event.shiftKey;
        if (!directions[event.code] || event.ctrlKey || event.altKey || event.metaKey || typing(event.target)) {
            return;
        }
        event.preventDefault();
        if (!this.held.has(event.code)) {
            if (!this.active) {
                this.last = performance.now();
            }
            this.held.add(event.code);
            this.board.requestRender();
        }
    }

    up(event) {
        this.fast = event.shiftKey;
        this.held.delete(event.code);
    }

    release() {
        this.held.clear();
    }

    // Moves the camera for the time since the last frame; true while a key is still held, so the board draws another frame.
    tick(now) {
        if (!this.active) {
            return false;
        }
        const seconds = Math.min((now - this.last) / 1000, MAX_STEP);
        this.last = now;
        let [x, z] = [0, 0];
        for (const code of this.held) {
            x += directions[code][0];
            z += directions[code][1];
        }
        const length = Math.hypot(x, z);
        if (length > 0) {
            const rig = this.board.rig;
            const step = SPEED * rig.distance * seconds * (this.fast ? 2 : 1) / length;
            rig.target.x += x * step;
            rig.target.z += z * step;
            rig.update();
            // The ground moved under a still mouse.
            if (this.board.hoverAt) {
                this.board.hover(this.board.hoverAt);
            }
        }
        return true;
    }

    dispose() {
        for (const [type, handler] of Object.entries(this.handlers)) {
            window.removeEventListener(type, handler);
        }
        this.held.clear();
    }
}
