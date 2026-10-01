// Helpers for the game board (Features/Games/Services/BoardInteropService.cs).

// Keeps the page from scrolling while the wheel zooms the board. Blazor may register its wheel listener as passive, and a
// passive listener cannot cancel scrolling, so this one is added with passive: false. Blazor still gets the event.
export function init(element) {
    element.addEventListener('wheel', event => event.preventDefault(), { passive: false });
}

// The board element's position and size in the window, for fitting the map and zooming at the pointer.
export function measure(element) {
    const rect = element.getBoundingClientRect();
    return { left: rect.left, top: rect.top, width: rect.width, height: rect.height };
}

// Whether the player asked for reduced motion, so the board's animations skip moving units.
export function prefersReducedMotion() {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}
