const ON_LAYOUT_CHANGED = "OnLayoutChanged";

class TableLayoutMonitor {
    #callback;
    #resize;
    #removal;
    #events = new AbortController();
    #stacked;

    constructor(element, callback) {
        this.#callback = callback;
        this.#resize = new ResizeObserver(async entries => {
            // 320 px аналитике, 420 px конструктору фильтра и 5 px разделителю.
            const stacked = entries[0].contentRect.width < 745;
            if (stacked === this.#stacked) return;
            this.#stacked = stacked;
            try { await this.#callback.invokeMethodAsync(ON_LAYOUT_CHANGED, stacked); }
            catch { this.dispose(); }
        });
        if (callback) this.#resize.observe(element);
        const updateTitle = event => {
            const text = event.target.closest?.(".guard-cell-text");
            if (!text || !element.contains(text)) return;
            if (text.scrollWidth > text.clientWidth) text.title = text.textContent;
            else text.removeAttribute("title");
        };
        element.addEventListener("pointerover", updateTitle, { signal: this.#events.signal });
        element.addEventListener("focusin", updateTitle, { signal: this.#events.signal });
        // Очистка также работает, когда серверный circuit уже потерян.
        this.#removal = new MutationObserver(() => {
            if (!element.isConnected) this.dispose();
        });
        this.#removal.observe(document.body, { childList: true, subtree: true });
    }

    dispose() {
        this.#resize.disconnect();
        this.#removal.disconnect();
        this.#events.abort();
    }
}

export function initialize(element, callback) {
    return new TableLayoutMonitor(element, callback);
}
