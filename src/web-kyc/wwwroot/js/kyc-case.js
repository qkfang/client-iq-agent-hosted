(function () {
    const { esc, statusClass, post, watch, relativeTime, donut } = window.kyc;
    const board = document.querySelector('[data-case-board]');
    if (!board) return;

    const customerId = board.dataset.customerId;
    const ui = { stages: new Set(), categories: new Set(), group: null, filter: 'All', checkGroup: null };

    const feed = watch(`?handler=Feed&customerId=${encodeURIComponent(customerId)}`, render);

    const icon = status => ({ ok: '&#10003;', blocked: '!', active: '', muted: '&#8211;' }[statusClass(status)] ?? '');

    const outcomeClass = outcome => ({
        'Pass': 'ok',
        'Attention': 'active',
        'Fail': 'blocked',
        'Not Applicable': 'muted'
    }[outcome] ?? 'pending');

    function heroCard(c) {
        const stages = c.stages.map(s => {
            const cls = statusClass(s.status);
            const open = ui.stages.has(s.key) && s.detail;
            return `<li class="stage-row is-${cls}${open ? ' is-open' : ''}" data-stage="${esc(s.key)}">
                <button type="button" class="stage-main">
                    <span class="stage-icon">${icon(s.status)}</span>
                    <span class="stage-label">${esc(s.label)}</span>
                    <span class="stage-meta">${esc(s.status)} &middot; ${esc(s.owner)}</span>
                </button>
                ${s.detail ? `<p class="stage-detail">${esc(s.detail)}</p>` : ''}
            </li>`;
        }).join('');

        const outcome = c.finalStatus
            ? `Case closed as <strong>${esc(c.finalStatus)}</strong>. All CIP evidence is recorded against ${esc(c.customerId)}.`
            : `Next step: <strong>${esc(c.nextStepsRequired || '—')}</strong> &mdash; actionable by ${esc(c.actionableBy)}. Every conclusion carries its reasoning and source, so reviewers approve rather than re-key.`;

        return `<article class="hero-card">
            <header class="hero-head">
                <span class="hero-icon">&#128737;</span>
                <div class="hero-title">
                    <h2>KYC / AML &mdash; CIP check progression</h2>
                    <p class="muted">Agent runs enrichment, risk scoring and the CIP decision tree &middot; specialists keep sign-off</p>
                </div>
                <span class="hero-tag">KYC / AML</span>
            </header>
            <div class="hero-body">
                <div class="ring ring-lg">
                    ${donut(c.readinessPercent, 148)}
                    <span class="ring-value">${c.readinessPercent}%</span>
                    <span class="ring-label">Ready-to-trade</span>
                </div>
                <div class="hero-copy">
                    <p>${esc(c.summary)}</p>
                    <p class="muted">${esc(c.jurisdiction)} CIP &middot; ${esc(c.entityType)} &middot; ${esc(c.productScope)} &middot; contact ${esc(c.businessContact)}</p>
                </div>
            </div>
            <ul class="stage-list">${stages}</ul>
            <footer class="hero-outcome"><span class="outcome-icon">&#9678;</span><p>${outcome}</p></footer>
        </article>`;
    }

    function riskCard(c) {
        const risk = c.riskAssessment;
        if (!risk) {
            return section('Risk assessment', '<p class="empty-state">Waiting for the risk assessment agent.</p>');
        }

        const categories = risk.categories.map((cat, index) => {
            const open = ui.categories.has(index);
            return `<li class="score-row is-${esc(cat.indicator)}${open ? ' is-open' : ''}" data-category="${index}">
                <button type="button" class="score-main">
                    <span class="score-name">${esc(cat.name)}</span>
                    <span class="score-bar"><i style="width:${Math.min(100, Math.abs(cat.score) / Math.max(1, cat.max) * 100)}%"></i></span>
                    <span class="score-value">${cat.score} / ${cat.max}</span>
                </button>
                <p class="score-reason">${esc(cat.reason)}</p>
            </li>`;
        }).join('');

        const body = `<div class="risk-summary">
                <span class="chip chip-${risk.riskRating === 'Low' ? 'ok' : risk.riskRating === 'High' ? 'blocked' : 'pending'}">${esc(risk.riskRating)} risk</span>
                <span class="score-total">${risk.totalScore}<small> / ${risk.maxPossibleScore}</small></span>
                <span class="muted">${esc(risk.approvalState)}${risk.reviewer ? ` &middot; ${esc(risk.reviewer)}` : ''}</span>
            </div>
            <ul class="score-list">${categories}</ul>
            ${approvalActions('risk', risk.approvalState)}`;

        return section('Risk assessment', body);
    }

    function cipCard(c) {
        const cip = c.cipResult;
        if (!cip) {
            return section('CIP schedule', '<p class="empty-state">Waiting for the CIP evaluation agent to walk the decision tree.</p>');
        }

        const steps = cip.stepByStepReasoning.map(s => `<li class="reason-step">
                <span class="reason-index">${s.step}</span>
                <div>
                    <p class="reason-question">${esc(s.question)}</p>
                    <p class="reason-answer"><span class="chip chip-${s.answer === 'Yes' ? 'ok' : 'pending'}">${esc(s.answer)}</span> ${esc(s.reasoning)}</p>
                </div>
            </li>`).join('');

        const body = `<div class="clause">
                <span class="clause-number">${esc(cip.clauseNumber)}</span>
                <span class="clause-name">${esc(cip.clauseName)}</span>
            </div>
            <ol class="reason-list">${steps}</ol>
            <p class="clause-conclusion">${esc(cip.conclusion)}</p>
            <div class="source-chips">${cip.sources.map(s => `<span class="chip chip-muted">${esc(s)}</span>`).join('')}</div>
            ${approvalActions('requirements', cip.approvalState)}`;

        return section('CIP schedule', body);
    }

    function requirementsCard(c) {
        if (!c.requirements.length) {
            return section('CIP requirements', '<p class="empty-state">Requirements appear once a CIP schedule is selected.</p>');
        }

        const groups = [...new Set(c.requirements.map(r => r.group))];
        if (!groups.includes(ui.group)) {
            ui.group = groups[0];
        }

        const filters = ['All', 'Outstanding', 'In Review', 'Satisfied'];
        const visible = c.requirements.filter(r =>
            r.group === ui.group && (ui.filter === 'All' || r.status === ui.filter));

        const rows = visible.map(r => `<li class="req-row is-${statusClass(r.status)}">
                <span class="req-id">${r.id}</span>
                <div class="req-body">
                    <p class="req-text">${esc(r.requirement)}</p>
                    <p class="muted">${esc(r.source)}${r.evidence ? ` &middot; ${esc(r.evidence)}` : ''} &middot; ${esc(r.owner)}</p>
                </div>
                <span class="req-actions">
                    ${['Outstanding', 'In Review', 'Satisfied'].map(status =>
                        `<button type="button" class="pill${r.status === status ? ' is-active' : ''}" data-requirement="${r.id}" data-status="${status}">${status}</button>`).join('')}
                </span>
            </li>`).join('');

        const satisfied = c.requirements.filter(r => r.status === 'Satisfied').length;
        const body = `<div class="req-toolbar">
                <span class="tabs">${groups.map(g =>
                    `<button type="button" class="tab${g === ui.group ? ' is-active' : ''}" data-group="${esc(g)}">${esc(g)}</button>`).join('')}</span>
                <span class="tabs tabs-filter">${filters.map(f =>
                    `<button type="button" class="pill${f === ui.filter ? ' is-active' : ''}" data-filter="${esc(f)}">${esc(f)}</button>`).join('')}</span>
            </div>
            <ul class="req-list">${rows || '<li class="empty-state">Nothing in this view.</li>'}</ul>
            <p class="muted req-count">${satisfied} of ${c.requirements.length} requirements satisfied</p>`;

        return section('CIP requirements', body);
    }

    function checksCard(c) {
        const checks = c.policyChecks ?? [];
        if (!checks.length) {
            return section('CIP rulebook', '<p class="empty-state">Rulebook loads when the KYC check is started.</p>');
        }

        const groups = [...new Set(checks.map(p => p.group))];
        if (!groups.includes(ui.checkGroup)) {
            ui.checkGroup = groups[0];
        }

        const tabs = groups.map(g => {
            const inGroup = checks.filter(p => p.group === g);
            const done = inGroup.filter(p => p.outcome !== 'Pending').length;
            return `<button type="button" class="tab${g === ui.checkGroup ? ' is-active' : ''}" data-check-group="${esc(g)}">${esc(g)} <small>${done}/${inGroup.length}</small></button>`;
        }).join('');

        const rows = checks.filter(p => p.group === ui.checkGroup).map(p => `<li class="check-row is-${outcomeClass(p.outcome)}">
                <span class="check-tick">${p.outcome === 'Pending' ? '' : icon(p.outcome === 'Fail' ? 'Blocked' : 'Completed')}</span>
                <div class="check-body">
                    <p class="check-title"><code>${esc(p.id)}</code> ${esc(p.title)}</p>
                    <p class="muted">${esc(p.finding || p.question)}</p>
                    <p class="check-meta"><span class="chip chip-muted">${esc(p.iq)}</span> ${esc(p.source || p.reference)}</p>
                </div>
                <span class="chip chip-${outcomeClass(p.outcome)}">${esc(p.outcome)}</span>
            </li>`).join('');

        const cleared = checks.filter(p => p.outcome !== 'Pending').length;
        const body = `<div class="req-toolbar"><span class="tabs">${tabs}</span></div>
            <ul class="check-list">${rows}</ul>
            <p class="muted req-count">${cleared} of ${checks.length} ${esc(c.jurisdiction)} CIP rules checked</p>`;

        return section('CIP rulebook', body);
    }

    function activityCard(c) {
        const items = c.activity.map(a => `<li class="activity-row is-${statusClass(a.status)}">
                <span class="activity-kind kind-${esc(a.kind)}">${esc(a.kind)}</span>
                <div>
                    <p class="activity-step">${esc(a.step)}</p>
                    <p class="muted">${esc(a.message)}</p>
                    <p class="activity-time">${esc(a.actor)} &middot; ${esc(relativeTime(a.timestampUtc))}</p>
                </div>
            </li>`).join('');

        return section('Agent activity map', `<ul class="activity-list">${items || '<li class="empty-state">No agent activity yet.</li>'}</ul>`);
    }

    function approvalActions(target, state) {
        if (state !== 'Pending') {
            return `<p class="approval-done chip chip-${statusClass(state)}">${esc(state)}</p>`;
        }
        return `<div class="approval-actions">
            <button type="button" class="btn btn-primary" data-approve="${target}" data-state="Approved">Approve</button>
            <button type="button" class="btn btn-secondary" data-approve="${target}" data-state="Rejected">Reject</button>
        </div>`;
    }

    function section(title, body) {
        return `<section class="panel"><h2>${esc(title)}</h2>${body}</section>`;
    }

    function render(c) {
        board.innerHTML = `${heroCard(c)}
            <div class="case-columns">
                <div class="case-col">${checksCard(c)}${riskCard(c)}${cipCard(c)}</div>
                <div class="case-col">${activityCard(c)}</div>
            </div>
            ${requirementsCard(c)}`;
    }

    board.addEventListener('click', async event => {
        const stage = event.target.closest('[data-stage]');
        if (stage) {
            const key = stage.dataset.stage;
            ui.stages.has(key) ? ui.stages.delete(key) : ui.stages.add(key);
            stage.classList.toggle('is-open');
            return;
        }

        const category = event.target.closest('[data-category]');
        if (category) {
            const index = Number(category.dataset.category);
            ui.categories.has(index) ? ui.categories.delete(index) : ui.categories.add(index);
            category.classList.toggle('is-open');
            return;
        }

        const group = event.target.closest('[data-group]');
        if (group) {
            ui.group = group.dataset.group;
            feed.invalidate();
            feed.refresh();
            return;
        }

        const checkGroup = event.target.closest('[data-check-group]');
        if (checkGroup) {
            ui.checkGroup = checkGroup.dataset.checkGroup;
            feed.invalidate();
            feed.refresh();
            return;
        }

        const filter = event.target.closest('[data-filter]');
        if (filter) {
            ui.filter = filter.dataset.filter;
            feed.invalidate();
            feed.refresh();
            return;
        }

        const approve = event.target.closest('[data-approve]');
        if (approve) {
            render(await post('Approve', { customerId, target: approve.dataset.approve, state: approve.dataset.state }));
            return;
        }

        const requirement = event.target.closest('[data-requirement]');
        if (requirement) {
            render(await post('Requirement', {
                customerId,
                requirementId: requirement.dataset.requirement,
                status: requirement.dataset.status
            }));
        }
    });
})();
