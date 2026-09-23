window.fitSocialFace = {
    _stream: null,
    start: async function (videoId) {
        try {
            const video = document.getElementById(videoId);
            if (!video) return false;
            const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "user" }, audio: false });
            this._stream = stream;
            video.srcObject = stream;
            await video.play();
            return true;
        } catch (e) {
            console.warn("Camera start failed", e);
            return false;
        }
    },
    capture: function (videoId, canvasId) {
        const video = document.getElementById(videoId);
        const canvas = document.getElementById(canvasId);
        if (!video || !canvas) return null;
        canvas.width = video.videoWidth || 640;
        canvas.height = video.videoHeight || 480;
        const ctx = canvas.getContext("2d");
        // Mirror fix: video is mirrored via CSS scaleX(-1), but canvas draw is raw - mirror it back
        ctx.translate(canvas.width, 0);
        ctx.scale(-1, 1);
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
        return canvas.toDataURL("image/jpeg", 0.85);
    },
    stop: function (videoId) {
        try {
            if (this._stream) {
                this._stream.getTracks().forEach(t => t.stop());
                this._stream = null;
            }
            const video = document.getElementById(videoId);
            if (video) video.srcObject = null;
        } catch {}
    }
};
