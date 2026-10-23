let screen;
function togglePassword(event) {
    const button = event.target.closest('[data-account-password]');
    if (!button || !screen.contains(button)) return;
    const input = document.getElementById(button.dataset.accountPassword);
    if (!input || !screen.contains(input)) return;
    const show = input.type === 'password';
    input.type = show ? 'text' : 'password';
    button.setAttribute('aria-pressed', String(show));
    button.setAttribute('aria-label', show ? 'Скрыть пароль' : 'Показать пароль');
    const icon = button.querySelector('.rzi');
    if (icon) icon.textContent = show ? 'visibility_off' : 'visibility';
}
function toggleTheme(event) {
    const button = event.target.closest('[data-account-theme-toggle]');
    if (!button || !screen.contains(button)) return;
    const dark = screen.dataset.accountTheme !== 'dark';
    applyTheme(button, dark);
    const theme = dark ? 'software-dark' : 'software';
    document.cookie = `ApplicationTheme=${theme};path=/;max-age=31536000;SameSite=Lax${location.protocol === 'https:' ? ';Secure' : ''}`;
    // Обновляем существующий Radzen stylesheet без перезагрузки формы и без SignalR.
    const styles = document.querySelectorAll('link[rel="stylesheet"]');
    for (const link of styles) {
        const url = new URL(link.href, document.baseURI);
        if (url.origin === location.origin && /\/_content\/Radzen\.Blazor\/css\/software(?:-dark)?(?:-base)?\.css$/.test(url.pathname)) {
            url.pathname = url.pathname.replace(/software(?:-dark)?(-base)?\.css$/, (_, suffix) => `${theme}${suffix ?? ''}.css`);
            link.href = url.href;
        }
    }
    button.setAttribute('aria-label', dark ? 'Включить светлую тему' : 'Включить тёмную тему');
    button.setAttribute('title', dark ? 'Светлая тема' : 'Тёмная тема');
    const icon = button.querySelector('.rzi');
    if (icon) icon.textContent = dark ? 'light_mode' : 'dark_mode';
}
export function initialize(element) {
    dispose();
    screen = element.closest('.account-screen');
    screen?.addEventListener('click', togglePassword);
    screen?.addEventListener('click', toggleTheme);
}
export function applyTheme(element, dark) {
    element.closest('.account-screen')?.setAttribute('data-account-theme', dark ? 'dark' : 'light');
}
export function dispose() {
    screen?.removeEventListener('click', togglePassword);
    screen?.removeEventListener('click', toggleTheme);
    screen = null;
}
function start() {
    const toggle = document.querySelector('[data-account-theme-toggle]');
    if (toggle) initialize(toggle);
}
if (typeof document !== 'undefined') {
    start();
}
