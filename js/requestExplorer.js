export function watchPendingEdits(reference) {
    // This application runs on WebAssembly. Flush synchronously while the page
    // is still alive, before a background tab can throttle the autosave timer.
    const flush = () => reference.invokeMethod("FlushPendingRequest");
    const hide = () => {
        if (document.visibilityState === "hidden") flush();
    };
    window.addEventListener("pagehide", flush);
    document.addEventListener("visibilitychange", hide);
    return {
        dispose() {
            window.removeEventListener("pagehide", flush);
            document.removeEventListener("visibilitychange", hide);
        }
    };
}
