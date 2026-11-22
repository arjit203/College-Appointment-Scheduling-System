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
