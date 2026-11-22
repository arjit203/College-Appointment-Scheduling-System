// =====================================================================
// auth.js - logic for the Login page (index.html) and Register page (register.html)
// Needs api.js to be loaded first.
// =====================================================================

document.addEventListener("DOMContentLoaded", () => {
    // Already logged in with a valid token? Skip the login/register page.
    if (isLoggedIn()) {
        const role = getPrimaryRole(getCurrentUser());
        if (role) {
            window.location.replace(getDashboardUrl(role));
            return;
        }
    }
    // An old, expired token is useless - remove it.
    clearSession();

    const loginForm = document.getElementById("loginForm");
    if (loginForm) {
        setUpLoginPage(loginForm);
    }

    const registerForm = document.getElementById("registerForm");
    if (registerForm) {
        setUpRegisterPage(registerForm);
    }
});

// ---------------------------------------------------------------------
// LOGIN
// ---------------------------------------------------------------------
function setUpLoginPage(form) {
    const alertBox = document.getElementById("alertBox");
    const button = document.getElementById("loginButton");

    // Browsers sometimes refill a form with the previous values (e.g. after
    // logging out). On a shared college computer that would leave the last
    // person's password in the box, so we always start with an empty form.
    form.reset();

    showMessageFromUrl(alertBox);
    checkApiStatus();

    form.addEventListener("submit", async (event) => {
        event.preventDefault();          // stop the browser's normal form submit (page reload)
        clearAlert(alertBox);

        // Bootstrap validation: show red messages under empty/invalid fields.
        form.classList.add("was-validated");
        if (!form.checkValidity()) {
            return;
        }

        // Exactly the shape LoginRequestDto expects: { email, password }
        const credentials = {
            email: document.getElementById("email").value.trim(),
            password: document.getElementById("password").value
        };

        setButtonLoading(button, true, "Signing in...");
        try {
            // Response: { token, expiresAt, user: { id, fullName, email, roles: [...] } }
            const loginResponse = await apiPost("/api/auth/login", credentials, false);

            const role = getPrimaryRole(loginResponse.user);
            if (!role) {
                showAlert(alertBox, "danger", "Your account has no role assigned. Please contact the administrator.");
                return;
            }

            saveSession(loginResponse);
            window.location.replace(getDashboardUrl(role));
        } catch (error) {
            showAlert(alertBox, "danger", error.messages || ["Login failed. Please try again."]);
        } finally {
            setButtonLoading(button, false);
        }
    });
}

// Messages passed in the URL, e.g. index.html?reason=loggedout
function showMessageFromUrl(alertBox) {
    const reason = new URLSearchParams(window.location.search).get("reason");
    const messages = {
        registered: ["success", "Registration successful! Please log in with your new account."],
        loggedout: ["info", "You have been logged out."],
        expired: ["warning", "Your session has expired. Please log in again."],
        login: ["warning", "Please log in to continue."]
    };

    if (messages[reason]) {
        const [type, text] = messages[reason];
        showAlert(alertBox, type, text);
    }

    // Pre-fill the email after registration.
    const email = new URLSearchParams(window.location.search).get("email");
    if (email) {
        document.getElementById("email").value = email;
    }
}

// Small "API online / offline" indicator at the bottom of the login card
// (this replaces the Phase 1 status page).
async function checkApiStatus() {
    const statusText = document.getElementById("apiStatus");
    if (!statusText) {
        return;
    }
    try {
        const status = await apiGet("/api/status");
        statusText.textContent = status.databaseConnected ? "System online" : "Database not connected";
        statusText.className = status.databaseConnected ? "status-dot text-success" : "status-dot text-warning";
    } catch {
        statusText.textContent = "Server offline";
        statusText.className = "status-dot text-danger";
    }
}

// ---------------------------------------------------------------------
// REGISTER (students only - there is no role field anywhere)
// ---------------------------------------------------------------------
function setUpRegisterPage(form) {
    const alertBox = document.getElementById("alertBox");
    const button = document.getElementById("registerButton");
    const password = document.getElementById("password");
    const confirmPassword = document.getElementById("confirmPassword");

    form.reset();   // start empty (see the note in setUpLoginPage)

    // Re-check "passwords match" whenever either box changes.
    const checkPasswordsMatch = () => {
        const message = confirmPassword.value && confirmPassword.value !== password.value
            ? "Passwords do not match."
            : "";
        confirmPassword.setCustomValidity(message);   // non-empty = field is invalid
    };
    password.addEventListener("input", checkPasswordsMatch);
    confirmPassword.addEventListener("input", checkPasswordsMatch);

    form.addEventListener("submit", async (event) => {
        event.preventDefault();
        clearAlert(alertBox);
        checkPasswordsMatch();

        form.classList.add("was-validated");
        if (!form.checkValidity()) {
            return;
        }

        // Exactly the shape RegisterRequestDto expects.
        const newStudent = {
            fullName: document.getElementById("fullName").value.trim(),
            email: document.getElementById("email").value.trim(),
            password: password.value,
            confirmPassword: confirmPassword.value
        };

        setButtonLoading(button, true, "Creating account...");
        try {
            await apiPost("/api/auth/register", newStudent, false);   // 201 Created
            showAlert(alertBox, "success", "Account created! Taking you to the login page...");
            form.reset();
            form.classList.remove("was-validated");

            const loginUrl = "index.html?reason=registered&email=" + encodeURIComponent(newStudent.email);
            setTimeout(() => window.location.replace(loginUrl), 1500);
        } catch (error) {
            // e.g. 409 "An account with this email already exists."
            //      400 "Passwords must have at least one uppercase ('A'-'Z')."
            showAlert(alertBox, "danger", error.messages || ["Registration failed. Please try again."]);
        } finally {
            setButtonLoading(button, false);
        }
    });
}
