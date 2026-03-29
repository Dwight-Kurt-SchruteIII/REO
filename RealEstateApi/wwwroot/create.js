const STORAGE_TOKEN_KEY = "realestate.jwt";
const STORAGE_USER_KEY = "realestate.user";

const state = {
  properties: [],
  contracts: [],
  noticeTimer: null,
  accessToken: null,
  currentUser: null
};

const dom = {
  apiStatus: document.getElementById("apiStatus"),
  refreshBtn: document.getElementById("refreshBtn"),
  logoutBtn: document.getElementById("logoutBtn"),
  authMeta: document.getElementById("authMeta"),
  tenantPropertyId: document.getElementById("tenantPropertyId"),
  createTenantForm: document.getElementById("createTenantForm"),
  paymentTenantContractId: document.getElementById("paymentTenantContractId"),
  createPaymentForm: document.getElementById("createPaymentForm"),
  notice: document.getElementById("notice")
};

initialize();

async function initialize() {
  hydrateAuthState();

  const signedIn = await syncAuthSession();
  if (!signedIn) {
    clearAuthState();
    applyAuthUiState();
    window.location.replace("/login.html");
    return;
  }

  wireEvents();
  applyAuthUiState();
  setDefaultDates();
  await loadReferenceData();
}

function wireEvents() {
  dom.refreshBtn.addEventListener("click", () => loadReferenceData(true));

  dom.logoutBtn.addEventListener("click", async () => {
    await apiRequest("/auth/logout", {
      method: "POST",
      skipAuth: true
    });

    clearAuthState();
    applyAuthUiState();
    window.location.replace("/login.html");
  });

  dom.paymentTenantContractId.addEventListener("change", () => {
    syncPaymentAmountFromSelectedTenant(true);
  });

  dom.createTenantForm.addEventListener("submit", async (event) => {
    event.preventDefault();

    if (!ensureWriteAccess()) {
      return;
    }

    const propertyId = Number(dom.tenantPropertyId.value);
    if (!Number.isFinite(propertyId) || propertyId <= 0) {
      notify("error", "Select a property first.");
      return;
    }

    const payload = {
      tenantName: dom.createTenantForm.tenantName.value.trim(),
      monthlyRent: toNumber(dom.createTenantForm.monthlyRent.value),
      startDate: toIsoDate(dom.createTenantForm.startDate.value),
      endDate: toIsoDateOrNull(dom.createTenantForm.endDate.value),
      propertyId
    };

    const response = await apiRequest("/tenantcontracts", {
      method: "POST",
      body: payload
    });

    if (!response.ok) {
      showApiError(response, "Tenant contract was not created.");
      return;
    }

    const createdId = readId(response.data);
    const createdContract = normalizeContract(response.data || {});

    dom.createTenantForm.reset();
    dom.createTenantForm.startDate.value = todayAsInputDate();

    await loadReferenceData();

    if (createdId) {
      dom.paymentTenantContractId.value = String(createdId);
    }

    if (createdContract.monthlyRent > 0) {
      dom.createPaymentForm.amount.value = String(createdContract.monthlyRent);
    }

    notify("success", "Tenant contract created.");
  });

  dom.createPaymentForm.addEventListener("submit", async (event) => {
    event.preventDefault();

    if (!ensureWriteAccess()) {
      return;
    }

    const tenantContractId = Number(dom.paymentTenantContractId.value);
    if (!Number.isFinite(tenantContractId) || tenantContractId <= 0) {
      notify("error", "Select tenant first.");
      return;
    }

    const payload = {
      amount: toNumber(dom.createPaymentForm.amount.value),
      paymentDate: toIsoDate(dom.createPaymentForm.paymentDate.value),
      dueDate: toIsoDateOrNull(dom.createPaymentForm.dueDate.value),
      tenantContractId
    };

    const response = await createPaymentRequest(payload);

    if (!response.ok) {
      if (response.status === 404 || response.status === 405) {
        notify("error", "Payment endpoint is not available. Restart API and try again.");
        return;
      }
      showApiError(response, "Payment was not created.");
      return;
    }

    dom.createPaymentForm.dueDate.value = "";
    dom.createPaymentForm.paymentDate.value = todayAsInputDate();
    syncPaymentAmountFromSelectedTenant(true);

    notify("success", "Payment created.");
  });
}

function setDefaultDates() {
  const today = todayAsInputDate();
  dom.createTenantForm.startDate.value = today;
  dom.createPaymentForm.paymentDate.value = today;
}

async function loadReferenceData(showRefreshNotice = false) {
  const [propertyResponse, contractResponse] = await Promise.all([
    apiRequest("/properties"),
    apiRequest("/tenantcontracts")
  ]);

  if (!propertyResponse.ok || !contractResponse.ok) {
    if (propertyResponse.status === 401 || contractResponse.status === 401) {
      clearAuthState();
      applyAuthUiState();
      window.location.replace("/login.html");
      return;
    }

    setApiStatus(false);
    notify("error", "Failed to load API data.");
    return;
  }

  state.properties = (propertyResponse.data || []).map(normalizeProperty);
  state.contracts = (contractResponse.data || []).map(normalizeContract);

  setApiStatus(true);
  renderPropertyOptions();
  renderContractOptions();
  syncPaymentAmountFromSelectedTenant(false);

  if (showRefreshNotice) {
    notify("info", "Data refreshed.");
  }
}

function renderPropertyOptions() {
  const previous = dom.tenantPropertyId.value;

  if (state.properties.length === 0) {
    dom.tenantPropertyId.innerHTML = '<option value="">No properties available</option>';
    return;
  }

  dom.tenantPropertyId.innerHTML = state.properties
    .map((property) => `<option value="${property.id}">${escapeHtml(property.name)} | ${escapeHtml(property.address)}</option>`)
    .join("");

  if (previous && state.properties.some((property) => String(property.id) === previous)) {
    dom.tenantPropertyId.value = previous;
  }
}

function renderContractOptions() {
  const previous = dom.paymentTenantContractId.value;

  if (state.contracts.length === 0) {
    dom.paymentTenantContractId.innerHTML = '<option value="">No tenants available</option>';
    return;
  }

  dom.paymentTenantContractId.innerHTML = state.contracts
    .map((contract) => {
      const property = state.properties.find((item) => item.id === contract.propertyId);
      const propertyName = property ? property.name : `Property ${contract.propertyId}`;
      return `<option value="${contract.id}">${escapeHtml(contract.tenantName)} | ${formatCurrency(contract.monthlyRent)} | ${escapeHtml(propertyName)}</option>`;
    })
    .join("");

  if (previous && state.contracts.some((contract) => String(contract.id) === previous)) {
    dom.paymentTenantContractId.value = previous;
    return;
  }

  dom.paymentTenantContractId.value = String(state.contracts[0].id);
}

function getSelectedPaymentContract() {
  const selectedId = Number(dom.paymentTenantContractId.value);
  if (!Number.isFinite(selectedId) || selectedId <= 0) {
    return null;
  }

  return state.contracts.find((item) => item.id === selectedId) || null;
}

function syncPaymentAmountFromSelectedTenant(force) {
  const selected = getSelectedPaymentContract();
  if (!selected) {
    return;
  }

  if (force || dom.createPaymentForm.amount.value === "") {
    dom.createPaymentForm.amount.value = String(selected.monthlyRent);
  }

  if (dom.createPaymentForm.paymentDate.value === "") {
    dom.createPaymentForm.paymentDate.value = todayAsInputDate();
  }
}

function ensureWriteAccess() {
  if (state.currentUser) {
    return true;
  }

  notify("error", "Sign in first to use write actions.");
  return false;
}

function hydrateAuthState() {
  const token = window.localStorage.getItem(STORAGE_TOKEN_KEY);
  const userRaw = window.localStorage.getItem(STORAGE_USER_KEY);

  state.accessToken = token || null;

  if (!userRaw) {
    state.currentUser = null;
    return;
  }

  try {
    state.currentUser = JSON.parse(userRaw);
  } catch {
    state.currentUser = null;
  }
}

async function syncAuthSession() {
  const response = await apiRequest("/auth/me", { skipAuth: true });

  if (!response.ok) {
    return false;
  }

  state.currentUser = response.data ?? null;
  saveAuthState();
  return true;
}

function saveAuthState() {
  if (state.accessToken) {
    window.localStorage.setItem(STORAGE_TOKEN_KEY, state.accessToken);
  } else {
    window.localStorage.removeItem(STORAGE_TOKEN_KEY);
  }

  if (state.currentUser) {
    window.localStorage.setItem(STORAGE_USER_KEY, JSON.stringify(state.currentUser));
  } else {
    window.localStorage.removeItem(STORAGE_USER_KEY);
  }
}

function clearAuthState() {
  state.accessToken = null;
  state.currentUser = null;
  saveAuthState();
}

function applyAuthUiState() {
  const signedIn = Boolean(state.currentUser);

  dom.logoutBtn.classList.toggle("hidden", !signedIn);

  if (signedIn && state.currentUser) {
    dom.authMeta.textContent = `Signed in: ${state.currentUser.username} (${state.currentUser.role})`;
  } else {
    dom.authMeta.textContent = "Not signed in";
  }

  document
    .querySelectorAll('[data-write-action="true"]')
    .forEach((button) => {
      button.disabled = !signedIn;
      button.title = signedIn ? "" : "Sign in to enable this action.";
    });
}

async function apiRequest(url, options = {}) {
  const request = {
    method: options.method || "GET",
    headers: {
      Accept: "application/json"
    }
  };

  if (!options.skipAuth && state.accessToken) {
    request.headers.Authorization = `Bearer ${state.accessToken}`;
  }

  if (options.body !== undefined) {
    request.headers["Content-Type"] = "application/json";
    request.body = JSON.stringify(options.body);
  }

  let response;
  try {
    response = await fetch(url, request);
  } catch {
    return { ok: false, status: 0, data: null };
  }

  const contentType = response.headers.get("content-type") || "";
  let data = null;

  if (contentType.includes("application/json")) {
    try {
      data = await response.json();
    } catch {
      data = null;
    }
  } else {
    try {
      data = await response.text();
    } catch {
      data = null;
    }
  }

  return {
    ok: response.ok,
    status: response.status,
    data
  };
}

async function createPaymentRequest(payload) {
  const urls = [
    "/payments",
    "/api/payments",
    `/api/tenantContractId/${payload.tenantContractId}/payments`
  ];

  let lastResponse = null;

  for (const url of urls) {
    const response = await apiRequest(url, {
      method: "POST",
      body: payload
    });

    if (response.status !== 404 && response.status !== 405) {
      return response;
    }

    lastResponse = response;
  }

  return lastResponse ?? { ok: false, status: 404, data: null };
}

function showApiError(response, fallbackMessage) {
  if (response.status === 400 && response.data && typeof response.data === "object" && response.data.errors) {
    notify("error", formatValidationErrors(response.data.errors));
    return;
  }

  if (response.status === 401) {
    clearAuthState();
    applyAuthUiState();
    window.location.replace("/login.html");
    return;
  }

  if (response.status === 403) {
    notify("error", "Forbidden (403). Your role has no access to this action.");
    return;
  }

  if (response.status === 409) {
    notify("error", "Operation blocked by business rule (409 Conflict).");
    return;
  }

  if (response.status === 404) {
    notify("error", "Resource not found (404).");
    return;
  }

  if (response.status === 0) {
    notify("error", "Network error. API is not reachable.");
    setApiStatus(false);
    return;
  }

  notify("error", `${fallbackMessage} (status ${response.status})`);
}

function formatValidationErrors(errors) {
  const chunks = [];
  for (const [field, messages] of Object.entries(errors)) {
    if (!Array.isArray(messages)) {
      continue;
    }
    for (const message of messages) {
      if (field) {
        chunks.push(`${field}: ${message}`);
      } else {
        chunks.push(String(message));
      }
    }
  }
  return chunks.join(" | ");
}

function notify(type, message) {
  dom.notice.className = `notice ${type}`;
  dom.notice.textContent = message;
  dom.notice.classList.remove("hidden");

  if (state.noticeTimer) {
    clearTimeout(state.noticeTimer);
  }

  state.noticeTimer = window.setTimeout(() => {
    dom.notice.classList.add("hidden");
  }, 4200);
}

function setApiStatus(isOnline) {
  if (isOnline) {
    dom.apiStatus.textContent = "API online";
    dom.apiStatus.className = "status-badge status-online";
  } else {
    dom.apiStatus.textContent = "API offline";
    dom.apiStatus.className = "status-badge status-offline";
  }
}

function readId(data) {
  if (!data || typeof data !== "object") {
    return null;
  }

  const id = data.id ?? data.Id;
  const numberId = Number(id);
  return Number.isFinite(numberId) ? numberId : null;
}

function toIsoDate(value) {
  if (!value) {
    return null;
  }
  return `${value}T00:00:00`;
}

function toIsoDateOrNull(value) {
  return value ? `${value}T00:00:00` : null;
}

function todayAsInputDate() {
  return new Date().toISOString().slice(0, 10);
}

function toNumber(value) {
  return Number(value);
}

function normalizeProperty(raw) {
  return {
    id: Number(raw.id ?? raw.Id),
    name: String(raw.name ?? raw.Name ?? ""),
    address: String(raw.address ?? raw.Address ?? "")
  };
}

function normalizeContract(raw) {
  return {
    id: Number(raw.id ?? raw.Id),
    tenantName: String(raw.tenantName ?? raw.TenantName ?? ""),
    monthlyRent: Number(raw.monthlyRent ?? raw.MonthlyRent ?? 0),
    propertyId: Number(raw.propertyId ?? raw.PropertyId)
  };
}

function formatCurrency(number) {
  return new Intl.NumberFormat("cs-CZ", {
    style: "currency",
    currency: "CZK",
    maximumFractionDigits: 0
  }).format(number || 0);
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}
