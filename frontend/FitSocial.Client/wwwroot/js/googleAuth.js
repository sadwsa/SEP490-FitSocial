// Tích hợp nút đăng nhập Google (Google Identity Services) cho Blazor WASM.
window.fitSocialGoogle = {
    renderButton: function (elementId, clientId, text, dotNetRef) {
        var el = document.getElementById(elementId);
        if (!el) {
            return false;
        }
        if (el.getAttribute("data-gis-rendered") === "1") {
            return true;
        }
        if (typeof google === "undefined" || !google.accounts || !google.accounts.id) {
            return false;
        }
        try {
            google.accounts.id.initialize({
                client_id: clientId,
                callback: function (response) {
                    dotNetRef.invokeMethodAsync("OnGoogleCredential", response.credential);
                }
            });
            // Google chỉ cho phép width 200-400px: kẹp trong khoảng đó, nút căn giữa khung full-width
            var w = Math.min(Math.max(el.offsetWidth || 300, 200), 400);
            google.accounts.id.renderButton(el, {
                theme: "outline",
                size: "large",
                width: w,
                text: text || "signin_with",
                shape: "rectangular"
            });
            el.setAttribute("data-gis-rendered", "1");
            return true;
        } catch (e) {
            el.innerHTML = "";
            return false;
        }
    }
};
