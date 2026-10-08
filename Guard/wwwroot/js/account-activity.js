export function subscribe(reference) {
  let last = 0;
  const listener = event => {
    if (!event.isTrusted || document.visibilityState !== 'visible') return;
    const now = Date.now();
    if (now - last < 60000) return;
    last = now;
    reference.invokeMethodAsync('RecordActivityAsync').catch(() => {});
  };
  document.addEventListener('pointerdown', listener, true);
  document.addEventListener('keydown', listener, true);
  return { dispose() {
    document.removeEventListener('pointerdown', listener, true);
    document.removeEventListener('keydown', listener, true);
  }};
}
