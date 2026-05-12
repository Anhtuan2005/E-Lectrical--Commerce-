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
  if (!chartElement || !window.Chart || !window.adminRevenueLabels || !window.adminRevenueValues) return;
  new Chart(chartElement, {
    type: "line",
    data: {
      labels: window.adminRevenueLabels,
      datasets: [{
        label: "Doanh thu",
        data: window.adminRevenueValues,
        borderColor: "#0071e3",
        backgroundColor: "rgba(0, 113, 227, 0.12)",
        tension: 0.32,
        fill: true
      }]
    },
    options: adminChartOptions(false)
  });
}

function initReportCharts() {
  if (!window.Chart || !window.reportData) return;
  var money = function (value) { return Number(value).toLocaleString("vi-VN") + " ₫"; };

  createChart("dailyRevenueChart", {
    type: "line",
    data: { labels: window.reportData.dailyLabels, datasets: [{ label: "Doanh thu", data: window.reportData.dailyValues, borderColor: "#0071e3", backgroundColor: "rgba(0,113,227,.12)", fill: true, tension: 0.35 }] },
    options: adminChartOptions(false)
  });
  createChart("categoryRevenueChart", {
    type: "doughnut",
    data: { labels: window.reportData.categoryLabels, datasets: [{ data: window.reportData.categoryValues, backgroundColor: ["#0071e3", "#34c759", "#ff9f0a", "#ff3b30", "#5856d6"] }] },
    options: { responsive: true, plugins: { tooltip: { callbacks: { label: function (ctx) { return ctx.label + ": " + money(ctx.raw); } } } } }
  });
  createChart("topProductsChart", {
    type: "bar",
    data: { labels: window.reportData.productLabels, datasets: [{ label: "Đã bán", data: window.reportData.productSold, backgroundColor: "#0071e3" }] },
    options: adminChartOptions(true)
  });
  createChart("periodCompareChart", {
    type: "bar",
    data: { labels: ["Kỳ trước", "Kỳ này"], datasets: [{ label: "Doanh thu", data: [window.reportData.previousRevenue, window.reportData.currentRevenue], backgroundColor: ["#a1a1a6", "#34c759"] }] },
    options: adminChartOptions(false)
  });
}

function createChart(id, config) {
  var el = document.getElementById(id);
  if (el) new Chart(el, config);
}

function adminChartOptions(horizontal) {
  return {
    indexAxis: horizontal ? "y" : "x",
    responsive: true,
    plugins: { legend: { display: false } },
    scales: {
      y: {
        ticks: {
          callback: function (value) {
            return Number(value).toLocaleString("vi-VN") + " ₫";
          }
        }
      }
    }
  };
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
