const $ = (sel) => document.querySelector(sel);

function showToast(message) {
  const el = $("#toast");
  el.textContent = message;
  el.classList.add("show");
  setTimeout(() => el.classList.remove("show"), 1800);
}

async function apiFetch(path, options = {}) {
  const resp = await fetch(path, {
    headers: { "Content-Type": "application/json", ...(options.headers || {}) },
    ...options
  });

  if (resp.ok) {
    const ct = resp.headers.get("content-type") || "";
    if (ct.includes("application/json")) return await resp.json();
    return await resp.text();
  }

  const ct = resp.headers.get("content-type") || "";
  let detail = "";
  if (ct.includes("application/json")) {
    try {
      const j = await resp.json();
      detail = j.detail || j.title || JSON.stringify(j);
    } catch {
      detail = "";
    }
  } else {
    try { detail = await resp.text(); } catch { detail = ""; }
  }
  const msg = `HTTP ${resp.status} ${resp.statusText}${detail ? `: ${detail}` : ""}`;
  throw new Error(msg);
}

function setActiveTab(name) {
  document.querySelectorAll(".tab").forEach(btn => {
    btn.classList.toggle("active", btn.dataset.tab === name);
  });
  $("#tab-monitors").classList.toggle("hidden", name !== "monitors");
  $("#tab-ai").classList.toggle("hidden", name !== "ai");
}

function httpTargetTemplate() {
  return {
    Method: "GET",
    Url: "https://example.com",
    Body: null,
    Headers: { "User-Agent": "AutoTest" },
    Query: {},
    Timeout: 10
  };
}

function tcpTargetTemplate() {
  return { Host: "127.0.0.1", Port: 80, Timeout: 5 };
}

function statusLabel(status) {
  const map = {
    0: "Pending",
    1: "Running",
    2: "Success",
    3: "Failed",
    4: "Disabled"
  };
  return map[status] ?? String(status);
}

function renderMonitorRow(m) {
  const tr = document.createElement("tr");

  const last = m.lastRunTime ? new Date(m.lastRunTime).toLocaleString() : "-";
  const enabled = m.isEnabled ? "是" : "否";

  tr.innerHTML = `
    <td>${escapeHtml(m.name)}</td>
    <td><span class="pill">${escapeHtml(m.targetType)}</span></td>
    <td>${escapeHtml(statusLabel(m.status))}</td>
    <td>${enabled}</td>
    <td>${escapeHtml(last)}</td>
    <td>${m.assertionCount ?? 0}</td>
    <td class="actions">
      <button class="btn" data-act="load">加载</button>
      <button class="btn" data-act="run">运行</button>
      <button class="btn" data-act="latest">最新</button>
    </td>
  `;

  tr.querySelectorAll("button").forEach(btn => {
    btn.addEventListener("click", async () => {
      const act = btn.dataset.act;
      if (act === "load") {
        await loadMonitor(m.id);
      } else if (act === "run") {
        await runMonitor(m.id);
      } else if (act === "latest") {
        await loadLatestExecution(m.id);
      }
    });
  });

  return tr;
}

function escapeHtml(s) {
  return String(s ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll("\"", "&quot;")
    .replaceAll("'", "&#39;");
}

async function refreshMonitors() {
  const tbody = $("#monitorTable tbody");
  tbody.innerHTML = "";
  $("#monitorListHint").textContent = "";

  try {
    const items = await apiFetch("/api/monitor/list?take=50", { method: "GET" });
    if (!Array.isArray(items) || items.length === 0) {
      $("#monitorListHint").textContent = "暂无监控。可以在右侧创建一个 HTTP 监控。";
      return;
    }
    items.forEach(m => tbody.appendChild(renderMonitorRow(m)));
  } catch (e) {
    $("#monitorListHint").textContent = String(e.message || e);
  }
}

function clearEditor() {
  $("#monitorId").value = "";
  $("#monitorName").value = "";
  $("#targetType").value = "HTTP";
  $("#targetConfig").value = JSON.stringify(httpTargetTemplate(), null, 2);
  $("#isEnabled").checked = true;
  $("#assertField").value = "StatusCode";
  $("#assertExpected").value = "200";
  $("#assertHeaderKey").value = "";
  $("#assertOperator").value = "Equal";
  $("#execMeta").innerHTML = "";
  $("#execResultJson").textContent = "";
  $("#assertionResultTable tbody").innerHTML = "";
  $("#executionTable tbody").innerHTML = "";
}

async function loadMonitor(id) {
  const monitor = await apiFetch(`/api/monitor/${id}`, { method: "GET" });
  $("#monitorId").value = monitor.id;
  $("#monitorName").value = monitor.name;
  $("#targetType").value = monitor.target?.type || "HTTP";
  $("#isEnabled").checked = !!monitor.isEnabled;

  if (monitor.target) {
    $("#targetConfig").value = JSON.stringify(monitor.target, null, 2);
  }

  showToast("已加载监控");
}

function buildMonitorDtoFromEditor(isNew) {
  const id = $("#monitorId").value.trim();
  const name = $("#monitorName").value.trim();
  const targetType = $("#targetType").value;
  const targetConfigText = $("#targetConfig").value.trim();
  const isEnabled = $("#isEnabled").checked;

  if (!name) throw new Error("名称不能为空");
  if (!targetConfigText) throw new Error("TargetConfig 不能为空");

  let targetConfigJson;
  try { targetConfigJson = JSON.parse(targetConfigText); } catch { throw new Error("TargetConfig 不是合法 JSON"); }

  const assertions = [];
  const expected = $("#assertExpected").value.trim();
  if (expected) {
    const field = $("#assertField").value;
    const headerKey = $("#assertHeaderKey").value.trim();
    const op = $("#assertOperator").value.trim() || "Equal";

    const config = {
      Id: crypto.randomUUID(),
      Field: field,
      Operator: op,
      HeaderKey: headerKey,
      Expected: expected
    };

    assertions.push({
      Id: config.Id,
      Type: "HTTP",
      ConfigJson: JSON.stringify(config)
    });
  }

  return {
    Id: isNew ? crypto.randomUUID() : id,
    Name: name,
    TargetType: targetType,
    TargetConfig: JSON.stringify(targetConfigJson),
    IsEnabled: isEnabled,
    Assertions: assertions
  };
}

async function saveMonitor() {
  const id = $("#monitorId").value.trim();
  const isNew = !id;

  const dto = buildMonitorDtoFromEditor(isNew);
  if (isNew) {
    const newId = await apiFetch("/api/monitor", { method: "POST", body: JSON.stringify(dto) });
    $("#monitorId").value = (typeof newId === "string" ? newId : String(newId));
    showToast("创建成功");
  } else {
    await apiFetch(`/api/monitor/${id}`, { method: "PUT", body: JSON.stringify(dto) });
    showToast("更新成功");
  }
  await refreshMonitors();
}

async function deleteMonitor() {
  const id = $("#monitorId").value.trim();
  if (!id) throw new Error("请先加载或创建一个监控");
  await apiFetch(`/api/monitor/${id}`, { method: "DELETE" });
  showToast("已删除");
  clearEditor();
  await refreshMonitors();
}

async function runMonitor(id) {
  await apiFetch(`/api/monitor/${id}/run`, { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" } });
  showToast("已入队执行");
}

function renderExecMeta(record) {
  const meta = [
    ["ExecutionId", record.id],
    ["MonitorId", record.monitorId],
    ["StartedAt", record.startedAt ? new Date(record.startedAt).toLocaleString() : "-"],
    ["FinishedAt", record.finishedAt ? new Date(record.finishedAt).toLocaleString() : "-"],
    ["IsExecutionSuccess", record.isExecutionSuccess ? "true" : "false"],
    ["ErrorMessage", record.errorMessage || ""],
    ["ResultType", record.resultType || ""]
  ];
  $("#execMeta").innerHTML = meta.map(([k, v]) => `<div><div class="k">${escapeHtml(k)}</div><div class="v">${escapeHtml(v)}</div></div>`).join("");
}

function renderAssertionResults(assertions) {
  const tbody = $("#assertionResultTable tbody");
  tbody.innerHTML = "";
  (assertions || []).forEach(a => {
    const tr = document.createElement("tr");
    const ok = a.isSuccess ? "ok" : "bad";
    tr.innerHTML = `
      <td>${escapeHtml(a.target)}</td>
      <td><span class="pill ${ok}">${a.isSuccess ? "true" : "false"}</span></td>
      <td>${escapeHtml(a.actual || "")}</td>
      <td>${escapeHtml(a.expected || "")}</td>
      <td>${escapeHtml(a.message || "")}</td>
    `;
    tbody.appendChild(tr);
  });
}

async function loadLatestExecution(monitorId) {
  $("#execMeta").innerHTML = "";
  $("#execResultJson").textContent = "";
  $("#assertionResultTable tbody").innerHTML = "";
  try {
    const data = await apiFetch(`/api/monitor/${monitorId}/executions/latest`, { method: "GET" });
    renderExecMeta(data.record);

    const json = data.record?.resultJson;
    if (typeof json === "string" && json.trim()) {
      try { $("#execResultJson").textContent = JSON.stringify(JSON.parse(json), null, 2); }
      catch { $("#execResultJson").textContent = json; }
    }

    renderAssertionResults(data.assertions);
  } catch (e) {
    $("#execResultJson").textContent = String(e.message || e);
  }
}

async function loadExecutions() {
  const id = $("#monitorId").value.trim();
  if (!id) throw new Error("请先加载或创建一个监控");

  const tbody = $("#executionTable tbody");
  tbody.innerHTML = "";

  const items = await apiFetch(`/api/monitor/${id}/executions?take=20`, { method: "GET" });
  (items || []).forEach(r => {
    const tr = document.createElement("tr");
    const started = r.startedAt ? new Date(r.startedAt).toLocaleString() : "-";
    tr.innerHTML = `
      <td>${escapeHtml(started)}</td>
      <td><span class="pill ${r.isExecutionSuccess ? "ok" : "bad"}">${r.isExecutionSuccess ? "true" : "false"}</span></td>
      <td>${escapeHtml(r.resultType || "")}</td>
      <td><button class="btn" data-act="assertions">断言</button></td>
    `;
    tr.querySelector("button").addEventListener("click", async () => {
      const assertions = await apiFetch(`/api/monitor/executions/${r.id}/assertions`, { method: "GET" });
      renderExecMeta(r);
      const json = r.resultJson;
      if (typeof json === "string" && json.trim()) {
        try { $("#execResultJson").textContent = JSON.stringify(JSON.parse(json), null, 2); }
        catch { $("#execResultJson").textContent = json; }
      } else {
        $("#execResultJson").textContent = "";
      }
      renderAssertionResults(assertions);
      showToast("已加载该次执行");
    });
    tbody.appendChild(tr);
  });
}

function addChatMessage(role, text) {
  const box = $("#aiChatBox");
  const wrap = document.createElement("div");
  wrap.className = "chat-msg";
  wrap.innerHTML = `<div class="chat-role">${escapeHtml(role)}</div><div class="chat-text">${escapeHtml(text)}</div>`;
  box.appendChild(wrap);
  box.scrollTop = box.scrollHeight;
}

async function sendAi() {
  const system = $("#aiSystem").value.trim();
  const message = $("#aiMessage").value.trim();
  if (!message) return;
  $("#aiMessage").value = "";
  $("#aiHint").textContent = "";

  addChatMessage("user", message);
  try {
    const data = await apiFetch("/api/ai/chat", { method: "POST", body: JSON.stringify({ message, system }) });
    addChatMessage("assistant", data.reply ?? String(data));
  } catch (e) {
    $("#aiHint").textContent = String(e.message || e);
    addChatMessage("error", String(e.message || e));
  }
}

async function checkServer() {
  try {
    const txt = await apiFetch("/api/monitor", { method: "GET" });
    $("#serverStatus").textContent = typeof txt === "string" ? txt : "OK";
  } catch {
    $("#serverStatus").textContent = "连接失败";
  }
}

function wireUi() {
  document.querySelectorAll(".tab").forEach(btn => {
    btn.addEventListener("click", () => setActiveTab(btn.dataset.tab));
  });

  $("#btnRefreshMonitors").addEventListener("click", refreshMonitors);
  $("#btnNewMonitor").addEventListener("click", () => { clearEditor(); showToast("已切换到新建"); });
  $("#btnFillHttpTemplate").addEventListener("click", () => { $("#targetType").value = "HTTP"; $("#targetConfig").value = JSON.stringify(httpTargetTemplate(), null, 2); });
  $("#btnFillTcpTemplate").addEventListener("click", () => { $("#targetType").value = "TCP"; $("#targetConfig").value = JSON.stringify(tcpTargetTemplate(), null, 2); });

  $("#btnSaveMonitor").addEventListener("click", async () => {
    try { await saveMonitor(); } catch (e) { showToast(String(e.message || e)); }
  });
  $("#btnDeleteMonitor").addEventListener("click", async () => {
    try { await deleteMonitor(); } catch (e) { showToast(String(e.message || e)); }
  });

  $("#btnRunMonitor").addEventListener("click", async () => {
    const id = $("#monitorId").value.trim();
    if (!id) { showToast("请先保存/加载一个监控"); return; }
    try { await runMonitor(id); } catch (e) { showToast(String(e.message || e)); }
  });

  $("#btnLoadLatest").addEventListener("click", async () => {
    const id = $("#monitorId").value.trim();
    if (!id) { showToast("请先保存/加载一个监控"); return; }
    await loadLatestExecution(id);
  });

  $("#btnLoadExecutions").addEventListener("click", async () => {
    try { await loadExecutions(); } catch (e) { showToast(String(e.message || e)); }
  });

  $("#btnAiSend").addEventListener("click", sendAi);
  $("#aiMessage").addEventListener("keydown", (e) => {
    if (e.key === "Enter") sendAi();
  });
  $("#btnAiClear").addEventListener("click", () => {
    $("#aiChatBox").innerHTML = "";
    $("#aiHint").textContent = "";
  });
}

async function init() {
  wireUi();
  setActiveTab("monitors");
  clearEditor();
  await checkServer();
  await refreshMonitors();
}

init();

