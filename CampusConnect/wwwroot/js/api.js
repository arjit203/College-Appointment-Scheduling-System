// =====================================================================
// api.js - shared code used by EVERY page
//   1. Remembering who is logged in (token + user info in localStorage)
//   2. Calling the backend API (apiGet / apiPost / apiPut / apiDelete)
//   3. Turning backend errors into readable messages
//   4. Small UI helpers (alerts, loading buttons)
//
// SECURITY NOTE: everything here is for convenience and navigation only.
// The real security is on the server: every protected API checks the JWT
// and the user's role. Changing values in the browser cannot bypass that.
// =====================================================================

const TOKEN_KEY = "campusconnect_token";
const USER_KEY = "campusconnect_user";

// ---------------------------------------------------------------------
// 1. Session (who is logged in)
// ---------------------------------------------------------------------

// Called after a successful login. We store the token and the user's
// public details - NEVER the password.
function saveSession(loginResponse) {
    localStorage.setItem(TOKEN_KEY, loginResponse.token);
    localStorage.setItem(USER_KEY, JSON.stringify(loginResponse.user));
}

function getToken() {
    return localStorage.getItem(TOKEN_KEY);
}

function getCurrentUser() {
    const json = localStorage.getItem(USER_KEY);
    if (!json) {
        return null;
    }
    try {
        return JSON.parse(json);
    } catch {
        return null;
    }
}

function clearSession() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
}

// A JWT looks like "header.payload.signature". The payload is Base64URL-encoded
// JSON, so the browser can READ it (but cannot change it without breaking the
// signature). We only read "exp" - the expiry time in seconds since 1970.
function isTokenExpired(token) {
    try {
        const payloadPart = token.split(".")[1];
        const base64 = payloadPart.replace(/-/g, "+").replace(/_/g, "/");
        const payload = JSON.parse(atob(base64));
        return Date.now() >= payload.exp * 1000;
    } catch {
        return true; // anything unreadable is treated as expired
    }
}

function isLoggedIn() {
    const token = getToken();
    return token !== null && !isTokenExpired(token);
}

// The backend sends a LIST of roles. A user normally has one; if there were
// ever more, the most powerful one decides which dashboard to open.
function getPrimaryRole(user) {
    if (!user || !Array.isArray(user.roles)) {
        return null;
    }
    if (user.roles.includes("Admin")) return "Admin";
    if (user.roles.includes("Faculty")) return "Faculty";
    if (user.roles.includes("Student")) return "Student";
    return null;
}

function getDashboardUrl(role) {
    switch (role) {
        case "Admin": return "admin-dashboard.html";
        case "Faculty": return "faculty-dashboard.html";
        case "Student": return "student-dashboard.html";
        default: return "index.html";
    }
}

// ---------------------------------------------------------------------
// 2. Calling the API
// ---------------------------------------------------------------------

// A custom error type, so pages can show the message and check the status code.
class ApiError extends Error {
    constructor(status, messages) {
        super(messages.join(" "));
        this.status = status;       // HTTP status code (0 = could not reach the server)
        this.messages = messages;   // list of readable messages
    }
}

// The one function that actually calls fetch().
//   method: "GET", "POST", "PUT" or "DELETE"
//   url:    e.g. "/api/auth/me"
//   body:   a JavaScript object to send as JSON (optional)
//   sendToken: false for login/register (they don't need a token)
async function apiRequest(method, url, body, sendToken = true) {
    const headers = { "Accept": "application/json" };

    if (body !== undefined) {
        headers["Content-Type"] = "application/json";
    }

    const token = getToken();
    if (sendToken && token) {
        headers["Authorization"] = "Bearer " + token;   // how the backend recognises us
    }

    let response;
    try {
        response = await fetch(url, {
            method: method,
            headers: headers,
            body: body !== undefined ? JSON.stringify(body) : undefined
        });
    } catch {
        // fetch only throws when the server can't be reached at all.
        throw new ApiError(0, ["Cannot reach the server. Please check that CampusConnect is running."]);
    }

    // Our token was rejected (expired or invalid) -> log out and go to the login page.
    if (response.status === 401 && sendToken && token) {
        clearSession();
        window.location.replace("index.html?reason=expired");
        throw new ApiError(401, ["Your session has expired. Please log in again."]);
    }

    // Read the body (some responses, like 204 No Content, have none).
    const text = await response.text();
    let data = null;
    if (text) {
        try {
            data = JSON.parse(text);
        } catch {
            data = text;
        }
    }

    // fetch does NOT throw for 400/404/500 - we must check response.ok ourselves.
    if (!response.ok) {
        throw new ApiError(response.status, extractErrorMessages(data, response.status));
    }

    return data;
}

// Short helpers so pages don't repeat themselves.
function apiGet(url) { return apiRequest("GET", url); }
function apiPost(url, body, sendToken = true) { return apiRequest("POST", url, body, sendToken); }
function apiPut(url, body) { return apiRequest("PUT", url, body); }
function apiDelete(url) { return apiRequest("DELETE", url); }

// The backend returns errors in three shapes. This turns any of them
// into a simple list of sentences.
//   { "message": "..." }                                  (our controllers)
//   { "message": "...", "errors": ["...", "..."] }        (Identity password rules)
//   { "title": "...", "errors": { "Email": ["..."] } }    (automatic DTO validation)
function extractErrorMessages(data, status) {
    const messages = [];

    if (data && typeof data === "object") {
        if (Array.isArray(data.errors)) {
            messages.push(...data.errors);
        } else if (data.errors && typeof data.errors === "object") {
            for (const fieldMessages of Object.values(data.errors)) {
                messages.push(...fieldMessages);
            }
        }

        if (messages.length === 0 && data.message) {
            messages.push(data.message);
        }
        if (messages.length === 0 && data.title) {
            messages.push(data.title);
        }
    }

    if (messages.length === 0) {
        if (status === 403) messages.push("You do not have permission to do this.");
        else if (status === 404) messages.push("The requested item was not found.");
        else if (status >= 500) messages.push("Something went wrong on the server. Please try again.");
        else messages.push("The request failed (status " + status + ").");
    }

    // Remove duplicates (e.g. the same rule reported twice).
    return [...new Set(messages)];
}

// ---------------------------------------------------------------------
// 3. Small UI helpers
// ---------------------------------------------------------------------

// Shows a Bootstrap alert inside the given element.
// We use textContent (never innerHTML) for messages, so text coming from the
// server or the user can never run as HTML/JavaScript (prevents XSS).
function showAlert(container, type, messages) {
    container.replaceChildren();

    const alert = document.createElement("div");
    alert.className = "alert alert-" + type + " py-2 mb-0";
    alert.setAttribute("role", "alert");

    const list = Array.isArray(messages) ? messages : [messages];
    if (list.length === 1) {
        alert.textContent = list[0];
    } else {
        const ul = document.createElement("ul");
        ul.className = "mb-0 ps-3";
        for (const message of list) {
            const li = document.createElement("li");
            li.textContent = message;
            ul.appendChild(li);
        }
        alert.appendChild(ul);
    }

    container.appendChild(alert);
}

function clearAlert(container) {
    container.replaceChildren();
}

// Disables a button and shows a small spinner while a request is running.
function setButtonLoading(button, isLoading, loadingText) {
    if (isLoading) {
        button.dataset.originalText = button.textContent;
        button.disabled = true;
        button.replaceChildren();

        const spinner = document.createElement("span");
        spinner.className = "spinner-border spinner-border-sm me-2";
        spinner.setAttribute("aria-hidden", "true");

        button.append(spinner, document.createTextNode(loadingText));
    } else {
        button.disabled = false;
        button.textContent = button.dataset.originalText || button.textContent;
    }
}

// ---------------------------------------------------------------------
// 4. Dates and times
// The API uses LOCAL COLLEGE TIME without a time zone, e.g. "2026-12-01T10:00:00".
// new Date("2026-12-01T10:00:00") reads that as local time in the browser.
// ---------------------------------------------------------------------

function formatDate(value) {
    return new Date(value).toLocaleDateString("en-IN", { weekday: "short", day: "2-digit", month: "short", year: "numeric" });
}

function formatTime(value) {
    return new Date(value).toLocaleTimeString("en-IN", { hour: "2-digit", minute: "2-digit" });
}

function formatDateTime(value) {
    return formatDate(value) + ", " + formatTime(value);
}

// Builds the value the API expects from a date box ("2026-12-01") and a time box ("10:00").
function toApiDateTime(dateValue, timeValue) {
    return dateValue + "T" + timeValue + ":00";
}

// Today's date as "YYYY-MM-DD" (local), used as the minimum for date boxes.
function todayForDateInput() {
    const now = new Date();
    const month = String(now.getMonth() + 1).padStart(2, "0");
    const day = String(now.getDate()).padStart(2, "0");
    return now.getFullYear() + "-" + month + "-" + day;
}

// ---------------------------------------------------------------------
// 5. Building tables and badges (always with textContent - never innerHTML)
// ---------------------------------------------------------------------

// One colour per appointment status, used on every page.
const STATUS_STYLES = {
    Pending: "text-bg-warning",
    Approved: "text-bg-primary",
    Rejected: "text-bg-danger",
    Completed: "text-bg-success",
    Cancelled: "text-bg-secondary"
};

function createStatusBadge(status) {
    const badge = document.createElement("span");
    badge.className = "badge " + (STATUS_STYLES[status] || "text-bg-light");
    badge.textContent = status;
    return badge;
}

// A table cell. "content" can be text, a number, an element, or a list of those.
function createCell(content, className) {
    const cell = document.createElement("td");
    if (className) {
        cell.className = className;
    }
    const items = Array.isArray(content) ? content : [content];
    for (const item of items) {
        if (item instanceof Node) {
            cell.appendChild(item);
        } else if (item !== null && item !== undefined) {
            cell.appendChild(document.createTextNode(String(item)));
        }
    }
    return cell;
}

function createRow(cells) {
    const row = document.createElement("tr");
    for (const cell of cells) {
        row.appendChild(cell);
    }
    return row;
}

// Fills a table body with a single message row ("Loading...", "No records", an error).
function showTableMessage(tableBody, columnCount, message) {
    const cell = document.createElement("td");
    cell.colSpan = columnCount;
    cell.className = "text-center text-muted py-4";
    cell.textContent = message;
    tableBody.replaceChildren(createRow([cell]));
}

function createButton(text, className, onClick) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "btn btn-sm " + className;
    button.textContent = text;
    button.addEventListener("click", onClick);
    return button;
}

// Small grey second line inside a cell, e.g. a student's email under their name.
function createSmallText(text) {
    const small = document.createElement("div");
    small.className = "small text-muted";
    small.textContent = text;
    return small;
}

// ---------------------------------------------------------------------
// 6. Confirmation dialog (Bootstrap modal) for delete / cancel / reject
// Usage:  if (await confirmAction("Delete department", "Are you sure?", "Delete")) { ... }
// ---------------------------------------------------------------------
function confirmAction(title, message, confirmText = "Confirm", buttonClass = "btn-danger") {
    return new Promise((resolve) => {
        let modalElement = document.getElementById("confirmModal");

        // Create the dialog once and reuse it.
        if (!modalElement) {
            modalElement = document.createElement("div");
            modalElement.id = "confirmModal";
            modalElement.className = "modal fade";
            modalElement.tabIndex = -1;
            modalElement.innerHTML = `
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h2 class="modal-title h5" id="confirmTitle"></h2>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body" id="confirmMessage"></div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Go back</button>
                            <button type="button" class="btn" id="confirmButton"></button>
                        </div>
                    </div>
                </div>`;   // fixed markup only - the changing text is set with textContent below
            document.body.appendChild(modalElement);
        }

        modalElement.querySelector("#confirmTitle").textContent = title;
        modalElement.querySelector("#confirmMessage").textContent = message;
        const confirmButton = modalElement.querySelector("#confirmButton");
        confirmButton.textContent = confirmText;
        confirmButton.className = "btn " + buttonClass;

        const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
        let isOpen = false;     // has the opening animation finished?
        let answered = false;

        // Give the answer exactly once.
        const finish = (result) => {
            if (answered) return;
            answered = true;
            confirmButton.removeEventListener("click", onConfirm);
            modalElement.removeEventListener("hidden.bs.modal", onHidden);
            resolve(result);
        };

        // Clicked "Confirm": answer straight away, then close.
        // (Bootstrap ignores hide() while the dialog is still opening, so if the
        //  click was very quick we close it as soon as it has finished opening.)
        const onConfirm = () => {
            finish(true);
            if (isOpen) modal.hide();
        };
        const onShown = () => {
            isOpen = true;
            if (answered) modal.hide();
        };
        // Closed any other way (Go back, X, Esc, clicking outside) = "no".
        const onHidden = () => finish(false);

        confirmButton.addEventListener("click", onConfirm);
        modalElement.addEventListener("shown.bs.modal", onShown, { once: true });
        modalElement.addEventListener("hidden.bs.modal", onHidden);
        modal.show();
    });
}

// When any Bootstrap dialog starts closing, move keyboard focus off it.
// Without this, the browser warns that a hidden dialog still holds focus,
// which confuses screen readers (an accessibility issue).
document.addEventListener("hide.bs.modal", () => {
    if (document.activeElement instanceof HTMLElement) {
        document.activeElement.blur();
    }
});

// Closes a Bootstrap dialog. If it is still opening (e.g. a very quick click),
// Bootstrap ignores hide(), so we close it again as soon as it has fully opened.
function closeModal(modalElement) {
    const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
    modal.hide();
    if (modalElement.classList.contains("show")) {
        modalElement.addEventListener("shown.bs.modal", () => modal.hide(), { once: true });
    }
}
