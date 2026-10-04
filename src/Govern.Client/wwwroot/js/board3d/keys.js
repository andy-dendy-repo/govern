// The board's keyboard: W, A, S and D pan the camera while held, north, west, south and east on the map,
// faster the further out the camera is; Shift doubles the speed. Keys typed into a field (a city's name), keys for an open dialog,
// list or menu, and keys with Ctrl, Alt or Cmd are left alone; letting go of the window or the tab, or pressing Cmd (macOS sends
// no key-up for the other keys while it is down), stops the pan.

// Map units per second at a camera distance of 1, so the view crosses about its own width in a second and a half.
const SPEED = 0.6;
// A frame longer than this (a background tab) doesn't jump the camera.
const MAX_STEP = 0.1;
// While panning, the tile under a still mouse is picked again at most this often (ms): a pick ray-casts the whole ground.
const HOVER_EVERY = 100;
// By physical key, so the same keys pan on any keyboard layout. The arrow keys stay with the page's controls (lists, sliders).
const directions = { KeyW: [0, -1], KeyS: [0, 1], KeyA: [-1, 0], KeyD: [1, 0] };

// Whether the key press belongs to something else on the page: a text field, a select, editable text, or an open dialog, list
// or menu (a Radzen drop-down's type-ahead is a plain element inside a listbox).
function belongsElsewhere(target) {
    return target instanceof HTMLElement && (target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName)
        || target.closest('[role=dialog], [role=listbox], [role=menu], [role=combobox], .rz-dropdown, .rz-popup') !== null);
}

export class KeyPan {
    // `board` gives the camera rig, requestRender() and hover(ndc).
    constructor(board) {
        this.board = board;
        this.held = new Set();
        this.fast = false;
        this.last = 0;
        this.lastHover = 0;
        this.hoverPending = false;
        this.handlers = {
            keydown: event => this.down(event),
            keyup: event => this.up(event),
            blur: () => this.release(),
        };
        this.onVisibility = () => this.release();
        for (const [type, handler] of Object.entries(this.handlers)) {
            window.addEventListener(type, handler);
        }
        document.addEventListener('visibilitychange', this.onVisibility);
    }

    get active() {
        return this.held.size > 0;
    }

    down(event) {
        this.fast = event.shiftKey;
        if (event.key === 'Meta') {
            this.release();
            return;
        }
        if (!directions[event.code] || event.ctrlKey || event.altKey || event.metaKey || belongsElsewhere(event.target)) {
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
        if (event.key === 'Meta') {
            this.release();
            return;
        }
        this.held.delete(event.code);
    }

    release() {
        this.held.clear();
    }

    // Moves the camera for the time since the last frame; true while a key is still held, so the board draws another frame.
    tick(now) {
        if (!this.active) {
            // The pan stopped: pick the tile under the mouse once more where it ended.
            if (this.hoverPending) {
                this.hoverPending = false;
                this.hover();
            }
            return false;
        }
        const seconds = Math.min((now - this.last) / 1000, MAX_STEP);
        this.last = now;
        let x = 0;
        let z = 0;
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
            this.hoverPending = true;
            if (now - this.lastHover >= HOVER_EVERY) {
                this.lastHover = now;
                this.hoverPending = false;
                this.hover();
            }
        }
        return true;
    }

    hover() {
        if (this.board.hoverAt) {
            this.board.hover(this.board.hoverAt);
        }
    }

    dispose() {
        for (const [type, handler] of Object.entries(this.handlers)) {
            window.removeEventListener(type, handler);
        }
        document.removeEventListener('visibilitychange', this.onVisibility);
        this.held.clear();
    }
}
