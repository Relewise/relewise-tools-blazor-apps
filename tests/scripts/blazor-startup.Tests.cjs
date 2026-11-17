const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');

const html = fs.readFileSync(path.join(__dirname, '../../src/Relewise.BlazorApps/wwwroot/index.html'), 'utf8');
const source = html.match(/<script id="blazor-startup">([\s\S]*?)<\/script>/)[1];

function startApp(startResult, bootstrapAvailable = true) {
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
    const context = {
        document: { getElementById: id => elements[id] },
        window: { location: { reload: () => { reloads++; } } },
        console: { error: () => {} },
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
                options.configureRuntime({ withConfig: values => Object.assign(config, values) });
                return startResult;
            }
        };
    }
    const completion = vm.runInNewContext(source, context);
    return { completion, elements, config, notifyError: () => notifyError(), get observing() { return observing; }, get reloads() { return reloads; }, get starts() { return starts; } };
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
