const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');

const html = fs.readFileSync(path.join(__dirname, '../../src/Relewise.BlazorApps/wwwroot/index.html'), 'utf8');
const scripts = [...html.matchAll(/<script(?:\s[^>]*)?>([\s\S]*?)<\/script>/g)].map(match => match[1]);

function loadPage(url) {
    const location = new URL(url);
    let href = html.match(/<base href="([^"]+)"/)[1];
    let redirected = false;
    location.replace = () => { redirected = true; };
    const context = {
        window: { location, history: { replaceState: (_, __, value) => { location.href = new URL(value, location).href; } } },
        document: { getElementsByTagName: () => [{ setAttribute: (_, value) => { href = value; } }] }
    };
    for (const script of scripts) vm.runInNewContext(script, context);
    return { base: new URL(href, location).href, redirected };
}

for (const route of ['/', '/Recommendations', '/Recommendations/', '/Models/', '/Searches/']) {
    test(`tunnel modules resolve from the app root at ${route}`, () => {
        const result = loadPage(`https://example-7176.euw.devtunnels.ms${route}`);
        assert.equal(new URL('./js/recommendationLayout.js', result.base).href,
            'https://example-7176.euw.devtunnels.ms/js/recommendationLayout.js');
        assert.equal(result.redirected, false);
    });
}

test('localhost routes use the app root', () => {
    assert.equal(loadPage('https://localhost:7176/Recommendations/').base, 'https://localhost:7176/');
});

test('GitHub Pages retains the repository prefix', () => {
    assert.equal(loadPage('https://example.github.io/tools/Recommendations/').base, 'https://example.github.io/tools/');
});

test('GitHub Pages restores redirected routes under the repository prefix', () => {
    assert.equal(loadPage('https://example.github.io/tools/?/Recommendations/').base, 'https://example.github.io/tools/');
});

test('GitHub Pages adds the repository trailing slash', () => {
    assert.equal(loadPage('https://example.github.io/tools').redirected, true);
});
