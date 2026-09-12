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
      searchBtn.setAttribute("aria-expanded", String(search.classList.contains("active")));
      var input = search.querySelector("input");
      if (search.classList.contains("active") && input) input.focus();
      else if (input && input.value) search.submit();
    });
  }

  var hamburger = document.querySelector(".nav-hamburger");
  var drawer = document.querySelector(".nav-drawer");
  var overlay = document.querySelector(".nav-overlay");
  var close = document.querySelector(".nav-drawer-close");
  if (hamburger) hamburger.addEventListener("click", openDrawer);
  if (overlay) overlay.addEventListener("click", closeDrawer);
  if (close) close.addEventListener("click", closeDrawer);

  document.addEventListener("keydown", function (event) {
    if (!drawer || !drawer.classList.contains("open")) return;
    if (event.key === "Escape") {
      event.preventDefault();
      closeDrawer();
      return;
    }
    if (event.key !== "Tab") return;

    var focusable = Array.prototype.slice.call(drawer.querySelectorAll("a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex='-1'])"));
    if (!focusable.length) return;
    var first = focusable[0];
    var last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  });

  document.querySelectorAll(".avatar-menu").forEach(function (menu) {
    var toggle = menu.querySelector(".avatar-btn");
    if (!toggle) return;
    toggle.addEventListener("click", function (event) {
      event.stopPropagation();
      var open = menu.classList.toggle("is-open");
      toggle.setAttribute("aria-expanded", String(open));
    });
  });

  document.addEventListener("click", function () {
    document.querySelectorAll(".avatar-menu.is-open").forEach(function (menu) {
      menu.classList.remove("is-open");
      var toggle = menu.querySelector(".avatar-btn");
      if (toggle) toggle.setAttribute("aria-expanded", "false");
    });
  });

  document.addEventListener("keydown", function (event) {
    if (event.key !== "Escape") return;
    document.querySelectorAll(".avatar-menu.is-open").forEach(function (menu) {
      menu.classList.remove("is-open");
      var toggle = menu.querySelector(".avatar-btn");
      if (toggle) {
        toggle.setAttribute("aria-expanded", "false");
        toggle.focus();
      }
    });
  });

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
  var hamburger = document.querySelector(".nav-hamburger");
  drawerTrigger = document.activeElement;
  if (drawer) {
    drawer.removeAttribute("inert");
    drawer.setAttribute("aria-hidden", "false");
    drawer.classList.add("open");
    var close = drawer.querySelector(".nav-drawer-close");
    if (close) close.focus();
  }
  if (overlay) {
    overlay.setAttribute("aria-hidden", "false");
    overlay.classList.add("open");
  }
  if (hamburger) hamburger.setAttribute("aria-expanded", "true");
  document.body.style.overflow = "hidden";
}

function closeDrawer() {
  var drawer = document.querySelector(".nav-drawer");
  var overlay = document.querySelector(".nav-overlay");
  var hamburger = document.querySelector(".nav-hamburger");
  if (drawer) {
    drawer.classList.remove("open");
    drawer.setAttribute("aria-hidden", "true");
    drawer.setAttribute("inert", "");
  }
  if (overlay) {
    overlay.classList.remove("open");
    overlay.setAttribute("aria-hidden", "true");
  }
  if (hamburger) hamburger.setAttribute("aria-expanded", "false");
  document.body.style.overflow = "";
  if (drawerTrigger && typeof drawerTrigger.focus === "function") drawerTrigger.focus();
  drawerTrigger = null;
}

function initCatalogFilters() {
  var root = document.querySelector("[data-catalog-filters]");
  if (!root) return;

  var toggle = root.querySelector("[data-catalog-filter-toggle]");
  var panel = root.querySelector(".catalog-sidebar");
  var results = root.querySelector(".catalog-results");
  var layoutButtons = root.querySelectorAll("[data-catalog-layout]");
  var categoryExpand = root.querySelector("[data-category-expand]");

  function setOpen(open) {
    root.classList.toggle("filter-open", open);
    if (toggle) toggle.setAttribute("aria-expanded", String(open));
    if (panel) panel.hidden = !open;
  }

  function setLayout(layout) {
    var listView = layout === "list";
    if (results) results.classList.toggle("is-list-view", listView);
    layoutButtons.forEach(function (button) {
      var active = button.dataset.catalogLayout === layout;
      button.classList.toggle("active", active);
      button.setAttribute("aria-pressed", String(active));
    });
    try {
      window.localStorage.setItem("techvora-catalog-layout", layout);
    } catch (error) {
      // Storage can be disabled without affecting this control.
    }
  }

  if (toggle && panel) {
    toggle.addEventListener("click", function () {
      setOpen(panel.hidden);
    });

    document.addEventListener("keydown", function (event) {
      if (event.key === "Escape" && !panel.hidden) {
        setOpen(false);
        toggle.focus();
      }
    });
  }

  layoutButtons.forEach(function (button) {
    button.addEventListener("click", function () {
      setLayout(button.dataset.catalogLayout || "grid");
    });
  });

  if (categoryExpand) {
    categoryExpand.addEventListener("click", function () {
      var expanded = root.classList.toggle("categories-expanded");
      categoryExpand.setAttribute("aria-expanded", String(expanded));
      var label = categoryExpand.querySelector("span");
      if (label) label.textContent = expanded ? "Thu gọn" : "Xem thêm";
    });
  }

  var savedLayout = "grid";
  try {
    savedLayout = window.localStorage.getItem("techvora-catalog-layout") || "grid";
  } catch (error) {
    savedLayout = "grid";
  }

  setOpen(false);
  setLayout(savedLayout === "list" ? "list" : "grid");
}

function initHeroSlider() {
  var slider = document.querySelector(".hero-slider");
  var track = document.querySelector(".slider-track");
  var dots = slider ? slider.querySelectorAll(".dot") : [];
  if (!slider || !track || dots.length === 0) return;
  var total = dots.length;
  var current = 0;
  var timer;
  var slides = slider.querySelectorAll(".slide");
  var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  function goTo(index) {
    current = (index + total) % total;
    track.style.transform = "translateX(-" + current * 100 + "%)";
    dots.forEach(function (dot, i) {
      var active = i === current;
      dot.classList.toggle("active", active);
      dot.setAttribute("aria-current", String(active));
    });
    slides.forEach(function (slide, i) {
      var active = i === current;
      slide.setAttribute("aria-hidden", String(!active));
      if (active) slide.removeAttribute("inert");
      else slide.setAttribute("inert", "");
    });
  }

  function startAuto() {
    if (reduceMotion || document.hidden || timer) return;
    timer = setInterval(function () { goTo(current + 1); }, 5000);
  }
  function stopAuto() {
    clearInterval(timer);
    timer = null;
  }

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
  slider.addEventListener("focusin", stopAuto);
  slider.addEventListener("focusout", startAuto);
  document.addEventListener("visibilitychange", function () {
    if (document.hidden) stopAuto();
    else startAuto();
  });
  goTo(0);
  startAuto();
}

function initCountdown() {
  var elements = [];
  var legacy = document.getElementById("countdown");
  if (legacy) elements.push(legacy);
  document.querySelectorAll("[data-countdown]").forEach(function (el) {
    if (el !== legacy) elements.push(el);
  });
  if (!elements.length) return;

  function pad(n) { return String(n).padStart(2, "0"); }

  elements.forEach(function (el) {
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

    tick();
    setInterval(tick, 1000);
  });
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

function initCountUp() {
  var counters = Array.prototype.slice.call(document.querySelectorAll("[data-count-target]"));
  if (!counters.length) return;

  function formatValue(value, suffix) {
    var rounded = Math.round(value);
    var formatted = rounded >= 1000 ? rounded.toLocaleString("en-US") : String(rounded);
    return formatted + (suffix || "");
  }

  function animateCounter(element) {
    if (element.dataset.countStarted === "true") return;
    element.dataset.countStarted = "true";

    var target = Number(element.getAttribute("data-count-target") || "0");
    if (!Number.isFinite(target)) return;

    var suffix = element.getAttribute("data-count-suffix") || "";
    var duration = 2000;
    var start = null;

    function step(timestamp) {
      if (start === null) start = timestamp;
      var progress = Math.min((timestamp - start) / duration, 1);
      var eased = 1 - Math.pow(1 - progress, 4);
      element.textContent = formatValue(target * eased, suffix);
      if (progress < 1) {
        requestAnimationFrame(step);
      } else {
        element.textContent = formatValue(target, suffix);
      }
    }

    element.textContent = formatValue(0, suffix);
    requestAnimationFrame(step);
  }

  var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  if (reduceMotion || !("IntersectionObserver" in window)) {
    counters.forEach(function (element) {
      var target = Number(element.getAttribute("data-count-target") || "0");
      if (Number.isFinite(target)) {
        element.textContent = formatValue(target, element.getAttribute("data-count-suffix") || "");
      }
    });
    return;
  }

  counters.forEach(function (element) {
    element.textContent = formatValue(0, element.getAttribute("data-count-suffix") || "");
  });

  var observer = new IntersectionObserver(function (entries) {
    entries.forEach(function (entry) {
      if (entry.isIntersecting) {
        animateCounter(entry.target);
        observer.unobserve(entry.target);
      }
    });
  }, { threshold: 0.35 });

  counters.forEach(function (element) { observer.observe(element); });
}

