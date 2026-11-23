// =====================================================================
// admin.js - Admin dashboard: statistics, departments, faculty.
// Loaded after api.js and dashboard.js on admin-dashboard.html.
// APIs used (all Admin-protected on the server):
//   GET  /api/admin/dashboard                        totals
//   GET  /api/admin/dashboard/appointment-statistics counts per status
//   GET  /api/admin/dashboard/recent-appointments    latest 10
//   GET/POST/PUT/DELETE /api/departments
//   GET/POST/PUT/DELETE /api/faculty
// =====================================================================

let departments = [];   // last loaded list, used to fill the department drop-downs

function initRolePage() {
    document.getElementById("refreshOverviewButton").addEventListener("click", loadOverview);
    setUpDepartmentForms();
    setUpFacultyForms();
}

// Called by dashboard.js every time a sidebar section is opened.
function onSectionShown(name) {
    if (name === "overview") loadOverview();
    if (name === "departments") loadDepartments();
    if (name === "faculty") loadFacultySection();
}

// =====================================================================
// OVERVIEW
// =====================================================================
async function loadOverview() {
    const statusTable = document.getElementById("statusTableBody");
    const recentTable = document.getElementById("recentTableBody");
    showTableMessage(statusTable, 3, "Loading...");
    showTableMessage(recentTable, 7, "Loading...");

    try {
        // The three requests are independent, so the browser sends them together.
        const [summary, statistics, recent] = await Promise.all([
            apiGet("/api/admin/dashboard"),
            apiGet("/api/admin/dashboard/appointment-statistics"),
            apiGet("/api/admin/dashboard/recent-appointments")
        ]);

        // Each stat card has data-stat="totalStudents" etc. - the same names as the JSON.
        for (const element of document.querySelectorAll("[data-stat]")) {
            element.textContent = summary[element.dataset.stat];
        }

        renderStatusTable(statistics);
        renderRecentAppointments(recent);
    } catch (error) {
        showTableMessage(statusTable, 3, "Could not load statistics.");
        showTableMessage(recentTable, 7, "Could not load recent appointments.");
        showPageMessage("danger", error.messages);
    }
}

// Appointments per status, shown as a table with simple progress bars (no chart library).
function renderStatusTable(statistics) {
    const table = document.getElementById("statusTableBody");
    table.replaceChildren();

    for (const item of statistics.statuses) {
        const percent = statistics.totalAppointments === 0
            ? 0
            : Math.round((item.count / statistics.totalAppointments) * 100);

        const bar = document.createElement("div");
        bar.className = "progress";
        bar.setAttribute("role", "progressbar");
        bar.setAttribute("aria-label", item.status);
        bar.setAttribute("aria-valuenow", percent);
        bar.setAttribute("aria-valuemin", "0");
        bar.setAttribute("aria-valuemax", "100");
        const fill = document.createElement("div");
        fill.className = "progress-bar " + (STATUS_STYLES[item.status] || "");
        fill.style.width = percent + "%";
        bar.appendChild(fill);

        table.appendChild(createRow([
            createCell(createStatusBadge(item.status)),
            createCell(item.count, "text-end fw-semibold"),
            createCell([bar, createSmallText(percent + "%")], "w-50")
        ]));
    }
}

function renderRecentAppointments(appointments) {
    const table = document.getElementById("recentTableBody");
    if (appointments.length === 0) {
        showTableMessage(table, 7, "No appointments yet.");
        return;
    }

    table.replaceChildren();
    for (const a of appointments) {
        table.appendChild(createRow([
            createCell([formatDate(a.startTime), createSmallText(formatTime(a.startTime) + " – " + formatTime(a.endTime))]),
            createCell(a.studentName),
            createCell([a.facultyName, createSmallText(a.departmentName)]),
            createCell(a.appointmentType),
            createCell(createStatusBadge(a.status)),
            createCell(formatDateTime(a.createdAt), "small text-muted"),
            createCell("#" + a.appointmentId, "small text-muted")
        ]));
    }
}

// =====================================================================
// DEPARTMENTS
// =====================================================================
async function loadDepartments() {
    const table = document.getElementById("departmentTableBody");
    showTableMessage(table, 4, "Loading...");

    try {
        departments = await apiGet("/api/departments");
        renderDepartments();
    } catch (error) {
        showTableMessage(table, 4, "Could not load departments.");
        showPageMessage("danger", error.messages);
    }
}

function renderDepartments() {
    const table = document.getElementById("departmentTableBody");
    if (departments.length === 0) {
        showTableMessage(table, 4, "No departments yet. Add the first one above.");
        return;
    }

    table.replaceChildren();
    for (const department of departments) {
        table.appendChild(createRow([
            createCell(department.name, "fw-semibold"),
            createCell(department.description || "—", "text-muted"),
            createCell(department.facultyCount, "text-center"),
            createCell([
                createButton("Edit", "btn-outline-primary me-1", () => openEditDepartment(department)),
                createButton("Delete", "btn-outline-danger", () => deleteDepartment(department))
            ], "text-end text-nowrap")
        ]));
    }
}

function setUpDepartmentForms() {
    // ----- Add -----
    const addForm = document.getElementById("addDepartmentForm");
    addForm.addEventListener("submit", async (event) => {
        event.preventDefault();
        addForm.classList.add("was-validated");
        if (!addForm.checkValidity()) {
            return;
        }

        const button = document.getElementById("addDepartmentButton");
        setButtonLoading(button, true, "Adding...");
        try {
            const created = await apiPost("/api/departments", {
                name: document.getElementById("departmentName").value.trim(),
                description: document.getElementById("departmentDescription").value.trim() || null
            });
            addForm.reset();
            addForm.classList.remove("was-validated");
            showPageMessage("success", `Department "${created.name}" was added.`);
            await loadDepartments();
        } catch (error) {
            showPageMessage("danger", error.messages);   // e.g. 409 duplicate name
        } finally {
            setButtonLoading(button, false);
        }
    });

    // ----- Edit (modal) -----
    const editForm = document.getElementById("editDepartmentForm");
    editForm.addEventListener("submit", async (event) => {
        event.preventDefault();
        editForm.classList.add("was-validated");
        if (!editForm.checkValidity()) {
            return;
        }

        const id = document.getElementById("editDepartmentId").value;
        const alertBox = document.getElementById("editDepartmentAlert");
        const button = document.getElementById("saveDepartmentButton");
        setButtonLoading(button, true, "Saving...");
        try {
            await apiPut(`/api/departments/${id}`, {
                name: document.getElementById("editDepartmentName").value.trim(),
                description: document.getElementById("editDepartmentDescription").value.trim() || null
            });
            closeModal(document.getElementById("editDepartmentModal"));
            showPageMessage("success", "Department updated.");
            await loadDepartments();
        } catch (error) {
            showAlert(alertBox, "danger", error.messages);
        } finally {
            setButtonLoading(button, false);
        }
    });
}

function openEditDepartment(department) {
    const form = document.getElementById("editDepartmentForm");
    form.classList.remove("was-validated");
    clearAlert(document.getElementById("editDepartmentAlert"));
    document.getElementById("editDepartmentId").value = department.id;
    document.getElementById("editDepartmentName").value = department.name;
    document.getElementById("editDepartmentDescription").value = department.description || "";
    bootstrap.Modal.getOrCreateInstance(document.getElementById("editDepartmentModal")).show();
}

async function deleteDepartment(department) {
    const confirmed = await confirmAction(
        "Delete department",
        `Delete "${department.name}"? This cannot be undone.`,
        "Delete");
    if (!confirmed) {
        return;
    }

    try {
        await apiDelete(`/api/departments/${department.id}`);
        showPageMessage("success", `Department "${department.name}" was deleted.`);
        await loadDepartments();
    } catch (error) {
        showPageMessage("danger", error.messages);   // e.g. 409 "faculty members belong to it"
    }
}

// =====================================================================
// FACULTY
// =====================================================================
async function loadFacultySection() {
    const table = document.getElementById("facultyTableBody");
    showTableMessage(table, 5, "Loading...");

    try {
        // Departments are needed for the drop-downs in the add/edit forms.
        departments = await apiGet("/api/departments");
        fillDepartmentSelect(document.getElementById("facultyDepartment"));
        fillDepartmentSelect(document.getElementById("editFacultyDepartment"));

        const faculty = await apiGet("/api/faculty");
        renderFaculty(faculty);
    } catch (error) {
        showTableMessage(table, 5, "Could not load faculty.");
        showPageMessage("danger", error.messages);
    }
}

function fillDepartmentSelect(select) {
    const selected = select.value;
    select.replaceChildren();

    const placeholder = document.createElement("option");
    placeholder.value = "";
    placeholder.textContent = departments.length ? "Choose a department..." : "Add a department first";
    select.appendChild(placeholder);

    for (const department of departments) {
        const option = document.createElement("option");
        option.value = department.id;
        option.textContent = department.name;
        select.appendChild(option);
    }
    select.value = selected;
}

function renderFaculty(faculty) {
    const table = document.getElementById("facultyTableBody");
    if (faculty.length === 0) {
        showTableMessage(table, 5, "No faculty members yet. Add the first one above.");
        return;
    }

    table.replaceChildren();
    for (const member of faculty) {
        table.appendChild(createRow([
            createCell([member.fullName, createSmallText(member.email)], "fw-semibold"),
            createCell(member.departmentName),
            createCell(member.designation),
            createCell(member.officeLocation || "—", "text-muted"),
            createCell([
                createButton("Edit", "btn-outline-primary me-1", () => openEditFaculty(member)),
                createButton("Delete", "btn-outline-danger", () => deleteFaculty(member))
            ], "text-end text-nowrap")
        ]));
    }
}

function setUpFacultyForms() {
    // ----- Add (creates the login account + Faculty role + profile on the server) -----
    const addForm = document.getElementById("addFacultyForm");
    addForm.addEventListener("submit", async (event) => {
        event.preventDefault();
        addForm.classList.add("was-validated");
        if (!addForm.checkValidity()) {
            return;
        }

        const button = document.getElementById("addFacultyButton");
        setButtonLoading(button, true, "Creating...");
        try {
            const created = await apiPost("/api/faculty", {
                fullName: document.getElementById("facultyFullName").value.trim(),
                email: document.getElementById("facultyEmail").value.trim(),
                password: document.getElementById("facultyPassword").value,
                departmentId: Number(document.getElementById("facultyDepartment").value),
                designation: document.getElementById("facultyDesignation").value.trim(),
                officeLocation: document.getElementById("facultyOffice").value.trim() || null
            });
            addForm.reset();
            addForm.classList.remove("was-validated");
            showPageMessage("success",
                `Faculty account created for ${created.fullName}. They can now log in with ${created.email} and the password you set.`);
            await loadFacultySection();
        } catch (error) {
            showPageMessage("danger", error.messages);   // duplicate email, weak password...
        } finally {
            setButtonLoading(button, false);
        }
    });

    // ----- Edit (modal) -----
    const editForm = document.getElementById("editFacultyForm");
    editForm.addEventListener("submit", async (event) => {
        event.preventDefault();
        editForm.classList.add("was-validated");
        if (!editForm.checkValidity()) {
            return;
        }

        const id = document.getElementById("editFacultyId").value;
        const alertBox = document.getElementById("editFacultyAlert");
        const button = document.getElementById("saveFacultyButton");
        setButtonLoading(button, true, "Saving...");
        try {
            await apiPut(`/api/faculty/${id}`, {
                fullName: document.getElementById("editFacultyFullName").value.trim(),
                departmentId: Number(document.getElementById("editFacultyDepartment").value),
                designation: document.getElementById("editFacultyDesignation").value.trim(),
                officeLocation: document.getElementById("editFacultyOffice").value.trim() || null
            });
            closeModal(document.getElementById("editFacultyModal"));
            showPageMessage("success", "Faculty details updated.");
            await loadFacultySection();
        } catch (error) {
            showAlert(alertBox, "danger", error.messages);
        } finally {
            setButtonLoading(button, false);
        }
    });
}

function openEditFaculty(member) {
    const form = document.getElementById("editFacultyForm");
    form.classList.remove("was-validated");
    clearAlert(document.getElementById("editFacultyAlert"));
    document.getElementById("editFacultyId").value = member.id;
    document.getElementById("editFacultyEmail").value = member.email;
    document.getElementById("editFacultyFullName").value = member.fullName;
    document.getElementById("editFacultyDepartment").value = member.departmentId;
    document.getElementById("editFacultyDesignation").value = member.designation;
    document.getElementById("editFacultyOffice").value = member.officeLocation || "";
    bootstrap.Modal.getOrCreateInstance(document.getElementById("editFacultyModal")).show();
}

async function deleteFaculty(member) {
    const confirmed = await confirmAction(
        "Delete faculty account",
        `Delete the account of ${member.fullName}? They will no longer be able to log in. ` +
        "Accounts with appointments or availability slots cannot be deleted.",
        "Delete");
    if (!confirmed) {
        return;
    }

    try {
        await apiDelete(`/api/faculty/${member.id}`);
        showPageMessage("success", `${member.fullName}'s account was deleted.`);
        await loadFacultySection();
    } catch (error) {
        showPageMessage("danger", error.messages);   // 409 has appointments / slots
    }
}
