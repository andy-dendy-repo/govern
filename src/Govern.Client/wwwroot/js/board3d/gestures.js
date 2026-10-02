// The board's pointer gestures: one finger or the mouse drags to pan, two fingers pinch to zoom around them and pan with them,
// the wheel zooms, and a press that hardly moved is a click. A gesture that ever had two fingers never ends in a click, and
// lifting one of two fingers hands the pan to the other without a jump.

// How far, in pixels, a press may move and still count as a click: a little for the mouse, a finger more, since a tap
// wobbles.
const DRAG_THRESHOLD = 4;
const TAP_THRESHOLD = 10;
const ZOOM_STEP = 1.2;

export class Gestures {
    // `board` gives the canvas, the camera rig, requestRender() and click(ndc).
    constructor(board) {
        this.board = board;
        this.canvas = board.canvas;
        this.pointers = new Map();
        this.press = null;
        this.handlers = {
            pointerdown: event => this.down(event),
            pointermove: event => this.move(event),
            pointerup: event => this.up(event, true),
            pointercancel: event => this.up(event, false),
            // A pointer whose up the canvas never saw (it went up elsewhere) is dropped when its capture ends.
            lostpointercapture: event => this.up(event, false),
            wheel: event => this.wheel(event),
        };
        for (const [type, handler] of Object.entries(this.handlers)) {
            this.canvas.addEventListener(type, handler, { passive: type !== 'wheel' });
        }
    }

    // Normalised device coordinates (-1 to 1) of a point on the page.
    ndc(point) {
        const rect = this.canvas.getBoundingClientRect();
        return { x: ((point.x - rect.left) / rect.width) * 2 - 1, y: -((point.y - rect.top) / rect.height) * 2 + 1 };
    }

    down(event) {
        // A primary pointer starts a new gesture: anything left from one whose end was missed is dropped.
        if (event.isPrimary) {
            this.pointers.clear();
            this.press = null;
        }
        if (this.pointers.size >= 2) {
            return;
        }
        this.pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
        try {
            this.canvas.setPointerCapture(event.pointerId);
        } catch {
            // A pointer the browser no longer knows (it went up meanwhile) can't be captured; its events still arrive.
        }
        if (this.pointers.size === 1) {
            const slop = event.pointerType === 'mouse' ? DRAG_THRESHOLD : TAP_THRESHOLD;
            this.press = { id: event.pointerId, x: event.clientX, y: event.clientY, slop, dragged: false, pinched: false };
        } else if (this.press) {
            this.press.pinched = true;
        }
    }

    move(event) {
        const before = this.pointers.get(event.pointerId);
        if (!before) {
            return;
        }
        const after = { x: event.clientX, y: event.clientY };
        this.pointers.set(event.pointerId, after);
        if (this.pointers.size === 2) {
            const [a, b] = [...this.pointers.entries()].map(([id, point]) => id === event.pointerId ? [before, point] : [point, point]);
            this.pinch(a[0], b[0], a[1], b[1]);
            return;
        }
        const press = this.press;
        if (!press || press.id !== event.pointerId) {
            return;
        }
        press.dragged ||= Math.abs(after.x - press.x) + Math.abs(after.y - press.y) > press.slop;
        if (press.dragged) {
            this.board.rig.pan(this.ndc(before), this.ndc(after));
            this.board.requestRender();
        }
    }

    // Two fingers moved from (a0, b0) to (a1, b1): the ground under their midpoint follows it, and the view zooms by how much
    // further apart they are.
    pinch(a0, b0, a1, b1) {
        const from = Math.hypot(b0.x - a0.x, b0.y - a0.y);
        const to = Math.hypot(b1.x - a1.x, b1.y - a1.y);
        const middle = (p, q) => this.ndc({ x: (p.x + q.x) / 2, y: (p.y + q.y) / 2 });
        this.board.rig.pinch(middle(a0, b0), middle(a1, b1), from > 0 && to > 0 ? to / from : 1);
        this.board.requestRender();
    }

    // A pointer went up (or was cancelled): a click when it was the only one and hardly moved; with one finger left after a
    // pinch, that finger pans on from where it is.
    up(event, released) {
        if (!this.pointers.delete(event.pointerId)) {
            return;
        }
        const press = this.press;
        if (this.pointers.size === 1 && press) {
            const [[id]] = this.pointers;
            press.id = id;
            press.dragged = true;
            return;
        }
        if (this.pointers.size > 0) {
            return;
        }
        this.press = null;
        if (released && press && press.id === event.pointerId && !press.dragged && !press.pinched) {
            this.board.click(this.ndc({ x: event.clientX, y: event.clientY }));
        }
    }

    // One mouse notch zooms by ZOOM_STEP; a trackpad's small deltas zoom smoothly.
    wheel(event) {
        event.preventDefault();
        const pixels = event.deltaMode === 1 ? event.deltaY * 100 / 3 : event.deltaMode === 2 ? event.deltaY * 800 : event.deltaY;
        if (pixels !== 0) {
            this.board.rig.zoomAt(this.ndc({ x: event.clientX, y: event.clientY }), Math.pow(ZOOM_STEP, -Math.max(-3, Math.min(3, pixels / 100))));
            this.board.requestRender();
        }
    }

    dispose() {
        for (const [type, handler] of Object.entries(this.handlers)) {
            this.canvas.removeEventListener(type, handler);
        }
        this.pointers.clear();
    }
}
