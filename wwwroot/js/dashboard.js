// VetCare Connect — Dashboard interactions & Notification Hub

document.addEventListener('DOMContentLoaded', () => {
    // Confirmation prompts for destructive actions
    document.querySelectorAll('form[data-confirm]').forEach(form => {
        form.addEventListener('submit', (e) => {
            const message = form.getAttribute('data-confirm') || 'Are you sure?';
            if (!confirm(message)) {
                e.preventDefault();
            }
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
