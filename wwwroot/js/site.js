// VetCare Connect Interactive Engine

document.addEventListener('DOMContentLoaded', () => {
    // 1. Role Portal Switcher Data & Handler
    const roleData = {
        owner: {
            title: 'Pet Owner Portal',
            desc: 'Easily schedule appointments, track pet health records, manage billing, and receive vaccination reminders.',
            pills: ['Pet Profiles', 'Book Appointments', 'View Records', 'Pay Bills', 'Vet Direct Chat'],
            btnText: 'Open Pet Owner Portal',
            img: '/images/happy-dog.png',
            icon: `<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path><polyline points="9 22 9 12 15 12 15 22"></polyline></svg>`
        },
        vet: {
            title: 'Veterinarian Portal',
            desc: 'Access patient medical histories, write electronic prescriptions, record treatment plans, and consult with pet parents directly.',
            pills: ['Patient History', 'e-Prescriptions', 'Treatment Logs', 'Lab Results', 'Tele-Consult'],
            btnText: 'Open Vet Portal',
            img: '/images/vet-care.png',
            icon: `<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 12h-4l-3 9L9 3l-3 9H2"></path></svg>`
        },
        staff: {
            title: 'Clinic Staff Portal',
            desc: 'Manage daily front-desk operations, check-in patients, manage room schedules, and coordinate vet shifts.',
            pills: ['Reception Check-in', 'Room Allocation', 'Shift Schedules', 'Client Care', 'Queue Management'],
            btnText: 'Open Staff Portal',
            img: '/images/vet-care.png',
            icon: `<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path><circle cx="9" cy="7" r="4"></circle><path d="M23 21v-2a4 4 0 0 0-3-3.87"></path><path d="M16 3.13a4 4 0 0 1 0 7.75"></path></svg>`
        },
        admin: {
            title: 'Administrator Portal',
            desc: 'Full control over clinic settings, staff permissions, multi-branch management, revenue analytics, and audit logs.',
            pills: ['Role Permissions', 'Multi-Branch', 'Financial Dashboard', 'System Audit', 'Staff Management'],
            btnText: 'Open Admin Portal',
            img: '/images/happy-dog.png',
            icon: `<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="3"></circle><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path></svg>`
        },
        supplier: {
            title: 'Supplier Portal',
            desc: 'Streamline pharmaceutical supplies, process clinic purchase orders, track deliveries, and manage vendor catalogs.',
            pills: ['Purchase Orders', 'Supply Catalog', 'Delivery Tracking', 'Invoicing', 'Vendor Analytics'],
            btnText: 'Open Supplier Portal',
            img: '/images/vet-care.png',
            icon: `<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path></svg>`
        }
    };

    const tabBtns = document.querySelectorAll('.portal-tab-btn');
    const portalRoleTitle = document.getElementById('portalRoleTitle');
    const portalRoleDesc = document.getElementById('portalRoleDesc');
    const portalPillsContainer = document.getElementById('portalPillsContainer');
    const portalCtaBtn = document.getElementById('portalCtaBtn');
    const portalRoleIcon = document.getElementById('portalRoleIcon');
    const portalImg = document.getElementById('portalImg');

    tabBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            tabBtns.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');

            const roleKey = btn.getAttribute('data-role');
            const data = roleData[roleKey];
            if (data) {
                if (portalRoleTitle) portalRoleTitle.textContent = data.title;
                if (portalRoleDesc) portalRoleDesc.textContent = data.desc;
                if (portalCtaBtn) portalCtaBtn.innerHTML = `${data.btnText} <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="5" y1="12" x2="19" y2="12"></line><polyline points="12 5 19 12 12 19"></polyline></svg>`;
                if (portalRoleIcon) portalRoleIcon.innerHTML = data.icon;
                if (portalImg) portalImg.src = data.img;

                if (portalPillsContainer) {
                    portalPillsContainer.innerHTML = data.pills.map(p => `<span class="portal-subpill">${p}</span>`).join('');
                }
            }
        });
    });

    // 2. Navbar scroll state
    const navbar = document.querySelector('.vc-navbar');
    window.addEventListener('scroll', () => {
        if (window.scrollY > 40) {
            navbar.classList.add('scrolled');
        } else {
            navbar.classList.remove('scrolled');
        }
    });

    // 3. Toast Notification Helper
    window.showToast = function(message) {
        let toast = document.getElementById('vcToast');
        if (!toast) {
            toast = document.createElement('div');
            toast.id = 'vcToast';
            toast.className = 'vc-toast';
            document.body.appendChild(toast);
        }
        toast.innerHTML = `<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg> <span>${message}</span>`;
        toast.classList.add('show');
        setTimeout(() => {
            toast.classList.remove('show');
        }, 3500);
    };

    // 4. Modal Triggers
    const appointmentModal = document.getElementById('appointmentModal');
    const openModalBtns = document.querySelectorAll('.open-appointment-modal');
    const closeModalBtns = document.querySelectorAll('.close-modal');

    openModalBtns.forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            if (appointmentModal) appointmentModal.classList.add('active');
        });
    });

    closeModalBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            if (appointmentModal) appointmentModal.classList.remove('active');
        });
    });

    // Close on backdrop click
    if (appointmentModal) {
        appointmentModal.addEventListener('click', (e) => {
            if (e.target === appointmentModal) {
                appointmentModal.classList.remove('active');
            }
        });
    }

    // Appointment form submit
    const bookingForm = document.getElementById('bookingForm');
    if (bookingForm) {
        bookingForm.addEventListener('submit', (e) => {
            e.preventDefault();
            appointmentModal.classList.remove('active');
            window.showToast('Appointment successfully scheduled! We sent a confirmation to your email.');
            bookingForm.reset();
        });
    }

    // Action Pills Toast handler
    document.querySelectorAll('.btn-action-tag, .module-pill-tag').forEach(tag => {
        tag.addEventListener('click', () => {
            const text = tag.textContent.trim();
            window.showToast(`Selected module: ${text}`);
        });
    });
});
