const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');

test('pending edits flush synchronously on page hide and listeners are removed on disposal', () => {
    const window = new EventTarget();
    const document = new EventTarget();
    document.visibilityState = 'visible';
    const source = fs.readFileSync(path.join(__dirname, '../../src/Relewise.BlazorApps/wwwroot/js/requestExplorer.js'), 'utf8');
    const context = { window, document };
    vm.runInNewContext(source.replace('export function', 'function'), context);
    const calls = [];
    const subscription = context.watchPendingEdits({ invokeMethod: name => calls.push(name) });
    document.dispatchEvent(new Event('visibilitychange'));
    assert.equal(calls.length, 0);
    document.visibilityState = 'hidden';
    document.dispatchEvent(new Event('visibilitychange'));
    assert.deepEqual(calls, ['FlushPendingRequest']);
    window.dispatchEvent(new Event('pagehide'));
    assert.deepEqual(calls, ['FlushPendingRequest', 'FlushPendingRequest']);
    subscription.dispose();
    document.dispatchEvent(new Event('visibilitychange'));
    window.dispatchEvent(new Event('pagehide'));
    assert.equal(calls.length, 2);
});
