document.addEventListener("DOMContentLoaded", function () {
  initAdminConfirm();
  initAdminNav();
  initDashboardChart();
  initReportCharts();
  initBannerSort();
  initOrderPolling();
});

function initAdminConfirm() {
  document.querySelectorAll("[data-confirm]").forEach(function (form) {
    form.addEventListener("submit", function (event) {
      if (!window.confirm(form.dataset.confirm)) {
        event.preventDefault();
      }
    });
  });
}

function initAdminNav() {
  var path = window.location.pathname.toLowerCase();
  document.querySelectorAll(".adm-nav-item").forEach(function (item) {
    var href = item.getAttribute("href").toLowerCase();
    item.classList.toggle("active", path === href.toLowerCase() || (href !== "/admin/dashboard" && path.startsWith(href)));
  });
}

function initDashboardChart() {
  var chartElement = document.getElementById("revenueChart");
  if (!chartElement || !window.adminRevenueLabels || !window.adminRevenueValues) return;
  if (!window.Chart) {
    showChartFallback(chartElement);
    return;
  }
  var colors = adminChartColors();
  new Chart(chartElement, {
    type: "line",
    data: {
      labels: window.adminRevenueLabels,
      datasets: [{
        label: "Doanh thu",
        data: window.adminRevenueValues,
        borderColor: colors.accent,
        backgroundColor: colors.accentFill,
        tension: 0.32,
        fill: true
      }]
    },
    options: adminChartOptions({ currency: true })
  });
}

function initReportCharts() {
  if (!window.reportData) return;
  if (!window.Chart) {
    document.querySelectorAll("#dailyRevenueChart,#categoryRevenueChart,#topProductsChart,#periodCompareChart").forEach(showChartFallback);
    return;
  }
  var colors = adminChartColors();
  var money = function (value) { return Number(value).toLocaleString("vi-VN") + " ₫"; };

  createChart("dailyRevenueChart", {
    type: "line",
    data: { labels: window.reportData.dailyLabels, datasets: [{ label: "Doanh thu", data: window.reportData.dailyValues, borderColor: colors.accent, backgroundColor: colors.accentFill, fill: true, tension: 0.35 }] },
    options: adminChartOptions({ currency: true })
  });
  createChart("categoryRevenueChart", {
    type: "doughnut",
    data: { labels: window.reportData.categoryLabels, datasets: [{ data: window.reportData.categoryValues, backgroundColor: colors.palette }] },
    options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: "bottom", labels: { boxWidth: 10, usePointStyle: true } }, tooltip: { callbacks: { label: function (ctx) { return ctx.label + ": " + money(ctx.raw); } } } } }
  });
  createChart("topProductsChart", {
    type: "bar",
    data: { labels: window.reportData.productLabels, datasets: [{ label: "Đã bán", data: window.reportData.productSold, backgroundColor: colors.accent }] },
    options: adminChartOptions({ horizontal: true, currency: false })
  });
  createChart("periodCompareChart", {
    type: "bar",
    data: { labels: ["Kỳ trước", "Kỳ này"], datasets: [{ label: "Doanh thu", data: [window.reportData.previousRevenue, window.reportData.currentRevenue], backgroundColor: [colors.neutral, colors.success] }] },
    options: adminChartOptions({ currency: true })
  });
}

function createChart(id, config) {
  var el = document.getElementById(id);
  if (el) new Chart(el, config);
}

function adminChartOptions(config) {
  config = config || {};
  var horizontal = Boolean(config.horizontal);
  var currency = config.currency !== false;
  var numericAxis = horizontal ? "x" : "y";
  var scales = {
    x: { grid: { color: "oklch(0.89 0.012 248 / 0.72)" }, ticks: { color: "oklch(0.52 0.025 255)" } },
    y: { grid: { color: "oklch(0.89 0.012 248 / 0.72)" }, ticks: { color: "oklch(0.52 0.025 255)" } }
  };
  scales[numericAxis].ticks.callback = function (value) {
    var formatted = Number(value).toLocaleString("vi-VN");
    return currency ? formatted + " ₫" : formatted;
  };

  return {
    indexAxis: horizontal ? "y" : "x",
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false }, tooltip: { backgroundColor: "oklch(0.215 0.027 260)", padding: 10 } },
    scales: scales
  };
}

function adminChartColors() {
  return {
    accent: "oklch(0.58 0.19 252)",
    accentFill: "oklch(0.58 0.19 252 / 0.14)",
    success: "oklch(0.63 0.16 150)",
    neutral: "oklch(0.67 0.018 255)",
    palette: [
      "oklch(0.58 0.19 252)",
      "oklch(0.68 0.15 195)",
      "oklch(0.63 0.16 150)",
      "oklch(0.73 0.15 72)",
      "oklch(0.62 0.2 27)",
      "oklch(0.58 0.16 305)"
    ]
  };
}

function showChartFallback(canvas) {
  if (!canvas || canvas.dataset.fallbackShown === "true") return;
  canvas.dataset.fallbackShown = "true";
  canvas.style.display = "none";
  var fallback = document.createElement("div");
  fallback.className = "empty-state";
  fallback.textContent = "Không tải được biểu đồ. Bảng số liệu vẫn hiển thị bên dưới.";
  canvas.insertAdjacentElement("afterend", fallback);
}

function initBannerSort() {
  var tbody = document.querySelector("[data-sortable-banners]");
  if (!tbody) return;
  var dragging;
  tbody.querySelectorAll("tr").forEach(function (row) {
    row.draggable = true;
    row.addEventListener("dragstart", function () {
      dragging = row;
      row.classList.add("dragging");
    });
    row.addEventListener("dragend", function () {
      row.classList.remove("dragging");
      saveBannerSort(tbody);
    });
    row.addEventListener("dragover", function (event) {
      event.preventDefault();
      var after = getDragAfterElement(tbody, event.clientY);
      if (after == null) {
        tbody.appendChild(dragging);
      } else {
        tbody.insertBefore(dragging, after);
      }
    });
  });
}

function getDragAfterElement(container, y) {
  var rows = [].slice.call(container.querySelectorAll("tr:not(.dragging)"));
  return rows.reduce(function (closest, child) {
    var box = child.getBoundingClientRect();
    var offset = y - box.top - box.height / 2;
    if (offset < 0 && offset > closest.offset) return { offset: offset, element: child };
    return closest;
  }, { offset: Number.NEGATIVE_INFINITY }).element;
}

function saveBannerSort(tbody) {
  var ids = [].slice.call(tbody.querySelectorAll("tr")).map(function (row) { return Number(row.dataset.id); });
  fetch("/Admin/Banner/Sort", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(ids)
  });
}

function initOrderPolling() {
  var lastCheck = Date.now();
  setInterval(function () {
    fetch("/Admin/Order/NewCount?since=" + lastCheck)
      .then(function (response) { return response.json(); })
      .then(function (data) {
        if (data.count > 0) {
          showAdminToast("Có " + data.count + " đơn hàng mới!", "info");
          var badge = document.getElementById("new-order-badge");
          if (badge) badge.textContent = data.count;
          lastCheck = Date.now();
        }
      });
  }, 30000);
}

function showAdminToast(message, type) {
  var stack = document.getElementById("admin-toast-stack");
  if (!stack) return;
  var toast = document.createElement("div");
  toast.className = "adm-toast " + (type || "info");
  toast.textContent = message;
  stack.appendChild(toast);
  setTimeout(function () { toast.remove(); }, 3200);
}
