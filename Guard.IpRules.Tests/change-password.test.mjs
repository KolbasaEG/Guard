import { readFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { test } from 'node:test';

const source = await readFile(new URL('../Guard/wwwroot/js/change-password.js', import.meta.url), 'utf8');
const { changePassword } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);

test('password response protocol', async t => {
    let calls, reloads, navigations;
    const base = 'https://guard.test/nested/';
    const container = { querySelector: selector => { assert.equal(selector, 'form'); return {}; } };
    globalThis.document = { baseURI: base };
    globalThis.FormData = class {};
    function response(status, json) {
        calls = reloads = 0; navigations = [];
        globalThis.window = { location: { reload: () => reloads++, assign: url => navigations.push(String(url)) } };
        globalThis.fetch = async (url, options) => {
            calls++; assert.equal(String(url), `${base}Account/ChangeOwnPassword`);
            assert.equal(options.method, 'POST'); assert.equal(options.credentials, 'same-origin');
            return { ok: status === 200, status, headers: new Headers(json ? { 'content-type': 'application/json' } : {}),
                json: async () => json };
        };
    }
    await t.test('successful refresh reloads circuit once', async () => {
        response(200); assert.equal(await changePassword(container), null);
        assert.equal(reloads, 1); assert.equal(calls, 1);
    });
    await t.test('persisted password without refreshed cookie stays for sign-in message', async () => {
        response(200, { requiresSignIn: true, message: 'confirmed' });
        assert.deepEqual(await changePassword(container), { requiresSignIn: true });
        assert.equal(reloads, 0); assert.deepEqual(navigations, []); assert.equal(calls, 1);
    });
    await t.test('validation remains an editable error', async () => {
        response(400, { error: 'validation' });
        assert.deepEqual(await changePassword(container), { error: 'validation' }); assert.equal(reloads, 0);
    });
    await t.test('unauthorized navigates to local login', async () => {
        response(401); await changePassword(container);
        assert.deepEqual(navigations, [`${base}Account/Login`]);
    });
    await t.test('unknown server result never invites blind retry', async () => {
        response(500); const result = await changePassword(container);
        assert.match(result.error, /Проверьте вход с новым паролем/); assert.equal(calls, 1); assert.equal(reloads, 0);
    });
    await t.test('transport failure propagates without retry', async () => {
        response(500); globalThis.fetch = async () => { calls++; throw new Error('synthetic transport failure'); };
        await assert.rejects(() => changePassword(container)); assert.equal(calls, 1); assert.equal(reloads, 0);
    });
});
