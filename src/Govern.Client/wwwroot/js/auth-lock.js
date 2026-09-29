// Runs the .NET callback while holding a Web Lock shared by all tabs, so only one tab refreshes the session at a time.
export function runExclusive(name, callback) {
    if (navigator.locks && navigator.locks.request) {
        return navigator.locks.request(name, () => callback.invokeMethodAsync('RunLockedAsync'));
    }

    return callback.invokeMethodAsync('RunLockedAsync');
}
