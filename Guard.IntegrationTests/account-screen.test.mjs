import { readFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { test } from 'node:test';
const source = await readFile(new URL('../Guard/Components/Account/Shared/AccountThemeToggle.razor.js', import.meta.url), 'utf8');
const ui = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);

test('account local controls preserve form values without server connection', () => {
    const listeners = new Set(), attributes = new Map();
    const input = { type: 'password', value: 'synthetic secret' };
    const icon = { textContent: 'visibility' };
    const eye = { dataset: { accountPassword: 'Input.Password' }, setAttribute: (key,value) => attributes.set(key,value), querySelector: () => icon };
    const screen = { dataset: { accountTheme: 'light' }, contains: value => value === eye || value === input || value === toggle,
        addEventListener: (event,fn) => listeners.add(fn), removeEventListener: (event,fn) => listeners.delete(fn),
        setAttribute: (key,value) => { if (key === 'data-account-theme') screen.dataset.accountTheme = value; } };
    const themeIcon = { textContent: 'dark_mode' };
    const toggle = { closest: () => screen, setAttribute: (key,value) => attributes.set(key,value), querySelector: () => themeIcon };
    const link = { href: 'https://guard.test/_content/Radzen.Blazor/css/software.css?v=11' };
    const baseLink = { href: 'https://guard.test/_content/Radzen.Blazor/css/software-base.css?v=11' };
    globalThis.location = { origin: 'https://guard.test', protocol: 'https:' };
    globalThis.document = { getElementById: () => input, querySelectorAll: () => [link, baseLink], baseURI: 'https://guard.test/' };
    ui.initialize(toggle);
    function click(kind) { for (const fn of listeners) fn({ target: { closest: selector => selector === kind ? (kind === '[data-account-password]' ? eye : toggle) : null } }); }
    click('[data-account-password]');
    assert.equal(input.type, 'text'); assert.equal(attributes.get('aria-pressed'), 'true');
    assert.equal(icon.textContent, 'visibility_off');
    click('[data-account-theme-toggle]');
    assert.equal(screen.dataset.accountTheme, 'dark');
    assert.equal(link.href, 'https://guard.test/_content/Radzen.Blazor/css/software-dark.css?v=11');
    assert.equal(baseLink.href, 'https://guard.test/_content/Radzen.Blazor/css/software-dark-base.css?v=11');
    assert.match(document.cookie, /^ApplicationTheme=software-dark;path=\/;max-age=31536000;SameSite=Lax;Secure$/);
    assert.equal(input.value, 'synthetic secret');
    click('[data-account-theme-toggle]');
    assert.equal(screen.dataset.accountTheme, 'light');
    assert.equal(link.href, 'https://guard.test/_content/Radzen.Blazor/css/software.css?v=11');
    assert.equal(baseLink.href, 'https://guard.test/_content/Radzen.Blazor/css/software-base.css?v=11');
    assert.equal(attributes.get('aria-label'), 'Включить тёмную тему');
    click('[data-account-password]');
    assert.equal(input.type, 'password'); assert.equal(attributes.get('aria-label'), 'Показать пароль');
    ui.dispose(); assert.equal(listeners.size, 0);
});
