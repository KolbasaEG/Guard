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
        return result.error || "Не удалось изменить пароль. Повторите попытку.";
    }
    return "Не удалось изменить пароль. Обновите страницу и повторите попытку.";
}
