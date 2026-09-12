function initSearchAutocomplete() {
  var input = document.querySelector("[data-search-autocomplete]");
  var suggestions = document.getElementById("searchSuggestions");
  if (!input || !suggestions) return;
  var timer;
  var activeRequest;

  function closeSuggestions() {
    suggestions.classList.remove("show");
    suggestions.innerHTML = "";
  }

  input.addEventListener("input", function () {
    clearTimeout(timer);
    if (activeRequest) activeRequest.abort();
    var value = input.value.trim();
    if (value.length < 2) {
      closeSuggestions();
      return;
    }
    timer = setTimeout(function () {
      activeRequest = new AbortController();
      fetch("/Product/Search?q=" + encodeURIComponent(value), { signal: activeRequest.signal })
        .then(function (response) { return response.json(); })
        .then(function (items) {
          suggestions.innerHTML = items.map(function (item) {
            return '<a data-product-click="' + escapeHtml(item.id) + '" href="' + escapeHtml(item.url || ("/Product/Detail/" + item.id)) + '"><img src="' + escapeHtml(item.imageUrl || "/images/placeholder.svg") + '" alt="" loading="lazy" decoding="async"><span>' + escapeHtml(item.name) + '<small>' + escapeHtml(item.category || "Sản phẩm") + '</small></span><strong>' + escapeHtml(item.price) + "</strong></a>";
          }).join("");
          suggestions.classList.toggle("show", items.length > 0);
        })
        .catch(function (error) {
          if (!error || error.name !== "AbortError") closeSuggestions();
        });
    }, 180);
  });

  document.addEventListener("click", function (event) {
    if (!suggestions.contains(event.target) && event.target !== input) closeSuggestions();
  });
}

function initReviewFilters() {
  var filters = document.querySelectorAll("[data-review-filter]");
  var list = document.getElementById("reviewList");
  if (!list) return;
  list.dataset.visibleLimit = list.dataset.visibleLimit || "5";

  filters.forEach(function (button) {
    button.addEventListener("click", function () {
      filters.forEach(function (item) {
        var active = item === button;
        item.classList.toggle("active", active);
        item.setAttribute("aria-pressed", String(active));
      });
      list.dataset.visibleLimit = "5";
      applyReviewFilter();
    });
  });

  applyReviewFilter();
}

function applyReviewFilter() {
  var list = document.getElementById("reviewList");
  if (!list) return;
  var active = document.querySelector("[data-review-filter].active");
  var filter = active ? active.dataset.reviewFilter : "all";
  var loadMore = document.querySelector("[data-load-reviews]");
  var limit = loadMore ? Math.max(5, Math.floor(parseLocalizedNumber(list.dataset.visibleLimit, 5))) : Number.MAX_SAFE_INTEGER;
  var shown = 0;
  var matched = 0;

  document.querySelectorAll("[data-review-item]").forEach(function (item) {
    var matches = filter === "all" || item.dataset.rating === filter;
    if (!matches) {
      item.classList.add("is-hidden");
      return;
    }

    matched++;
    shown++;
    item.classList.toggle("is-hidden", shown > limit);
  });

  var empty = document.querySelector("[data-review-empty]");
  if (empty) empty.hidden = matched > 0;

  if (loadMore) {
    loadMore.hidden = matched <= limit;
  }
}

function initReviewPagination() {
  var button = document.querySelector("[data-load-reviews]");
  if (!button) return;
  button.addEventListener("click", function () {
    var list = document.getElementById("reviewList");
    if (!list) return;
    list.dataset.visibleLimit = String(parseLocalizedNumber(list.dataset.visibleLimit, 5) + 5);
    applyReviewFilter();
  });
}

function initProductGallery() {
  var main = document.getElementById("mainProductImage");
  if (!main) return;
  document.querySelectorAll("[data-gallery-thumb]").forEach(function (thumb) {
    thumb.addEventListener("click", function () {
      main.src = thumb.dataset.galleryThumb;
      document.querySelectorAll("[data-gallery-thumb]").forEach(function (item) { item.classList.remove("active"); });
      thumb.classList.add("active");
    });
  });
}

function initProductTabs() {
  document.querySelectorAll(".tab-btn[data-tab]").forEach(function (button) {
    button.addEventListener("click", function () {
      var tab = button.dataset.tab;
      document.querySelectorAll(".tab-btn[data-tab]").forEach(function (item) { item.classList.toggle("active", item.dataset.tab === tab); });
      document.querySelectorAll(".tab-panel[data-tab-panel]").forEach(function (panel) { panel.classList.toggle("active", panel.dataset.tabPanel === tab); });
    });
  });
}

function initProductSpecModal() {
  var modal = document.querySelector("[data-spec-modal]");
  var open = document.querySelector("[data-spec-modal-open]");
  if (!modal || !open) return;

  var closeButtons = modal.querySelectorAll("[data-spec-modal-close]");
  var lastFocus = null;
  var lockedScrollY = 0;

  if (modal.parentElement !== document.body) {
    document.body.appendChild(modal);
  }

  function lockPage() {
    lockedScrollY = window.scrollY || document.documentElement.scrollTop || 0;
    document.body.style.top = "-" + lockedScrollY + "px";
    document.body.classList.add("spec-modal-lock");
  }

  function unlockPage() {
    document.body.classList.remove("spec-modal-lock");
    document.body.style.top = "";
    window.scrollTo(0, lockedScrollY);
  }

  function setOpen(isOpen) {
    if (isOpen) {
      lastFocus = document.activeElement;
      modal.hidden = false;
      lockPage();
      var close = modal.querySelector("[data-spec-modal-close]");
      if (close) close.focus();
      refreshIcons();
      return;
    }

    modal.hidden = true;
    unlockPage();
    if (lastFocus && typeof lastFocus.focus === "function") lastFocus.focus();
  }

  open.addEventListener("click", function () {
    setOpen(true);
  });

  closeButtons.forEach(function (button) {
    button.addEventListener("click", function () {
      setOpen(false);
    });
  });

  modal.addEventListener("click", function (event) {
    if (event.target === modal) setOpen(false);
  });

  document.addEventListener("keydown", function (event) {
    if (!modal.hidden && event.key === "Escape") setOpen(false);
  });
}

var addressOptions = (function () {
  var detailed = {
    "Hồ Chí Minh": {
      "Quận 1": ["Phường Bến Nghé", "Phường Bến Thành", "Phường Cầu Kho", "Phường Cô Giang"],
      "Quận 3": ["Phường Võ Thị Sáu", "Phường 9", "Phường 10", "Phường 11"],
      "Quận 7": ["Phường Tân Phong", "Phường Tân Phú", "Phường Tân Quy", "Phường Phú Mỹ"],
      "Thành phố Thủ Đức": ["Phường Thảo Điền", "Phường An Phú", "Phường Hiệp Bình Chánh", "Phường Linh Trung"],
      "Quận Bình Thạnh": ["Phường 1", "Phường 3", "Phường 17", "Phường 25"]
    },
    "Hà Nội": {
      "Quận Ba Đình": ["Phường Điện Biên", "Phường Đội Cấn", "Phường Kim Mã", "Phường Ngọc Hà"],
      "Quận Hoàn Kiếm": ["Phường Hàng Bạc", "Phường Hàng Bài", "Phường Tràng Tiền", "Phường Cửa Nam"],
      "Quận Cầu Giấy": ["Phường Dịch Vọng", "Phường Nghĩa Đô", "Phường Quan Hoa", "Phường Trung Hòa"],
      "Quận Đống Đa": ["Phường Cát Linh", "Phường Láng Hạ", "Phường Ô Chợ Dừa", "Phường Trung Liệt"]
    },
    "Đà Nẵng": {
      "Quận Hải Châu": ["Phường Hải Châu I", "Phường Hải Châu II", "Phường Thạch Thang", "Phường Hòa Thuận Đông"],
      "Quận Thanh Khê": ["Phường An Khê", "Phường Chính Gián", "Phường Thạc Gián", "Phường Xuân Hà"],
      "Quận Sơn Trà": ["Phường An Hải Bắc", "Phường An Hải Đông", "Phường Mân Thái", "Phường Phước Mỹ"]
    },
    "Cần Thơ": {
      "Quận Ninh Kiều": ["Phường An Cư", "Phường An Hòa", "Phường Cái Khế", "Phường Tân An"],
      "Quận Cái Răng": ["Phường Ba Láng", "Phường Hưng Phú", "Phường Lê Bình", "Phường Thường Thạnh"],
      "Quận Bình Thủy": ["Phường An Thới", "Phường Bình Thủy", "Phường Long Hòa", "Phường Trà An"]
    },
    "Hải Phòng": {
      "Quận Hồng Bàng": ["Phường Hoàng Văn Thụ", "Phường Minh Khai", "Phường Sở Dầu", "Phường Thượng Lý"],
      "Quận Lê Chân": ["Phường An Biên", "Phường Dư Hàng", "Phường Niệm Nghĩa", "Phường Trại Cau"],
      "Quận Ngô Quyền": ["Phường Cầu Đất", "Phường Đằng Giang", "Phường Lạch Tray", "Phường Máy Tơ"]
    },
    "Bình Dương": {
      "Thành phố Thủ Dầu Một": ["Phường Chánh Nghĩa", "Phường Hiệp Thành", "Phường Phú Cường", "Phường Phú Hòa"],
      "Thành phố Dĩ An": ["Phường An Bình", "Phường Dĩ An", "Phường Đông Hòa", "Phường Tân Đông Hiệp"],
      "Thành phố Thuận An": ["Phường An Phú", "Phường Bình Hòa", "Phường Lái Thiêu", "Phường Thuận Giao"]
    },
    "Đồng Nai": {
      "Thành phố Biên Hòa": ["Phường An Bình", "Phường Bửu Long", "Phường Long Bình", "Phường Tân Phong"],
      "Thành phố Long Khánh": ["Phường Bảo Vinh", "Phường Xuân An", "Phường Xuân Bình", "Phường Xuân Trung"],
      "Huyện Trảng Bom": ["Thị trấn Trảng Bom", "Xã An Viễn", "Xã Bắc Sơn", "Xã Hố Nai 3"]
    }
  };
  [
    "Bà Rịa - Vũng Tàu", "An Giang", "Bắc Giang", "Bắc Ninh", "Bến Tre", "Bình Định",
    "Bình Phước", "Bình Thuận", "Cà Mau", "Đắk Lắk", "Đồng Tháp", "Gia Lai",
    "Khánh Hòa", "Lâm Đồng", "Long An", "Nghệ An", "Quảng Nam", "Quảng Ninh",
    "Thanh Hóa", "Thừa Thiên Huế"
  ].forEach(function (province) {
    detailed[province] = detailed[province] || {
      "Thành phố trung tâm": ["Phường 1", "Phường 2", "Phường 3"],
      "Thị xã trung tâm": ["Phường trung tâm", "Phường phía Bắc", "Phường phía Nam"],
      "Huyện trung tâm": ["Thị trấn", "Xã trung tâm", "Xã lân cận"]
    };
  });
  return detailed;
})();

