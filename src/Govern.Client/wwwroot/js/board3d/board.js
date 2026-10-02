// The 3D game board (Features/Games/Services/Board3DInteropService.cs). Each mounted host element has its own Board3D; the
// calls below find it by the element. Drawing and camera only: what shows is decided in C# (BoardScene).
import { measure as measureBoard } from './bench.js';
import { Board3D } from './scene.js';

const boards = new WeakMap();

// Creates the board in `host`; false when WebGL is unavailable, so the page can say so.
export function mount(host, dotnet) {
    unmount(host);
    try {
        boards.set(host, new Board3D(host, dotnet));
        return true;
    } catch {
        return false;
    }
}

export function render(host, scene) {
    boards.get(host)?.setScene(scene);
}

export function renderOverlay(host, overlay) {
    boards.get(host)?.setOverlay(overlay);
}

export function renderMotion(host, motion) {
    boards.get(host)?.setMotion(motion);
}

// Keeps `element` beside the tile (q, r) as the camera moves, until unanchor.
export function anchor(host, element, q, r) {
    boards.get(host)?.anchor(element, q, r);
}

export function unanchor(host) {
    boards.get(host)?.anchor(null);
}

export function fit(host, rect) {
    boards.get(host)?.fit(rect);
}

export function focus(host, x, z) {
    boards.get(host)?.focus(x, z);
}

export function zoom(host, factor) {
    boards.get(host)?.zoom(factor);
}

// The pointer cursor while the hovered tile does something when clicked (.NET decides, BoardClickResolver).
export function setPointer(host, actionable) {
    boards.get(host)?.setPointer(actionable);
}

// Measures the board on the page (the bench at /design/board, Features/Design/Services/BoardBenchService.cs); null without one.
export function measure() {
    const host = document.querySelector('.board3d');
    return host && boards.get(host) ? measureBoard(boards.get(host)) : null;
}

export function unmount(host) {
    boards.get(host)?.dispose();
    boards.delete(host);
}
