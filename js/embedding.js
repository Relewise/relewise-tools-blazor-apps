// Keep the call in this window's JavaScript realm rather than passing a
// cross-window function reference through .NET's JS interop resolver.
export function notifyParentReady(targetOrigin) {
    window.parent.postMessage({ MyRelewiseAppReady: true }, targetOrigin);
}
