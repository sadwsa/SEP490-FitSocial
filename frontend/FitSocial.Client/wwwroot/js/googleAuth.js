// Tích hợp nút đăng nhập Google (OAuth2 redirect flow) cho Blazor WASM.
window.fitSocialGoogle = {
    // OAuth2 redirect flow (không popup, không FedCM): sang trang Google rồi quay về /login-callback.
    // Role được nhúng vào đầu state để trang callback gửi kèm lên backend.
    signInRedirect: function (clientId, redirectUri, role) {
        var safeRole = (role === "COACH" || role === "TRAINEE") ? role : "";
        var state = safeRole + "." + Math.random().toString(36).slice(2) + Date.now().toString(36);
        var url = "https://accounts.google.com/o/oauth2/v2/auth" +
            "?client_id=" + encodeURIComponent(clientId) +
            "&redirect_uri=" + encodeURIComponent(redirectUri) +
            "&response_type=code" +
            "&scope=" + encodeURIComponent("openid email profile") +
            "&state=" + encodeURIComponent(state) +
            "&prompt=select_account";
        window.location.href = url;
    }
};

window.fitSocialStorage = {
    sessionGet: function (key) {
        try {
            var raw = sessionStorage.getItem(key);
            return raw === null || raw === undefined ? null : JSON.parse(raw);
        } catch (e) {
            return null;
        }
    },
    sessionSet: function (key, value) {
        sessionStorage.setItem(key, JSON.stringify(value));
    },
    sessionRemove: function (key) {
        sessionStorage.removeItem(key);
    }
};
