const cleanups = new WeakMap();

export function initialize(workspace) {
    dispose(workspace);
    const article = workspace.closest(".tool-content");
    const resize = () => {
        const bottomPadding = article ? parseFloat(getComputedStyle(article).paddingBottom) : 24;
        const height = Math.max(240, Math.floor(window.innerHeight - workspace.getBoundingClientRect().top - bottomPadding - 1));
        const value = `${height}px`;
        if (workspace.style.getPropertyValue("--workspace-height") !== value)
            workspace.style.setProperty("--workspace-height", value);
    };
    const observer = new ResizeObserver(resize);
    observer.observe(article ?? workspace.parentElement);
    window.addEventListener("resize", resize);
    resize();
    cleanups.set(workspace, () => {
        observer.disconnect();
        window.removeEventListener("resize", resize);
    });
}

export function dispose(workspace) {
    cleanups.get(workspace)?.();
    cleanups.delete(workspace);
}
