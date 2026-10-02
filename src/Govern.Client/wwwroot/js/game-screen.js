// The full-screen game view (Features/Games/Services/GameScreenInteropService.cs): keeps the height of the top strip in the
// screen's --gv-hud-top property, so the board's own controls sit below it, enters and leaves the browser's full screen, and
// reads the reduced-motion preference.

const watched = new WeakMap();

// Follows the strip's height and the screen's full-screen state; true when the browser can show the screen in full screen.
export function watch(screen, strip, dotnet) {
    unwatch(screen);
    const place = () => {
        const bottom = strip.getBoundingClientRect().bottom - screen.getBoundingClientRect().top;
        screen.style.setProperty('--gv-hud-top', `${Math.ceil(bottom)}px`);
    };
    const observer = new ResizeObserver(place);
    observer.observe(strip);
    place();
    const changed = () => dotnet.invokeMethodAsync('OnFullScreenChanged', document.fullscreenElement === screen);
    screen.addEventListener('fullscreenchange', changed);
    watched.set(screen, () => {
        observer.disconnect();
        screen.removeEventListener('fullscreenchange', changed);
    });
    return Boolean(document.fullscreenEnabled && screen.requestFullscreen);
}

// Stops following the screen (the game table is gone).
export function unwatch(screen) {
    watched.get(screen)?.();
    watched.delete(screen);
}

// Whether the player asked for reduced motion, so the board's animations skip moving units.
export function prefersReducedMotion() {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

export async function enter(screen) {
    try {
        await screen.requestFullscreen();
    } catch {
        // The browser refused (no user gesture, or not allowed here); the screen stays as it is.
    }
}

export async function exit() {
    if (document.fullscreenElement) {
        await document.exitFullscreen();
    }
}
