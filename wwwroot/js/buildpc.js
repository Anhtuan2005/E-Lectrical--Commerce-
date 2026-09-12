(function () {
  "use strict";

  var slotProducts = window.SLOT_PRODUCTS || {};
  var smartGoals = window.SMART_GOALS || [];
  var build = {};
  window.build = build;

  var currentSlot = null;
  var latestSmartBuild = null;
  var activeSmartVariant = null;
  var selectedGoal = smartGoals[0] ? smartGoals[0].key : "gaming";

  var modal = document.getElementById("slotModal");
  var modalTitle = document.getElementById("modalTitle");
  var modalSearch = document.getElementById("modalSearch");
  var modalList = document.getElementById("modalProductList");
  var modalClose = document.getElementById("modalClose");
  var summaryList = document.getElementById("summaryList");
  var buildTotal = document.getElementById("buildTotal");
  var btnAddAll = document.getElementById("btnAddAll");
  var btnReset = document.getElementById("btnReset");
  var warningsEl = document.getElementById("compatWarnings");
  var wattageInfo = document.getElementById("wattageInfo");
  var wattageVal = document.getElementById("wattageVal");
  var summaryAdvisorText = document.getElementById("summaryAdvisorText");

  var smartBuildForm = document.getElementById("smartBuildForm");
  var smartBudget = document.getElementById("smartBudget");
  var smartNote = document.getElementById("smartNote");
  var smartGenerate = document.getElementById("smartGenerate");
  var smartResult = document.getElementById("smartResult");
  var smartResultTitle = document.getElementById("smartResultTitle");
  var smartResultTotal = document.getElementById("smartResultTotal");
  var smartResultDelta = document.getElementById("smartResultDelta");
  var smartResultSlots = document.getElementById("smartResultSlots");
  var smartInsights = document.getElementById("smartInsights");
  var smartWarnings = document.getElementById("smartWarnings");
  var smartCompatibility = document.getElementById("smartCompatibility");
  var smartVariantTabs = document.getElementById("smartVariantTabs");
  var smartApplyBuild = document.getElementById("smartApplyBuild");

  document.querySelectorAll("[data-smart-goal]").forEach(function (button) {
    button.addEventListener("click", function () {
      selectedGoal = button.dataset.smartGoal || selectedGoal;
      document.querySelectorAll("[data-smart-goal]").forEach(function (goalButton) {
        var selected = goalButton === button;
        goalButton.classList.toggle("is-active", selected);
        goalButton.setAttribute("aria-pressed", selected ? "true" : "false");
      });

      if (smartBudget && button.dataset.budget) {
        smartBudget.value = button.dataset.budget;
      }
    });
  });

  document.querySelectorAll("[data-choose]").forEach(function (button) {
    button.addEventListener("click", function () {
      openModal(button.dataset.choose);
    });
  });

  document.querySelectorAll("[data-clear]").forEach(function (button) {
    button.addEventListener("click", function () {
      clearSlot(button.dataset.clear);
    });
  });

  if (smartBuildForm) {
    smartBuildForm.addEventListener("submit", function (event) {
      event.preventDefault();
      generateSmartBuild();
    });
  }

  if (smartApplyBuild) {
    smartApplyBuild.addEventListener("click", function () {
      if (activeSmartVariant && activeSmartVariant.slots) {
        applySmartBuild(activeSmartVariant.slots, true);
      }
    });
  }

  if (modalClose) modalClose.addEventListener("click", closeModal);
  if (modal) {
    modal.addEventListener("click", function (event) {
      if (event.target === modal) closeModal();
    });
  }

  if (modalSearch) {
    modalSearch.addEventListener("input", function () {
      if (currentSlot) renderModalProducts(currentSlot, modalSearch.value.toLowerCase());
    });
  }

  if (btnAddAll) btnAddAll.addEventListener("click", addAllToCart);
  if (btnReset) btnReset.addEventListener("click", resetBuild);

  async function generateSmartBuild() {
    if (!smartGenerate) return;

    setSmartLoading(true);
    try {
      var response = await fetch("/BuildPc/SmartBuild", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "RequestVerificationToken": antiForgeryToken()
        },
        body: JSON.stringify({
          goal: selectedGoal,
          budget: Number(smartBudget && smartBudget.value ? smartBudget.value : 0),
          note: smartNote ? smartNote.value : ""
        })
      });

      var data = await readJsonResponse(response);
      if (!response.ok || !data.success) {
        throw new Error(data.message || "Chưa tạo được cấu hình. Bạn thử lại sau nhé.");
      }

      if (smartBudget && data.budget) {
        smartBudget.value = String(Math.round(Number(data.budget)));
      }

      latestSmartBuild = data;
      var primaryVariant = renderSmartResult(data);
      applySmartBuild((primaryVariant && primaryVariant.slots) || data.slots || [], false);
      showNotice(data.message || "Đã tạo cấu hình gợi ý.", "success");
      if (smartResult) smartResult.scrollIntoView({ behavior: "smooth", block: "start" });
    } catch (error) {
      console.error("Smart PC Builder error:", error);
      showNotice(error.message || "Chưa tạo được cấu hình. Bạn thử lại sau nhé.", "error");
      if (summaryAdvisorText) summaryAdvisorText.textContent = error.message || "Trợ lý cấu hình chưa có phản hồi.";
    } finally {
      setSmartLoading(false);
    }
  }

  function renderSmartResult(data) {
    if (!smartResult) return null;

    smartResult.hidden = false;
    var variants = data.variants && data.variants.length ? data.variants : [data];
    var activeKey = data.variantKey || (variants[0] && variants[0].key) || "balanced";
    renderSmartVariantTabs(variants, activeKey);

    var primary = variants.find(function (variant) {
      return variant.key === activeKey;
    }) || variants[0] || data;

    renderSmartVariant(primary, data.goalLabel);
    return primary;
  }

  function renderSmartVariantTabs(variants, activeKey) {
    if (!smartVariantTabs) return;

    smartVariantTabs.innerHTML = variants.map(function (variant) {
      var isActive = variant.key === activeKey;
      return '' +
        '<button class="smart-variant-tab ' + (isActive ? "is-active" : "") + '" type="button" role="tab" aria-selected="' + (isActive ? "true" : "false") + '" data-variant-key="' + escapeHtml(variant.key) + '">' +
          '<span>' + escapeHtml(variant.label || "Cấu hình") + '</span>' +
          '<strong>' + escapeHtml(variant.badge || formatMoney(variant.total)) + '</strong>' +
          '<small>' + escapeHtml(formatMoney(variant.total)) + '</small>' +
        '</button>';
    }).join("");

    smartVariantTabs.querySelectorAll("[data-variant-key]").forEach(function (button) {
      button.addEventListener("click", function () {
        var variants = latestSmartBuild && latestSmartBuild.variants && latestSmartBuild.variants.length
          ? latestSmartBuild.variants
          : [];
        var variant = variants.find(function (entry) {
          return entry.key === button.dataset.variantKey;
        });
        if (!variant) return;

        smartVariantTabs.querySelectorAll(".smart-variant-tab").forEach(function (tab) {
          var selected = tab === button;
          tab.classList.toggle("is-active", selected);
          tab.setAttribute("aria-selected", selected ? "true" : "false");
        });
        renderSmartVariant(variant, latestSmartBuild.goalLabel);
        applySmartBuild(variant.slots || [], false);
      });
    });
  }

  function renderSmartVariant(data, goalLabel) {
    activeSmartVariant = data;

    if (smartResultTitle) {
      smartResultTitle.textContent = (goalLabel ? "Cấu hình " + goalLabel : "Cấu hình gợi ý") + " - " + (data.label || "Cân bằng");
    }
    if (smartResultTotal) smartResultTotal.textContent = formatMoney(data.total);
    if (smartResultDelta) {
      var remaining = Number(data.remaining || 0);
      smartResultDelta.textContent = remaining >= 0
        ? "Còn dư " + formatMoney(remaining)
        : "Vượt " + formatMoney(Math.abs(remaining));
      smartResultDelta.classList.toggle("is-over", remaining < 0);
    }

    setScore("smartPerformance", data.performanceScore);
    setScore("smartBalance", data.balanceScore);
    setScore("smartUpgrade", data.upgradeScore);
    setScore("smartValue", data.valueScore);

    if (smartResultSlots) {
      smartResultSlots.innerHTML = (data.slots || []).map(function (slot) {
        return '' +
          '<article class="smart-slot-card">' +
            '<a href="' + escapeHtml(slot.url || "#") + '">' +
              '<img src="' + escapeHtml(slot.imageUrl || "/images/placeholder.svg") + '" alt="' + escapeHtml(slot.name) + '" loading="lazy" decoding="async" />' +
            '</a>' +
            '<div>' +
              '<span>' + escapeHtml(slot.label || slot.slot) + '</span>' +
              '<h3>' + escapeHtml(slot.name) + '</h3>' +
              '<p>' + escapeHtml(slot.reason || "") + '</p>' +
              '<div><strong>' + escapeHtml(slot.price || formatMoney(slot.priceRaw)) + '</strong><em>' + escapeHtml(slot.meta || "") + '</em></div>' +
            '</div>' +
          '</article>';
      }).join("");
    }

    if (smartInsights) {
      smartInsights.innerHTML = (data.insights || []).map(function (item) {
        return '<li>' + escapeHtml(item) + '</li>';
      }).join("");
    }

    renderCompatibilityChecks(data.compatibilityChecks || []);

    if (smartWarnings) {
      smartWarnings.innerHTML = (data.warnings || []).map(function (item) {
        return '<div class="ai-warning"><i data-lucide="triangle-alert" aria-hidden="true"></i><span>' + escapeHtml(item) + '</span></div>';
      }).join("");
    }

    if (summaryAdvisorText) {
      var firstInsight = data.insights && data.insights.length ? data.insights[0] : data.message;
      summaryAdvisorText.textContent = firstInsight || "Trợ lý cấu hình đã cập nhật cấu hình.";
    }

    refreshIcons();
  }

  function renderCompatibilityChecks(checks) {
    if (!smartCompatibility) return;

    if (!checks.length) {
      smartCompatibility.innerHTML = '<div class="compat-check compat-check-info"><i data-lucide="circle-help" aria-hidden="true"></i><div><strong>Chưa có dữ liệu kiểm tra</strong><span>Trợ lý cấu hình cần đủ linh kiện để đối chiếu.</span></div></div>';
      return;
    }

    smartCompatibility.innerHTML = checks.map(function (check) {
      var severity = check.severity || "info";
      return '' +
        '<div class="compat-check compat-check-' + escapeHtml(severity) + '">' +
          '<i data-lucide="' + escapeHtml(check.icon || iconForSeverity(severity)) + '" aria-hidden="true"></i>' +
          '<div>' +
            '<strong>' + escapeHtml(check.label || "Kiểm tra") + '</strong>' +
            '<span>' + escapeHtml(check.message || "") + '</span>' +
            '<small>' + escapeHtml(check.detail || "") + '</small>' +
          '</div>' +
        '</div>';
    }).join("");
  }

  function iconForSeverity(severity) {
    return {
      ok: "circle-check",
      warning: "triangle-alert",
      error: "octagon-alert",
      info: "circle-help"
    }[severity] || "circle-help";
  }

  function applySmartBuild(slots, announce) {
    Object.keys(build).forEach(function (slot) {
      clearSlot(slot, true);
    });

    slots.forEach(function (slot) {
      selectProduct(slot.slot, {
        id: slot.productId || slot.id,
        name: slot.name,
        priceRaw: slot.priceRaw,
        price: slot.price,
        imageUrl: slot.imageUrl,
        category: slot.category,
        stock: slot.stock,
        url: slot.url,
        reason: slot.reason,
        meta: slot.meta
      }, true);
    });

    updateSummary();
    checkCompatibility();
    persistBuild();

    if (announce) {
      showNotice("Đã áp dụng lại cấu hình Trợ lý cấu hình.", "success");
    }
  }

  function openModal(slot) {
    currentSlot = slot;
    modalTitle.textContent = "Chọn " + getSlotLabel(slot);
    modalSearch.value = "";
    renderModalProducts(slot, "");
    modal.hidden = false;
    document.body.style.overflow = "hidden";
    modalSearch.focus();
  }

  function closeModal() {
    modal.hidden = true;
    document.body.style.overflow = "";
    currentSlot = null;
  }

  function renderModalProducts(slot, filter) {
    var products = (slotProducts[slot] || []).filter(function (product) {
      return !filter || product.name.toLowerCase().indexOf(filter) >= 0;
    });

    if (!products.length) {
      modalList.innerHTML = '<p class="modal-empty">Chưa có sản phẩm phù hợp cho slot này.</p>';
      return;
    }

    modalList.innerHTML = products.map(function (product) {
      var selected = build[slot] && Number(build[slot].id) === Number(product.id);
      return '' +
        '<article class="modal-product-item ' + (selected ? "selected" : "") + '" data-product-id="' + product.id + '">' +
          '<img src="' + escapeHtml(product.imageUrl || "/images/placeholder.svg") + '" alt="' + escapeHtml(product.name) + '" loading="lazy" decoding="async" />' +
          '<div class="modal-product-info">' +
            '<span>' + escapeHtml(product.name) + '</span>' +
            '<strong>' + escapeHtml(product.price) + '</strong>' +
            '<small>' + escapeHtml(product.category || getSlotLabel(slot)) + '</small>' +
          '</div>' +
          '<button class="btn-primary modal-select-btn" type="button">' + (selected ? "Đã chọn" : "Chọn") + '</button>' +
        '</article>';
    }).join("");

    modalList.querySelectorAll("[data-product-id]").forEach(function (item) {
      item.querySelector(".modal-select-btn").addEventListener("click", function () {
        var product = products.find(function (entry) { return String(entry.id) === String(item.dataset.productId); });
        if (product) selectProduct(currentSlot, product);
        closeModal();
      });
    });
  }

  function selectProduct(slot, product, silent) {
    if (!slot || !product) return;
    build[slot] = product;
    updateSlotRow(slot, product);
    if (!silent) {
      updateSummary();
      checkCompatibility();
      persistBuild();
    }
  }

  function updateSlotRow(slot, product) {
    var row = document.querySelector('[data-slot="' + cssEscape(slot) + '"]');
    if (!row) return;
    row.querySelector(".slot-selected-name").textContent = product.name;
    row.querySelector(".slot-price-display").textContent = product.price || formatMoney(product.priceRaw);
    row.querySelector(".slot-clear-btn").hidden = false;
    row.classList.add("has-product");
  }

  function clearSlot(slot, silent) {
    delete build[slot];
    var row = document.querySelector('[data-slot="' + cssEscape(slot) + '"]');
    if (row) {
      row.querySelector(".slot-selected-name").textContent = "Chưa chọn";
      row.querySelector(".slot-price-display").textContent = "-";
      row.querySelector(".slot-clear-btn").hidden = true;
      row.classList.remove("has-product");
    }

    if (!silent) {
      updateSummary();
      checkCompatibility();
      persistBuild();
    }
  }

  function updateSummary() {
    var entries = Object.entries(build);
    if (!entries.length) {
      summaryList.innerHTML = '<li class="summary-empty">Chưa chọn linh kiện nào</li>';
    } else {
      summaryList.innerHTML = entries.map(function (entry) {
        var slot = entry[0];
        var product = entry[1];
        var shortName = product.name.length > 30 ? product.name.substring(0, 30) + "..." : product.name;
        return '<li><span>' + getSlotLabel(slot) + '</span><span>' + escapeHtml(shortName) + '</span><strong>' + escapeHtml(product.price || formatMoney(product.priceRaw)) + '</strong></li>';
      }).join("");
    }

    var total = entries.reduce(function (sum, entry) {
      return sum + Number(entry[1].priceRaw || 0);
    }, 0);

    buildTotal.textContent = formatMoney(total);
    if (btnAddAll) btnAddAll.disabled = entries.length === 0;
    updateWattage();
    updateSummaryAdvisor();
  }

  function updateWattage() {
    var total = estimateBuildWattage();

    wattageInfo.hidden = total <= 0;
    if (total > 0) wattageVal.textContent = total;
  }

  function checkCompatibility() {
    var checks = getClientCompatibilityChecks();
    var warnings = checks
      .filter(function (check) { return check.severity === "warning" || check.severity === "error"; })
      .map(function (check) { return check.message; });

    warningsEl.innerHTML = warnings.map(function (warning) {
      return '<div class="compat-warn">' + escapeHtml(warning) + '</div>';
    }).join("");

    updateSummaryAdvisor(warnings);
    return warnings;
  }

  async function addAllToCart() {
    if (!btnAddAll) return;
    var items = Object.entries(build);
    if (!items.length) return;

    btnAddAll.disabled = true;
    btnAddAll.textContent = "Đang thêm...";

    try {
      var body = new URLSearchParams();
      items.forEach(function (entry) {
        var slot = entry[0];
        var product = entry[1];
        body.append("productIds", product.id);
        body.append("slots", slot);
      });
      body.append("groupName", getCurrentBuildName());
      var response = await fetch("/BuildPc/AddToCart", {
        method: "POST",
        headers: {
          "Content-Type": "application/x-www-form-urlencoded",
          "RequestVerificationToken": antiForgeryToken()
        },
        body: body.toString()
      });
      var data = await response.json();
      if (!data.success) {
        btnAddAll.disabled = false;
        btnAddAll.textContent = "Thêm tất cả vào giỏ";
        showNotice(data.message, "error");
        return;
      }

      var cartCount = document.getElementById("cart-count");
      if (cartCount) cartCount.textContent = data.itemCount;
      btnAddAll.textContent = "Đã thêm vào giỏ";
      window.location.href = data.redirectUrl || "/Cart";
    } catch (error) {
      console.error("Lỗi thêm giỏ:", error);
      btnAddAll.disabled = false;
      btnAddAll.textContent = "Thêm tất cả vào giỏ";
      showNotice("Chưa thêm được cấu hình vào giỏ.", "error");
    }
  }

  function getCurrentBuildName() {
    if (activeSmartVariant && activeSmartVariant.label) {
      return "Bộ cấu hình Smart PC - " + activeSmartVariant.label;
    }

    return "Bộ cấu hình Smart PC";
  }

  function resetBuild() {
    if (!Object.keys(build).length) return;
    if (!window.confirm("Đặt lại toàn bộ cấu hình?")) return;
    Object.keys(build).forEach(function (slot) {
      clearSlot(slot, true);
    });
    updateSummary();
    checkCompatibility();
    persistBuild();
  }

  function updateSummaryAdvisor(warnings) {
    if (!summaryAdvisorText) return;

    var entries = Object.entries(build);
    if (!entries.length) {
      summaryAdvisorText.textContent = "Tạo cấu hình thông minh để nhận đánh giá nhanh.";
      return;
    }

    var total = entries.reduce(function (sum, entry) {
      return sum + Number(entry[1].priceRaw || 0);
    }, 0);
    var currentWarnings = warnings || [];
    if (currentWarnings.length) {
      summaryAdvisorText.textContent = currentWarnings[0];
      return;
    }

    summaryAdvisorText.textContent = entries.length + "/8 nhóm linh kiện, tổng " + formatMoney(total) + ". Cấu hình hiện chưa có cảnh báo nhanh.";
  }

  function getClientCompatibilityChecks() {
    var checks = [];
    var estimatedWatts = estimateBuildWattage();
    var recommendedPsu = recommendPsuWattage(estimatedWatts);

    if (build.CPU && build.Mainboard) {
      var cpuPlatform = getCpuPlatform(build.CPU);
      var boardPlatform = getMainboardPlatform(build.Mainboard);
      if (cpuPlatform && boardPlatform && cpuPlatform !== boardPlatform) {
        checks.push({ severity: "error", message: "CPU và mainboard có dấu hiệu khác nền tảng socket." });
      }
    } else if (build.CPU || build.Mainboard) {
      checks.push({ severity: "warning", message: "Chọn đủ CPU và Mainboard để kiểm tra socket." });
    }

    if (build.RAM && build.Mainboard) {
      var ramType = getMemoryType(build.RAM);
      var boardMemory = getMemoryType(build.Mainboard);
      if (ramType && boardMemory && ramType !== boardMemory) {
        checks.push({ severity: "error", message: "RAM và mainboard có dấu hiệu khác chuẩn DDR." });
      }
    }

    if (build.PSU && estimatedWatts > 0) {
      var psuWattage = extractWattage(build.PSU.name);
      if (psuWattage > 0 && psuWattage < recommendedPsu) {
        checks.push({ severity: "error", message: "Nguồn " + psuWattage + "W thấp hơn gợi ý " + recommendedPsu + "W." });
      }
    } else if (build.VGA && !build.PSU) {
      checks.push({ severity: "warning", message: "Bạn đã chọn VGA, hãy chọn thêm nguồn phù hợp." });
    }

    if (build.CPU && getComponentTier("CPU", build.CPU) >= 9 && (!build.Cooling || getComponentTier("Cooling", build.Cooling) < 8)) {
      checks.push({ severity: "warning", message: "CPU mạnh nên đi với tản nhiệt tốt hơn." });
    }

    if (build.VGA && getComponentTier("VGA", build.VGA) >= 8 && build.Case && !hasAirflowCase(build.Case)) {
      checks.push({ severity: "warning", message: "VGA mạnh nên đi với case airflow tốt." });
    }

    return checks;
  }

  async function readJsonResponse(response) {
    var text = await response.text();
    if (!text) return {};
    try {
      return JSON.parse(text);
    } catch (error) {
      return { success: false, message: text };
    }
  }

  function setSmartLoading(isLoading) {
    smartBuildForm.classList.toggle("is-loading", isLoading);
    smartGenerate.disabled = isLoading;
    smartGenerate.innerHTML = isLoading
      ? '<i data-lucide="loader-circle" aria-hidden="true"></i> Đang phân tích...'
      : '<i data-lucide="sparkles" aria-hidden="true"></i> Tạo cấu hình thông minh';
    refreshIcons();
  }

  function setScore(prefix, value) {
    var score = Math.max(0, Math.min(100, Number(value || 0)));
    var scoreEl = document.getElementById(prefix + "Score");
    var barEl = document.getElementById(prefix + "Bar");
    if (scoreEl) scoreEl.textContent = Math.round(score);
    if (barEl) barEl.style.width = score + "%";
  }

  function estimateBuildWattage() {
    return Object.keys(build).reduce(function (sum, slot) {
      return sum + estimatePartWattage(slot, build[slot]);
    }, 0);
  }

  function estimatePartWattage(slot, product) {
    var text = normalizeText((product && product.name) || "");
    if (slot === "CPU") {
      if (text.indexOf("7800x3d") >= 0 || text.indexOf("ryzen 7") >= 0) return 120;
      if (text.indexOf("i7") >= 0 || text.indexOf("i9") >= 0) return 145;
      if (text.indexOf("i5") >= 0) return 95;
      return 75;
    }
    if (slot === "VGA") {
      if (text.indexOf("4080") >= 0) return 330;
      if (text.indexOf("4070") >= 0) return 230;
      if (text.indexOf("7800") >= 0) return 270;
      if (text.indexOf("4060") >= 0) return 130;
      return 180;
    }
    return { RAM: 12, SSD: 8, Mainboard: 55, Cooling: 15 }[slot] || 0;
  }

  function recommendPsuWattage(estimatedWattage) {
    return Math.min(1200, Math.max(550, Math.ceil((estimatedWattage * 1.35 + 80) / 50) * 50));
  }

  function getComponentTier(slot, product) {
    var text = normalizeText((product && product.name) || "");
    if (slot === "CPU") {
      if (text.indexOf("ryzen 9") >= 0 || text.indexOf("i9") >= 0) return 10;
      if (text.indexOf("7800x3d") >= 0 || text.indexOf("ryzen 7") >= 0 || text.indexOf("i7") >= 0) return 9;
      if (text.indexOf("i5") >= 0 || text.indexOf("ryzen 5") >= 0) return 6;
    }
    if (slot === "VGA") {
      if (text.indexOf("4080") >= 0 || text.indexOf("4090") >= 0) return 10;
      if (text.indexOf("4070") >= 0) return 9;
      if (text.indexOf("7800") >= 0) return 8;
      if (text.indexOf("4060") >= 0) return 6;
    }
    if (slot === "RAM") {
      if (text.indexOf("64gb") >= 0) return 10;
      if (text.indexOf("32gb") >= 0) return 8;
      if (text.indexOf("16gb") >= 0) return 5;
    }
    if (slot === "Cooling" && (text.indexOf("aio") >= 0 || text.indexOf("nh d15") >= 0)) return 9;
    return 5;
  }

  function getCpuPlatform(product) {
    var text = normalizeText((product && product.name) || "");
    if (text.indexOf("intel") >= 0 || text.indexOf("core i") >= 0) return "intel-lga1700";
    if (text.indexOf("7800") >= 0 || text.indexOf("am5") >= 0 || text.indexOf("ryzen 7000") >= 0) return "amd-am5";
    if (text.indexOf("ryzen") >= 0 || text.indexOf("am4") >= 0) return "amd-am4";
    return "";
  }

  function getMainboardPlatform(product) {
    var text = normalizeText((product && product.name) || "");
    if (text.indexOf("b760") >= 0 || text.indexOf("intel") >= 0) return "intel-lga1700";
    if (text.indexOf("b650") >= 0 || text.indexOf("am5") >= 0) return "amd-am5";
    if (text.indexOf("b550") >= 0 || text.indexOf("am4") >= 0) return "amd-am4";
    return "";
  }

  function getMemoryType(product) {
    var text = normalizeText((product && product.name) || "");
    if (text.indexOf("ddr5") >= 0) return "ddr5";
    if (text.indexOf("ddr4") >= 0) return "ddr4";
    return "";
  }

  function hasAirflowCase(product) {
    var text = normalizeText((product && product.name) || "");
    return text.indexOf("flow") >= 0 || text.indexOf("mesh") >= 0 || text.indexOf("airflow") >= 0 || text.indexOf("lancool") >= 0;
  }

  function extractWattage(value) {
    var match = String(value || "").toLowerCase().match(/(\d{3,4})\s*w/);
    return match ? parseInt(match[1], 10) : 0;
  }

  function normalizeText(value) {
    return String(value || "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .replace(/đ/g, "d")
      .replace(/Đ/g, "d")
      .toLowerCase();
  }

  function getSlotLabel(slot) {
    return {
      CPU: "CPU",
      VGA: "Card đồ họa",
      RAM: "RAM",
      SSD: "Ổ cứng SSD",
      Mainboard: "Mainboard",
      PSU: "Nguồn",
      Case: "Vỏ case",
      Cooling: "Tản nhiệt"
    }[slot] || slot;
  }

  function antiForgeryToken() {
    var token = document.querySelector('input[name="__RequestVerificationToken"]');
    return token ? token.value : "";
  }

  function formatMoney(value) {
    return Number(value || 0).toLocaleString("vi-VN") + " ₫";
  }

  function showNotice(message, type) {
    if (window.showToast) {
      window.showToast(message, type || "success");
    }
  }

  function refreshIcons() {
    if (window.lucide) window.lucide.createIcons();
  }

  function escapeHtml(value) {
    return String(value || "").replace(/[&<>"']/g, function (ch) {
      return ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" })[ch];
    });
  }

  function cssEscape(value) {
    return window.CSS && CSS.escape ? CSS.escape(value) : String(value).replace(/"/g, '\\"');
  }

  function persistBuild() {
    try {
      sessionStorage.setItem("techvoraSmartPcBuild", JSON.stringify(build));
    } catch (error) {
      console.warn("Không thể lưu cấu hình Smart PC:", error);
    }
  }
})();
