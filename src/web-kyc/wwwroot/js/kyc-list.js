(function () {
    const { esc, statusClass, post, watch, relativeTime, donut } = window.kyc;
    const list = document.querySelector('[data-customer-list]');
    if (!list) return;

    const feed = watch('?handler=Feed', data => render(data.customers));

    function initials(name) {
        return name.split(/\s+/).filter(Boolean).slice(0, 2).map(w => w[0]).join('').toUpperCase();
    }

    function metric(label, value) {
        return value
            ? `<div class="metric"><dt>${esc(label)}</dt><dd>${esc(value)}</dd></div>`
            : '';
    }

    function render(customers) {
        if (!customers.length) {
            list.innerHTML = '<p class="empty-state">No customers in scope.</p>';
            return;
        }

        list.innerHTML = customers.map(c => {
            const started = c.caseStatus !== 'Not started';
            return `<article class="customer-card is-${statusClass(c.caseStatus)}">
                <header class="customer-head">
                    <span class="avatar">${esc(initials(c.customerName))}</span>
                    <div class="customer-title">
                        <h2>${esc(c.customerName)}</h2>
                        <p class="muted">${esc(c.customerId)} &middot; ${esc(c.jurisdiction)} &middot; ${esc(c.entityType)}</p>
                    </div>
                    <span class="chip chip-${statusClass(c.caseStatus)}">${esc(c.caseStatus)}</span>
                </header>
                <div class="customer-body">
                    <div class="ring ring-sm">
                        ${donut(c.readinessPercent, 76)}
                        <span class="ring-value">${c.readinessPercent}%</span>
                    </div>
                    <dl class="metrics">
                        ${metric('Current stage', started ? c.currentStage : '—')}
                        ${metric('Next step', started ? c.nextStepsRequired : 'Awaiting KYC start')}
                        ${metric('Risk rating', c.riskRating)}
                        ${metric('CIP schedule', c.cipClause)}
                        ${metric('Open requirements', started && c.openRequirements ? String(c.openRequirements) : '')}
                    </dl>
                </div>
                <footer class="customer-foot">
                    <span class="muted">${started ? `Updated ${esc(relativeTime(c.lastUpdatedUtc))} by ${esc(c.lastUpdatedBy)}` : `Contact ${esc(c.businessContact)}`}</span>
                    <span class="actions">
                        <button type="button" class="btn btn-secondary" data-start="${esc(c.customerId)}">${started ? 'Restart' : 'Start KYC check'}</button>
                        <a class="btn btn-primary${started ? '' : ' is-disabled'}" href="/case/${encodeURIComponent(c.customerId)}">Open</a>
                    </span>
                </footer>
            </article>`;
        }).join('');
    }

    list.addEventListener('click', async event => {
        const button = event.target.closest('[data-start]');
        if (!button) return;

        button.disabled = true;
        try {
            const customerId = button.dataset.start;
            await post('Start', { customerId });
            window.location.href = `/case/${encodeURIComponent(customerId)}`;
        } finally {
            button.disabled = false;
            feed.invalidate();
        }
    });
})();
