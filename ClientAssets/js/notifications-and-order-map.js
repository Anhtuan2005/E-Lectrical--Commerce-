function showToast(message, type) {
  var stack = document.getElementById("toast-stack");
  if (!stack) return;
  var toast = document.createElement("div");
  toast.className = "toast " + (type === "error" ? "toast-error" : "toast-success");
  toast.textContent = message;
  stack.appendChild(toast);
  setTimeout(function () { toast.remove(); }, 2600);
}

function escapeHtml(value) {
  return String(value || "").replace(/[&<>"']/g, function (ch) {
    return ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" })[ch];
  });
}

function initNotificationMenu() {
  document.querySelectorAll(".notification-menu").forEach(function (menu) {
    var toggle = menu.querySelector(".notification-toggle");
    if (!toggle) return;

    toggle.addEventListener("click", function (event) {
      event.stopPropagation();
      var isOpen = menu.classList.toggle("is-open");
      toggle.setAttribute("aria-expanded", String(isOpen));
    });
  });

  document.addEventListener("click", function () {
    document.querySelectorAll(".notification-menu.is-open").forEach(function (menu) {
      menu.classList.remove("is-open");
      var toggle = menu.querySelector(".notification-toggle");
      if (toggle) toggle.setAttribute("aria-expanded", "false");
    });
  });
}

function initOrderRouteMap() {
  var mapElements = Array.prototype.slice.call(document.querySelectorAll("[data-order-real-map]"));
  if (mapElements.length) {
    loadOrderLeafletAssets()
      .then(function () {
        mapElements.forEach(initOrderLeafletMap);
      })
      .catch(function () {
        mapElements.forEach(function (element) {
          element.classList.add("is-map-error");
          var loading = element.querySelector(".order-map-loading span");
          if (loading) loading.textContent = "Không tải được nền bản đồ. Vui lòng kiểm tra kết nối mạng.";
        });
      });
  }

  document.querySelectorAll("[data-order-route-expand]").forEach(function (button) {
    var panel = button.closest(".order-shipping-map-panel");
    var label = button.querySelector("span");
    if (!panel) return;

    button.addEventListener("click", function () {
      var expanded = panel.classList.toggle("is-expanded");
      button.setAttribute("aria-expanded", String(expanded));
      if (label) label.textContent = expanded ? "Thu gọn" : "Xem rõ hơn";

      if (expanded) {
        var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        panel.scrollIntoView({ behavior: reduceMotion ? "auto" : "smooth", block: "start" });
      }

      refreshOrderLeafletMaps(panel);
    });
  });
}

function loadOrderLeafletAssets() {
  if (window.L && typeof window.L.map === "function") {
    return Promise.resolve();
  }

  if (window.orderLeafletPromise) {
    return window.orderLeafletPromise;
  }

  window.orderLeafletPromise = new Promise(function (resolve, reject) {
    if (!document.querySelector("link[data-order-leaflet-css]")) {
      var link = document.createElement("link");
      link.rel = "stylesheet";
      link.href = "https://unpkg.com/leaflet@1.9.4/dist/leaflet.css";
      link.setAttribute("data-order-leaflet-css", "true");
      document.head.appendChild(link);
    }

    var existing = document.querySelector("script[data-order-leaflet-js]");
    if (existing) {
      existing.addEventListener("load", resolve, { once: true });
      existing.addEventListener("error", reject, { once: true });
      return;
    }

    var script = document.createElement("script");
    script.src = "https://unpkg.com/leaflet@1.9.4/dist/leaflet.js";
    script.defer = true;
    script.setAttribute("data-order-leaflet-js", "true");
    script.addEventListener("load", resolve, { once: true });
    script.addEventListener("error", reject, { once: true });
    document.body.appendChild(script);
  });

  return window.orderLeafletPromise;
}

function initOrderLeafletMap(element) {
  if (element._orderLeafletMap || !window.L) return;

  var route = [
    [10.8482, 106.6146],
    [10.935, 106.72],
    [11.18, 107.08],
    [11.62, 108.38],
    [12.24, 109.19],
    [13.78, 109.22],
    [16.05, 108.2],
    [18.43, 105.86]
  ];
  var progress = Number(element.getAttribute("data-route-progress") || "0");
  if (!Number.isFinite(progress)) progress = 0;

  var statusMap = element.closest(".order-route-map");
  var isComplete = statusMap && statusMap.classList.contains("is-complete");
  var isCancelled = statusMap && statusMap.classList.contains("is-cancelled");
  var isEstimatedArrival = statusMap && statusMap.classList.contains("is-estimated-arrival");
  var routeColor = isCancelled ? "#dc2626" : isComplete ? "#16a34a" : isEstimatedArrival ? "#d97706" : "#2563eb";

  var map = window.L.map(element, {
    zoomControl: true,
    attributionControl: true,
    scrollWheelZoom: false,
    doubleClickZoom: true,
    zoomSnap: 0.25,
    zoomDelta: 0.5
  });

  var tiles = window.L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
    maxZoom: 19,
    attribution: "&copy; OpenStreetMap"
  }).addTo(map);

  var routePoints = route.map(function (point) {
    return window.L.latLng(point[0], point[1]);
  });
  var progressData = getOrderRouteProgress(routePoints, progress, map);
  element._orderRouteFocus = progressData.point;

  window.L.polyline(routePoints, {
    color: "#f8fafc",
    opacity: 0.92,
    weight: 12,
    lineCap: "round",
    lineJoin: "round"
  }).addTo(map);

  window.L.polyline(routePoints, {
    color: "#64748b",
    opacity: 0.62,
    weight: 6,
    lineCap: "round",
    lineJoin: "round"
  }).addTo(map);

  window.L.polyline(progressData.path, {
    color: routeColor,
    opacity: 0.95,
    weight: 8,
    lineCap: "round",
    lineJoin: "round"
  }).addTo(map);

  window.L.marker(routePoints[0], {
    icon: createOrderRouteIcon("warehouse", "origin", 36),
    zIndexOffset: 400
  }).addTo(map).bindTooltip("Shop Techvora, Hóc Môn, gần bến xe An Sương", {
    direction: "top",
    offset: [0, -18]
  });

  window.L.marker(routePoints[routePoints.length - 1], {
    icon: createOrderRouteIcon("map-pin", "destination", 36),
    zIndexOffset: 400
  }).addTo(map).bindTooltip("Điểm nhận mô phỏng", {
    direction: "top",
    offset: [0, -18]
  });

  window.L.marker(progressData.point, {
    icon: createOrderRouteIcon("truck", "truck" + (isComplete ? " is-complete" : isCancelled ? " is-cancelled" : isEstimatedArrival ? " is-estimated-arrival" : ""), 46),
    zIndexOffset: 800
  }).addTo(map).bindTooltip("Vị trí mô phỏng theo mốc thời gian", {
    direction: "top",
    offset: [0, -21]
  });

  element._orderLeafletMap = map;
  element._orderLeafletFit = function () {
    map.setView(element._orderRouteFocus || routePoints[0], getOrderLeafletZoom(element), { animate: false });
  };

  tiles.on("load", function () {
    element.classList.add("is-map-ready");
  });

  setTimeout(function () {
    element.classList.add("is-map-ready");
    map.invalidateSize();
    element._orderLeafletFit();
    refreshIcons();
  }, 120);
}

function getOrderRouteProgress(routePoints, progress, map) {
  var clamped = Math.max(0, Math.min(100, progress)) / 100;
  if (routePoints.length < 2 || clamped <= 0) {
    return { point: routePoints[0], path: [routePoints[0]] };
  }

  var segments = [];
  var total = 0;
  for (var index = 0; index < routePoints.length - 1; index += 1) {
    var distance = map.distance(routePoints[index], routePoints[index + 1]);
    segments.push(distance);
    total += distance;
  }

  var target = total * clamped;
  var travelled = 0;
  var path = [routePoints[0]];

  for (var segmentIndex = 0; segmentIndex < segments.length; segmentIndex += 1) {
    var segmentDistance = segments[segmentIndex];
    var start = routePoints[segmentIndex];
    var end = routePoints[segmentIndex + 1];

    if (travelled + segmentDistance >= target) {
      var ratio = segmentDistance === 0 ? 0 : (target - travelled) / segmentDistance;
      var point = window.L.latLng(
        start.lat + ((end.lat - start.lat) * ratio),
        start.lng + ((end.lng - start.lng) * ratio)
      );
      path.push(point);
      return { point: point, path: path };
    }

    path.push(end);
    travelled += segmentDistance;
  }

  return { point: routePoints[routePoints.length - 1], path: routePoints.slice() };
}

function createOrderRouteIcon(icon, className, size) {
  return window.L.divIcon({
    className: "order-route-div-icon",
    html: '<span class="order-route-marker is-' + className + '"><i data-lucide="' + icon + '" aria-hidden="true"></i></span>',
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2]
  });
}

function getOrderLeafletZoom(element) {
  var panel = element.closest(".order-shipping-map-panel");
  var expanded = panel && panel.classList.contains("is-expanded");
  var width = element.clientWidth || 0;

  if (width < 520) {
    return expanded ? 9 : 8.25;
  }

  return expanded ? 9.25 : 8.5;
}

function refreshOrderLeafletMaps(panel) {
  if (!panel) return;

  window.setTimeout(function () {
    panel.querySelectorAll("[data-order-real-map]").forEach(function (element) {
      if (!element._orderLeafletMap) return;
      element._orderLeafletMap.invalidateSize();
      if (typeof element._orderLeafletFit === "function") {
        element._orderLeafletFit();
      }
    });
  }, 260);
}
