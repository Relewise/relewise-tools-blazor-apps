const storageKey = "relewise-tools.connection";

export function read() {
    const value = localStorage.getItem(storageKey);
    if (value === null) return null;
    const connection = JSON.parse(value);
    if (!Array.isArray(connection) || connection.length !== 3 || connection.some(value => typeof value !== "string")) {
        throw new Error("Invalid saved connection.");
    }
    return connection;
}

export function save(serverUrl, datasetId, apiKey) {
    localStorage.setItem(storageKey, JSON.stringify([serverUrl, datasetId, apiKey]));
}

export function forget() {
    localStorage.removeItem(storageKey);
}
