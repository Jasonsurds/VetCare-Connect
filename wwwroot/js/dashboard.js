// VetCare Connect — Dashboard interactions & Notification Hub

document.addEventListener('DOMContentLoaded', () => {
    // Confirmation prompts for destructive actions (modern dialog)
    document.querySelectorAll('form[data-confirm]').forEach(form => {
        form.addEventListener('submit', (e) => {
            if (form.dataset.confirmed === '1') return;
            e.preventDefault();

            let details = [];
            const raw = form.getAttribute('data-confirm-details');
            if (raw) {
                try { details = JSON.parse(raw); } catch (err) { details = []; }
            }

            openConfirmDialog({
                title: form.getAttribute('data-confirm-title') || 'Are you sure?',
                message: form.getAttribute('data-confirm') || 'Please confirm you want to continue.',
                okLabel: form.getAttribute('data-confirm-ok') || 'Yes, continue',
                tone: form.getAttribute('data-confirm-tone') || '',
                details: details
            }).then(ok => {
                if (ok) {
                    form.dataset.confirmed = '1';
                    form.submit();
                }
            });
        });
    });

    // Auto-dismiss success alerts after 5 seconds
    document.querySelectorAll('.dash-alert').forEach(alert => {
        setTimeout(() => {
            alert.style.transition = 'opacity .5s ease';
            alert.style.opacity = '0';
            setTimeout(() => alert.remove(), 500);
        }, 5000);
    });

    // ===== Interactive Notifications System =====
    initNotifications();
});

function initNotifications() {
    const bellBtn = document.getElementById('notifBellBtn');
    const popover = document.getElementById('notifPopover');
    const badge = document.getElementById('notifBadge');
    const countLabel = document.getElementById('notifCountLabel');
    const listContainer = document.getElementById('notifListContainer');
    const btnMarkAll = document.getElementById('btnMarkAllRead');

    if (!bellBtn || !popover) return;

    // Toggle popover
    bellBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        const isHidden = popover.classList.contains('d-none');
        if (isHidden) {
            popover.classList.remove('d-none');
            loadNotifications();
        } else {
            popover.classList.add('d-none');
        }
    });

    // Close when clicking outside
    document.addEventListener('click', (e) => {
        if (!popover.contains(e.target) && !bellBtn.contains(e.target)) {
            popover.classList.add('d-none');
        }
    });

    // Mark all as read button
    if (btnMarkAll) {
        btnMarkAll.addEventListener('click', async (e) => {
            e.stopPropagation();
            const token = getAntiForgeryToken();
            try {
                const res = await fetch('/Notifications/MarkAllRead', {
                    method: 'POST',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest',
                        'RequestVerificationToken': token
                    }
                });
                if (res.ok) {
                    loadNotifications();
                }
            } catch (err) {
                console.error('Failed to mark all as read', err);
            }
        });
    }

    // Initial load
    loadNotifications();

    // Poll every 25 seconds for new transactions
    setInterval(loadNotifications, 25000);

    async function loadNotifications() {
        try {
            const res = await fetch('/Notifications/GetLatest');
            if (!res.ok) return;
            const data = await res.json();

            // Update badge
            if (badge) {
                if (data.unreadCount > 0) {
                    badge.textContent = data.unreadCount > 99 ? '99+' : data.unreadCount;
                    badge.classList.remove('d-none');
                } else {
                    badge.classList.add('d-none');
                }
            }

            // Update header count tag
            if (countLabel) {
                if (data.unreadCount > 0) {
                    countLabel.textContent = `${data.unreadCount} new`;
                    countLabel.classList.remove('d-none');
                } else {
                    countLabel.classList.add('d-none');
                }
            }

            // Render list
            if (listContainer) {
                if (!data.notifications || data.notifications.length === 0) {
                    listContainer.innerHTML = `
                        <div class="p-4 text-center text-muted small">
                            <div class="mb-2" style="font-size:1.4rem;">✨</div>
                            <div>No notifications yet.</div>
                        </div>`;
                    return;
                }

                listContainer.innerHTML = data.notifications.map(n => {
                    const icon = getCategoryIcon(n.category);
                    const iconClass = (n.category || 'general').toLowerCase();
                    const unreadClass = n.isRead ? '' : 'unread';

                    return `
                        <a href="${escapeHtml(n.actionUrl)}" class="notif-item ${unreadClass}" data-id="${n.id}">
                            <div class="notif-icon-circle ${iconClass}">
                                <span>${icon}</span>
                            </div>
                            <div style="min-width:0; flex-grow:1;">
                                <div class="notif-item-title">${escapeHtml(n.title)}</div>
                                <div class="notif-item-desc">${escapeHtml(n.message)}</div>
                                <div class="notif-item-time">${escapeHtml(n.timeAgo)}</div>
                            </div>
                        </a>
                    `;
                }).join('');

                // Attach click handler to mark single as read
                listContainer.querySelectorAll('.notif-item').forEach(item => {
                    item.addEventListener('click', async (e) => {
                        const id = item.getAttribute('data-id');
                        if (id) {
                            const token = getAntiForgeryToken();
                            fetch(`/Notifications/MarkRead?id=${id}`, {
                                method: 'POST',
                                headers: {
                                    'X-Requested-With': 'XMLHttpRequest',
                                    'RequestVerificationToken': token
                                }
                            }).catch(() => {});
                        }
                    });
                });
            }
        } catch (err) {
            console.error('Error loading notifications:', err);
        }
    }
}

function getCategoryIcon(cat) {
    switch (cat) {
        case 'Appointment': return '📅';
        case 'Billing': return '💳';
        case 'Reminder': return '💉';
        default: return '🔔';
    }
}

function getAntiForgeryToken() {
    const input = document.querySelector('#afForm input[name="__RequestVerificationToken"]') ||
                  document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
}

function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/[&<>"']/g, function (m) {
        return {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        }[m];
    });
}

// ===== Modern Confirmation Dialog =====
let confirmResolve = null;
let confirmDialog = null;

function ensureConfirmDialog() {
    if (confirmDialog) return confirmDialog;
    confirmDialog = document.createElement('div');
    confirmDialog.className = 'vc-confirm-overlay';
    confirmDialog.setAttribute('role', 'presentation');
    confirmDialog.innerHTML = `
        <div class="vc-confirm-box" role="dialog" aria-modal="true" aria-labelledby="vcConfirmTitle">
            <div class="vc-confirm-icon">
                <svg class="normal" width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>
                <svg class="warn" width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>
            </div>
            <h3 id="vcConfirmTitle" class="vc-confirm-title"></h3>
            <p class="vc-confirm-msg"></p>
            <div class="vc-confirm-details"></div>
            <div class="vc-confirm-actions">
                <button type="button" class="btn-vc-outline" data-action="cancel">Cancel</button>
                <button type="button" class="btn-vc" data-action="ok">Confirm</button>
            </div>
        </div>`;
    document.body.appendChild(confirmDialog);

    const box = confirmDialog.querySelector('.vc-confirm-box');
    const okBtn = box.querySelector('[data-action="ok"]');
    const cancelBtn = box.querySelector('[data-action="cancel"]');

    cancelBtn.addEventListener('click', () => closeConfirmDialog(false));
    okBtn.addEventListener('click', () => closeConfirmDialog(true));

    confirmDialog.addEventListener('click', (e) => {
        if (e.target === confirmDialog) closeConfirmDialog(false);
    });

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && confirmDialog.classList.contains('show')) {
            closeConfirmDialog(false);
        }
    });

    return confirmDialog;
}

function openConfirmDialog(opts) {
    const dialog = ensureConfirmDialog();
    const box = dialog.querySelector('.vc-confirm-box');
    const isDanger = opts.tone === 'danger';

    box.classList.toggle('danger', isDanger);
    box.querySelector('.vc-confirm-title').textContent = opts.title || 'Please confirm';
    box.querySelector('.vc-confirm-msg').textContent = opts.message || 'Are you sure you want to continue?';

    const okBtn = box.querySelector('[data-action="ok"]');
    okBtn.textContent = opts.okLabel || 'Confirm';
    okBtn.className = isDanger ? 'btn-vc-outline btn-vc-danger' : 'btn-vc';

    const detailsCtn = box.querySelector('.vc-confirm-details');
    detailsCtn.innerHTML = '';
    (opts.details || []).forEach(d => {
        const row = document.createElement('div');
        row.className = 'vc-confirm-detail';
        row.innerHTML = `<span class="k">${escapeHtml(d.label)}</span><span class="v">${escapeHtml(d.value)}</span>`;
        detailsCtn.appendChild(row);
    });

    dialog.classList.add('show');
    okBtn.focus();

    return new Promise(resolve => { confirmResolve = resolve; });
}

function closeConfirmDialog(result) {
    if (!confirmDialog) return;
    confirmDialog.classList.remove('show');
    const resolve = confirmResolve;
    confirmResolve = null;
    if (resolve) resolve(result);
}
