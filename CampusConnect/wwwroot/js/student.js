// =====================================================================
// student.js - Student dashboard: find faculty, book, my appointments.
// Loaded after api.js and dashboard.js on student-dashboard.html.
// APIs used:
//   GET  /api/faculty, /api/departments, /api/appointment-types
//   GET  /api/availability/faculty/{facultyId}     (only future, free slots)
//   POST /api/appointments                         { availabilitySlotId, appointmentTypeId, reason }
//   GET  /api/appointments/my-appointments         (the server returns ONLY this student's)
//   PUT  /api/appointments/{id}/cancel
// =====================================================================

let allFaculty = [];
let appointmentTypes = [];
let bookingFaculty = null;   // the faculty member whose booking dialog is open

function initRolePage() {
    document.getElementById("facultySearch").addEventListener("input", renderFaculty);
    document.getElementById("departmentFilter").addEventListener("change", renderFaculty);
    document.getElementById("bookingForm").addEventListener("submit", submitBooking);
}

function onSectionShown(name) {
    if (name === "find-faculty") loadFacultySection();
    if (name === "my-appointments") loadMyAppointments();
}

// =====================================================================
// FIND FACULTY (search + department filter happen in the browser)
// =====================================================================
async function loadFacultySection() {
    const table = document.getElementById("facultyTableBody");
    showTableMessage(table, 5, "Loading...");

    try {
        const [faculty, departments] = await Promise.all([
            apiGet("/api/faculty"),
            apiGet("/api/departments")
        ]);
        allFaculty = faculty;
        fillDepartmentFilter(departments);
        renderFaculty();
    } catch (error) {
        showTableMessage(table, 5, "Could not load faculty.");
        showPageMessage("danger", error.messages);
    }
}

function fillDepartmentFilter(departments) {
    const select = document.getElementById("departmentFilter");
    const selected = select.value;
    select.replaceChildren();

    const all = document.createElement("option");
    all.value = "";
    all.textContent = "All departments";
    select.appendChild(all);

    for (const department of departments) {
        const option = document.createElement("option");
        option.value = department.id;
        option.textContent = department.name;
        select.appendChild(option);
    }
    select.value = selected;
}

function renderFaculty() {
    const table = document.getElementById("facultyTableBody");
    const search = document.getElementById("facultySearch").value.trim().toLowerCase();
    const departmentId = document.getElementById("departmentFilter").value;

    const shown = allFaculty.filter(f =>
        f.fullName.toLowerCase().includes(search) &&
        (departmentId === "" || String(f.departmentId) === departmentId));

    document.getElementById("facultyCount").textContent =
        `${shown.length} of ${allFaculty.length} faculty member(s)`;

    if (shown.length === 0) {
        showTableMessage(table, 5, allFaculty.length === 0 ? "No faculty members yet." : "No faculty match your search.");
        return;
    }

    table.replaceChildren();
    for (const member of shown) {
        table.appendChild(createRow([
            createCell(member.fullName, "fw-semibold"),
            createCell(member.departmentName),
            createCell(member.designation),
            createCell(member.officeLocation || "—", "text-muted"),
            createCell(createButton("View slots & book", "btn-primary", () => openBooking(member)), "text-end text-nowrap")
        ]));
    }
}

// =====================================================================
// BOOKING DIALOG
// =====================================================================
async function openBooking(member) {
    bookingFaculty = member;

    const form = document.getElementById("bookingForm");
    form.reset();
    form.classList.remove("was-validated");
    clearAlert(document.getElementById("bookingAlert"));
    document.getElementById("bookingFacultyName").textContent = member.fullName;
    document.getElementById("bookingFacultyInfo").textContent =
        member.designation + ", " + member.departmentName + (member.officeLocation ? " · " + member.officeLocation : "");

    bootstrap.Modal.getOrCreateInstance(document.getElementById("bookingModal")).show();

    try {
        if (appointmentTypes.length === 0) {
            appointmentTypes = await apiGet("/api/appointment-types");
        }
        fillAppointmentTypes();
        await loadSlotsForBooking();
    } catch (error) {
        showAlert(document.getElementById("bookingAlert"), "danger", error.messages);
    }
}

function fillAppointmentTypes() {
    const select = document.getElementById("bookingType");
    select.replaceChildren();

    const placeholder = document.createElement("option");
    placeholder.value = "";
    placeholder.textContent = "Choose a type...";
    select.appendChild(placeholder);

    for (const type of appointmentTypes) {
        const option = document.createElement("option");
        option.value = type.id;
        option.textContent = `${type.name} (${type.durationInMinutes} min)`;
        select.appendChild(option);
    }
}

// Shows the free slots as a list of radio buttons - the student picks one.
async function loadSlotsForBooking() {
    const list = document.getElementById("slotList");
    const bookButton = document.getElementById("bookButton");
    list.replaceChildren(createSmallText("Loading available slots..."));

    const slots = await apiGet(`/api/availability/faculty/${bookingFaculty.id}`);
    list.replaceChildren();

    if (slots.length === 0) {
        list.appendChild(createSmallText("This faculty member has no free slots at the moment. Please check again later."));
        bookButton.disabled = true;
        return;
    }
    bookButton.disabled = false;

    for (const slot of slots) {
        const id = "slot-" + slot.id;

        const input = document.createElement("input");
        input.type = "radio";
        input.className = "btn-check";
        input.name = "slot";
        input.id = id;
        input.value = slot.id;
        input.required = true;   // one radio with "required" makes the whole group required

        const label = document.createElement("label");
        label.className = "btn btn-outline-primary slot-option";
        label.htmlFor = id;
        label.append(
            createSmallText(formatDate(slot.startTime)),
            document.createTextNode(formatTime(slot.startTime) + " – " + formatTime(slot.endTime)));

        list.append(input, label);
    }
}

async function submitBooking(event) {
    event.preventDefault();
    const form = document.getElementById("bookingForm");
    const alertBox = document.getElementById("bookingAlert");
    clearAlert(alertBox);

    form.classList.add("was-validated");
    const selectedSlot = form.querySelector('input[name="slot"]:checked');
    if (!selectedSlot) {
        showAlert(alertBox, "warning", "Please choose a time slot.");
        return;
    }
    if (!form.checkValidity()) {
        return;
    }

    const button = document.getElementById("bookButton");
    setButtonLoading(button, true, "Booking...");
    try {
        // StudentId, faculty and date are decided by the SERVER (from the token and the slot).
        const appointment = await apiPost("/api/appointments", {
            availabilitySlotId: Number(selectedSlot.value),
            appointmentTypeId: Number(document.getElementById("bookingType").value),
            reason: document.getElementById("bookingReason").value.trim()
        });

        closeModal(document.getElementById("bookingModal"));
        showPageMessage("success",
            `Appointment requested with ${appointment.facultyName} on ${formatDateTime(appointment.startTime)}. ` +
            "Status: Pending – you will see it change when the faculty member responds.");
    } catch (error) {
        showAlert(alertBox, "danger", error.messages);
        // If someone else just took the slot (409), show the up-to-date list.
        if (error.status === 409 || error.status === 400) {
            try { await loadSlotsForBooking(); } catch { /* the message above is enough */ }
        }
    } finally {
        setButtonLoading(button, false);
    }
}

// =====================================================================
// MY APPOINTMENTS
// =====================================================================
async function loadMyAppointments() {
    const table = document.getElementById("myAppointmentsBody");
    showTableMessage(table, 6, "Loading...");

    try {
        const appointments = await apiGet("/api/appointments/my-appointments");
        renderMyAppointments(appointments);
    } catch (error) {
        showTableMessage(table, 6, "Could not load your appointments.");
        showPageMessage("danger", error.messages);
    }
}

function renderMyAppointments(appointments) {
    const table = document.getElementById("myAppointmentsBody");

    const count = (status) => appointments.filter(a => a.status === status).length;
    document.getElementById("myStatPending").textContent = count("Pending");
    document.getElementById("myStatApproved").textContent = count("Approved");
    document.getElementById("myStatCompleted").textContent = count("Completed");
    document.getElementById("myStatTotal").textContent = appointments.length;

    if (appointments.length === 0) {
        showTableMessage(table, 6, "You have no appointments yet. Use “Find faculty” to book one.");
        return;
    }

    table.replaceChildren();
    for (const a of appointments) {
        // Only Pending and Approved appointments can be cancelled (same rule as the server).
        const canCancel = a.status === "Pending" || a.status === "Approved";
        const action = canCancel
            ? createButton("Cancel", "btn-outline-danger", () => cancelAppointment(a))
            : createSmallText("—");

        table.appendChild(createRow([
            createCell([formatDate(a.startTime), createSmallText(formatTime(a.startTime) + " – " + formatTime(a.endTime))]),
            createCell([a.facultyName, createSmallText(a.departmentName)]),
            createCell(a.appointmentTypeName),
            createCell(a.reason, "reason-cell"),
            createCell(createStatusBadge(a.status)),
            createCell(action, "text-end")
        ]));
    }
}

async function cancelAppointment(appointment) {
    const confirmed = await confirmAction(
        "Cancel appointment",
        `Cancel your appointment with ${appointment.facultyName} on ${formatDateTime(appointment.startTime)}?`,
        "Cancel appointment");
    if (!confirmed) {
        return;
    }

    try {
        await apiPut(`/api/appointments/${appointment.id}/cancel`);
        showPageMessage("success", "Your appointment was cancelled. The time slot is free for others again.");
        await loadMyAppointments();
    } catch (error) {
        showPageMessage("danger", error.messages);
    }
}
