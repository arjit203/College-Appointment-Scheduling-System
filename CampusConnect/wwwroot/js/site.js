// Calls GET /api/status and shows the result on the home page.
async function checkApiStatus() {
    const statusText = document.getElementById("apiStatus");

    try {
        const response = await fetch("/api/status");

        if (!response.ok) {
            statusText.textContent = "API returned an error: " + response.status;
            statusText.className = "mb-0 text-danger";
            return;
        }

        const data = await response.json(); // JSON text -> JavaScript object
        if (data.databaseConnected) {
            statusText.textContent = data.application + " is " + data.status + " – database connected";
            statusText.className = "mb-0 text-success";
        } else {
            statusText.textContent = data.application + " is " + data.status + " – database NOT connected";
            statusText.className = "mb-0 text-warning";
        }
    } catch (error) {
        statusText.textContent = "Could not reach the API.";
        statusText.className = "mb-0 text-danger";
    }
}

document.addEventListener("DOMContentLoaded", checkApiStatus);
