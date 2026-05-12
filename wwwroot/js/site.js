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
  initDetailAddToCart();
  initProfileAddressFill();
});

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
  var endTime = Date.now() + 6 * 3600000;
  function tick() {
    var diff = endTime - Date.now();
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
  var input = document.getElementById("qtyInput");
  if (!button || !input) return;
  button.addEventListener("click", function () {
    if (button.disabled) return;
    var qty = Math.max(Number(input.min || 1), Math.min(Number(input.max || 99), Number(input.value || 1)));
    var body = new URLSearchParams();
    body.append("productId", button.dataset.productId);
    body.append("quantity", qty);
    fetch("/Cart/Add", {
      method: "POST",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
        "RequestVerificationToken": antiForgeryToken()
      },
      body: body.toString()
    })
      .then(function (response) { return response.json(); })
      .then(function (data) {
        if (!data.success) return;
        var cartCount = document.getElementById("cart-count");
        if (cartCount) cartCount.textContent = data.itemCount;
        document.querySelectorAll(".cart-badge").forEach(function (badge) { badge.textContent = data.itemCount; });
        showToast(data.message, "success");
      });
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
  var stars = "";
  for (var i = 1; i <= 5; i++) stars += '<span class="' + (i <= review.rating ? "filled" : "") + '">★</span>';
  item.innerHTML =
    '<div class="review-avatar">' + escapeHtml(initial) + "</div>" +
    "<div>" +
    '<div class="review-meta"><strong>' + escapeHtml(review.user) + "</strong><span>" + escapeHtml(review.date) + "</span></div>" +
    '<div class="review-stars">' + stars + "</div>" +
    "<p>" + escapeHtml(review.comment) + "</p>" +
    "</div>";
  list.prepend(item);
}

function initVoucher() {
  var button = document.getElementById("applyVoucher");
  if (!button) return;
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
  });
}

function initShareTools() {
  document.querySelectorAll("[data-copy-link]").forEach(function (button) {
    button.addEventListener("click", function () {
      navigator.clipboard.writeText(window.location.href).then(function () {
        showToast("Đã copy link sản phẩm.", "success");
      });
    });
  });
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
      if (ward) ward.value = parts[parts.length - 3];
      if (district) district.value = parts[parts.length - 2];
      if (province) province.value = parts[parts.length - 1];
    } else if (street) {
      street.value = button.dataset.profileAddress;
    }
    showToast("Đã điền địa chỉ đã lưu.", "success");
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
