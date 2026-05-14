document.addEventListener("DOMContentLoaded", function () {
  initNavbar();
  initHeroSlider();
  initCountdown();
  initReveal();
  initWishlist();
  initCartButtons();
  initCartPage();
  initReviewForm();
  initVoucher();
  initQuantitySteppers();
  initShareTools();
  initAuthHelpers();
  initPaymentChoice();
  initSearchAutocomplete();
  initReviewPagination();
  initProductGallery();
  initProductTabs();
  initProductOptions();
  initDetailCoupon();
  initDetailAddToCart();
  initAddressDropdowns();
  initProfileAddressFill();
  refreshIcons();
});

function refreshIcons() {
  if (window.lucide && typeof window.lucide.createIcons === "function") {
    window.lucide.createIcons({
      attrs: {
        "stroke-width": 1.5
      }
    });
  }
}

function antiForgeryToken() {
  var token = document.querySelector("[name=__RequestVerificationToken]");
  return token ? token.value : "";
}

function initNavbar() {
  var nav = document.querySelector(".site-nav");
  if (nav) {
    window.addEventListener("scroll", function () {
      nav.classList.toggle("scrolled", window.scrollY > 10);
    });
  }

  var searchBtn = document.querySelector(".nav-search-btn");
  if (searchBtn) {
    searchBtn.addEventListener("click", function () {
      var search = document.querySelector(".nav-search");
      if (!search) return;
      search.classList.toggle("active");
      var input = search.querySelector("input");
      if (search.classList.contains("active") && input) input.focus();
      else if (input && input.value) search.submit();
    });
  }

  var hamburger = document.querySelector(".nav-hamburger");
  var overlay = document.querySelector(".nav-overlay");
  var close = document.querySelector(".nav-drawer-close");
  if (hamburger) hamburger.addEventListener("click", openDrawer);
  if (overlay) overlay.addEventListener("click", closeDrawer);
  if (close) close.addEventListener("click", closeDrawer);

  var path = window.location.pathname.toLowerCase();
  document.querySelectorAll(".nav-menu .nav-link").forEach(function (link) {
    var href = (link.getAttribute("href") || "").toLowerCase();
    var active = href === path || (href !== "/" && path.indexOf(href) === 0);
    if (path === "/" && href === "/") active = true;
    link.classList.toggle("active", active);
  });
}

function openDrawer() {
  var drawer = document.querySelector(".nav-drawer");
  var overlay = document.querySelector(".nav-overlay");
  if (drawer) drawer.classList.add("open");
  if (overlay) overlay.classList.add("open");
  document.body.style.overflow = "hidden";
}

function closeDrawer() {
  var drawer = document.querySelector(".nav-drawer");
  var overlay = document.querySelector(".nav-overlay");
  if (drawer) drawer.classList.remove("open");
  if (overlay) overlay.classList.remove("open");
  document.body.style.overflow = "";
}

function initHeroSlider() {
  var slider = document.querySelector(".hero-slider");
  var track = document.querySelector(".slider-track");
  var dots = document.querySelectorAll(".dot");
  if (!slider || !track || dots.length === 0) return;
  var total = dots.length;
  var current = 0;
  var timer;

  function goTo(index) {
    current = (index + total) % total;
    track.style.transform = "translateX(-" + current * 100 + "%)";
    dots.forEach(function (dot, i) { dot.classList.toggle("active", i === current); });
  }

  function startAuto() { timer = setInterval(function () { goTo(current + 1); }, 5000); }
  function stopAuto() { clearInterval(timer); }

  var next = document.querySelector(".slider-next");
  var prev = document.querySelector(".slider-prev");
  if (next) next.addEventListener("click", function () { stopAuto(); goTo(current + 1); startAuto(); });
  if (prev) prev.addEventListener("click", function () { stopAuto(); goTo(current - 1); startAuto(); });
  dots.forEach(function (dot) {
    dot.addEventListener("click", function () {
      stopAuto();
      goTo(Number(dot.dataset.index));
      startAuto();
    });
  });
  slider.addEventListener("mouseenter", stopAuto);
  slider.addEventListener("mouseleave", startAuto);
  startAuto();
}

function initCountdown() {
  var el = document.getElementById("countdown");
  if (!el) return;
  var midnight = new Date();
  midnight.setHours(24, 0, 0, 0);
  var endTime = midnight.getTime();
  function tick() {
    var diff = endTime - Date.now();
    if (diff <= 0) {
      midnight = new Date();
      midnight.setHours(24, 0, 0, 0);
      endTime = midnight.getTime();
      diff = endTime - Date.now();
    }
    if (diff <= 0) {
      el.textContent = "Đã kết thúc";
      return;
    }
    var h = Math.floor(diff / 3600000);
    var m = Math.floor((diff % 3600000) / 60000);
    var s = Math.floor((diff % 60000) / 1000);
    el.textContent = pad(h) + ":" + pad(m) + ":" + pad(s);
  }
  function pad(n) { return String(n).padStart(2, "0"); }
  tick();
  setInterval(tick, 1000);
}

function initReveal() {
  if (!("IntersectionObserver" in window)) {
    document.querySelectorAll(".reveal").forEach(function (el) { el.classList.add("revealed"); });
    return;
  }
  var observer = new IntersectionObserver(function (entries) {
    entries.forEach(function (entry) {
      if (entry.isIntersecting) {
        entry.target.classList.add("revealed");
        observer.unobserve(entry.target);
      }
    });
  }, { threshold: 0.1 });
  document.querySelectorAll(".reveal").forEach(function (el) { observer.observe(el); });
}

function initWishlist() {
  document.querySelectorAll(".btn-wishlist[data-product-id]").forEach(function (btn) {
    btn.addEventListener("click", function (e) {
      e.preventDefault();
      e.stopPropagation();
      fetch("/Wishlist/Toggle/" + btn.dataset.productId, {
        method: "POST",
        headers: { "RequestVerificationToken": antiForgeryToken() }
      })
        .then(function (response) {
          if (response.status === 401 || response.redirected) {
            window.location.href = "/Account/Login";
            return null;
          }
          return response.json();
        })
        .then(function (data) {
          if (!data) return;
          btn.classList.toggle("active", data.isWishlisted);
          btn.title = data.isWishlisted ? "Bỏ yêu thích" : "Yêu thích";
          var badge = document.getElementById("wishlist-count");
          if (badge) badge.textContent = data.count;
          showToast(data.message, "success");
        });
    });
  });
}

function initCartButtons() {
  document.querySelectorAll("[data-cart-product]").forEach(function (button) {
    button.addEventListener("click", function (event) {
      event.preventDefault();
      event.stopPropagation();
      if (button.disabled) return;
      var body = new URLSearchParams();
      body.append("productId", button.dataset.cartProduct);
      body.append("quantity", "1");
      fetch("/Cart/Add", {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: body.toString()
      })
        .then(function (response) { return response.json(); })
        .then(function (data) {
          if (!data.success) return;
          var cartCount = document.getElementById("cart-count");
          if (cartCount) cartCount.textContent = data.itemCount;
          showToast(data.message, "success");
        });
    });
  });
}

function initDetailAddToCart() {
  var button = document.getElementById("btnAddCart");
  var buyNow = document.getElementById("btnBuyNow");
  var input = document.getElementById("qtyInput");
  if (!input) return;

  function setBusy(source, busy) {
    if (!source) return;
    source.disabled = busy || source.dataset.stockDisabled === "true";
    if (busy) {
      source.dataset.originalText = source.dataset.originalText || source.textContent;
      source.textContent = "Đang xử lý...";
    } else if (source.dataset.originalText) {
      source.textContent = source.dataset.originalText;
    }
  }

  function addDetailProduct(redirectToCheckout) {
    var source = redirectToCheckout ? buyNow : button;
    if (!source || source.disabled) return;
    var qty = Math.max(Number(input.min || 1), Math.min(Number(input.max || 99), Number(input.value || 1)));
    input.value = qty;
    var body = new URLSearchParams();
    body.append("productId", source.dataset.productId);
    body.append("quantity", qty);
    setBusy(source, true);
    fetch("/Cart/Add", {
      method: "POST",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
        "RequestVerificationToken": antiForgeryToken()
      },
      body: body.toString()
    })
      .then(function (response) {
        if (!response.ok) throw new Error("Cart request failed");
        return response.json();
      })
      .then(function (data) {
        if (!data.success) {
          showToast(data.message || "Không thể thêm sản phẩm vào giỏ.", "error");
          return;
        }
        var cartCount = document.getElementById("cart-count");
        if (cartCount) cartCount.textContent = data.itemCount;
        document.querySelectorAll(".cart-badge").forEach(function (badge) { badge.textContent = data.itemCount; });
        if (redirectToCheckout) {
          window.location.href = "/Order/Checkout";
          return;
        }
        showToast(data.message, "success");
      })
      .catch(function () {
        showToast("Không thể thêm sản phẩm vào giỏ. Vui lòng thử lại.", "error");
      })
      .finally(function () {
        setBusy(source, false);
      });
  }

  if (button) {
    button.addEventListener("click", function () {
      addDetailProduct(false);
    });
  }

  if (buyNow) {
    buyNow.addEventListener("click", function () {
      addDetailProduct(true);
    });
  }
}

function initProductOptions() {
  var selected = {};
  document.querySelectorAll("[data-option-group]").forEach(function (group) {
    var key = group.dataset.optionGroup;
    var active = group.querySelector(".active");
    selected[key] = active ? (active.dataset.optionValue || active.textContent.trim()) : "";
    group.querySelectorAll("button[data-option-value]").forEach(function (button) {
      button.addEventListener("click", function () {
        group.querySelectorAll("button").forEach(function (item) { item.classList.remove("active"); });
        button.classList.add("active");
        selected[key] = button.dataset.optionValue || button.textContent.trim();
        updateSelectedVariant();
      });
    });
  });

  function updateSelectedVariant() {
    var target = document.getElementById("selectedVariant");
    if (!target) return;
    var color = selected.color || "";
    var storage = selected.storage || "";
    target.textContent = [color, storage].filter(Boolean).join(" / ");
  }

  updateSelectedVariant();
}

function initDetailCoupon() {
  var button = document.querySelector("[data-save-detail-coupon]");
  var input = document.getElementById("pdpCouponInput");
  var message = document.getElementById("pdpCouponMessage");
  if (!button || !input) return;

  var saved = sessionStorage.getItem("techvoraVoucherCode");
  if (saved) input.value = saved;

  button.addEventListener("click", function () {
    var code = input.value.trim().toUpperCase();
    if (!code) {
      if (message) message.textContent = "Nhập mã giảm giá trước khi áp dụng.";
      showToast("Nhập mã giảm giá trước khi áp dụng.", "error");
      return;
    }
    sessionStorage.setItem("techvoraVoucherCode", code);
    if (message) message.textContent = "Đã lưu mã " + code + ". Mã sẽ tự điền ở checkout.";
    showToast("Đã lưu mã giảm giá cho checkout.", "success");
  });
}

function initCartPage() {
  document.querySelectorAll(".cart-qty").forEach(function (input) {
    input.addEventListener("change", function () {
      var row = input.closest(".cart-item");
      if (!row) return;
      updateCart("/Cart/Update", row.dataset.productId, input.value, row);
    });
  });
  document.querySelectorAll(".cart-remove").forEach(function (button) {
    button.addEventListener("click", function () {
      var row = button.closest(".cart-item");
      if (!row) return;
      updateCart("/Cart/Remove", row.dataset.productId, 0, row, true);
    });
  });
}

function updateCart(url, productId, quantity, row, removeRow) {
  var body = new URLSearchParams();
  body.append("productId", productId);
  body.append("quantity", quantity);
  fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: body.toString()
  })
    .then(function (response) { return response.json(); })
    .then(function (data) {
      if (!data.success) return;
      var cartCount = document.getElementById("cart-count");
      var cartTotal = document.getElementById("cart-total");
      if (cartCount) cartCount.textContent = data.itemCount;
      if (cartTotal) cartTotal.textContent = data.total;
      var item = data.items.find(function (entry) { return String(entry.productId) === String(productId); });
      if (item && row) {
        var lineTotal = row.querySelector(".line-total");
        if (lineTotal) lineTotal.textContent = item.lineTotal;
      }
      if ((removeRow || Number(quantity) <= 0) && row) row.remove();
      showToast(data.message, "success");
      if (data.itemCount === 0) window.location.reload();
    });
}

function initReviewForm() {
  var form = document.getElementById("reviewForm");
  if (!form) return;
  form.addEventListener("submit", function (event) {
    event.preventDefault();
    var body = new URLSearchParams(new FormData(form));
    fetch("/Review/Submit", {
      method: "POST",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
        "RequestVerificationToken": antiForgeryToken()
      },
      body: body.toString()
    })
      .then(function (response) { return response.json(); })
      .then(function (data) {
        if (!data.success) {
          showToast(data.message, "error");
          return;
        }
        appendReview(data.review);
        form.remove();
        refreshIcons();
        showToast(data.message, "success");
      });
  });
}

function appendReview(review) {
  var list = document.getElementById("reviewList");
  if (!list) return;
  var item = document.createElement("article");
  item.className = "review-item new";
  var initial = review.user ? review.user.substring(0, 1).toUpperCase() : "K";
  var stars = '<span class="star-meter" aria-hidden="true">';
  for (var i = 1; i <= 5; i++) {
    stars += '<span class="star-meter-star" style="--fill:' + (i <= review.rating ? 100 : 0) + '%"><span>★</span></span>';
  }
  stars += "</span>";
  item.innerHTML =
    '<div class="review-avatar">' + escapeHtml(initial) + "</div>" +
    "<div>" +
    '<div class="review-meta"><strong>' + escapeHtml(review.user) + "</strong><span>" + escapeHtml(review.date) + "</span></div>" +
    stars +
    "<p>" + escapeHtml(review.comment) + "</p>" +
    "</div>";
  list.prepend(item);
}

function initVoucher() {
  var button = document.getElementById("applyVoucher");
  if (!button) return;
  var savedCode = sessionStorage.getItem("techvoraVoucherCode");
  var savedInput = document.getElementById("voucherInput");
  if (savedCode && savedInput && !savedInput.value) savedInput.value = savedCode;
  button.addEventListener("click", function () {
    var input = document.getElementById("voucherInput");
    var message = document.getElementById("voucherMessage");
    if (!input || !message) return;
    var body = new URLSearchParams();
    body.append("code", input.value);
    fetch("/Voucher/Validate", {
      method: "POST",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
        "RequestVerificationToken": antiForgeryToken()
      },
      body: body.toString()
    })
      .then(function (response) { return response.json(); })
      .then(function (data) {
        message.textContent = data.message;
        message.className = "voucher-message " + (data.valid ? "success" : "error");
        if (!data.valid) return;
        var voucherCode = document.getElementById("VoucherCode");
        var discountAmount = document.getElementById("DiscountAmount");
        var discountText = document.getElementById("discountText");
        var discountLine = document.getElementById("discountLine");
        var total = document.getElementById("checkoutTotal");
        if (voucherCode) voucherCode.value = data.code;
        if (discountAmount) discountAmount.value = data.discountAmount;
        if (discountText) discountText.textContent = data.formattedDiscount || data.discountLabel;
        if (discountLine) discountLine.classList.add("show");
        if (total) {
          total.textContent = data.total;
          total.classList.remove("bump");
          requestAnimationFrame(function () { total.classList.add("bump"); });
        }
        showToast(data.message, "success");
        sessionStorage.removeItem("techvoraVoucherCode");
      });
  });
}

function initQuantitySteppers() {
  document.querySelectorAll(".quantity-stepper").forEach(function (stepper) {
    var input = stepper.querySelector("input");
    var minus = stepper.querySelector("[data-qty-minus]");
    var plus = stepper.querySelector("[data-qty-plus]");
    if (!input) return;
    if (minus) minus.addEventListener("click", function () {
      input.value = Math.max(Number(input.min || 1), Number(input.value || 1) - 1);
      input.dispatchEvent(new Event("change", { bubbles: true }));
    });
    if (plus) plus.addEventListener("click", function () {
      input.value = Math.min(Number(input.max || 99), Number(input.value || 1) + 1);
      input.dispatchEvent(new Event("change", { bubbles: true }));
    });
    input.addEventListener("change", updateProductSubtotal);
  });
  updateProductSubtotal();
}

function updateProductSubtotal() {
  var card = document.querySelector("[data-product-price]");
  var input = document.getElementById("qtyInput");
  var subtotal = document.getElementById("pdpSubtotal");
  if (!card || !input || !subtotal) return;
  var price = Number(card.dataset.productPrice || 0);
  var qty = Math.max(Number(input.min || 1), Math.min(Number(input.max || 99), Number(input.value || 1)));
  input.value = qty;
  subtotal.textContent = new Intl.NumberFormat("vi-VN").format(price * qty) + " ₫";
}

function initShareTools() {
  document.querySelectorAll("[data-copy-link]").forEach(function (button) {
    button.addEventListener("click", function () {
      var link = window.location.href;
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(link).then(function () {
          showToast("Đã copy link sản phẩm.", "success");
        }).catch(function () {
          fallbackCopy(link);
        });
      } else {
        fallbackCopy(link);
      }
    });
  });
}

function fallbackCopy(value) {
  var textarea = document.createElement("textarea");
  textarea.value = value;
  textarea.setAttribute("readonly", "");
  textarea.style.position = "fixed";
  textarea.style.opacity = "0";
  document.body.appendChild(textarea);
  textarea.select();
  document.execCommand("copy");
  textarea.remove();
  showToast("Đã copy link sản phẩm.", "success");
}

function initAuthHelpers() {
  document.querySelectorAll("[data-toggle-password]").forEach(function (button) {
    button.addEventListener("click", function () {
      var input = button.parentElement.querySelector("[data-password-input]");
      if (!input) return;
      input.type = input.type === "password" ? "text" : "password";
      button.textContent = input.type === "password" ? "Hiện" : "Ẩn";
    });
  });
  document.querySelectorAll("[data-password-strength]").forEach(function (input) {
    input.addEventListener("input", function () {
      var score = 0;
      if (input.value.length >= 6) score++;
      if (/[A-Z]/.test(input.value)) score++;
      if (/[0-9]/.test(input.value)) score++;
      if (/[^A-Za-z0-9]/.test(input.value)) score++;
      var meter = document.querySelector(".password-meter span");
      if (meter) meter.style.width = Math.max(15, score * 25) + "%";
      var bar = document.getElementById("strengthBar");
      var label = document.getElementById("strengthLabel");
      var labels = ["Chưa nhập", "Yếu", "Trung bình", "Mạnh", "Rất mạnh"];
      var colors = ["#e2e8f0", "#ef4444", "#f59e0b", "#22c55e", "#0066ff"];
      if (bar) {
        bar.style.width = score * 25 + "%";
        bar.style.background = colors[score];
      }
      if (label) label.textContent = labels[score];
    });
  });
}

function initPaymentChoice() {
  var safe = document.getElementById("vnpaySafe");
  var submit = document.getElementById("checkoutSubmit");
  document.querySelectorAll('input[name="PaymentMethod"]').forEach(function (radio) {
    radio.addEventListener("change", function () {
      var isVnpay = radio.value === "VNPAY" && radio.checked;
      if (safe) safe.classList.toggle("show", isVnpay);
      if (submit && isVnpay) submit.textContent = "Thanh toán VNPAY";
      if (submit && !isVnpay) submit.textContent = "Đặt hàng";
    });
  });
}

function initSearchAutocomplete() {
  var input = document.querySelector("[data-search-autocomplete]");
  var suggestions = document.getElementById("searchSuggestions");
  if (!input || !suggestions) return;
  var timer;
  input.addEventListener("input", function () {
    clearTimeout(timer);
    var value = input.value.trim();
    if (value.length < 2) {
      suggestions.classList.remove("show");
      suggestions.innerHTML = "";
      return;
    }
    timer = setTimeout(function () {
      fetch("/Product/Search?q=" + encodeURIComponent(value))
        .then(function (response) { return response.json(); })
        .then(function (items) {
          suggestions.innerHTML = items.map(function (item) {
            return '<a href="/Product/Detail/' + item.id + '"><img src="' + escapeHtml(item.imageUrl) + '" alt=""><span>' + escapeHtml(item.name) + '</span><strong>' + escapeHtml(item.price) + "</strong></a>";
          }).join("");
          suggestions.classList.toggle("show", items.length > 0);
        });
    }, 180);
  });
}

function initReviewPagination() {
  var button = document.querySelector("[data-load-reviews]");
  if (!button) return;
  button.addEventListener("click", function () {
    document.querySelectorAll(".review-item.is-hidden").forEach(function (item, index) {
      if (index < 5) item.classList.remove("is-hidden");
    });
    if (!document.querySelector(".review-item.is-hidden")) button.remove();
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

function fillSelect(select, placeholder, values, selectedValue) {
  if (!select) return;
  select.innerHTML = '<option value="">' + placeholder + "</option>";
  values.forEach(function (value) {
    var option = document.createElement("option");
    option.value = value;
    option.textContent = value;
    if (value === selectedValue) option.selected = true;
    select.appendChild(option);
  });
  select.disabled = values.length === 0;
}

function initAddressDropdowns() {
  var province = document.getElementById("Province");
  var district = document.querySelector("[data-district-select]");
  var ward = document.querySelector("[data-ward-select]");
  if (!province || !district || !ward) return;

  var currentDistrict = district.dataset.current || district.value;
  var currentWard = ward.dataset.current || ward.value;

  function districtsForProvince() {
    return addressOptions[province.value] || {};
  }

  function populateDistricts(selectedDistrict) {
    var districts = Object.keys(districtsForProvince());
    fillSelect(district, "-- Chọn quận huyện --", districts, selectedDistrict || "");
    populateWards(district.value, currentWard);
  }

  function populateWards(selectedDistrict, selectedWard) {
    var wards = districtsForProvince()[selectedDistrict] || [];
    fillSelect(ward, "-- Chọn phường xã --", wards, selectedWard || "");
  }

  province.addEventListener("change", function () {
    currentDistrict = "";
    currentWard = "";
    populateDistricts("");
  });

  district.addEventListener("change", function () {
    currentWard = "";
    populateWards(district.value, "");
  });

  populateDistricts(currentDistrict);
}

function initProfileAddressFill() {
  var button = document.querySelector("[data-profile-address]");
  if (!button) return;
  button.addEventListener("click", function () {
    var parts = button.dataset.profileAddress.split(",").map(function (part) { return part.trim(); }).filter(Boolean);
    var street = document.getElementById("Street");
    var ward = document.getElementById("Ward");
    var district = document.getElementById("District");
    var province = document.getElementById("Province");
    if (parts.length >= 4) {
      if (street) street.value = parts.slice(0, parts.length - 3).join(", ");
      if (province) {
        province.value = parts[parts.length - 1];
        province.dispatchEvent(new Event("change", { bubbles: true }));
      }
      if (district) {
        district.value = parts[parts.length - 2];
        district.dispatchEvent(new Event("change", { bubbles: true }));
      }
      if (ward) ward.value = parts[parts.length - 3];
    } else if (street) {
      street.value = button.dataset.profileAddress;
    }
    showToast("Đã điền địa chỉ đã lưu.", "success");
  });
}

var addressApiBase = "https://provinces.open-api.vn/api";
var addressCache = {
  provinces: null,
  districtsByProvince: {},
  wardsByDistrict: {}
};

function normalizeAddressText(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/\s+/g, " ")
    .replace(/^(tinh|thanh pho|tp\.?|quan|huyen|thi xa|thi tran|phuong|xa)\s+/i, "")
    .trim();
}

function stripAddressPrefix(value) {
  return String(value || "")
    .trim()
    .replace(/^(tỉnh|tinh|thành phố|thanh pho|tp\.?|quận|quan|huyện|huyen|thị xã|thi xa|thị trấn|thi tran|phường|phuong|xã|xa)\s+/i, "")
    .trim();
}

function plainAddressText(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/\s+/g, " ")
    .trim();
}

function addressPartKind(value) {
  var text = plainAddressText(value);
  if (/^(tinh|tp\.?)\s+/.test(text)) return "province";
  if (/^thanh pho\s+/.test(text)) return "city";
  if (/^(quan|huyen|thi xa)\s+/.test(text)) return "district";
  if (/^(phuong|xa|thi tran)\s+/.test(text)) return "ward";
  return "";
}

function findAddressMatch(items, name) {
  var key = normalizeAddressText(name);
  if (!key) return null;
  return items.find(function (item) { return normalizeAddressText(item.name) === key; }) || items[0] || null;
}

function parseSavedAddressParts(parts) {
  var parsed = {
    streetParts: [],
    province: "",
    district: "",
    ward: ""
  };
  var used = {};

  for (var i = parts.length - 1; i >= 0; i--) {
    var kind = addressPartKind(parts[i]);
    var previousKind = i > 0 ? addressPartKind(parts[i - 1]) : "";

    if (!parsed.province && kind === "province") {
      parsed.province = parts[i];
      used[i] = true;
    } else if (!parsed.district && kind === "district") {
      parsed.district = parts[i];
      used[i] = true;
    } else if (!parsed.ward && kind === "ward") {
      parsed.ward = parts[i];
      used[i] = true;
    } else if (kind === "city") {
      if (!parsed.district && i === parts.length - 1 && previousKind === "ward") {
        parsed.district = parts[i];
      } else if (!parsed.province) {
        parsed.province = parts[i];
      }
      used[i] = true;
    }
  }

  if (!parsed.province && !parsed.district && !parsed.ward && parts.length >= 4) {
    parsed.province = parts[parts.length - 1];
    parsed.district = parts[parts.length - 2];
    parsed.ward = parts[parts.length - 3];
    used[parts.length - 1] = true;
    used[parts.length - 2] = true;
    used[parts.length - 3] = true;
  }

  parsed.streetParts = parts.filter(function (_, index) { return !used[index]; });
  return parsed;
}

function fetchAddressJson(url) {
  return fetch(url, { headers: { Accept: "application/json" } }).then(function (response) {
    if (!response.ok) throw new Error("Address API request failed");
    return response.json();
  });
}

function setSelectStatus(select, placeholder, disabled) {
  if (!select) return;
  select.innerHTML = '<option value="">' + placeholder + "</option>";
  select.disabled = Boolean(disabled);
}

function fillAddressSelect(select, placeholder, items, selectedValue) {
  if (!select) return null;
  select.innerHTML = '<option value="">' + placeholder + "</option>";
  var selectedOption = null;
  var selectedKey = normalizeAddressText(selectedValue);

  items.forEach(function (item) {
    var option = document.createElement("option");
    option.value = item.name;
    option.textContent = item.name;
    option.dataset.code = item.code;
    if (selectedKey && normalizeAddressText(item.name) === selectedKey) {
      option.selected = true;
      selectedOption = option;
    }
    select.appendChild(option);
  });

  if (selectedValue && !selectedOption) {
    selectedOption = document.createElement("option");
    selectedOption.value = selectedValue;
    selectedOption.textContent = selectedValue;
    selectedOption.selected = true;
    select.appendChild(selectedOption);
  }

  select.disabled = items.length === 0;
  return selectedOption;
}

function selectedAddressCode(select) {
  if (!select || !select.selectedOptions || !select.selectedOptions.length) return "";
  return select.selectedOptions[0].dataset.code || "";
}

function initAddressDropdowns() {
  var province = document.querySelector("[data-province-select]") || document.getElementById("Province");
  var district = document.querySelector("[data-district-select]");
  var ward = document.querySelector("[data-ward-select]");
  var street = document.getElementById("Street");
  if (!province || !district || !ward) return;

  var currentProvince = province.dataset.current || province.value;
  var currentDistrict = district.dataset.current || district.value;
  var currentWard = ward.dataset.current || ward.value;
  var loadToken = 0;

  function clearWards() {
    setSelectStatus(ward, "-- Chọn phường xã --", true);
  }

  function clearDistricts() {
    setSelectStatus(district, "-- Chọn quận huyện --", true);
    clearWards();
  }

  function loadProvinces() {
    if (addressCache.provinces) {
      fillAddressSelect(province, "-- Chọn tỉnh thành --", addressCache.provinces, currentProvince);
      return Promise.resolve(addressCache.provinces);
    }

    var fallbackOptions = Array.prototype.slice.call(province.options)
      .filter(function (option) { return option.value; })
      .map(function (option) { return { name: option.value, code: option.dataset.code || "" }; });

    setSelectStatus(province, "Đang tải tỉnh thành...", true);
    return fetchAddressJson(addressApiBase + "/p/")
      .then(function (items) {
        addressCache.provinces = items;
        fillAddressSelect(province, "-- Chọn tỉnh thành --", items, currentProvince);
        return items;
      })
      .catch(function () {
        fillAddressSelect(province, "-- Chọn tỉnh thành --", fallbackOptions, currentProvince);
        return fallbackOptions;
      })
      .finally(function () {
        province.disabled = false;
      });
  }

  function loadDistricts(provinceCode, selectedDistrict) {
    var token = ++loadToken;
    if (!provinceCode) {
      clearDistricts();
      return Promise.resolve([]);
    }

    if (addressCache.districtsByProvince[provinceCode]) {
      fillAddressSelect(district, "-- Chọn quận huyện --", addressCache.districtsByProvince[provinceCode], selectedDistrict);
      return Promise.resolve(addressCache.districtsByProvince[provinceCode]);
    }

    setSelectStatus(district, "Đang tải quận huyện...", true);
    clearWards();
    return fetchAddressJson(addressApiBase + "/p/" + encodeURIComponent(provinceCode) + "?depth=2")
      .then(function (data) {
        var districts = data.districts || [];
        addressCache.districtsByProvince[provinceCode] = districts;
        if (token === loadToken) fillAddressSelect(district, "-- Chọn quận huyện --", districts, selectedDistrict);
        return districts;
      })
      .catch(function () {
        if (token === loadToken) clearDistricts();
        return [];
      });
  }

  function loadWards(districtCode, selectedWard) {
    if (!districtCode) {
      clearWards();
      return Promise.resolve([]);
    }

    if (addressCache.wardsByDistrict[districtCode]) {
      fillAddressSelect(ward, "-- Chọn phường xã --", addressCache.wardsByDistrict[districtCode], selectedWard);
      return Promise.resolve(addressCache.wardsByDistrict[districtCode]);
    }

    setSelectStatus(ward, "Đang tải phường xã...", true);
    return fetchAddressJson(addressApiBase + "/d/" + encodeURIComponent(districtCode) + "?depth=2")
      .then(function (data) {
        var wards = data.wards || [];
        addressCache.wardsByDistrict[districtCode] = wards;
        fillAddressSelect(ward, "-- Chọn phường xã --", wards, selectedWard);
        return wards;
      })
      .catch(function () {
        clearWards();
        return [];
      });
  }

  function applyAddressParts(parts) {
    if (!parts || parts.length < 4) {
      if (street && parts && parts.length) street.value = parts.join(", ");
      return Promise.resolve();
    }

    currentProvince = parts[parts.length - 1];
    currentDistrict = parts[parts.length - 2];
    currentWard = parts[parts.length - 3];
    if (street) street.value = parts.slice(0, parts.length - 3).join(", ");

    return loadProvinces().then(function () {
      fillAddressSelect(province, "-- Chọn tỉnh thành --", addressCache.provinces || [], currentProvince);
      return loadDistricts(selectedAddressCode(province), currentDistrict);
    })
      .then(function () { return loadWards(selectedAddressCode(district), currentWard); });
  }

  function findDistrictByName(districtName) {
    var query = stripAddressPrefix(districtName) || normalizeAddressText(districtName);
    if (!query) return Promise.resolve(null);

    return fetchAddressJson(addressApiBase + "/d/search/?q=" + encodeURIComponent(query))
      .then(function (items) {
        return findAddressMatch(items || [], districtName);
      })
      .catch(function () {
        return null;
      });
  }

  function setProvinceFromCode(provinceCode) {
    if (!provinceCode) return Promise.resolve("");
    var cachedProvince = (addressCache.provinces || []).find(function (item) {
      return String(item.code) === String(provinceCode);
    });

    if (cachedProvince) {
      currentProvince = cachedProvince.name;
      fillAddressSelect(province, "-- Chá»n tá»‰nh thÃ nh --", addressCache.provinces || [], currentProvince);
      return Promise.resolve(String(cachedProvince.code));
    }

    return fetchAddressJson(addressApiBase + "/p/" + encodeURIComponent(provinceCode))
      .then(function (item) {
        currentProvince = item.name;
        fillAddressSelect(province, "-- Chá»n tá»‰nh thÃ nh --", addressCache.provinces || [item], currentProvince);
        return String(item.code);
      })
      .catch(function () {
        return "";
      });
  }

  function applySmartAddressParts(parts) {
    if (!parts || !parts.length) return Promise.resolve();

    var parsed = parseSavedAddressParts(parts);
    if (!parsed.province && !parsed.district && !parsed.ward) {
      if (street) street.value = parts.join(", ");
      return Promise.resolve();
    }

    currentProvince = parsed.province;
    currentDistrict = parsed.district;
    currentWard = parsed.ward;
    if (street) street.value = parsed.streetParts.join(", ");

    return loadProvinces().then(function () {
      if (currentProvince) {
        fillAddressSelect(province, "-- Chá»n tá»‰nh thÃ nh --", addressCache.provinces || [], currentProvince);
        return selectedAddressCode(province);
      }

      return findDistrictByName(currentDistrict).then(function (matchedDistrict) {
        return setProvinceFromCode(matchedDistrict && matchedDistrict.province_code);
      });
    }).then(function (provinceCode) {
      return loadDistricts(provinceCode || selectedAddressCode(province), currentDistrict);
    })
      .then(function () { return loadWards(selectedAddressCode(district), currentWard); });
  }

  window.techvoraAddressPicker = {
    applyParts: applySmartAddressParts
  };

  province.addEventListener("change", function () {
    currentProvince = province.value;
    currentDistrict = "";
    currentWard = "";
    loadDistricts(selectedAddressCode(province), "");
  });

  district.addEventListener("change", function () {
    currentDistrict = district.value;
    currentWard = "";
    loadWards(selectedAddressCode(district), "");
  });

  clearDistricts();
  loadProvinces().then(function () {
    return loadDistricts(selectedAddressCode(province), currentDistrict);
  }).then(function () {
    return loadWards(selectedAddressCode(district), currentWard);
  });
}

function initProfileAddressFill() {
  var button = document.querySelector("[data-profile-address]");
  if (!button) return;
  button.addEventListener("click", function () {
    var parts = button.dataset.profileAddress.split(",").map(function (part) { return part.trim(); }).filter(Boolean);
    var street = document.getElementById("Street");
    if (window.techvoraAddressPicker && typeof window.techvoraAddressPicker.applyParts === "function") {
      window.techvoraAddressPicker.applyParts(parts).then(function () {
        showToast("Đã điền địa chỉ đã lưu.", "success");
      });
    } else {
      if (street) street.value = button.dataset.profileAddress;
      showToast("Đã điền địa chỉ đã lưu.", "success");
    }
  });
}

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
