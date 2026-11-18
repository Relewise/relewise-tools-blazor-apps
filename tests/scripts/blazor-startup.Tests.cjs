const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');

const html = fs.readFileSync(path.join(__dirname, '../../src/Relewise.BlazorApps/wwwroot/index.html'), 'utf8');
const source = html.match(/<script id="blazor-startup">([\s\S]*?)<\/script>/)[1];

function startApp(startResult, bootstrapAvailable = true, fetchResource) {
    const elements = Object.fromEntries(['loading-state', 'startup-error', 'startup-retry', 'blazor-error-ui'].map(id => [id, {
        hidden: id === 'startup-error',
        style: {},
        addEventListener: (event, listener) => { elements[id][event] = listener; },
        focus: () => { elements[id].focused = true; }
    }]));
    const config = { enableDownloadRetry: false, disableIntegrityCheck: false };
    let reloads = 0;
    let starts = 0;
    let notifyError;
    let observing = false;
    let loadBootResource;
    const context = {
        document: { getElementById: id => elements[id] },
        window: { location: { reload: () => { reloads++; } } },
        console: { error: () => {} },
        fetch: fetchResource,
        MutationObserver: class {
            constructor(callback) { notifyError = callback; }
            observe() { observing = true; }
            disconnect() { observing = false; }
        }
    };
    if (bootstrapAvailable) {
        context.Blazor = {
            start: options => {
                starts++;
                loadBootResource = options.loadBootResource;
                options.configureRuntime({ withConfig: values => Object.assign(config, values) });
                return startResult;
            }
        };
    }
    const completion = vm.runInNewContext(source, context);
    return { completion, elements, config, loadBootResource, notifyError: () => notifyError(), get observing() { return observing; }, get reloads() { return reloads; }, get starts() { return starts; } };
}

test('starts Blazor once with native download retries and integrity checks enabled', async () => {
    assert.match(html, /<script src="_framework\/blazor\.webassembly\.js" autostart="false">/);
    const app = startApp(Promise.resolve());
    await app.completion;
    assert.equal(app.starts, 1);
    assert.equal(app.config.enableDownloadRetry, true);
    assert.equal(app.config.disableIntegrityCheck, false);
    assert.equal(app.elements['startup-error'].hidden, true);
    assert.equal(app.reloads, 0);
    assert.equal(app.observing, false);
});

test('only retried integrity-checked downloads bypass the browser cache', async () => {
    const requests = [];
    const response = new Response();
    const app = startApp(Promise.resolve(), true, async (uri, options) => {
        requests.push({ uri, cache: options.cache, integrity: options.integrity });
        return response;
    });
    await app.completion;

    const uri = '_framework/System.Console.test.wasm';
    const integrity = 'sha256-example';
    assert.equal(app.loadBootResource('assembly', 'System.Console.wasm', uri, integrity), undefined);
    assert.equal(app.loadBootResource('assembly', 'Other.wasm', '_framework/Other.wasm', integrity), undefined);
    for (let attempt = 0; attempt < 2; attempt++) {
        assert.equal(app.loadBootResource('dotnetjs', 'dotnet.js', '_framework/dotnet.js', integrity), undefined);
        assert.equal(app.loadBootResource('configuration', 'appsettings.json', 'appsettings.json', ''), undefined);
        assert.equal(await app.loadBootResource('assembly', 'System.Console.wasm', uri, integrity), response);
    }
    assert.deepEqual(requests, [
        { uri, cache: 'reload', integrity },
        { uri, cache: 'reload', integrity }
    ]);
});

test('startup rejection replaces stalled progress with a focused retry action', async () => {
    const app = startApp(Promise.reject(new TypeError('Failed to fetch')));
    await app.completion;
    assert.equal(app.elements['loading-state'].hidden, true);
    assert.equal(app.elements['startup-error'].hidden, false);
    assert.equal(app.elements['blazor-error-ui'].hidden, true);
    assert.equal(app.elements['startup-retry'].focused, true);
    assert.equal(app.observing, false);
    assert.equal(app.reloads, 0);
    app.elements['startup-retry'].click();
    assert.equal(app.reloads, 1);
    assert.equal(app.starts, 1);
});

test('runtime error notification offers recovery even while Blazor.start stays pending', () => {
    const app = startApp(new Promise(() => {}));
    assert.equal(app.observing, true);
    app.elements['blazor-error-ui'].style.display = 'none';
    app.notifyError();
    assert.equal(app.elements['loading-state'].hidden, false);
    assert.equal(app.elements['startup-error'].hidden, true);
    app.elements['blazor-error-ui'].style.display = 'block';
    app.notifyError();
    assert.equal(app.elements['loading-state'].hidden, true);
    assert.equal(app.elements['startup-error'].hidden, false);
    assert.equal(app.elements['blazor-error-ui'].hidden, true);
    assert.equal(app.observing, false);
    assert.equal(app.reloads, 0);
});

test('a missing Blazor bootstrap also offers recovery instead of leaving the spinner', async () => {
    const app = startApp(undefined, false);
    await app.completion;
    assert.equal(app.elements['loading-state'].hidden, true);
    assert.equal(app.elements['startup-error'].hidden, false);
    app.elements['startup-retry'].click();
    assert.equal(app.reloads, 1);
});
