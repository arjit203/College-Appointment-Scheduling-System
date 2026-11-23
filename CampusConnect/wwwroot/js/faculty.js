// =====================================================================
// faculty.js - Faculty dashboard: overview, availability slots, appointment requests.
// Loaded after api.js and dashboard.js on faculty-dashboard.html.
// APIs used (the server only ever returns/changes THIS faculty member's data):
//   GET    /api/availability/my-slots
//   POST   /api/availability            PUT/DELETE /api/availability/{id}
//   GET    /api/appointments/faculty/my-appointments
//   PUT    /api/appointments/{id}/status   { "status": "Approved" | "Rejected" | "Completed" }
// =====================================================================

let appointments = [];   // last loaded list (used by the overview and the filter)

function initRolePage() {
    setUpSlotForms();

    document.getElementById("statusFilter").addEventListener("change", renderAppointments);
    document.getElementById("goToRequestsButton").addEventListener("click", () => {
        document.getElementById("statusFilter").value = "Pending";
        showSection("appointments");
    });
}

function onSectionShown(name) {
    if (name === "overview") loadOverview();
    if (name === "availability") loadSlots();
    if (name === "appointments") loadAppointments();
}

// =====================================================================
// OVERVIEW - counted in the browser from this faculty member's own appointments
// =====================================================================
async function loadOverview() {
    try {
        appointments = await apiGet("/api/appointments/faculty/my-appointments");

        const count = (status) => appointments.filter(a => a.status === status).length;
        document.getElementById("statTotal").textContent = appointments.length;
        document.getElementById("statPending").textContent = count("Pending");
        document.getElementById("statApproved").textContent = count("Approved");
        document.getElementById("statCompleted").textContent = count("Completed");

        renderUpcoming();
    } catch (error) {
        showPageMessage("danger", error.messages);
    }
}

// The next approved meetings (today or later).
function renderUpcoming() {
    const table = document.getElementById("upcomingTableBody");
    const now = new Date();
    const upcoming = appointments
        .filter(a => a.status === "Approved" && new Date(a.endTime) >= now)
        .sort((a, b) => new Date(a.startTime) - new Date(b.startTime))
        .slice(0, 5);

    if (upcoming.length === 0) {
        showTableMessage(table, 3, "No upcoming approved appointments.");
        return;
    }

    table.replaceChildren();
    for (const a of upcoming) {
        table.appendChild(createRow([
            createCell([formatDate(a.startTime), createSmallText(formatTime(a.startTime) + " – " + formatTime(a.endTime))]),
            createCell([a.studentName, createSmallText(a.studentEmail)]),
            createCell(a.appointmentTypeName)
        ]));
    }
}

// =====================================================================
// AVAILABILITY SLOTS
// =====================================================================
async function loadSlots() {
    const table = document.getElementById("slotTableBody");
    showTableMessage(table, 5, "Loading...");

    try {
        const slots = await apiGet("/api/availability/my-slots");
        renderSlots(slots);
    } catch (error) {
        showTableMessage(table, 5, "Could not load your slots.");
        showPageMessage("danger", error.messages);
    }
}

function renderSlots(slots) {
    const table = document.getElementById("slotTableBody");
    if (slots.length === 0) {
        showTableMessage(table, 5, "You have no slots yet. Add one above so students can book you.");
        return;
    }

    const now = new Date();
    table.replaceChildren();

    for (const slot of slots) {
        const isPast = new Date(slot.startTime) <= now;

        let statusBadge;
        if (slot.isBooked) {
            statusBadge = createBadge("Booked", "text-bg-primary");
        } else if (isPast) {
            statusBadge = createBadge("Past", "text-bg-light");
        } else {
            statusBadge = createBadge("Available", "text-bg-success");
        }

        // Booked and past slots can't be changed - the buttons are disabled here,
        // and the server refuses as well (409 / 400) if someone tries anyway.
        const locked = slot.isBooked || isPast;
        const editButton = createButton("Edit", "btn-outline-primary me-1", () => openEditSlot(slot));
        const deleteButton = createButton("Delete", "btn-outline-danger", () => deleteSlot(slot));
        editButton.disabled = locked;
        deleteButton.disabled = locked;
        if (locked) {
            const reason = slot.isBooked ? "Booked slots cannot be changed" : "Past slots cannot be changed";
            editButton.title = reason;
            deleteButton.title = reason;
        }

        table.appendChild(createRow([
            createCell(formatDate(slot.startTime), "fw-semibold"),
            createCell(formatTime(slot.startTime) + " – " + formatTime(slot.endTime)),
            createCell(slot.durationInMinutes + " min"),
            createCell(statusBadge),
            createCell([editButton, deleteButton], "text-end text-nowrap")
        ]));
    }
}

function createBadge(text, className) {
    const badge = document.createElement("span");
    badge.className = "badge " + className;
    badge.textContent = text;
    return badge;
}

function setUpSlotForms() {
    // Dates before today can't be picked (the server checks "in the future" too).
    document.getElementById("slotDate").min = todayForDateInput();
    document.getElementById("editSlotDate").min = todayForDateInput();

    // ----- Add -----
    const addForm = document.getElementById("addSlotForm");
    addForm.addEventListener("submit", async (event) => {
        event.preventDefault();
        addForm.classList.add("was-validated");
        if (!addForm.checkValidity()) {
            return;
        }

        const date = document.getElementById("slotDate").value;
        const button = document.getElementById("addSlotButton");
        setButtonLoading(button, true, "Adding...");
        try {
            await apiPost("/api/availability", {
                startTime: toApiDateTime(date, document.getElementById("slotStart").value),
                endTime: toApiDateTime(date, document.getElementById("slotEnd").value)
            });
            // Keep the date so several slots on the same day are quick to add.
            document.getElementById("slotStart").value = document.getElementById("slotEnd").value;
            document.getElementById("slotEnd").value = "";
            addForm.classList.remove("was-validated");
            showPageMessage("success", "Slot added. Students can now book it.");
            await loadSlots();
        } catch (error) {
            showPageMessage("danger", error.messages);   // overlap (409), past / end before start (400)
        } finally {
            setButtonLoading(button, false);
        }
    });

    // ----- Edit (modal) -----
    const editForm = document.getElementById("editSlotForm");
    editForm.addEventListener("submit", async (event) => {
        event.preventDefault();
        editForm.classList.add("was-validated");
        if (!editForm.checkValidity()) {
            return;
        }

        const id = document.getElementById("editSlotId").value;
        const date = document.getElementById("editSlotDate").value;
        const alertBox = document.getElementById("editSlotAlert");
        const button = document.getElementById("saveSlotButton");
        setButtonLoading(button, true, "Saving...");
        try {
            await apiPut(`/api/availability/${id}`, {
                startTime: toApiDateTime(date, document.getElementById("editSlotStart").value),
                endTime: toApiDateTime(date, document.getElementById("editSlotEnd").value)
            });
            closeModal(document.getElementById("editSlotModal"));
            showPageMessage("success", "Slot updated.");
            await loadSlots();
        } catch (error) {
            showAlert(alertBox, "danger", error.messages);
        } finally {
            setButtonLoading(button, false);
        }
    });
}

function openEditSlot(slot) {
    document.getElementById("editSlotForm").classList.remove("was-validated");
    clearAlert(document.getElementById("editSlotAlert"));
    document.getElementById("editSlotId").value = slot.id;
    // API value "2026-12-01T10:00:00" -> date "2026-12-01", time "10:00"
    document.getElementById("editSlotDate").value = slot.startTime.substring(0, 10);
    document.getElementById("editSlotStart").value = slot.startTime.substring(11, 16);
    document.getElementById("editSlotEnd").value = slot.endTime.substring(11, 16);
    bootstrap.Modal.getOrCreateInstance(document.getElementById("editSlotModal")).show();
}

async function deleteSlot(slot) {
    const confirmed = await confirmAction(
        "Delete slot",
        `Delete the slot on ${formatDate(slot.startTime)} at ${formatTime(slot.startTime)}?`,
        "Delete");
    if (!confirmed) {
        return;
    }

    try {
        await apiDelete(`/api/availability/${slot.id}`);
        showPageMessage("success", "Slot deleted.");
        await loadSlots();
    } catch (error) {
        showPageMessage("danger", error.messages);   // booked / has appointment history (409)
    }
}

// =====================================================================
// APPOINTMENT REQUESTS
// =====================================================================
async function loadAppointments() {
    const table = document.getElementById("appointmentTableBody");
    showTableMessage(table, 6, "Loading...");

    try {
        appointments = await apiGet("/api/appointments/faculty/my-appointments");
        renderAppointments();
    } catch (error) {
        showTableMessage(table, 6, "Could not load appointments.");
        showPageMessage("danger", error.messages);
    }
}

function renderAppointments() {
    const table = document.getElementById("appointmentTableBody");
    const filter = document.getElementById("statusFilter").value;
    const shown = filter ? appointments.filter(a => a.status === filter) : appointments;

    if (shown.length === 0) {
        showTableMessage(table, 6, filter ? `No ${filter.toLowerCase()} appointments.` : "No appointment requests yet.");
        return;
    }

    table.replaceChildren();
    for (const a of shown) {
        table.appendChild(createRow([
            createCell([formatDate(a.startTime), createSmallText(formatTime(a.startTime) + " – " + formatTime(a.endTime))]),
            createCell([a.studentName, createSmallText(a.studentEmail)]),
            createCell(a.appointmentTypeName),
            createCell(a.reason, "reason-cell"),
            createCell(createStatusBadge(a.status)),
            createCell(createActionButtons(a), "text-end text-nowrap")
        ]));
    }
}

// Only the actions allowed for the current status are shown:
//   Pending  -> Approve / Reject
//   Approved -> Mark completed
// (The server enforces the same rules.)
function createActionButtons(appointment) {
    const buttons = [];

    if (appointment.status === "Pending") {
        buttons.push(createButton("Approve", "btn-success me-1",
            () => changeStatus(appointment, "Approved")));
        buttons.push(createButton("Reject", "btn-outline-danger",
            () => changeStatus(appointment, "Rejected")));
    } else if (appointment.status === "Approved") {
        buttons.push(createButton("Mark completed", "btn-outline-success",
            () => changeStatus(appointment, "Completed")));
    } else {
        buttons.push(createSmallText("No actions"));
    }

    return buttons;
}

async function changeStatus(appointment, newStatus) {
    const questions = {
        Approved: ["Approve appointment", `Approve ${appointment.studentName}'s appointment on ${formatDateTime(appointment.startTime)}?`, "Approve", "btn-success"],
        Rejected: ["Reject appointment", `Reject ${appointment.studentName}'s request? The time slot will become free again.`, "Reject", "btn-danger"],
        Completed: ["Mark as completed", `Mark the meeting with ${appointment.studentName} as completed?`, "Mark completed", "btn-success"]
    };
    const [title, message, buttonText, buttonClass] = questions[newStatus];

    if (!await confirmAction(title, message, buttonText, buttonClass)) {
        return;
    }

    try {
        await apiPut(`/api/appointments/${appointment.id}/status`, { status: newStatus });
        showPageMessage("success", `Appointment ${newStatus.toLowerCase()}.`);
        await loadAppointments();
    } catch (error) {
        showPageMessage("danger", error.messages);   // e.g. 409 invalid status change
    }
}
