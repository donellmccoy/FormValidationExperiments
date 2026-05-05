// Reports user activity to a .NET callback and broadcasts activity / logout
// across browser tabs via BroadcastChannel so all tabs share a single idle
// timer and sign out together.
//
// Activity events are throttled so we send at most one ping per `throttleMs`
// window. `start()` is idempotent — calling it again replaces the prior
// registration.
const CHANNEL_NAME = 'ect-idle';
let _state = null;

export function start(dotNetRef, throttleMs) {
    stop();

    const events = ['mousemove', 'mousedown', 'keydown', 'touchstart', 'scroll', 'wheel'];
    let lastSent = 0;

    const channel = ('BroadcastChannel' in window) ? new BroadcastChannel(CHANNEL_NAME) : null;

    const ping = (broadcast) => {
        const now = Date.now();
        if (now - lastSent < throttleMs) { return; }
        lastSent = now;
        dotNetRef.invokeMethodAsync('OnUserActivity');
        if (broadcast && channel) {
            channel.postMessage({ type: 'activity', at: now });
        }
    };

    const onActivity = () => ping(true);
    const onVisibility = () => { if (document.visibilityState === 'visible') { ping(true); } };

    const onChannelMessage = (e) => {
        if (!e.data) { return; }
        if (e.data.type === 'activity') {
            // Other tab saw activity — refresh our timer without re-broadcasting.
            ping(false);
        } else if (e.data.type === 'logout') {
            dotNetRef.invokeMethodAsync('OnRemoteLogout');
        }
    };

    events.forEach(e => window.addEventListener(e, onActivity, { passive: true, capture: true }));
    document.addEventListener('visibilitychange', onVisibility);
    if (channel) { channel.addEventListener('message', onChannelMessage); }

    _state = { events, onActivity, onVisibility, channel, onChannelMessage };
}

export function stop() {
    if (!_state) { return; }
    _state.events.forEach(e => window.removeEventListener(e, _state.onActivity, { capture: true }));
    document.removeEventListener('visibilitychange', _state.onVisibility);
    if (_state.channel) {
        _state.channel.removeEventListener('message', _state.onChannelMessage);
        _state.channel.close();
    }
    _state = null;
}

// Broadcast a logout to all other tabs. Safe to call after stop().
export function broadcastLogout() {
    try {
        if ('BroadcastChannel' in window) {
            const c = new BroadcastChannel(CHANNEL_NAME);
            c.postMessage({ type: 'logout', at: Date.now() });
            c.close();
        }
    } catch { /* ignore */ }
}

