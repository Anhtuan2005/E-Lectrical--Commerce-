document.addEventListener("DOMContentLoaded", function () {
  initAdminConfirm();
  initAdminToasts();
  initAdminNav();
  initAdminBulkOrders();
  initDashboardChart();
  initReportCharts();
  initBannerSort();
  initOrderNotifications();
  initGhnAddressForms();
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

function initAdminToasts() {
  document.querySelectorAll(".adm-toast").forEach(scheduleAdminToastDismiss);
}

function scheduleAdminToastDismiss(toast) {
  if (!toast || toast.dataset.dismissBound === "true" || toast.dataset.dismiss === "manual") return;
  toast.dataset.dismissBound = "true";
  var delay = Number(toast.dataset.dismissMs) || 4200;
  setTimeout(function () {
    dismissAdminToast(toast);
  }, delay);
}

function dismissAdminToast(toast) {
  if (!toast || !toast.isConnected || toast.classList.contains("is-leaving")) return;
  toast.classList.add("is-leaving");
  setTimeout(function () {
    toast.remove();
  }, 240);
}

function initAdminBulkOrders() {
  var form = document.querySelector("[data-bulk-orders]");
  if (!form) return;

  var all = form.querySelector("[data-bulk-check-all]");
  var checks = [].slice.call(form.querySelectorAll("[data-bulk-check]"));
  var submit = form.querySelector("[data-bulk-submit]");
  var selected = form.querySelector("[data-bulk-selected]");

  function sync() {
    var enabled = checks.filter(function (check) { return !check.disabled; });
    var checked = enabled.filter(function (check) { return check.checked; });
    if (selected) selected.textContent = String(checked.length);
    if (submit) submit.disabled = checked.length === 0;
    if (all) {
      all.checked = enabled.length > 0 && checked.length === enabled.length;
      all.indeterminate = checked.length > 0 && checked.length < enabled.length;
    }
    checks.forEach(function (check) {
      var row = check.closest("tr");
      if (row) row.classList.toggle("is-selected", check.checked);
    });
  }

  if (all) {
    all.addEventListener("change", function () {
      checks.forEach(function (check) {
        if (!check.disabled) check.checked = all.checked;
      });
      sync();
    });
  }

  checks.forEach(function (check) {
    check.addEventListener("change", sync);
  });

  sync();
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

function initOrderNotifications() {
  var lastCheck = Date.now();
  var realtimeConnected = false;

  function incrementBadge(id, amount) {
    var badge = document.getElementById(id);
    if (!badge) return;
    var current = Number(badge.textContent) || 0;
    badge.textContent = String(current + amount);
  }

  function handleNewOrders(count, message, url) {
    if (count <= 0) return;
    incrementBadge("new-order-badge", count);
    incrementBadge("admin-notification-badge", count);
    showAdminToast(message || ("Có " + count + " đơn hàng mới!"), "info", url);
    lastCheck = Date.now();
  }

  function pollNewOrders() {
    if (realtimeConnected) return;
    fetch("/Admin/Order/NewCount?since=" + lastCheck)
      .then(function (response) { return response.json(); })
      .then(function (data) {
        if (data.count > 0) {
          handleNewOrders(data.count);
        }
      })
      .catch(function () {
        // The next interval retries without interrupting the admin workflow.
      });
  }

  setInterval(pollNewOrders, 30000);

  if (!window.signalR) return;
  var connection = new window.signalR.HubConnectionBuilder()
    .withUrl("/hubs/admin-notifications")
    .withAutomaticReconnect([0, 2000, 10000, 30000])
    .build();

  connection.on("OrderCreated", function (order) {
    var total = Number(order.totalAmount || 0).toLocaleString("vi-VN") + " ₫";
    handleNewOrders(
      1,
      "Đơn #" + order.code + " mới từ " + order.customerName + ", " + total,
      order.url
    );
  });
  connection.onreconnecting(function () {
    realtimeConnected = false;
  });
  connection.onreconnected(function () {
    realtimeConnected = true;
    lastCheck = Date.now();
  });
  connection.onclose(function () {
    realtimeConnected = false;
  });
  connection.start()
    .then(function () {
      realtimeConnected = true;
      lastCheck = Date.now();
    })
    .catch(function () {
      realtimeConnected = false;
    });
}

function initGhnAddressForms() {
  var forms = [].slice.call(document.querySelectorAll("[data-ghn-address-form]"));
  if (!forms.length) return;

  var provincePromise;
  var districtPromises = {};
  var wardPromises = {};

  function setSelect(select, placeholder, items, disabled) {
    select.innerHTML = "";
    var placeholderOption = document.createElement("option");
    placeholderOption.value = "";
    placeholderOption.textContent = placeholder;
    select.appendChild(placeholderOption);
    (items || []).forEach(function (item) {
      var option = document.createElement("option");
      option.value = item.code;
      option.textContent = item.name;
      select.appendChild(option);
    });
    select.disabled = Boolean(disabled);
  }

  function fetchGhnOptions(url) {
    return fetch(url, { headers: { Accept: "application/json" } })
      .then(function (response) { return response.json(); })
      .then(function (data) {
        if (!data.success) throw new Error(data.message || "Không tải được dữ liệu GHN.");
        return data.items || [];
      });
  }

  function loadProvinces() {
    if (!provincePromise) {
      provincePromise = fetchGhnOptions("/Admin/Shipping/GhnProvinces");
    }
    return provincePromise;
  }

  function loadDistricts(provinceId) {
    if (!provinceId) return Promise.resolve([]);
    if (!districtPromises[provinceId]) {
      districtPromises[provinceId] = fetchGhnOptions("/Admin/Shipping/GhnDistricts?provinceId=" + encodeURIComponent(provinceId));
    }
    return districtPromises[provinceId];
  }

  function loadWards(districtId) {
    if (!districtId) return Promise.resolve([]);
    if (!wardPromises[districtId]) {
      wardPromises[districtId] = fetchGhnOptions("/Admin/Shipping/GhnWards?districtId=" + encodeURIComponent(districtId));
    }
    return wardPromises[districtId];
  }

  forms.forEach(function (form) {
    var province = form.querySelector("[data-ghn-province]");
    var district = form.querySelector("[data-ghn-district]");
    var ward = form.querySelector("[data-ghn-ward]");
    if (!province || !district || !ward || province.disabled) return;

    setSelect(province, "Đang tải tỉnh...", [], true);
    setSelect(district, "Chọn tỉnh trước", [], true);
    setSelect(ward, "Chọn quận trước", [], true);

    loadProvinces()
      .then(function (items) {
        setSelect(province, "Tỉnh GHN", items, false);
      })
      .catch(function (error) {
        setSelect(province, error.message || "Không tải được GHN", [], true);
      });

    province.addEventListener("change", function () {
      setSelect(district, province.value ? "Đang tải quận..." : "Chọn tỉnh trước", [], true);
      setSelect(ward, "Chọn quận trước", [], true);
      loadDistricts(province.value)
        .then(function (items) {
          setSelect(district, "Quận GHN", items, false);
        })
        .catch(function (error) {
          setSelect(district, error.message || "Không tải được quận", [], true);
        });
    });

    district.addEventListener("change", function () {
      setSelect(ward, district.value ? "Đang tải phường..." : "Chọn quận trước", [], true);
      loadWards(district.value)
        .then(function (items) {
          setSelect(ward, "Phường GHN", items, false);
        })
        .catch(function (error) {
          setSelect(ward, error.message || "Không tải được phường", [], true);
        });
    });
  });
}

function showAdminToast(message, type, url) {
  var stack = document.getElementById("admin-toast-stack");
  if (!stack) return;
  var toast = document.createElement(url ? "a" : "div");
  toast.className = "adm-toast " + (type || "info");
  if (url) toast.href = url;
  toast.textContent = message;
  stack.appendChild(toast);
  toast.dataset.dismissMs = "3200";
  scheduleAdminToastDismiss(toast);
}
