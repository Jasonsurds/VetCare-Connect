// Shared appointment slot picker.
// Used by Views/Appointments/Create.cshtml and Views/Rewards/Redeem.cshtml.
// Requires these elements to exist on the page:
//   #vetId (select), #appDate (date), #appointmentDate (hidden),
//   #slotArea, #slotGrid, #slotMsg, #selTime
(function () {
    const vetSel = document.getElementById('vetId');
    if (!vetSel) return;

    const dateInput = document.getElementById('appDate');
    const hidden = document.getElementById('appointmentDate');
    const slotArea = document.getElementById('slotArea');
    const slotGrid = document.getElementById('slotGrid');
    const slotMsg = document.getElementById('slotMsg');
    const selTime = document.getElementById('selTime');

    function fmt(d) {
        const m = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        return d.getFullYear() + '-' + m + '-' + day;
    }

    const today = new Date();
    dateInput.min = fmt(today);
    const max = new Date(today);
    max.setDate(max.getDate() + 30);
    dateInput.max = fmt(max);

    // Open the native calendar popup when the box is clicked/focused.
    function openCalendar() {
        if (typeof dateInput.showPicker === 'function') {
            try { dateInput.showPicker(); } catch (e) { /* not visible yet - ignore */ }
        }
    }
    dateInput.addEventListener('click', openCalendar);
    dateInput.addEventListener('focus', openCalendar);

    function clearSlots(message) {
        slotGrid.innerHTML = '';
        slotGrid.querySelectorAll('.active').forEach(function (x) { x.classList.remove('active'); });
        hidden.value = '';
        if (selTime) { selTime.classList.add('d-none'); }
        slotArea.classList.remove('d-none');
        if (message) {
            slotMsg.classList.remove('d-none');
            slotMsg.textContent = message;
        } else {
            slotMsg.classList.add('d-none');
        }
    }

    async function loadSlots() {
        const vet = vetSel.value;
        const d = dateInput.value;

        if (!vet && !d) { slotArea.classList.add('d-none'); hidden.value = ''; return; }
        if (!vet) { clearSlots('Select a veterinarian first, then pick a date to see the open times.'); return; }
        if (!d) { clearSlots('Pick a date above - its available times will appear here.'); return; }

        let json = [];
        try {
            const res = await fetch('/Appointments/AvailableSlots?vetId=' + encodeURIComponent(vet) + '&date=' + encodeURIComponent(d));
            json = await res.json();
        } catch (e) { return; }

        clearSlots();

        if (!json.length) {
            slotMsg.classList.remove('d-none');
            slotMsg.textContent = 'No open slots for this date. Please pick another day.';
            return;
        }

        json.forEach(function (slot) {
            const b = document.createElement('button');
            b.type = 'button';
            b.className = 'btn-vc-outline btn-vc-sm';
            b.textContent = slot.label;
            b.addEventListener('click', function () {
                slotGrid.querySelectorAll('.active').forEach(function (x) { x.classList.remove('active'); });
                b.classList.add('active');
                hidden.value = d + 'T' + slot.value;
                if (selTime) {
                    selTime.querySelector('span').textContent = b.textContent + ' - ' + new Date(d + 'T00:00:00').toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' });
                    selTime.classList.remove('d-none');
                }
            });
            slotGrid.appendChild(b);
        });
    }

    vetSel.addEventListener('change', function () { hidden.value = ''; loadSlots(); });
    dateInput.addEventListener('change', loadSlots);
    dateInput.addEventListener('input', loadSlots);
})();