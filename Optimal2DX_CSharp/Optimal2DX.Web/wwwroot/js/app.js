/**
 * Cut2DX Web Edition - Main Application Logic
 * Supports Multi-Row Clipboard Paste, Excel/CSV Import, Interactive SVG Diagram Viewer & Reports
 */

// Application State
const appState = {
  demandList: [],
  repoList: [],
  optResult: null,
  currentPatternIdx: 0,
  
  // SVG Viewport Pan & Zoom
  zoom: 1,
  panX: 0,
  panY: 0,
  isDragging: false,
  dragStartX: 0,
  dragStartY: 0,

  // Paste / Import Temporary Buffer
  pendingPasteData: null,
  pendingExcelData: null
};

// Preset 1: Mẫu theo ảnh (8 quy cách, 1.050 chi tiết / 163 tấm kho)
const SAMPLE_IMAGE_DEMAND = [
  { length: 1568, width: 953,  quantity: 45,  label: "Cánh tủ trên",    grain: "≡ Dọc (Theo Dài)", canRotate: false, material: "MDF" },
  { length: 754,  width: 1008, quantity: 45,  label: "Hông tủ trái",    grain: "≡ Dọc (Theo Dài)", canRotate: false, material: "MDF" },
  { length: 1284, width: 959,  quantity: 36,  label: "Hông tủ phải",    grain: "≡ Dọc (Theo Dài)", canRotate: false, material: "MDF" },
  { length: 618,  width: 642,  quantity: 96,  label: "Đáy tủ",          grain: "≡ Dọc (Theo Dài)", canRotate: false, material: "MDF" },
  { length: 482,  width: 340,  quantity: 288, label: "Ngăn kéo lớn",    grain: "≡ Dọc (Theo Dài)", canRotate: false, material: "MDF" },
  { length: 779,  width: 340,  quantity: 180, label: "Đợt ngăn di động",grain: "≡ Dọc (Theo Dài)", canRotate: false, material: "MDF" },
  { length: 663,  width: 340,  quantity: 270, label: "Thành ngăn kéo",  grain: "≡ Dọc (Theo Dài)", canRotate: false, material: "MDF" },
  { length: 650,  width: 340,  quantity: 90,  label: "Hậu tủ nhỏ",      grain: "≡ Dọc (Theo Dài)", canRotate: false, material: "MDF" }
];

const SAMPLE_IMAGE_REPO = [
  { length: 2440, width: 1220, quantity: 163, label: "Ván MDF 2440x1220", sheetGrain: "≡ Dọc (Theo Dài)", material: "MDF", trimTop: 2, trimLeft: 2, trimBottom: 0, trimRight: 0, price: 0 }
];

// Document Ready Initialization
document.addEventListener("DOMContentLoaded", () => {
  initEventListeners();
  loadSampleImagePreset();
});

// -------------------------------------------------------------
// EVENT LISTENERS INITIALIZATION
// -------------------------------------------------------------
function initEventListeners() {
  // Tabs switching
  document.querySelectorAll(".input-tab").forEach(tab => {
    tab.addEventListener("click", () => {
      document.querySelectorAll(".input-tab").forEach(t => t.classList.remove("active"));
      document.querySelectorAll(".left-panel .tab-pane").forEach(p => p.classList.remove("active"));
      tab.classList.add("active");
      const targetId = tab.dataset.tab;
      document.getElementById(targetId)?.classList.add("active");
    });
  });

  document.querySelectorAll(".result-tab-btn").forEach(btn => {
    btn.addEventListener("click", () => {
      document.querySelectorAll(".result-tab-btn").forEach(b => b.classList.remove("active"));
      document.querySelectorAll(".result-tab-content").forEach(c => c.classList.remove("active"));
      btn.classList.add("active");
      const targetId = btn.dataset.tab;
      document.getElementById(targetId)?.classList.add("active");
    });
  });

  // Presets
  document.getElementById("btnPresetImage")?.addEventListener("click", loadSampleImagePreset);
  document.getElementById("btnPresetGrain")?.addEventListener("click", loadSampleGrainPreset);
  document.getElementById("btnPresetBatch")?.addEventListener("click", loadSampleBatchPreset);
  document.getElementById("btnClearAll")?.addEventListener("click", clearAllData);
  document.getElementById("btnHelpGrainTop")?.addEventListener("click", () => openModal("modalGrainHelp"));
  document.getElementById("btnHelpGrainBanner")?.addEventListener("click", () => openModal("modalGrainHelp"));

  // Demand Table buttons
  document.getElementById("btnAddDemandRow")?.addEventListener("click", () => addDemandRow());
  document.getElementById("btnDeleteSelectedDemand")?.addEventListener("click", deleteSelectedDemandRows);
  document.getElementById("chkSelectAllDemand")?.addEventListener("change", (e) => {
    document.querySelectorAll(".chk-demand-item").forEach(c => c.checked = e.target.checked);
  });

  // Repo Table buttons
  document.getElementById("btnAddRepoRow")?.addEventListener("click", () => addRepoRow());
  document.getElementById("btnDeleteSelectedRepo")?.addEventListener("click", deleteSelectedRepoRows);
  document.getElementById("chkSelectAllRepo")?.addEventListener("change", (e) => {
    document.querySelectorAll(".chk-repo-item").forEach(c => c.checked = e.target.checked);
  });

  // Direct Grid Paste (Ctrl+V) Listeners
  initGridPasteListeners();

  // Modal Open Buttons
  document.getElementById("btnTopPasteClip")?.addEventListener("click", () => openPasteModal("demand"));
  document.getElementById("btnPasteDemand")?.addEventListener("click", () => openPasteModal("demand"));
  document.getElementById("btnPasteRepo")?.addEventListener("click", () => openPasteModal("repo"));

  document.getElementById("btnTopImportExcel")?.addEventListener("click", () => openExcelModal("auto"));
  document.getElementById("btnImportDemandExcel")?.addEventListener("click", () => openExcelModal("demand"));
  document.getElementById("btnImportRepoExcel")?.addEventListener("click", () => openExcelModal("repo"));

  // Modal Close buttons
  document.querySelectorAll("[data-close]").forEach(btn => {
    btn.addEventListener("click", () => {
      const modalId = btn.dataset.close;
      closeModal(modalId);
    });
  });

  // Clipboard Modal Actions
  document.getElementById("btnModalReadClipboard")?.addEventListener("click", readFromBrowserClipboard);
  document.getElementById("btnParseClipboardText")?.addEventListener("click", parsePastedText);
  document.getElementById("btnApplyPaste")?.addEventListener("click", applyPastedData);
  document.getElementById("txtPasteArea")?.addEventListener("input", () => {
    if (document.getElementById("txtPasteArea").value.trim().length > 0) {
      parsePastedText();
    }
  });

  // Excel Modal Actions & Drag and Drop
  initExcelImportHandlers();

  // Run Optimization
  document.getElementById("btnRunOptimization")?.addEventListener("click", executeOptimization);

  // SVG Diagram Navigation & Tools
  document.getElementById("btnPrevPattern")?.addEventListener("click", () => changePattern(appState.currentPatternIdx - 1));
  document.getElementById("btnNextPattern")?.addEventListener("click", () => changePattern(appState.currentPatternIdx + 1));
  document.getElementById("selPatternIndex")?.addEventListener("change", (e) => changePattern(parseInt(e.target.value)));

  document.getElementById("btnZoomIn")?.addEventListener("click", () => zoomSvg(1.2));
  document.getElementById("btnZoomOut")?.addEventListener("click", () => zoomSvg(0.8));
  document.getElementById("btnZoomReset")?.addEventListener("click", resetSvgZoom);
  document.getElementById("btnZoomFit")?.addEventListener("click", fitSvgZoom);
  document.getElementById("btnExportSvg")?.addEventListener("click", exportCurrentSvg);

  // Interactive SVG Pan
  initSvgPanListeners();

  // Export Buttons
  document.getElementById("btnExportHtmlTop")?.addEventListener("click", downloadHtmlReport);
  document.getElementById("btnExportHtmlBottom")?.addEventListener("click", downloadHtmlReport);
  document.getElementById("btnExportExcelTop")?.addEventListener("click", downloadExcelResult);
  document.getElementById("btnExportExcelBottom")?.addEventListener("click", downloadExcelResult);
  document.getElementById("btnExportTxt")?.addEventListener("click", downloadTxtResult);
}

// -------------------------------------------------------------
// GRID RENDERING & DATA MANAGEMENT
// -------------------------------------------------------------

function renderDemandTable() {
  const tbody = document.getElementById("tbodyDemand");
  if (!tbody) return;
  tbody.innerHTML = "";

  let totalQty = 0;

  appState.demandList.forEach((item, idx) => {
    totalQty += (parseInt(item.quantity) || 1);
    const tr = document.createElement("tr");

    tr.innerHTML = `
      <td><input type="checkbox" class="chk-demand-item" data-idx="${idx}" /></td>
      <td style="color:var(--text-muted); font-weight:700;">${idx + 1}</td>
      <td><input type="number" class="inp-dem-len" data-idx="${idx}" value="${item.length}" min="1" /></td>
      <td><input type="number" class="inp-dem-wid" data-idx="${idx}" value="${item.width}" min="1" /></td>
      <td><input type="number" class="inp-dem-qty" data-idx="${idx}" value="${item.quantity}" min="1" /></td>
      <td><input type="text" class="inp-dem-lbl" data-idx="${idx}" value="${escapeHtml(item.label || '')}" placeholder="Nhãn chi tiết" /></td>
      <td>
        <select class="sel-dem-grain" data-idx="${idx}">
          <option value="≡ Dọc (Theo Dài)" ${item.grain?.includes("Dọc") ? "selected" : ""}>≡ Dọc (Theo Dài)</option>
          <option value="||| Ngang (Theo Rộng)" ${item.grain?.includes("Ngang") ? "selected" : ""}>||| Ngang (Theo Rộng)</option>
          <option value="🔄 Tự do (Không thớ)" ${item.grain?.includes("Tự do") ? "selected" : ""}>🔄 Tự do (Không thớ)</option>
          <option value="🔒 Cố định (Không xoay)" ${item.grain?.includes("Cố định") ? "selected" : ""}>🔒 Cố định (Không xoay)</option>
        </select>
      </td>
      <td style="text-align: center;">
        <input type="checkbox" class="chk-dem-rot" data-idx="${idx}" ${item.canRotate ? "checked" : ""} />
      </td>
      <td><input type="text" class="inp-dem-mat" data-idx="${idx}" value="${escapeHtml(item.material || '')}" placeholder="MDF" /></td>
      <td><button class="btn-del-row" data-action="del-demand" data-idx="${idx}" title="Xóa dòng này">&times;</button></td>
    `;

    tbody.appendChild(tr);
  });

  // Attach dynamic input change events
  tbody.querySelectorAll("input, select").forEach(el => {
    el.addEventListener("change", handleDemandChange);
  });

  tbody.querySelectorAll(".btn-del-row").forEach(btn => {
    btn.addEventListener("click", (e) => {
      const idx = parseInt(e.target.dataset.idx);
      appState.demandList.splice(idx, 1);
      renderDemandTable();
    });
  });

  // Update summary badge
  document.getElementById("badgeDemandCount").textContent = appState.demandList.length;
  document.getElementById("lblDemandSummary").textContent = `${appState.demandList.length} quy cách (${totalQty.toLocaleString()} chi tiết)`;
}

function handleDemandChange(e) {
  const idx = parseInt(e.target.dataset.idx);
  if (isNaN(idx) || idx < 0 || idx >= appState.demandList.length) return;
  const item = appState.demandList[idx];

  if (e.target.classList.contains("inp-dem-len")) item.length = parseInt(e.target.value) || 0;
  else if (e.target.classList.contains("inp-dem-wid")) item.width = parseInt(e.target.value) || 0;
  else if (e.target.classList.contains("inp-dem-qty")) {
    item.quantity = parseInt(e.target.value) || 1;
    updateDemandSummaryBadge();
  }
  else if (e.target.classList.contains("inp-dem-lbl")) item.label = e.target.value;
  else if (e.target.classList.contains("inp-dem-mat")) item.material = e.target.value;
  else if (e.target.classList.contains("sel-dem-grain")) {
    item.grain = e.target.value;
    const canRot = item.grain.includes("Tự do");
    item.canRotate = canRot;
    const tr = e.target.closest("tr");
    const chkRot = tr.querySelector(".chk-dem-rot");
    if (chkRot) chkRot.checked = canRot;
  }
  else if (e.target.classList.contains("chk-dem-rot")) {
    item.canRotate = e.target.checked;
    if (item.canRotate && !item.grain.includes("Tự do")) {
      item.grain = "🔄 Tự do (Không thớ)";
      const tr = e.target.closest("tr");
      const selGrain = tr.querySelector(".sel-dem-grain");
      if (selGrain) selGrain.value = item.grain;
    }
  }
}

function updateDemandSummaryBadge() {
  const totalQty = appState.demandList.reduce((sum, item) => sum + (parseInt(item.quantity) || 1), 0);
  document.getElementById("lblDemandSummary").textContent = `${appState.demandList.length} quy cách (${totalQty.toLocaleString()} chi tiết)`;
}

function addDemandRow(len = 500, wid = 350, qty = 1, lbl = "", grain = "≡ Dọc (Theo Dài)", mat = "MDF") {
  const nextIdx = appState.demandList.length + 1;
  appState.demandList.push({
    length: len,
    width: wid,
    quantity: qty,
    label: lbl || `Chi tiết #${nextIdx}`,
    grain: grain,
    canRotate: grain.includes("Tự do"),
    material: mat
  });
  renderDemandTable();
  // Scroll to bottom
  const wrapper = document.getElementById("wrapperDemandTable");
  wrapper.scrollTop = wrapper.scrollHeight;
}

function deleteSelectedDemandRows() {
  const checked = document.querySelectorAll(".chk-demand-item:checked");
  if (checked.length === 0) {
    showToast("Vui lòng chọn ít nhất một dòng cần xóa!", "error");
    return;
  }
  const indicesToDelete = Array.from(checked).map(c => parseInt(c.dataset.idx)).sort((a, b) => b - a);
  indicesToDelete.forEach(idx => appState.demandList.splice(idx, 1));
  renderDemandTable();
  showToast(`Đã xóa ${indicesToDelete.length} chi tiết!`, "info");
}

function renderRepoTable() {
  const tbody = document.getElementById("tbodyRepo");
  if (!tbody) return;
  tbody.innerHTML = "";

  let totalQty = 0;

  appState.repoList.forEach((item, idx) => {
    totalQty += (parseInt(item.quantity) || 1);
    const tr = document.createElement("tr");

    tr.innerHTML = `
      <td><input type="checkbox" class="chk-repo-item" data-idx="${idx}" /></td>
      <td style="color:var(--text-muted); font-weight:700;">${idx + 1}</td>
      <td><input type="number" class="inp-repo-len" data-idx="${idx}" value="${item.length}" min="1" /></td>
      <td><input type="number" class="inp-repo-wid" data-idx="${idx}" value="${item.width}" min="1" /></td>
      <td><input type="number" class="inp-repo-qty" data-idx="${idx}" value="${item.quantity}" min="1" /></td>
      <td><input type="text" class="inp-repo-lbl" data-idx="${idx}" value="${escapeHtml(item.label || '')}" placeholder="Tên tấm ván" /></td>
      <td>
        <select class="sel-repo-grain" data-idx="${idx}">
          <option value="≡ Dọc (Theo Dài)" ${item.sheetGrain?.includes("Dọc") ? "selected" : ""}>≡ Dọc (Theo Dài)</option>
          <option value="||| Ngang (Theo Rộng)" ${item.sheetGrain?.includes("Ngang") ? "selected" : ""}>||| Ngang (Theo Rộng)</option>
          <option value="🔄 Không thớ" ${item.sheetGrain?.includes("Không thớ") ? "selected" : ""}>🔄 Không thớ</option>
        </select>
      </td>
      <td><input type="number" class="inp-repo-top" data-idx="${idx}" value="${item.trimTop || 0}" min="0" style="padding:4px 2px;" /></td>
      <td><input type="number" class="inp-repo-left" data-idx="${idx}" value="${item.trimLeft || 0}" min="0" style="padding:4px 2px;" /></td>
      <td><input type="number" class="inp-repo-bot" data-idx="${idx}" value="${item.trimBottom || 0}" min="0" style="padding:4px 2px;" /></td>
      <td><input type="number" class="inp-repo-right" data-idx="${idx}" value="${item.trimRight || 0}" min="0" style="padding:4px 2px;" /></td>
      <td><button class="btn-del-row" data-action="del-repo" data-idx="${idx}" title="Xóa tấm này">&times;</button></td>
    `;

    tbody.appendChild(tr);
  });

  // Attach change events
  tbody.querySelectorAll("input, select").forEach(el => {
    el.addEventListener("change", handleRepoChange);
  });

  tbody.querySelectorAll(".btn-del-row").forEach(btn => {
    btn.addEventListener("click", (e) => {
      const idx = parseInt(e.target.dataset.idx);
      appState.repoList.splice(idx, 1);
      renderRepoTable();
    });
  });

  document.getElementById("badgeRepoCount").textContent = appState.repoList.length;
  document.getElementById("lblRepoSummary").textContent = `${appState.repoList.length} quy cách (${totalQty.toLocaleString()} tấm)`;
}

function handleRepoChange(e) {
  const idx = parseInt(e.target.dataset.idx);
  if (isNaN(idx) || idx < 0 || idx >= appState.repoList.length) return;
  const item = appState.repoList[idx];

  if (e.target.classList.contains("inp-repo-len")) item.length = parseInt(e.target.value) || 0;
  else if (e.target.classList.contains("inp-repo-wid")) item.width = parseInt(e.target.value) || 0;
  else if (e.target.classList.contains("inp-repo-qty")) {
    item.quantity = parseInt(e.target.value) || 1;
    updateRepoSummaryBadge();
  }
  else if (e.target.classList.contains("inp-repo-lbl")) item.label = e.target.value;
  else if (e.target.classList.contains("sel-repo-grain")) item.sheetGrain = e.target.value;
  else if (e.target.classList.contains("inp-repo-top")) item.trimTop = parseInt(e.target.value) || 0;
  else if (e.target.classList.contains("inp-repo-left")) item.trimLeft = parseInt(e.target.value) || 0;
  else if (e.target.classList.contains("inp-repo-bot")) item.trimBottom = parseInt(e.target.value) || 0;
  else if (e.target.classList.contains("inp-repo-right")) item.trimRight = parseInt(e.target.value) || 0;
}

function updateRepoSummaryBadge() {
  const totalQty = appState.repoList.reduce((sum, item) => sum + (parseInt(item.quantity) || 1), 0);
  document.getElementById("lblRepoSummary").textContent = `${appState.repoList.length} quy cách (${totalQty.toLocaleString()} tấm)`;
}

function addRepoRow(len = 2440, wid = 1220, qty = 10, lbl = "", grain = "≡ Dọc (Theo Dài)", top = 2, left = 2, bot = 0, right = 0) {
  const nextIdx = appState.repoList.length + 1;
  appState.repoList.push({
    length: len,
    width: wid,
    quantity: qty,
    label: lbl || `Ván MDF #${nextIdx} (${len}x${wid})`,
    sheetGrain: grain,
    material: "MDF",
    trimTop: top,
    trimLeft: left,
    trimBottom: bot,
    trimRight: right,
    price: 0
  });
  renderRepoTable();
  const wrapper = document.getElementById("wrapperRepoTable");
  wrapper.scrollTop = wrapper.scrollHeight;
}

function deleteSelectedRepoRows() {
  const checked = document.querySelectorAll(".chk-repo-item:checked");
  if (checked.length === 0) {
    showToast("Vui lòng chọn ít nhất một tấm cần xóa!", "error");
    return;
  }
  const indicesToDelete = Array.from(checked).map(c => parseInt(c.dataset.idx)).sort((a, b) => b - a);
  indicesToDelete.forEach(idx => appState.repoList.splice(idx, 1));
  renderRepoTable();
  showToast(`Đã xóa ${indicesToDelete.length} tấm kho!`, "info");
}

// -------------------------------------------------------------
// PRESETS
// -------------------------------------------------------------
function loadSampleImagePreset() {
  appState.demandList = JSON.parse(JSON.stringify(SAMPLE_IMAGE_DEMAND));
  appState.repoList = JSON.parse(JSON.stringify(SAMPLE_IMAGE_REPO));

  document.getElementById("numBladeWidth").value = 3.2;
  document.getElementById("numOptLevel").value = 20;
  document.getElementById("numRandomSeed").value = 1;
  document.getElementById("numMinUseful").value = 100;
  document.getElementById("selMethod").value = "0"; // Guillotine
  document.getElementById("chkMinimizePatterns").checked = true;

  renderDemandTable();
  renderRepoTable();
  showToast("Đã nạp bộ dữ liệu mẫu chuẩn theo ảnh (1.050 chi tiết / 163 tấm kho)!", "success");
}

function loadSampleGrainPreset() {
  appState.demandList = [
    { length: 1200, width: 400, quantity: 20, label: "Mặt bàn (Thớ Dọc)",    grain: "≡ Dọc (Theo Dài)", canRotate: false, material: "Gỗ sồi" },
    { length: 400,  width: 800, quantity: 20, label: "Hông tủ (Thớ Ngang)",  grain: "||| Ngang (Theo Rộng)", canRotate: false, material: "Gỗ sồi" },
    { length: 500,  width: 350, quantity: 40, label: "Đáy ngăn kéo (Tự do)", grain: "🔄 Tự do (Không thớ)", canRotate: true, material: "Gỗ sồi" },
    { length: 600,  width: 300, quantity: 15, label: "Khóa xoay cố định",    grain: "🔒 Cố định (Không xoay)", canRotate: false, material: "Gỗ sồi" }
  ];
  appState.repoList = [
    { length: 2440, width: 1220, quantity: 30, label: "Tấm ván sồi 2440x1220", sheetGrain: "≡ Dọc (Theo Dài)", material: "Gỗ sồi", trimTop: 5, trimLeft: 5, trimBottom: 5, trimRight: 5, price: 0 }
  ];

  document.getElementById("numBladeWidth").value = 3.2;
  document.getElementById("numOptLevel").value = 50;
  document.getElementById("chkMinimizePatterns").checked = true;

  renderDemandTable();
  renderRepoTable();
  showToast("Đã nạp ví dụ thớ gỗ & kiểm soát xoay!", "success");
}

function loadSampleBatchPreset() {
  appState.demandList = [
    { length: 800, width: 400, quantity: 120, label: "Cánh tủ chuẩn 800x400", grain: "≡ Dọc (Theo Dài)", canRotate: false, material: "MFC" },
    { length: 600, width: 400, quantity: 80,  label: "Ngăn kéo 600x400",      grain: "≡ Dọc (Theo Dài)", canRotate: false, material: "MFC" },
    { length: 400, width: 300, quantity: 200, label: "Hộc tủ 400x300",        grain: "≡ Dọc (Theo Dài)", canRotate: false, material: "MFC" }
  ];
  appState.repoList = [
    { length: 2440, width: 1220, quantity: 60, label: "Ván MFC Phủ Melamine", sheetGrain: "≡ Dọc (Theo Dài)", material: "MFC", trimTop: 3, trimLeft: 3, trimBottom: 3, trimRight: 3, price: 0 }
  ];

  document.getElementById("numBladeWidth").value = 3.2;
  document.getElementById("numOptLevel").value = 50;
  document.getElementById("chkMinimizePatterns").checked = true;

  renderDemandTable();
  renderRepoTable();
  showToast("Đã nạp ví dụ sản xuất hàng loạt (Batch Mode)!", "success");
}

function clearAllData() {
  if (confirm("Bạn có chắc chắn muốn xóa sạch toàn bộ danh sách chi tiết và tấm kho?")) {
    appState.demandList = [];
    appState.repoList = [];
    renderDemandTable();
    renderRepoTable();
    resetResultsView();
    showToast("Đã xóa sạch dữ liệu!", "info");
  }
}

// -------------------------------------------------------------
// MULTI-ROW CLIPBOARD PASTE
// -------------------------------------------------------------
function initGridPasteListeners() {
  const demandWrapper = document.getElementById("wrapperDemandTable");
  const repoWrapper = document.getElementById("wrapperRepoTable");

  demandWrapper?.addEventListener("paste", (e) => handleDirectGridPaste(e, "demand"));
  repoWrapper?.addEventListener("paste", (e) => handleDirectGridPaste(e, "repo"));
}

function handleDirectGridPaste(e, target) {
  // If user is currently typing in an input element and only a single cell is targeted, check text
  const clipboardText = (e.clipboardData || window.clipboardData).getData("text");
  if (!clipboardText) return;

  // If text contains newline or tab, it is multi-row/multi-column tabular data!
  if (clipboardText.includes("\n") || clipboardText.includes("\t")) {
    e.preventDefault();
    const rows = parseDelimitedText(clipboardText);
    if (rows.length === 0) return;

    if (target === "demand") {
      const parsedItems = mapRowsToDemand(rows);
      appState.demandList.push(...parsedItems);
      renderDemandTable();
      showToast(`✅ Đã dán thành công ${parsedItems.length} chi tiết từ Clipboard vào bảng!`, "success");
    } else {
      const parsedItems = mapRowsToRepo(rows);
      appState.repoList.push(...parsedItems);
      renderRepoTable();
      showToast(`✅ Đã dán thành công ${parsedItems.length} tấm kho từ Clipboard vào bảng!`, "success");
    }
  }
}

function openPasteModal(defaultTarget = "demand") {
  document.getElementById("selPasteTarget").value = defaultTarget;
  document.getElementById("txtPasteArea").value = "";
  document.getElementById("pastePreviewSection").style.display = "none";
  document.getElementById("btnApplyPaste").disabled = true;
  appState.pendingPasteData = null;
  openModal("modalClipboard");
  setTimeout(() => document.getElementById("txtPasteArea").focus(), 150);
}

async function readFromBrowserClipboard() {
  try {
    const text = await navigator.clipboard.readText();
    if (text) {
      document.getElementById("txtPasteArea").value = text;
      parsePastedText();
      showToast("Đã đọc dữ liệu từ bộ nhớ tạm!", "info");
    } else {
      showToast("Bộ nhớ tạm (Clipboard) đang trống!", "warning");
    }
  } catch (err) {
    showToast("Vui lòng dán trực tiếp (Ctrl+V) vào ô văn bản!", "warning");
  }
}

function parsePastedText() {
  const text = document.getElementById("txtPasteArea").value;
  if (!text || text.trim().length === 0) {
    document.getElementById("pastePreviewSection").style.display = "none";
    document.getElementById("btnApplyPaste").disabled = true;
    return;
  }

  const rawRows = parseDelimitedText(text);
  if (rawRows.length === 0) return;

  const target = document.getElementById("selPasteTarget").value;
  let parsedItems = [];

  if (target === "demand") {
    parsedItems = mapRowsToDemand(rawRows);
  } else {
    parsedItems = mapRowsToRepo(rawRows);
  }

  appState.pendingPasteData = { target, items: parsedItems };
  renderPastePreview(target, parsedItems);
}

function renderPastePreview(target, items) {
  const previewSec = document.getElementById("pastePreviewSection");
  const thead = document.getElementById("theadPastePreview");
  const tbody = document.getElementById("tbodyPastePreview");
  const rowCount = document.getElementById("lblPasteRowCount");

  previewSec.style.display = "block";
  rowCount.textContent = items.length;
  document.getElementById("btnApplyPaste").disabled = items.length === 0;

  if (target === "demand") {
    thead.innerHTML = `
      <tr>
        <th>#</th>
        <th>Chiều dài (mm)</th>
        <th>Chiều rộng (mm)</th>
        <th>Số lượng</th>
        <th>Tên / Nhãn chi tiết</th>
        <th>Thớ gỗ</th>
        <th>Vật liệu</th>
      </tr>
    `;
    tbody.innerHTML = items.slice(0, 15).map((it, idx) => `
      <tr>
        <td>${idx + 1}</td>
        <td><strong>${it.length}</strong></td>
        <td><strong>${it.width}</strong></td>
        <td>${it.quantity}</td>
        <td>${escapeHtml(it.label)}</td>
        <td>${escapeHtml(it.grain)}</td>
        <td>${escapeHtml(it.material)}</td>
      </tr>
    `).join("");
    if (items.length > 15) {
      tbody.innerHTML += `<tr><td colspan="7" style="text-align:center; color:var(--text-muted); font-style:italic;">... và ${items.length - 15} dòng khác</td></tr>`;
    }
  } else {
    thead.innerHTML = `
      <tr>
        <th>#</th>
        <th>Chiều dài (mm)</th>
        <th>Chiều rộng (mm)</th>
        <th>Số lượng tồn kho</th>
        <th>Tên tấm ván</th>
        <th>Thớ gỗ</th>
        <th>Cắt mép (Trim T,L,B,R)</th>
      </tr>
    `;
    tbody.innerHTML = items.slice(0, 15).map((it, idx) => `
      <tr>
        <td>${idx + 1}</td>
        <td><strong>${it.length}</strong></td>
        <td><strong>${it.width}</strong></td>
        <td>${it.quantity}</td>
        <td>${escapeHtml(it.label)}</td>
        <td>${escapeHtml(it.sheetGrain)}</td>
        <td>${it.trimTop}, ${it.trimLeft}, ${it.trimBottom}, ${it.trimRight}</td>
      </tr>
    `).join("");
    if (items.length > 15) {
      tbody.innerHTML += `<tr><td colspan="7" style="text-align:center; color:var(--text-muted); font-style:italic;">... và ${items.length - 15} dòng khác</td></tr>`;
    }
  }
}

function applyPastedData() {
  if (!appState.pendingPasteData || appState.pendingPasteData.items.length === 0) return;
  const { target, items } = appState.pendingPasteData;
  const mode = document.getElementById("selPasteMode").value; // append or overwrite

  if (target === "demand") {
    if (mode === "overwrite") appState.demandList = items;
    else appState.demandList.push(...items);
    renderDemandTable();
    showToast(`Đã áp dụng thành công ${items.length} chi tiết cần cắt!`, "success");
  } else {
    if (mode === "overwrite") appState.repoList = items;
    else appState.repoList.push(...items);
    renderRepoTable();
    showToast(`Đã áp dụng thành công ${items.length} tấm kho!`, "success");
  }

  closeModal("modalClipboard");
}

function parseDelimitedText(text) {
  const lines = text.split(/\r?\n/).map(l => l.trim()).filter(l => l.length > 0);
  const rows = [];

  for (const line of lines) {
    let cells = [];
    if (line.includes("\t")) {
      cells = line.split("\t");
    } else if (line.includes(";")) {
      cells = line.split(";");
    } else if (line.includes(",")) {
      // Basic CSV split
      cells = line.split(",");
    } else {
      // Split by whitespace
      cells = line.split(/\s+/);
    }
    cells = cells.map(c => c.trim().replace(/^["']|["']$/g, ""));
    if (cells.length > 0) rows.push(cells);
  }

  return rows;
}

function mapRowsToDemand(rows) {
  if (rows.length === 0) return [];

  // Check if first row is header
  let startIndex = 0;
  const firstRowNorm = rows[0].map(c => removeDiacritics(c));
  const hasHeader = firstRowNorm.some(c => c.includes("dai") || c.includes("length") || c.includes("rong") || c.includes("width") || c.includes("sl") || c.includes("so luong"));

  let colMap = { len: -1, wid: -1, qty: -1, lbl: -1, grain: -1, mat: -1 };

  if (hasHeader) {
    startIndex = 1;
    firstRowNorm.forEach((c, idx) => {
      if (c.includes("dai") || c.includes("length") || c === "l") colMap.len = idx;
      else if (c.includes("rong") || c.includes("width") || c === "w") colMap.wid = idx;
      else if (c.includes("sl") || c.includes("so luong") || c.includes("soluong") || c.includes("qty") || c.includes("count")) colMap.qty = idx;
      else if (c.includes("nhan") || c.includes("ten") || c.includes("name") || c.includes("label") || c.includes("description") || c.includes("mo ta")) colMap.lbl = idx;
      else if (c.includes("tho") || c.includes("grain")) colMap.grain = idx;
      else if (c.includes("vat lieu") || c.includes("vatlieu") || c.includes("material")) colMap.mat = idx;
    });
  }

  // Fallback positional if not found
  if (colMap.len === -1) colMap.len = 0;
  if (colMap.wid === -1) colMap.wid = 1;
  if (colMap.qty === -1) colMap.qty = 2;
  if (colMap.lbl === -1) colMap.lbl = 3;
  if (colMap.grain === -1) colMap.grain = 4;
  if (colMap.mat === -1) colMap.mat = 5;

  const result = [];
  for (let r = startIndex; r < rows.length; r++) {
    const row = rows[r];
    const len = parsePositiveInt(row[colMap.len]);
    const wid = parsePositiveInt(row[colMap.wid]);
    if (len <= 0 || wid <= 0) continue;

    const qty = parsePositiveInt(row[colMap.qty]) || 1;
    const lbl = (row[colMap.lbl] || `Chi tiết #${result.length + 1}`).trim();
    const grain = normalizeGrainString(row[colMap.grain]);
    const mat = (row[colMap.mat] || "").trim();

    result.push({
      length: len,
      width: wid,
      quantity: qty,
      label: lbl,
      grain: grain,
      canRotate: grain.includes("Tự do"),
      material: mat
    });
  }

  return result;
}

function mapRowsToRepo(rows) {
  if (rows.length === 0) return [];

  let startIndex = 0;
  const firstRowNorm = rows[0].map(c => removeDiacritics(c));
  const hasHeader = firstRowNorm.some(c => c.includes("dai") || c.includes("length") || c.includes("rong") || c.includes("width") || c.includes("ton") || c.includes("kho") || c.includes("trim"));

  let colMap = { len: -1, wid: -1, qty: -1, lbl: -1, grain: -1, mat: -1, top: -1, left: -1, bot: -1, right: -1 };

  if (hasHeader) {
    startIndex = 1;
    firstRowNorm.forEach((c, idx) => {
      if (c.includes("dai") || c.includes("length") || c === "l") colMap.len = idx;
      else if (c.includes("rong") || c.includes("width") || c === "w") colMap.wid = idx;
      else if (c.includes("sl") || c.includes("so luong") || c.includes("ton") || c.includes("qty") || c.includes("count")) colMap.qty = idx;
      else if (c.includes("nhan") || c.includes("ten") || c.includes("name") || c.includes("label")) colMap.lbl = idx;
      else if (c.includes("tho") || c.includes("grain")) colMap.grain = idx;
      else if (c.includes("tren") || c.includes("top")) colMap.top = idx;
      else if (c.includes("trai") || c.includes("left")) colMap.left = idx;
      else if (c.includes("duoi") || c.includes("bottom")) colMap.bot = idx;
      else if (c.includes("phai") || c.includes("right")) colMap.right = idx;
      else if (c.includes("vat lieu") || c.includes("vatlieu") || c.includes("material")) colMap.mat = idx;
    });
  }

  if (colMap.len === -1) colMap.len = 0;
  if (colMap.wid === -1) colMap.wid = 1;
  if (colMap.qty === -1) colMap.qty = 2;
  if (colMap.lbl === -1) colMap.lbl = 3;
  if (colMap.grain === -1) colMap.grain = 4;

  const result = [];
  for (let r = startIndex; r < rows.length; r++) {
    const row = rows[r];
    const len = parsePositiveInt(row[colMap.len]);
    const wid = parsePositiveInt(row[colMap.wid]);
    if (len <= 0 || wid <= 0) continue;

    const qty = parsePositiveInt(row[colMap.qty]) || 1;
    const lbl = (row[colMap.lbl] || `Ván kho #${result.length + 1} (${len}x${wid})`).trim();
    const grain = normalizeSheetGrainString(row[colMap.grain]);
    const top = colMap.top !== -1 ? parsePositiveInt(row[colMap.top], 0) : 0;
    const left = colMap.left !== -1 ? parsePositiveInt(row[colMap.left], 0) : 0;
    const bot = colMap.bot !== -1 ? parsePositiveInt(row[colMap.bot], 0) : 0;
    const right = colMap.right !== -1 ? parsePositiveInt(row[colMap.right], 0) : 0;
    const mat = (colMap.mat !== -1 ? row[colMap.mat] : "")?.trim() || "MDF";

    result.push({
      length: len,
      width: wid,
      quantity: qty,
      label: lbl,
      sheetGrain: grain,
      material: mat,
      trimTop: top,
      trimLeft: left,
      trimBottom: bot,
      trimRight: right,
      price: 0
    });
  }

  return result;
}

// -------------------------------------------------------------
// EXCEL IMPORT WITH SHEETJS (CLIENT-SIDE OFFLINE & SERVER API)
// -------------------------------------------------------------
function initExcelImportHandlers() {
  const dropzone = document.getElementById("excelDropzone");
  const fileInput = document.getElementById("fileExcelInput");

  dropzone?.addEventListener("click", () => fileInput?.click());
  fileInput?.addEventListener("change", (e) => {
    if (e.target.files?.length > 0) processExcelFile(e.target.files[0]);
  });

  dropzone?.addEventListener("dragover", (e) => {
    e.preventDefault();
    dropzone.classList.add("dragover");
  });

  dropzone?.addEventListener("dragleave", () => {
    dropzone.classList.remove("dragover");
  });

  dropzone?.addEventListener("drop", (e) => {
    e.preventDefault();
    dropzone.classList.remove("dragover");
    if (e.dataTransfer.files?.length > 0) processExcelFile(e.dataTransfer.files[0]);
  });

  document.getElementById("btnApplyExcelImport")?.addEventListener("click", applyExcelImportData);
}

function openExcelModal(defaultTarget = "auto") {
  document.getElementById("selExcelTarget").value = defaultTarget;
  document.getElementById("excelPreviewSection").style.display = "none";
  document.getElementById("excelSheetOptions").style.display = "none";
  document.getElementById("lblExcelFileName").textContent = "";
  document.getElementById("btnApplyExcelImport").disabled = true;
  document.getElementById("fileExcelInput").value = "";
  appState.pendingExcelData = null;
  openModal("modalExcel");
}

function processExcelFile(file) {
  if (!file) return;
  document.getElementById("lblExcelFileName").textContent = `📁 File: ${file.name} (${Math.round(file.size / 1024)} KB)`;

  const reader = new FileReader();
  reader.onload = (e) => {
    try {
      const data = new Uint8Array(e.target.result);
      if (typeof XLSX === "undefined") {
        // Fallback to server API if SheetJS is somehow unavailable
        processExcelViaServer(file);
        return;
      }

      const workbook = XLSX.read(data, { type: "array" });
      const sheetNames = workbook.SheetNames;

      // Identify sheets
      let demandSheet = sheetNames.find(s => s.toLowerCase().includes("demand") || s.toLowerCase().includes("chitiet") || s.toLowerCase().includes("chi tiet"));
      let stockSheet = sheetNames.find(s => s.toLowerCase().includes("stock") || s.toLowerCase().includes("kho") || s.toLowerCase().includes("tam"));

      let demandRows = [];
      let repoRows = [];

      if (demandSheet) {
        const json = XLSX.utils.sheet_to_json(workbook.Sheets[demandSheet], { header: 1 });
        demandRows = mapRowsToDemand(json);
      }
      if (stockSheet) {
        const json = XLSX.utils.sheet_to_json(workbook.Sheets[stockSheet], { header: 1 });
        repoRows = mapRowsToRepo(json);
      }

      // If no specific sheet matched, read first sheet
      if (demandRows.length === 0 && repoRows.length === 0 && sheetNames.length > 0) {
        const firstSheetName = sheetNames[0];
        const json = XLSX.utils.sheet_to_json(workbook.Sheets[firstSheetName], { header: 1 });
        const target = document.getElementById("selExcelTarget").value;

        if (target === "repo") {
          repoRows = mapRowsToRepo(json);
        } else {
          demandRows = mapRowsToDemand(json);
        }
      }

      appState.pendingExcelData = { demandRows, repoRows, sheetNames };
      renderExcelPreview(demandRows, repoRows);
    } catch (err) {
      console.error(err);
      showToast("Lỗi khi đọc file Excel: " + err.message, "error");
    }
  };
  reader.readAsArrayBuffer(file);
}

async function processExcelViaServer(file) {
  const formData = new FormData();
  formData.append("file", file);

  try {
    const res = await fetch("/api/excel/import", {
      method: "POST",
      body: formData
    });
    const data = await res.json();
    if (data.success) {
      appState.pendingExcelData = {
        demandRows: data.demandPieces || [],
        repoRows: data.repoPieces || []
      };
      renderExcelPreview(appState.pendingExcelData.demandRows, appState.pendingExcelData.repoRows);
    } else {
      showToast(data.message || "Lỗi khi xử lý file Excel!", "error");
    }
  } catch (err) {
    showToast("Không thể kết nối đến máy chủ!", "error");
  }
}

function renderExcelPreview(demandRows, repoRows) {
  const previewSec = document.getElementById("excelPreviewSection");
  const thead = document.getElementById("theadExcelPreview");
  const tbody = document.getElementById("tbodyExcelPreview");
  const stats = document.getElementById("lblExcelStats");

  previewSec.style.display = "block";
  document.getElementById("excelSheetOptions").style.display = "block";
  document.getElementById("btnApplyExcelImport").disabled = (demandRows.length === 0 && repoRows.length === 0);

  stats.textContent = `${demandRows.length} chi tiết cần cắt • ${repoRows.length} tấm kho`;

  thead.innerHTML = `
    <tr>
      <th>Phân loại</th>
      <th>Dài (mm)</th>
      <th>Rộng (mm)</th>
      <th>Số lượng</th>
      <th>Tên / Nhãn</th>
      <th>Thớ gỗ</th>
      <th>Ghi chú</th>
    </tr>
  `;

  let html = "";
  demandRows.slice(0, 10).forEach(d => {
    html += `<tr><td><span class="badge badge-info">Demand</span></td><td><strong>${d.length}</strong></td><td><strong>${d.width}</strong></td><td>${d.quantity}</td><td>${escapeHtml(d.label)}</td><td>${escapeHtml(d.grain)}</td><td>Chi tiết</td></tr>`;
  });
  repoRows.slice(0, 5).forEach(r => {
    html += `<tr><td><span class="badge badge-warning">Stock</span></td><td><strong>${r.length}</strong></td><td><strong>${r.width}</strong></td><td>${r.quantity}</td><td>${escapeHtml(r.label)}</td><td>${escapeHtml(r.sheetGrain)}</td><td>Trim: ${r.trimTop},${r.trimLeft},${r.trimBottom},${r.trimRight}</td></tr>`;
  });

  if (demandRows.length > 10 || repoRows.length > 5) {
    html += `<tr><td colspan="7" style="text-align:center; color:var(--text-muted); font-style:italic;">... và nhiều dòng khác được phát hiện trong file Excel</td></tr>`;
  }
  tbody.innerHTML = html;
}

function applyExcelImportData() {
  if (!appState.pendingExcelData) return;
  const { demandRows, repoRows } = appState.pendingExcelData;
  const mode = document.getElementById("selExcelMode").value; // overwrite or append

  if (demandRows.length > 0) {
    if (mode === "overwrite") appState.demandList = demandRows;
    else appState.demandList.push(...demandRows);
    renderDemandTable();
  }

  if (repoRows.length > 0) {
    if (mode === "overwrite") appState.repoList = repoRows;
    else appState.repoList.push(...repoRows);
    renderRepoTable();
  }

  closeModal("modalExcel");
  showToast(`Đã nhập thành công ${demandRows.length} chi tiết và ${repoRows.length} tấm kho từ file Excel!`, "success");
}

// -------------------------------------------------------------
// RUN OPTIMIZATION & RENDER VISUAL RESULTS
// -------------------------------------------------------------
async function executeOptimization() {
  if (appState.demandList.length === 0) {
    showToast("Vui lòng thêm ít nhất một chi tiết cần cắt!", "error");
    return;
  }
  if (appState.repoList.length === 0) {
    showToast("Vui lòng thêm ít nhất một tấm vật liệu kho!", "error");
    return;
  }

  const btnRun = document.getElementById("btnRunOptimization");
  const spinner = document.getElementById("runSpinner");
  const btnText = document.getElementById("runBtnText");

  btnRun.disabled = true;
  spinner.style.display = "inline-block";
  btnText.textContent = " Đang tính toán tối ưu...";

  // Prepare payload
  const payload = {
    demandPieces: appState.demandList.map(d => ({
      length: parseInt(d.length) || 0,
      width: parseInt(d.width) || 0,
      quantity: parseInt(d.quantity) || 1,
      material: d.material || "",
      grain: d.grain || "≡ Dọc (Theo Dài)",
      label: d.label || "",
      canRotate: Boolean(d.canRotate),
      externalID: 0,
      priority: 0
    })),
    repoPieces: appState.repoList.map(r => ({
      length: parseInt(r.length) || 0,
      width: parseInt(r.width) || 0,
      quantity: parseInt(r.quantity) || 1,
      material: r.material || "",
      sheetGrain: r.sheetGrain || "≡ Dọc (Theo Dài)",
      label: r.label || "",
      price: 0,
      trimTop: parseInt(r.trimTop) || 0,
      trimLeft: parseInt(r.trimLeft) || 0,
      trimBottom: parseInt(r.trimBottom) || 0,
      trimRight: parseInt(r.trimRight) || 0,
      priority: 0,
      externalID: 0
    })),
    method: parseInt(document.getElementById("selMethod").value) || 0,
    bladeWidth: Math.round((parseFloat(document.getElementById("numBladeWidth").value) || 0) * 10) / 10,
    optimizationLevel: parseInt(document.getElementById("numOptLevel").value) || 50,
    randomSeed: parseInt(document.getElementById("numRandomSeed").value) || 1,
    minimalUsefulSize: parseInt(document.getElementById("numMinUseful").value) || 100,
    maximalCutLength: parseInt(document.getElementById("numMaxCutLen").value) || 0,
    minimizePatterns: document.getElementById("chkMinimizePatterns").checked,
    wasteHatchStyle: document.getElementById("selWasteHatch").value || "DiagonalCross"
  };

  try {
    const res = await fetch("/api/optimize", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    if (!res.ok) {
      const errData = await res.json().catch(() => ({}));
      showToast("Lỗi tối ưu hóa từ máy chủ: " + (errData.errorMessage || res.statusText), "error");
      return;
    }

    const data = await res.json();
    if (data.success) {
      appState.optResult = data;
      try {
        renderOptimizationResults(data);
        const placed = data.totalDemandPiecesPlaced ?? data.TotalDemandPiecesPlaced ?? 0;
        const used = data.totalRepoSheetsUsed ?? data.TotalRepoSheetsUsed ?? 0;
        const pats = data.totalUniquePatterns ?? data.TotalUniquePatterns ?? 0;
        const ms = data.executionTimeMs ?? data.ExecutionTimeMs ?? 0;
        showToast(`✅ Tối ưu hóa thành công trong ${ms} ms! Đã xếp ${placed} chi tiết lên ${used} tấm (${pats} mẫu sơ đồ).`, "success");
      } catch (renderErr) {
        console.error("Lỗi hiển thị kết quả:", renderErr);
        showToast("Lỗi hiển thị kết quả: " + renderErr.message, "error");
      }
    } else {
      showToast("Lỗi tối ưu hóa: " + (data.errorMessage || "Không xác định"), "error");
    }
  } catch (err) {
    console.error("Lỗi kết nối:", err);
    showToast("Không thể kết nối đến máy chủ: " + err.message, "error");
  } finally {
    btnRun.disabled = false;
    spinner.style.display = "none";
    btnText.textContent = "▶  BẮT ĐẦU TỐI ƯU HÓA";
  }
}

function renderOptimizationResults(res) {
  // 1. KPI Cards
  const util = res.overallUtilizationPercent ?? res.OverallUtilizationPercent ?? 0;
  const uniquePatterns = res.totalUniquePatterns ?? res.TotalUniquePatterns ?? 0;
  const sheetsUsed = res.totalRepoSheetsUsed ?? res.TotalRepoSheetsUsed ?? 0;
  const sheetsStock = res.totalRepoSheetsStock ?? res.TotalRepoSheetsStock ?? 0;
  const piecesPlaced = res.totalDemandPiecesPlaced ?? res.TotalDemandPiecesPlaced ?? 0;
  const piecesReq = res.totalDemandPiecesRequested ?? res.TotalDemandPiecesRequested ?? 0;
  const usefulArea = res.totalUsefulAreaM2 ?? res.TotalUsefulAreaM2 ?? 0;
  const wasteArea = res.totalWasteAreaM2 ?? res.TotalWasteAreaM2 ?? 0;
  const cutLength = res.totalCutLengthMm ?? res.TotalCutLengthMm ?? 0;
  const execMs = res.executionTimeMs ?? res.ExecutionTimeMs ?? 0;

  document.getElementById("kpiUtilization").textContent = `${Number(util).toFixed(1)}%`;
  document.getElementById("kpiUniquePatterns").textContent = uniquePatterns;
  document.getElementById("kpiSheetsUsed").textContent = `${sheetsUsed} / ${sheetsStock}`;
  document.getElementById("kpiPiecesPlaced").textContent = `${piecesPlaced} / ${piecesReq}`;
  
  const placedPct = piecesReq > 0 ? (piecesPlaced / piecesReq * 100).toFixed(1) : 0;
  document.getElementById("kpiPiecesPercent").textContent = `Đạt ${placedPct}% yêu cầu`;
  
  document.getElementById("kpiUsefulArea").textContent = `${Number(usefulArea).toFixed(2)} m²`;
  document.getElementById("kpiWasteArea").textContent = `Phế liệu: ${Number(wasteArea).toFixed(2)} m²`;
  document.getElementById("kpiCutLength").textContent = `${Math.round(cutLength).toLocaleString()} mm`;
  document.getElementById("kpiExecutionTime").textContent = `Thời gian: ${execMs} ms`;

  // 2. Patterns dropdown
  const patterns = res.patterns || res.Patterns || [];
  const selPattern = document.getElementById("selPatternIndex");
  selPattern.innerHTML = "";
  patterns.forEach((p, idx) => {
    const opt = document.createElement("option");
    opt.value = idx;
    const count = p.sheetCount ?? p.SheetCount ?? 1;
    const label = p.repositoryLabel ?? p.RepositoryLabel ?? `Tấm #${idx + 1}`;
    opt.textContent = `Sơ đồ #${idx + 1} (${count} tấm) - [${label}]`;
    selPattern.appendChild(opt);
  });

  // 3. Result Tab 1: Patterns Summary Table
  const tbodyPatterns = document.getElementById("tbodyResultPatterns");
  document.getElementById("countResultPatterns").textContent = patterns.length;
  tbodyPatterns.innerHTML = patterns.map((p, idx) => {
    const count = p.sheetCount ?? p.SheetCount ?? 1;
    const label = p.repositoryLabel ?? p.RepositoryLabel ?? `Tấm #${idx + 1}`;
    const len = p.sheetLength ?? p.SheetLength ?? 0;
    const wid = p.sheetWidth ?? p.SheetWidth ?? 0;
    const pPerSheet = p.piecesPerSheet ?? p.PiecesPerSheet ?? 0;
    const totPieces = p.totalPiecesInPattern ?? p.TotalPiecesInPattern ?? 0;
    const covered = Number(p.surfaceCoveredPercent ?? p.SurfaceCoveredPercent ?? 0).toFixed(1);
    const cutLen = Math.round(p.totalCutLength ?? p.TotalCutLength ?? 0).toLocaleString();
    const wasteSurf = Math.round(p.wasteSurface ?? p.WasteSurface ?? 0).toLocaleString();

    return `
    <tr style="cursor:pointer;" class="${idx === 0 ? 'selected' : ''}" data-pattern-idx="${idx}">
      <td><span class="badge badge-info">Sơ đồ #${idx + 1}</span></td>
      <td><strong>${escapeHtml(label)}</strong></td>
      <td>${len} x ${wid} mm</td>
      <td style="text-align:right;"><strong class="text-success">${count} tấm</strong></td>
      <td style="text-align:right;">${pPerSheet}</td>
      <td style="text-align:right;"><strong>${totPieces}</strong></td>
      <td style="text-align:right;"><strong>${covered}%</strong></td>
      <td style="text-align:right;">${cutLen}</td>
      <td style="text-align:right;">${wasteSurf}</td>
    </tr>`;
  }).join("");

  // Attach click to jump to pattern
  tbodyPatterns.querySelectorAll("tr").forEach(tr => {
    tr.addEventListener("click", () => {
      const idx = parseInt(tr.dataset.patternIdx);
      changePattern(idx);
    });
  });

  // Switch to first pattern
  changePattern(0);
}

function changePattern(idx) {
  const patterns = appState.optResult?.patterns || appState.optResult?.Patterns || [];
  if (patterns.length === 0) return;
  const count = patterns.length;
  if (idx < 0) idx = 0;
  if (idx >= count) idx = count - 1;

  appState.currentPatternIdx = idx;
  const pat = patterns[idx];

  const sheetCount = pat.sheetCount ?? pat.SheetCount ?? 1;
  const svgContent = pat.svgContent ?? pat.SvgContent ?? "";
  const utilizedPieces = pat.utilizedPieces ?? pat.UtilizedPieces ?? [];
  const cuts = pat.cuts ?? pat.Cuts ?? [];
  const wastePieces = pat.wastePieces ?? pat.WastePieces ?? [];

  // Update controls
  document.getElementById("selPatternIndex").value = idx;
  document.getElementById("btnPrevPattern").disabled = (idx === 0);
  document.getElementById("btnNextPattern").disabled = (idx === count - 1);

  // Badge stamp
  const stamp = document.getElementById("badgePatternStamp");
  stamp.style.display = "inline-flex";
  stamp.textContent = `✂ CẮT x${sheetCount} TẤM (Lặp ${sheetCount} lần)`;

  // Render SVG in viewport
  document.getElementById("svgEmptyState").style.display = "none";
  const container = document.getElementById("svgContainer");
  container.style.display = "flex";
  container.style.alignItems = "center";
  container.style.justifyContent = "center";
  container.innerHTML = svgContent;

  resetSvgZoom();
  initSvgInteractiveElements();

  // Highlight row in Patterns table
  document.querySelectorAll("#tbodyResultPatterns tr").forEach(tr => {
    tr.classList.toggle("selected", parseInt(tr.dataset.patternIdx) === idx);
  });

  // 4. Update Tab 2: Utilized Pieces Table
  const tbodyUtilized = document.getElementById("tbodyResultUtilized");
  document.getElementById("countResultPieces").textContent = utilizedPieces.length;
  tbodyUtilized.innerHTML = utilizedPieces.map(up => {
    const upIdx = up.index ?? up.Index ?? 0;
    const upLabel = up.label ?? up.Label ?? "";
    const tx = up.topX ?? up.TopX ?? 0;
    const ty = up.topY ?? up.TopY ?? 0;
    const bx = up.bottomX ?? up.BottomX ?? 0;
    const by = up.bottomY ?? up.BottomY ?? 0;
    const w = up.width ?? up.Width ?? 0;
    const h = up.height ?? up.Height ?? 0;
    const rot = up.rotated ?? up.Rotated ?? 0;
    const grain = up.grainLabel ?? up.GrainLabel ?? "";
    const extId = up.demandExternalID ?? up.DemandExternalID ?? 0;

    return `
    <tr data-piece-id="${upIdx}">
      <td><span class="badge" style="background:#f1f5f9; color:#334155;">#${upIdx}</span></td>
      <td><strong>${escapeHtml(upLabel)}</strong></td>
      <td style="font-family:monospace;">(${tx}, ${ty}) &rarr; (${bx}, ${by})</td>
      <td><strong>${w} x ${h} mm</strong></td>
      <td style="text-align:center;">1</td>
      <td style="text-align:center;"><strong class="text-primary">${sheetCount}</strong></td>
      <td>${escapeHtml(grain)}</td>
      <td style="text-align:center;">${rot === 1 ? '<span class="badge badge-warning">↻ 90°</span>' : '0°'}</td>
      <td style="color:var(--text-muted);">${extId}</td>
    </tr>`;
  }).join("");

  // 5. Update Tab 3: Cuts Table
  const tbodyCuts = document.getElementById("tbodyResultCuts");
  document.getElementById("countResultCuts").textContent = cuts.length;
  tbodyCuts.innerHTML = cuts.map(c => {
    const cIdx = c.index ?? c.Index ?? 0;
    const tx = c.topX ?? c.TopX ?? 0;
    const ty = c.topY ?? c.TopY ?? 0;
    const bx = c.bottomX ?? c.BottomX ?? 0;
    const by = c.bottomY ?? c.BottomY ?? 0;
    const dir = c.direction ?? c.Direction ?? "";
    const thick = c.cutThickness ?? c.CutThickness ?? 0;
    const lvl = c.level ?? c.Level ?? 0;

    return `
    <tr>
      <td>${cIdx}</td>
      <td style="font-family:monospace;">(${tx}, ${ty}) &rarr; (${bx}, ${by})</td>
      <td>${dir}</td>
      <td>${thick} mm</td>
      <td>${lvl > 0 ? 'Tầng ' + lvl : '-'}</td>
    </tr>`;
  }).join("");

  // 6. Update Tab 4: Waste Pieces Table
  const tbodyWaste = document.getElementById("tbodyResultWaste");
  document.getElementById("countResultWaste").textContent = wastePieces.length;
  tbodyWaste.innerHTML = wastePieces.map(w => {
    const wIdx = w.index ?? w.Index ?? 0;
    const tx = w.topX ?? w.TopX ?? 0;
    const ty = w.topY ?? w.TopY ?? 0;
    const bx = w.bottomX ?? w.BottomX ?? 0;
    const by = w.bottomY ?? w.BottomY ?? 0;
    const width = w.width ?? w.Width ?? 0;
    const height = w.height ?? w.Height ?? 0;
    const area = w.area ?? w.Area ?? 0;
    const isUseful = w.isUseful ?? w.IsUseful ?? false;
    const cls = w.classification ?? w.Classification ?? "";
    const grainAvail = w.grainAvailable ?? w.GrainAvailable ?? "";

    return `
    <tr>
      <td>${wIdx}</td>
      <td style="font-family:monospace;">(${tx}, ${ty}) &rarr; (${bx}, ${by})</td>
      <td><strong>${width} x ${height} mm</strong></td>
      <td style="text-align:right;">${Math.round(area).toLocaleString()}</td>
      <td style="text-align:right;"><strong>${Math.round(area * sheetCount).toLocaleString()}</strong></td>
      <td><span class="badge ${isUseful ? 'badge-success' : 'badge-warning'}">${cls}</span></td>
      <td>${escapeHtml(grainAvail)}</td>
    </tr>`;
  }).join("");
}

// -------------------------------------------------------------
// INTERACTIVE SVG TOOLTIP & ZOOM/PAN
// -------------------------------------------------------------
function initSvgInteractiveElements() {
  const tooltip = document.getElementById("svgTooltip");
  const pieces = document.querySelectorAll(".svg-viewport .svg-piece");

  pieces.forEach(p => {
    p.addEventListener("mousemove", (e) => {
      const label = p.dataset.label;
      const dim = p.dataset.dim;
      const pos = p.dataset.pos;
      const id = p.dataset.pieceId;

      tooltip.innerHTML = `<strong>Chi tiết #${id}: ${label}</strong><br/>Kích thước: <strong>${dim} mm</strong><br/>Tọa độ: ${pos}`;
      tooltip.style.left = (e.clientX + 14) + "px";
      tooltip.style.top = (e.clientY + 14) + "px";
      tooltip.style.display = "block";
    });

    p.addEventListener("mouseleave", () => {
      tooltip.style.display = "none";
    });

    p.addEventListener("click", () => {
      const id = p.dataset.pieceId;
      // Scroll to piece row in Utilized table
      const tr = document.querySelector(`#tbodyResultUtilized tr[data-piece-id="${id}"]`);
      if (tr) {
        document.querySelectorAll("#tbodyResultUtilized tr").forEach(r => r.classList.remove("selected"));
        tr.classList.add("selected");
        tr.scrollIntoView({ behavior: "smooth", block: "nearest" });
      }
    });
  });
}

function initSvgPanListeners() {
  const viewport = document.getElementById("svgViewport");
  if (!viewport) return;

  viewport.addEventListener("mousedown", (e) => {
    // Only drag with left mouse button
    if (e.button !== 0) return;
    appState.isDragging = true;
    appState.dragStartX = e.clientX - appState.panX;
    appState.dragStartY = e.clientY - appState.panY;
  });

  window.addEventListener("mousemove", (e) => {
    if (!appState.isDragging) return;
    appState.panX = e.clientX - appState.dragStartX;
    appState.panY = e.clientY - appState.dragStartY;
    applySvgTransform();
  });

  window.addEventListener("mouseup", () => {
    appState.isDragging = false;
  });

  viewport.addEventListener("wheel", (e) => {
    e.preventDefault();
    const factor = e.deltaY < 0 ? 1.15 : 0.85;
    zoomSvg(factor);
  });
}

function zoomSvg(factor) {
  appState.zoom *= factor;
  appState.zoom = Math.max(0.2, Math.min(5, appState.zoom));
  applySvgTransform();
}

function resetSvgZoom() {
  appState.zoom = 1;
  appState.panX = 0;
  appState.panY = 0;
  applySvgTransform();
}

function fitSvgZoom() {
  appState.zoom = 0.95;
  appState.panX = 0;
  appState.panY = 0;
  applySvgTransform();
}

function applySvgTransform() {
  const svg = document.querySelector("#svgContainer svg");
  if (svg) {
    svg.style.transform = `translate(${appState.panX}px, ${appState.panY}px) scale(${appState.zoom})`;
  }
}

function exportCurrentSvg() {
  const patterns = appState.optResult?.patterns || appState.optResult?.Patterns || [];
  if (patterns.length === 0) {
    showToast("Chưa có sơ đồ nào để tải!", "warning");
    return;
  }
  const pat = patterns[appState.currentPatternIdx];
  const svg = pat.svgContent ?? pat.SvgContent ?? "";
  const blob = new Blob([svg], { type: "image/svg+xml;charset=utf-8" });
  downloadBlob(blob, `SoDo_${appState.currentPatternIdx + 1}_Cut2DX.svg`);
  showToast(`Đã tải bản vẽ Sơ đồ #${appState.currentPatternIdx + 1} dạng SVG!`, "success");
}

function resetResultsView() {
  appState.optResult = null;
  document.getElementById("svgEmptyState").style.display = "block";
  document.getElementById("svgContainer").style.display = "none";
  document.getElementById("badgePatternStamp").style.display = "none";
  document.getElementById("selPatternIndex").innerHTML = '<option value="0">Sơ đồ: 0 / 0</option>';
  document.getElementById("btnPrevPattern").disabled = true;
  document.getElementById("btnNextPattern").disabled = true;

  document.getElementById("kpiUtilization").textContent = "0.0%";
  document.getElementById("kpiUniquePatterns").textContent = "0";
  document.getElementById("kpiSheetsUsed").textContent = "0 / 0";
  document.getElementById("kpiPiecesPlaced").textContent = "0 / 0";
  document.getElementById("kpiUsefulArea").textContent = "0.00 m²";
  document.getElementById("kpiWasteArea").textContent = "Phế liệu: 0.00 m²";
  document.getElementById("kpiCutLength").textContent = "0 mm";
  document.getElementById("kpiExecutionTime").textContent = "Thời gian: 0 ms";

  document.getElementById("tbodyResultPatterns").innerHTML = '<tr><td colspan="9" class="text-center" style="padding:20px; color:var(--text-muted);">Chưa có dữ liệu</td></tr>';
  document.getElementById("tbodyResultUtilized").innerHTML = '<tr><td colspan="9" class="text-center" style="padding:20px; color:var(--text-muted);">Chưa có dữ liệu</td></tr>';
  document.getElementById("tbodyResultCuts").innerHTML = '<tr><td colspan="5" class="text-center" style="padding:20px; color:var(--text-muted);">Chưa có dữ liệu</td></tr>';
  document.getElementById("tbodyResultWaste").innerHTML = '<tr><td colspan="7" class="text-center" style="padding:20px; color:var(--text-muted);">Chưa có dữ liệu</td></tr>';
}

// -------------------------------------------------------------
// EXPORTS: HTML REPORT, EXCEL RESULT, TXT
// -------------------------------------------------------------
async function downloadHtmlReport() {
  if (appState.demandList.length === 0 || appState.repoList.length === 0) {
    showToast("Vui lòng nhập dữ liệu và chạy tối ưu trước khi xuất báo cáo!", "warning");
    return;
  }

  showToast("Đang tạo file báo cáo HTML độc lập...", "info");

  const payload = {
    demandPieces: appState.demandList,
    repoPieces: appState.repoList,
    method: parseInt(document.getElementById("selMethod").value) || 0,
    bladeWidth: Math.round((parseFloat(document.getElementById("numBladeWidth").value) || 3.2) * 10) / 10,
    optimizationLevel: parseInt(document.getElementById("numOptLevel").value) || 50,
    randomSeed: parseInt(document.getElementById("numRandomSeed").value) || 1,
    minimalUsefulSize: parseInt(document.getElementById("numMinUseful").value) || 100,
    maximalCutLength: parseInt(document.getElementById("numMaxCutLen").value) || 0,
    minimizePatterns: document.getElementById("chkMinimizePatterns").checked,
    wasteHatchStyle: document.getElementById("selWasteHatch").value || "DiagonalCross"
  };

  try {
    const res = await fetch("/api/report/html", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    if (res.ok) {
      const blob = await res.blob();
      downloadBlob(blob, "BaoCao_SoDoCat_Cut2DX.html");
      showToast("✅ Đã xuất thành công file Báo cáo HTML!", "success");
    } else {
      const err = await res.json();
      showToast("Lỗi khi xuất báo cáo: " + (err.errorMessage || "Không xác định"), "error");
    }
  } catch (err) {
    showToast("Lỗi kết nối máy chủ!", "error");
  }
}

async function downloadExcelResult() {
  if (!appState.optResult) {
    showToast("Vui lòng bấm '▶ Bắt đầu tối ưu hóa' trước khi xuất Excel kết quả!", "warning");
    return;
  }

  showToast("Đang tạo file Excel kết quả...", "info");

  try {
    const res = await fetch("/api/excel/export-results", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(appState.optResult)
    });

    if (res.ok) {
      const blob = await res.blob();
      downloadBlob(blob, "Ket_Qua_Toi_Uu_Cut2DX.xlsx");
      showToast("✅ Đã xuất file Excel kết quả thành công!", "success");
    } else {
      showToast("Lỗi khi tạo file Excel kết quả!", "error");
    }
  } catch (err) {
    showToast("Lỗi kết nối máy chủ!", "error");
  }
}

function downloadTxtResult() {
  if (!appState.optResult) {
    showToast("Vui lòng chạy tối ưu hóa trước khi xuất file TXT!", "warning");
    return;
  }

  const res = appState.optResult;
  const util = Number(res.overallUtilizationPercent ?? res.OverallUtilizationPercent ?? 0).toFixed(2);
  const patsCount = res.totalUniquePatterns ?? res.TotalUniquePatterns ?? 0;
  const sheetsUsed = res.totalRepoSheetsUsed ?? res.TotalRepoSheetsUsed ?? 0;
  const sheetsStock = res.totalRepoSheetsStock ?? res.TotalRepoSheetsStock ?? 0;
  const piecesPlaced = res.totalDemandPiecesPlaced ?? res.TotalDemandPiecesPlaced ?? 0;
  const piecesReq = res.totalDemandPiecesRequested ?? res.TotalDemandPiecesRequested ?? 0;
  const usefulArea = Number(res.totalUsefulAreaM2 ?? res.TotalUsefulAreaM2 ?? 0).toFixed(3);
  const wasteArea = Number(res.totalWasteAreaM2 ?? res.TotalWasteAreaM2 ?? 0).toFixed(3);
  const cutLen = Number(res.totalCutLengthMm ?? res.TotalCutLengthMm ?? 0).toFixed(1);
  const execMs = res.executionTimeMs ?? res.ExecutionTimeMs ?? 0;
  const patterns = res.patterns || res.Patterns || [];

  let text = "=========================================================\n";
  text += "           KẾT QUẢ TỐI ƯU HÓA SƠ ĐỒ CẮT 2D - CUT2DX WEB  \n";
  text += "=========================================================\n\n";
  text += `1. TỔNG QUAN KPI:\n`;
  text += `• Tỷ lệ sử dụng vật liệu: ${util}%\n`;
  text += `• Số mẫu sơ đồ cắt (Patterns): ${patsCount}\n`;
  text += `• Số tấm vật liệu thực tế dùng: ${sheetsUsed} / ${sheetsStock} tấm\n`;
  text += `• Số chi tiết đã xếp: ${piecesPlaced} / ${piecesReq}\n`;
  text += `• Diện tích hữu ích: ${usefulArea} m²\n`;
  text += `• Diện tích phế liệu: ${wasteArea} m²\n`;
  text += `• Tổng mạch cưa: ${cutLen} mm\n`;
  text += `• Thời gian tính: ${execMs} ms\n\n`;

  text += `2. DANH SÁCH MẪU SƠ ĐỒ (CUTTING PATTERNS):\n`;
  patterns.forEach((p, idx) => {
    const pIdx = p.patternIndex ?? p.PatternIndex ?? idx;
    const repoLabel = p.repositoryLabel ?? p.RepositoryLabel ?? `Tấm #${idx + 1}`;
    const sLen = p.sheetLength ?? p.SheetLength ?? 0;
    const sWid = p.sheetWidth ?? p.SheetWidth ?? 0;
    const sCount = p.sheetCount ?? p.SheetCount ?? 1;
    const pPerSheet = p.piecesPerSheet ?? p.PiecesPerSheet ?? 0;
    const totPieces = p.totalPiecesInPattern ?? p.TotalPiecesInPattern ?? 0;
    const covered = Number(p.surfaceCoveredPercent ?? p.SurfaceCoveredPercent ?? 0).toFixed(1);
    const upList = p.utilizedPieces || p.UtilizedPieces || [];

    text += `---------------------------------------------------------\n`;
    text += `Sơ đồ #${pIdx + 1} • Tấm: [${repoLabel}] (${sLen}x${sWid} mm)\n`;
    text += `✂ CẮT x${sCount} TẤM | Chi tiết/tấm: ${pPerSheet} (Tổng: ${totPieces}) | Độ phủ: ${covered}%\n`;
    text += `Chi tiết trên sơ đồ:\n`;
    upList.forEach(up => {
      const uIdx = up.index ?? up.Index ?? 0;
      const uLabel = up.label ?? up.Label ?? "";
      const uW = up.width ?? up.Width ?? 0;
      const uH = up.height ?? up.Height ?? 0;
      const tx = up.topX ?? up.TopX ?? 0;
      const ty = up.topY ?? up.TopY ?? 0;
      const bx = up.bottomX ?? up.BottomX ?? 0;
      const by = up.bottomY ?? up.BottomY ?? 0;
      const rot = up.rotated ?? up.Rotated ?? 0;
      text += `  - #${uIdx} [${uLabel}]: ${uW}x${uH} mm @ (${tx}, ${ty})->(${bx}, ${by}) ${rot ? '(Xoay 90°)' : ''}\n`;
    });
  });

  const blob = new Blob([text], { type: "text/plain;charset=utf-8" });
  downloadBlob(blob, "Ket_Qua_Toi_Uu_Cut2DX.txt");
  showToast("Đã tải file TXT kết quả!", "success");
}

// -------------------------------------------------------------
// HELPER UTILITIES
// -------------------------------------------------------------
function removeDiacritics(str) {
  return String(str || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toLowerCase()
    .trim();
}

function openModal(id) {
  document.getElementById(id)?.classList.add("open");
}

function closeModal(id) {
  document.getElementById(id)?.classList.remove("open");
}

function showToast(msg, type = "info") {
  const container = document.getElementById("toastContainer");
  if (!container) return;

  const toast = document.createElement("div");
  toast.className = `toast toast-${type}`;
  toast.textContent = msg;

  container.appendChild(toast);
  setTimeout(() => {
    toast.style.opacity = "0";
    toast.style.transform = "translateY(10px)";
    toast.style.transition = "all 0.3s ease";
    setTimeout(() => toast.remove(), 300);
  }, 4000);
}

function parsePositiveInt(val, def = 0) {
  if (val === undefined || val === null) return def;
  const clean = String(val).replace(/,/g, "").trim();
  const parsed = parseInt(clean);
  return isNaN(parsed) ? def : Math.max(0, parsed);
}

function normalizeGrainString(val) {
  if (!val) return "≡ Dọc (Theo Dài)";
  const str = String(val).toLowerCase();
  if (str.includes("ngang") || str.includes("width") || str.includes("|||")) return "||| Ngang (Theo Rộng)";
  if (str.includes("tự do") || str.includes("tu do") || str.includes("none") || str.includes("không thớ") || str.includes("🔄")) return "🔄 Tự do (Không thớ)";
  if (str.includes("cố định") || str.includes("co dinh") || str.includes("fixed") || str.includes("🔒")) return "🔒 Cố định (Không xoay)";
  return "≡ Dọc (Theo Dài)";
}

function normalizeSheetGrainString(val) {
  if (!val) return "≡ Dọc (Theo Dài)";
  const str = String(val).toLowerCase();
  if (str.includes("ngang") || str.includes("width") || str.includes("|||")) return "||| Ngang (Theo Rộng)";
  if (str.includes("không thớ") || str.includes("khong tho") || str.includes("none") || str.includes("🔄")) return "🔄 Không thớ";
  return "≡ Dọc (Theo Dài)";
}

function escapeHtml(str) {
  return String(str || "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

function downloadBlob(blob, filename) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  setTimeout(() => {
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }, 100);
}
