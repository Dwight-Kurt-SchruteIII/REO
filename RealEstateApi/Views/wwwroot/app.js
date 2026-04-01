const STORAGE_TOKEN_KEY = "realestate.jwt";
const STORAGE_USER_KEY = "realestate.user";

const state = {
  properties: [],
  contracts: [],
  selectedPropertyId: null,
  selectedContractId: null,
  paymentDraftContractId: null,
  noticeTimer: null,
  accessToken: null,
  currentUser: null
};

const dom = {
  apiStatus: document.getElementById("apiStatus"),
  refreshBtn: document.getElementById("refreshBtn"),
  openCreateTenantPageBtn: document.getElementById("openCreateTenantPageBtn"),
  logoutBtn: document.getElementById("logoutBtn"),
  authMeta: document.getElementById("authMeta"),
  propertyCount: document.getElementById("propertyCount"),
  propertyList: document.getElementById("propertyList"),
  createPropertyForm: document.getElementById("createPropertyForm"),
  emptyState: document.getElementById("emptyState"),
  workspace: document.getElementById("workspace"),
  selectedPropertyName: document.getElementById("selectedPropertyName"),
  selectedPropertyMeta: document.getElementById("selectedPropertyMeta"),
  propertyFullUpdateDetails: document.getElementById("propertyFullUpdateDetails"),
  propertyPatchDetails: document.getElementById("propertyPatchDetails"),
  updatePropertyForm: document.getElementById("updatePropertyForm"),
  patchPropertyForm: document.getElementById("patchPropertyForm"),
  deletePropertyBtn: document.getElementById("deletePropertyBtn"),
  contractCount: document.getElementById("contractCount"),
  contractsList: document.getElementById("contractsList"),
  emptyContractState: document.getElementById("emptyContractState"),
  contractWorkspace: document.getElementById("contractWorkspace"),
  selectedContractName: document.getElementById("selectedContractName"),
  selectedContractMeta: document.getElementById("selectedContractMeta"),
  tenantPatchDetails: document.getElementById("tenantPatchDetails"),
  tenantPaymentDetails: document.getElementById("tenantPaymentDetails"),
  patchContractForm: document.getElementById("patchContractForm"),
  createPaymentInlineForm: document.getElementById("createPaymentInlineForm"),
  deleteContractBtn: document.getElementById("deleteContractBtn"),
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
  collapsePropertyEditors();
  collapseTenantEditors();
  loadAllData();
}

function wireEvents() {
  dom.refreshBtn.addEventListener("click", () => loadAllData(true));

  dom.openCreateTenantPageBtn.addEventListener("click", () => {
    if (!ensureWriteAccess()) {
      return;
    }

    window.location.href = "/create.html";
  });

  dom.logoutBtn.addEventListener("click", async () => {
    await apiRequest("/auth/logout", {
      method: "POST",
      skipAuth: true
    });

    clearAuthState();
    applyAuthUiState();
    window.location.replace("/login.html");
  });

  dom.createPropertyForm.addEventListener("submit", async (event) => {
    event.preventDefault();

    if (!ensureWriteAccess()) {
      return;
    }

    const payload = {
      name: dom.createPropertyForm.name.value.trim(),
      address: dom.createPropertyForm.address.value.trim(),
      house: dom.createPropertyForm.house.checked,
      purchasePrice: toNumber(dom.createPropertyForm.purchasePrice.value),
      currentValue: toNumber(dom.createPropertyForm.currentValue.value),
      purchaseDate: toIsoDate(dom.createPropertyForm.purchaseDate.value)
    };

    const response = await apiRequest("/properties", { method: "POST", body: payload });
    if (!response.ok) {
      showApiError(response, "Property was not created.");
      return;
    }

    dom.createPropertyForm.reset();
    await loadAllData();
    const createdId = readId(response.data);
    if (createdId) {
      state.selectedPropertyId = createdId;
      renderWorkspace();
      renderPropertyList();
    }
    notify("success", "Property created.");
  });

  dom.updatePropertyForm.addEventListener("submit", async (event) => {
    event.preventDefault();

    if (!ensureWriteAccess()) {
      return;
    }

    const selected = getSelectedProperty();
    if (!selected) {
      notify("error", "Select a property first.");
      return;
    }

    const payload = {
      name: dom.updatePropertyForm.name.value.trim(),
      address: dom.updatePropertyForm.address.value.trim(),
      house: dom.updatePropertyForm.house.checked,
      purchasePrice: toNumber(dom.updatePropertyForm.purchasePrice.value),
      currentValue: toNumber(dom.updatePropertyForm.currentValue.value),
      purchaseDate: toIsoDate(dom.updatePropertyForm.purchaseDate.value)
    };

    const response = await apiRequest(`/properties/${selected.id}`, {
      method: "PUT",
      body: payload
    });

    if (!response.ok) {
      showApiError(response, "Property update failed.");
      return;
    }

    await loadAllData();
    notify("success", "Property updated.");
  });

  dom.patchPropertyForm.addEventListener("submit", async (event) => {
    event.preventDefault();

    if (!ensureWriteAccess()) {
      return;
    }

    const selected = getSelectedProperty();
    if (!selected) {
      notify("error", "Select a property first.");
      return;
    }

    const payload = {};
    const address = dom.patchPropertyForm.address.value.trim();
    const currentValueRaw = dom.patchPropertyForm.currentValue.value;
    const purchaseDateRaw = dom.patchPropertyForm.purchaseDate.value;

    if (address) {
      payload.address = address;
    }
    if (currentValueRaw !== "") {
      payload.currentValue = toNumber(currentValueRaw);
    }
    if (purchaseDateRaw) {
      payload.purchaseDate = toIsoDate(purchaseDateRaw);
    }

    if (Object.keys(payload).length === 0) {
      notify("info", "Enter at least one value to patch.");
      return;
    }

    const response = await apiRequest(`/properties/${selected.id}`, {
      method: "PATCH",
      body: payload
    });

    if (!response.ok) {
      showApiError(response, "Property patch failed.");
      return;
    }

    dom.patchPropertyForm.reset();
    await loadAllData();
    notify("success", "Property patched.");
  });

  dom.deletePropertyBtn.addEventListener("click", async () => {
    if (!ensureWriteAccess()) {
      return;
    }

    const selected = getSelectedProperty();
    if (!selected) {
      notify("error", "Select a property first.");
      return;
    }

    const confirmed = window.confirm(`Delete property '${selected.name}'?`);
    if (!confirmed) {
      return;
    }

    const response = await apiRequest(`/properties/${selected.id}`, { method: "DELETE" });
    if (!response.ok) {
      showApiError(response, "Property delete failed.");
      return;
    }

    state.selectedPropertyId = null;
    state.selectedContractId = null;
    await loadAllData();
    notify("success", "Property deleted.");
  });

  dom.patchContractForm.addEventListener("submit", async (event) => {
    event.preventDefault();

    if (!ensureWriteAccess()) {
      return;
    }

    const selectedProperty = getSelectedProperty();
    const selectedContract = getSelectedContractForProperty(selectedProperty?.id);
    if (!selectedProperty || !selectedContract) {
      notify("error", "Select a contract first.");
      return;
    }

    const payload = {};
    const tenantName = dom.patchContractForm.tenantName.value.trim();
    const monthlyRentRaw = dom.patchContractForm.monthlyRent.value;
    const endDateRaw = dom.patchContractForm.endDate.value;

    if (tenantName) {
      payload.tenantName = tenantName;
    }

    if (monthlyRentRaw !== "") {
      payload.monthlyRent = toNumber(monthlyRentRaw);
    }

    if (endDateRaw) {
      payload.endDate = toIsoDate(endDateRaw);
    }

    if (Object.keys(payload).length === 0) {
      notify("info", "Enter at least one value to patch.");
      return;
    }

    const response = await apiRequest(`/tenantcontracts/${selectedContract.id}`, {
      method: "PATCH",
      body: payload
    });

    if (!response.ok) {
      showApiError(response, "Contract patch failed.");
      return;
    }

    dom.patchContractForm.reset();
    await loadAllData();
    state.selectedPropertyId = selectedContract.propertyId;
    state.selectedContractId = selectedContract.id;
    renderWorkspace();
    notify("success", "Contract patched.");
  });

  dom.createPaymentInlineForm.addEventListener("submit", async (event) => {
    event.preventDefault();

    if (!ensureWriteAccess()) {
      return;
    }

    const selectedProperty = getSelectedProperty();
    const selectedContract = getSelectedContractForProperty(selectedProperty?.id);
    if (!selectedProperty || !selectedContract) {
      notify("error", "Select a contract first.");
      return;
    }

    const paymentDate = dom.createPaymentInlineForm.paymentDate.value;
    const amountValue = dom.createPaymentInlineForm.amount.value;

    const payload = {
      paymentDate: toIsoDate(paymentDate),
      amount: toNumber(amountValue),
      dueDate: null,
      tenantContractId: selectedContract.id
    };

    const response = await createPaymentRequest(payload);

    if (!response.ok) {
      if (response.status === 404 || response.status === 405) {
        notify("error", "Payment endpoint is not available. Restart API and try again.");
        return;
      }
      showApiError(response, "Payment create failed.");
      return;
    }

    dom.createPaymentInlineForm.paymentDate.value = todayAsInputDate();
    dom.createPaymentInlineForm.amount.value = String(selectedContract.monthlyRent);
    notify("success", "Payment created.");
  });

  dom.deleteContractBtn.addEventListener("click", async () => {
    if (!ensureWriteAccess()) {
      return;
    }

    const selectedProperty = getSelectedProperty();
    const selectedContract = getSelectedContractForProperty(selectedProperty?.id);
    if (!selectedProperty || !selectedContract) {
      notify("error", "Select a contract first.");
      return;
    }

    const confirmed = window.confirm(`Delete contract for '${selectedContract.tenantName}'?`);
    if (!confirmed) {
      return;
    }

    const response = await apiRequest(`/tenantcontracts/${selectedContract.id}`, { method: "DELETE" });
    if (!response.ok) {
      showApiError(response, "Contract delete failed.");
      return;
    }

    state.selectedContractId = null;
    await loadAllData();
    state.selectedPropertyId = selectedProperty.id;
    renderWorkspace();
    notify("success", "Contract deleted.");
  });
}

function collapsePropertyEditors() {
  if (dom.propertyFullUpdateDetails) {
    dom.propertyFullUpdateDetails.open = false;
  }

  if (dom.propertyPatchDetails) {
    dom.propertyPatchDetails.open = false;
  }
}

function collapseTenantEditors() {
  if (dom.tenantPatchDetails) {
    dom.tenantPatchDetails.open = false;
  }

  if (dom.tenantPaymentDetails) {
    dom.tenantPaymentDetails.open = false;
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

async function loadAllData(showRefreshNotice = false) {
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

  if (!state.properties.some((item) => item.id === state.selectedPropertyId)) {
    state.selectedPropertyId = state.properties[0]?.id ?? null;
  }

  if (!state.contracts.some((item) => item.id === state.selectedContractId)) {
    state.selectedContractId = null;
  }

  renderPropertyList();
  renderWorkspace();

  if (showRefreshNotice) {
    notify("info", "Data refreshed.");
  }
}

function renderPropertyList() {
  dom.propertyCount.textContent = String(state.properties.length);

  if (state.properties.length === 0) {
    dom.propertyList.innerHTML = '<div class="empty-state"><p>No properties yet.</p></div>';
    return;
  }

  dom.propertyList.innerHTML = state.properties
    .map((property, index) => {
      const activeClass = property.id === state.selectedPropertyId ? "active" : "";
      const selectedChip = activeClass ? '<span class="selected-chip">Selected</span>' : "";
      return `
        <button class="property-item ${activeClass}" style="--i:${index}" data-property-id="${property.id}" type="button">
          <div class="item-headline">
            <p class="item-title">${escapeHtml(property.name)}</p>
            ${selectedChip}
          </div>
          <p class="item-meta">${escapeHtml(property.address)}</p>
          <p class="item-meta">Current value: ${formatCurrency(property.currentValue)}</p>
        </button>
      `;
    })
    .join("");

  dom.propertyList.querySelectorAll("[data-property-id]").forEach((button) => {
    button.addEventListener("click", () => {
      state.selectedPropertyId = Number(button.dataset.propertyId);
      state.selectedContractId = null;
      collapsePropertyEditors();
      collapseTenantEditors();
      renderPropertyList();
      renderWorkspace();
    });
  });
}

function renderWorkspace() {
  const selected = getSelectedProperty();

  if (!selected) {
    dom.emptyState.classList.remove("hidden");
    dom.workspace.classList.add("hidden");
    collapsePropertyEditors();
    collapseTenantEditors();
    return;
  }

  dom.emptyState.classList.add("hidden");
  dom.workspace.classList.remove("hidden");

  dom.selectedPropertyName.textContent = selected.name;
  dom.selectedPropertyMeta.textContent = `${selected.address} | Purchase ${formatCurrency(selected.purchasePrice)} | Current ${formatCurrency(selected.currentValue)}`;

  fillPropertyForms(selected);
  renderContracts(selected.id);
}

function fillPropertyForms(property) {
  dom.updatePropertyForm.name.value = property.name;
  dom.updatePropertyForm.address.value = property.address;
  dom.updatePropertyForm.purchasePrice.value = String(property.purchasePrice);
  dom.updatePropertyForm.currentValue.value = String(property.currentValue);
  dom.updatePropertyForm.purchaseDate.value = toDateInput(property.purchaseDate);
  dom.updatePropertyForm.house.checked = property.house;
}

function renderContracts(propertyId) {
  const contracts = state.contracts.filter((contract) => contract.propertyId === propertyId);
  dom.contractCount.textContent = String(contracts.length);

  if (contracts.length === 0) {
    dom.contractsList.innerHTML = '<div class="empty-state"><p>No contracts for this property yet.</p></div>';
    state.selectedContractId = null;
    renderContractWorkspace(null);
    return;
  }

  if (!contracts.some((item) => item.id === state.selectedContractId)) {
    state.selectedContractId = contracts[0].id;
  }

  dom.contractsList.innerHTML = contracts
    .map((contract, index) => {
      const activeClass = contract.id === state.selectedContractId ? "active" : "";
      const selectedChip = activeClass ? '<span class="selected-chip">Selected</span>' : "";
      return `
        <div class="contract-item ${activeClass}" style="--i:${index}">
          <div class="item-headline">
            <p class="item-title">${escapeHtml(contract.tenantName)}</p>
            ${selectedChip}
          </div>
          <p class="item-meta">Rent: ${formatCurrency(contract.monthlyRent)} | Start: ${formatDate(contract.startDate)} | End: ${formatDate(contract.endDate)}</p>
          <div class="item-actions">
            <button class="btn btn-ghost" data-select-contract-id="${contract.id}" type="button">Select</button>
            <button class="btn btn-secondary" data-open-payment-for-contract-id="${contract.id}" type="button">Create Payment</button>
          </div>
        </div>
      `;
    })
    .join("");

  dom.contractsList.querySelectorAll("[data-select-contract-id]").forEach((button) => {
    button.addEventListener("click", () => {
      state.selectedContractId = Number(button.dataset.selectContractId);
      collapseTenantEditors();
      renderContracts(propertyId);
    });
  });

  dom.contractsList.querySelectorAll("[data-open-payment-for-contract-id]").forEach((button) => {
    button.addEventListener("click", () => {
      state.selectedContractId = Number(button.dataset.openPaymentForContractId);
      renderContracts(propertyId);

      if (!ensureWriteAccess()) {
        return;
      }

      if (dom.tenantPatchDetails) {
        dom.tenantPatchDetails.open = false;
      }
      if (dom.tenantPaymentDetails) {
        dom.tenantPaymentDetails.open = true;
      }
      dom.createPaymentInlineForm.paymentDate.focus();
    });
  });

  renderContractWorkspace(getSelectedContractForProperty(propertyId));
}

function renderContractWorkspace(contract) {
  if (!contract) {
    dom.emptyContractState.classList.remove("hidden");
    dom.contractWorkspace.classList.add("hidden");
    state.paymentDraftContractId = null;
    collapseTenantEditors();
    return;
  }

  dom.emptyContractState.classList.add("hidden");
  dom.contractWorkspace.classList.remove("hidden");

  dom.selectedContractName.textContent = contract.tenantName;
  dom.selectedContractMeta.textContent = `Rent ${formatCurrency(contract.monthlyRent)} | Start ${formatDate(contract.startDate)} | End ${formatDate(contract.endDate)}`;

  if (state.paymentDraftContractId !== contract.id) {
    dom.createPaymentInlineForm.amount.value = String(contract.monthlyRent);
    dom.createPaymentInlineForm.paymentDate.value = todayAsInputDate();
    state.paymentDraftContractId = contract.id;
  }
}

function getSelectedProperty() {
  return state.properties.find((item) => item.id === state.selectedPropertyId) || null;
}

function getSelectedContract() {
  return state.contracts.find((item) => item.id === state.selectedContractId) || null;
}

function getSelectedContractForProperty(propertyId) {
  if (!propertyId) {
    return null;
  }

  const selected = getSelectedContract();
  if (!selected || selected.propertyId !== propertyId) {
    return null;
  }

  return selected;
}

function normalizeProperty(raw) {
  return {
    id: Number(raw.id ?? raw.Id),
    name: String(raw.name ?? raw.Name ?? ""),
    address: String(raw.address ?? raw.Address ?? ""),
    house: Boolean(raw.house ?? raw.House ?? false),
    purchasePrice: Number(raw.purchasePrice ?? raw.PurchasePrice ?? 0),
    currentValue: Number(raw.currentValue ?? raw.CurrentValue ?? 0),
    purchaseDate: String(raw.purchaseDate ?? raw.PurchaseDate ?? "")
  };
}

function normalizeContract(raw) {
  return {
    id: Number(raw.id ?? raw.Id),
    tenantName: String(raw.tenantName ?? raw.TenantName ?? ""),
    monthlyRent: Number(raw.monthlyRent ?? raw.MonthlyRent ?? 0),
    startDate: String(raw.startDate ?? raw.StartDate ?? ""),
    endDate: raw.endDate ?? raw.EndDate ?? null,
    propertyId: Number(raw.propertyId ?? raw.PropertyId)
  };
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

function toDateInput(value) {
  if (!value) {
    return "";
  }
  return String(value).slice(0, 10);
}

function todayAsInputDate() {
  return new Date().toISOString().slice(0, 10);
}

function toNumber(value) {
  return Number(value);
}

function formatCurrency(number) {
  return new Intl.NumberFormat("cs-CZ", {
    style: "currency",
    currency: "CZK",
    maximumFractionDigits: 0
  }).format(number || 0);
}

function formatDate(value) {
  if (!value) {
    return "open";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "open";
  }

  return new Intl.DateTimeFormat("cs-CZ").format(date);
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}
