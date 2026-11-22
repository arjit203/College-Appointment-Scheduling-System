// =====================================================================
// dashboard.js - shared code for every page that needs a logged-in user:
//   dashboard.html, admin-dashboard.html, faculty-dashboard.html, student-dashboard.html
// Needs api.js to be loaded first.
//
// Each page says which role it is for in its <body> tag, e.g.
//   <body data-required-role="Student">
// dashboard.html has no role - it just sends you to the right dashboard.
// =====================================================================

document.addEventListener("DOMContentLoaded", () => {
    const requiredRole = document.body.dataset.requiredRole || null;
    const user = requireLogin(requiredRole);
    if (!user) {
        return; // we are being redirected
    }

    // dashboard.html: just forward to the correct dashboard for this role.
    if (!requiredRole) {
        window.location.replace(getDashboardUrl(getPrimaryRole(user)));
        return;
    }

    showUserInNavbar(user);
    document.getElementById("logoutButton").addEventListener("click", logout);

    // Everything checked - show the page (it is hidden until now, see style.css).
    document.body.classList.remove("auth-pending");

    loadProfileFromServer();
});

// Page guard. Returns the user, or null if we had to redirect.
//   - No token / expired token -> login page
//   - Wrong role for this page  -> that user's own dashboard
// Remember: this only controls NAVIGATION. Even if someone edits localStorage
// to reach a page, every API call is still checked by the server.
function requireLogin(requiredRole) {
    if (!isLoggedIn()) {
        clearSession();
        window.location.replace("index.html?reason=login");
        return null;
    }

    const user = getCurrentUser();
    const role = getPrimaryRole(user);

    if (!role) {
        clearSession();
        window.location.replace("index.html?reason=login");
        return null;
    }

    if (requiredRole && role !== requiredRole) {
        window.location.replace(getDashboardUrl(role));
        return null;
    }

    return user;
}

function showUserInNavbar(user) {
    document.getElementById("navUserName").textContent = user.fullName;
    document.getElementById("navUserRole").textContent = getPrimaryRole(user);
    // Full name (taking only the first word would show "Welcome, Dr." for faculty).
    document.getElementById("welcomeName").textContent = user.fullName;
}

// Calls a PROTECTED API (GET /api/auth/me) with the JWT.
// This proves the token works and shows the details the SERVER has for us.
async function loadProfileFromServer() {
    const box = document.getElementById("profileBox");
    if (!box) {
        return;
    }

    try {
        const profile = await apiGet("/api/auth/me");   // { id, fullName, email, roles }

        document.getElementById("profileName").textContent = profile.fullName;
        document.getElementById("profileEmail").textContent = profile.email;
        document.getElementById("profileRoles").textContent = profile.roles.join(", ");
        document.getElementById("profileStatus").textContent = "Verified by server";
        document.getElementById("profileStatus").className = "badge text-bg-success";
    } catch (error) {
        document.getElementById("profileStatus").textContent = "Could not verify";
        document.getElementById("profileStatus").className = "badge text-bg-danger";
        showAlert(document.getElementById("pageAlert"), "danger", error.messages || ["Could not load your profile."]);
    }
}

// Logging out with JWT = forgetting the token in the browser.
// (The server keeps no session, so there is nothing to "switch off" there.)
function logout() {
    clearSession();
    window.location.replace("index.html?reason=loggedout");
}
