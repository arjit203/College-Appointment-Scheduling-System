// =====================================================================
// dashboard.js - shared "shell" for the role pages:
//   dashboard.html, admin-dashboard.html, faculty-dashboard.html, student-dashboard.html
// Needs api.js to be loaded first. The page's own script (admin.js, faculty.js
// or student.js) is loaded after this one and provides:
//   initRolePage(user)      - runs once, sets up forms and buttons
//   onSectionShown(name)    - runs each time a sidebar section is opened (loads its data)
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

    if (typeof initRolePage === "function") {
        initRolePage(user);
    }

    setUpSidebar();
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
    const welcome = document.getElementById("welcomeName");
    if (welcome) {
        welcome.textContent = user.fullName;
    }
}

// ---------------------------------------------------------------------
// Sidebar: each link has data-section="name" and shows <section id="section-name">.
// The open section is remembered in the address bar (#name) so a refresh keeps it.
// ---------------------------------------------------------------------
function setUpSidebar() {
    const links = document.querySelectorAll("[data-section]");

    for (const link of links) {
        link.addEventListener("click", (event) => {
            event.preventDefault();
            showSection(link.dataset.section);
        });
    }

    const fromAddressBar = window.location.hash.replace("#", "");
    const exists = document.getElementById("section-" + fromAddressBar);
    showSection(exists ? fromAddressBar : links[0].dataset.section);
}

function showSection(name) {
    for (const section of document.querySelectorAll(".page-section")) {
        section.classList.toggle("d-none", section.id !== "section-" + name);
    }
    for (const link of document.querySelectorAll("[data-section]")) {
        const isActive = link.dataset.section === name;
        link.classList.toggle("active", isActive);
        if (isActive) {
            link.setAttribute("aria-current", "page");
        } else {
            link.removeAttribute("aria-current");
        }
    }

    history.replaceState(null, "", "#" + name);
    clearAlert(document.getElementById("pageAlert"));

    if (typeof onSectionShown === "function") {
        onSectionShown(name);
    }
}

// Shows a message at the top of the page and scrolls to it.
function showPageMessage(type, messages) {
    const box = document.getElementById("pageAlert");
    showAlert(box, type, messages);
    box.scrollIntoView({ behavior: "smooth", block: "nearest" });
}

// Logging out with JWT = forgetting the token in the browser.
// (The server keeps no session, so there is nothing to "switch off" there.)
function logout() {
    clearSession();
    window.location.replace("index.html?reason=loggedout");
}
