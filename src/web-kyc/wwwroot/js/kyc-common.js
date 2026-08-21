// Shared helpers for the KYC/AML tracking pages.
window.kyc = (function () {
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    function esc(value) {
        return String(value ?? '').replace(/[&<>"']/g, c =>
            ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    function statusClass(status) {
        switch (status) {
            case 'Completed':
            case 'Satisfied':
            case 'Approved':
            case 'Ready to trade':
                return 'ok';
            case 'In Progress':
            case 'In Review':
                return 'active';
            case 'Blocked':
            case 'Rejected':
            case 'Escalated':
                return 'blocked';
            case 'Waived':
            case 'Not Applicable':
                return 'muted';
            default:
                return 'pending';
        }
    }

    async function post(handler, data) {
        const body = new URLSearchParams(data);
        const response = await fetch(`?handler=${handler}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token()
            },
            body
        });
        if (!response.ok) {
            throw new Error(`${handler} failed with ${response.status}`);
        }
        return response.json();
    }

    async function getJson(url) {
        const response = await fetch(url, { headers: { 'Accept': 'application/json' } });
        return response.ok ? response.json() : null;
    }

    // Polls the feed and only re-renders when the server version changes.
    function watch(url, render, intervalMs = 2000) {
        let lastVersion = -1;
        const pill = document.querySelector('[data-live-pill]');

        const tick = async () => {
            try {
                const data = await getJson(url);
                if (!data) return;
                if (data.version !== lastVersion) {
                    lastVersion = data.version;
                    render(data);
                    pill?.classList.add('is-pulsing');
                    setTimeout(() => pill?.classList.remove('is-pulsing'), 900);
                }
            } catch {
                /* keep polling */
            }
        };

        tick();
        setInterval(tick, intervalMs);
        return { refresh: tick, invalidate: () => { lastVersion = -1; } };
    }

    function relativeTime(iso) {
        if (!iso) return '';
        const seconds = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
        if (seconds < 60) return `${Math.floor(seconds)}s ago`;
        if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
        if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
        return new Date(iso).toLocaleDateString();
    }

    function donut(percent, size = 128) {
        const radius = size / 2 - 9;
        const circumference = 2 * Math.PI * radius;
        const dash = (Math.min(100, Math.max(0, percent)) / 100) * circumference;
        return `<svg class="donut" viewBox="0 0 ${size} ${size}" width="${size}" height="${size}" aria-hidden="true">
            <circle class="donut-track" cx="${size / 2}" cy="${size / 2}" r="${radius}"></circle>
            <circle class="donut-value" cx="${size / 2}" cy="${size / 2}" r="${radius}"
                stroke-dasharray="${dash} ${circumference}"></circle>
        </svg>`;
    }

    return { esc, statusClass, post, getJson, watch, relativeTime, donut };
})();
