/**
 * Infinite Scroll with Prefetch Pagination for FitSocial Feed.
 * Uses standard IntersectionObserver to prefetch subsequent batches when
 * the sentinel post (the 7th post of the latest batch) intersects the viewport.
 */
window.fitSocialInfiniteScroll = {
    observer: null,
    dotNetHelper: null,
    observedElementId: null,

    init: function (dotNetHelper) {
        this.dotNetHelper = dotNetHelper;
        if (this.observer) {
            this.observer.disconnect();
        }

        var self = this;
        this.observer = new IntersectionObserver(function (entries) {
            for (var i = 0; i < entries.length; i++) {
                if (entries[i].isIntersecting) {
                    var el = entries[i].target;
                    if (self.observer && el) {
                        self.observer.unobserve(el);
                    }
                    self.observedElementId = null;

                    if (self.dotNetHelper) {
                        self.dotNetHelper.invokeMethodAsync('OnPrefetchTriggered');
                    }
                    break;
                }
            }
        }, {
            root: null,
            rootMargin: '0px 0px 200px 0px', // trigger slightly before element scrolls into view
            threshold: 0.1
        });
    },

    observeTrigger: function (elementId) {
        if (!this.observer || !elementId) return;
        if (this.observedElementId === elementId) return;

        this.unobserveCurrent();
        var self = this;
        var el = document.getElementById(elementId);
        if (el) {
            self.observedElementId = elementId;
            self.observer.observe(el);
        } else {
            requestAnimationFrame(function () {
                var delayedEl = document.getElementById(elementId);
                if (delayedEl && self.observer && !self.observedElementId) {
                    self.observedElementId = elementId;
                    self.observer.observe(delayedEl);
                }
            });
        }
    },

    unobserveCurrent: function () {
        if (this.observer && this.observedElementId) {
            var el = document.getElementById(this.observedElementId);
            if (el) {
                this.observer.unobserve(el);
            }
            this.observedElementId = null;
        }
    },

    dispose: function () {
        if (this.observer) {
            this.observer.disconnect();
            this.observer = null;
        }
        this.dotNetHelper = null;
        this.observedElementId = null;
    }
};
