export async function changePassword(container) {
    const response = await fetch(new URL("Account/ChangeOwnPassword", document.baseURI), {
        method: "POST",
        credentials: "same-origin",
        body: new FormData(container.querySelector("form"))
    });
    if (response.redirected) {
        window.location.assign(response.url);
        return null;
    }
    if (response.ok) {
        if (response.headers.get("content-type")?.includes("application/json")) {
            const result = await response.json();
            if (result.requiresSignIn) return { requiresSignIn: true };
        }
        // Новый cookie содержит актуальный security stamp; перезагрузка обновляет circuit.
        window.location.reload();
        return null;
    }
    if (response.status === 401) {
        window.location.assign(new URL("Account/Login", document.baseURI));
        return null;
    }
    if (response.headers.get("content-type")?.includes("application/json")) {
        const result = await response.json();
        return { error: result.error || "Не удалось подтвердить результат. Проверьте вход с новым паролем перед повторной сменой." };
    }
    return { error: "Не удалось подтвердить результат. Проверьте вход с новым паролем перед повторной сменой." };
}
