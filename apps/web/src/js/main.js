// src/js/main.js
const AUTH_URL = 'http://localhost:5003';
const DATA_URL = 'http://localhost:5002';

function getToken() {
  return localStorage.getItem('token');
}

function setToken(token) {
  localStorage.setItem('token', token);
}

function logout() {
  localStorage.removeItem('token');
  window.location.href = '/index.html';
}

function getQueryParam(param) {
  const urlParams = new URLSearchParams(window.location.search);
  return urlParams.get(param);
}

// Show error utility
function showError(elementId, message) {
  const errorEl = document.getElementById(elementId);
  if (errorEl) {
    errorEl.textContent = message;
    errorEl.style.display = 'block';
  }
}

function hideError(elementId) {
  const errorEl = document.getElementById(elementId);
  if (errorEl) {
    errorEl.textContent = '';
    errorEl.style.display = 'none';
  }
}// Basic JavaScript entry point
document.addEventListener("DOMContentLoaded", () => {
    console.log("MCollector Dashboard loaded.");
    const appDiv = document.getElementById("app");
    if(appDiv) {
        appDiv.textContent = "Data loading...";
    }
});