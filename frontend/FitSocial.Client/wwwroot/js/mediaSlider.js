/**
 * FitSocial Post Media Slider & Video Controller
 * Supports Next/Prev slide playback, Sound Mute/Unmute, and Feed scroll IntersectionObserver autoplay
 */
window.fitMediaSlider = {
    observer: null,

    initObserver: function () {
        if (this.observer || !('IntersectionObserver' in window)) return;

        var self = this;
        this.observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                var container = entry.target;
                var activeSlide = container.querySelector('.slider-slide.is-active');
                var activeVideo = activeSlide 
                    ? activeSlide.querySelector('video') 
                    : container.querySelector('video.single-media-video');

                if (entry.isIntersecting && entry.intersectionRatio >= 0.3) {
                    if (activeVideo && activeVideo.paused) {
                        activeVideo.muted = true;
                        var playPromise = activeVideo.play();
                        if (playPromise !== undefined) {
                            playPromise.catch(function (e) {
                                console.warn("Scroll autoplay prevented:", e);
                            });
                        }
                    }
                } else {
                    if (activeVideo && !activeVideo.paused) {
                        activeVideo.pause();
                    }
                }
            });
        }, { threshold: [0, 0.3, 0.7] });
    },

    observe: function (containerId) {
        this.initObserver();
        var el = document.getElementById(containerId);
        if (el && this.observer) {
            this.observer.observe(el);
        }
    },

    unobserve: function (containerId) {
        var el = document.getElementById(containerId);
        if (el && this.observer) {
            this.observer.unobserve(el);
        }
    },

    playVideo: function (elementId) {
        var el = document.getElementById(elementId);
        if (!el || typeof el.play !== 'function') {
            setTimeout(function () {
                var retryEl = document.getElementById(elementId);
                if (retryEl && typeof retryEl.play === 'function') {
                    retryEl.currentTime = 0;
                    retryEl.muted = true;
                    var p = retryEl.play();
                    if (p !== undefined) p.catch(function () {});
                }
            }, 60);
            return;
        }

        try {
            el.currentTime = 0;
            el.muted = true; // Muted allows autoplay in all modern browsers without user gesture
            var playPromise = el.play();
            if (playPromise !== undefined) {
                playPromise.catch(function (error) {
                    console.warn("Autoplay play error:", error);
                });
            }
        } catch (e) {
            console.error("Error in playVideo:", e);
        }
    },

    pauseVideo: function (elementId) {
        var el = document.getElementById(elementId);
        if (!el || typeof el.pause !== 'function') return;
        try {
            el.pause();
            el.currentTime = 0;
        } catch (e) {
            console.error("Error in pauseVideo:", e);
        }
    },

    pauseAllVideos: function (containerId) {
        var container = document.getElementById(containerId);
        if (!container) return;
        try {
            var videos = container.querySelectorAll('video');
            for (var i = 0; i < videos.length; i++) {
                videos[i].pause();
                videos[i].currentTime = 0;
            }
        } catch (e) {
            console.error("Error in pauseAllVideos:", e);
        }
    },

    toggleMute: function (containerId) {
        var container = document.getElementById(containerId);
        if (!container) return false;
        var videos = container.querySelectorAll('video');
        var newMuted = false;
        if (videos.length > 0) {
            newMuted = !videos[0].muted;
            for (var i = 0; i < videos.length; i++) {
                videos[i].muted = newMuted;
            }
        }
        return newMuted;
    }
};
