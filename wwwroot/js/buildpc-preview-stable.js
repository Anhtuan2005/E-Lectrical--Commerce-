(function () {
  const partList = document.getElementById("pc3dPartList");
  const totalEl = document.getElementById("pc3dTotal");
  const emptyEl = document.getElementById("pc3dEmpty");
  const preview = document.getElementById("pc3dDomPreview");
  const previewCase = document.getElementById("pc3dDomCase");
  const caption = document.getElementById("pc3dDomCaption");

  const slotLabels = {
    CPU: "CPU",
    VGA: "Card \u0111\u1ed3 h\u1ecda",
    RAM: "RAM",
    SSD: "SSD",
    Mainboard: "Mainboard",
    PSU: "Ngu\u1ed3n",
    Case: "V\u1ecf case",
    Cooling: "T\u1ea3n nhi\u1ec7t"
  };

  const slotOrder = ["Case", "Mainboard", "CPU", "RAM", "VGA", "SSD", "PSU", "Cooling"];

  let rotateX = 6;
  let rotateY = -18;
  let scale = 1;
  let dragging = false;
  let startX = 0;
  let startY = 0;

  const buildState = readSavedBuild();
  renderBuildSummary(buildState);
  renderDomPreview(buildState);
  enablePreviewControls();
  window.buildpc3d = {
    openPage: (state) => {
      renderBuildSummary(state || {});
      renderDomPreview(state || {});
    },
    update: (state) => {
      renderBuildSummary(state || {});
      renderDomPreview(state || {});
    },
    close: () => {}
  };

  function readSavedBuild() {
    try {
      return JSON.parse(sessionStorage.getItem("techvoraBuildPcPreview") || "{}") || {};
    } catch {
      return {};
    }
  }

  function renderBuildSummary(buildState) {
    const entries = Object.entries(buildState || {}).sort(([slotA], [slotB]) => {
      const indexA = slotOrder.indexOf(slotA);
      const indexB = slotOrder.indexOf(slotB);
      return (indexA === -1 ? 99 : indexA) - (indexB === -1 ? 99 : indexB);
    });
    if (partList) {
      partList.innerHTML = entries.length
        ? entries.map(([slot, product]) => `<li><span>${escapeHtml(slotLabels[slot] || slot)}</span><strong>${escapeHtml(product.name)}</strong><em>${escapeHtml(product.price)}</em></li>`).join("")
        : "<li>Ch\u01b0a c\u00f3 linh ki\u1ec7n n\u00e0o.</li>";
    }
    const total = entries.reduce((sum, [, product]) => sum + Number(product.priceRaw || 0), 0);
    if (totalEl) totalEl.textContent = total.toLocaleString("vi-VN") + " \u0111";
    if (emptyEl) emptyEl.hidden = entries.length > 0;
  }

  function renderDomPreview(buildState) {
    if (!preview || !previewCase) return;
    const selectedSlots = new Set(Object.keys(buildState || {}));
    preview.querySelectorAll("[data-pc3d-slot]").forEach((part) => {
      const slot = part.getAttribute("data-pc3d-slot");
      const product = buildState?.[slot];
      part.classList.toggle("is-active", Boolean(product));
      part.classList.toggle("is-empty", !product);
      part.title = product ? `${slotLabels[slot] || slot}: ${product.name}` : `${slotLabels[slot] || slot} ch\u01b0a ch\u1ecdn`;
      const label = part.querySelector("span");
      if (label) label.textContent = slot === "VGA" ? "VGA" : slot;
    });

    const missingCore = ["CPU", "RAM", "VGA", "SSD", "PSU"].filter((slot) => !selectedSlots.has(slot));
    preview.classList.toggle("has-build", selectedSlots.size > 0);
    if (caption) {
      caption.textContent = selectedSlots.size
        ? `\u0110ang hi\u1ec3n th\u1ecb ${selectedSlots.size} linh ki\u1ec7n. K\u00e9o \u0111\u1ec3 xoay, scroll \u0111\u1ec3 zoom.`
        : "Ch\u1ecdn linh ki\u1ec7n \u1edf trang Build PC \u0111\u1ec3 d\u1ef1ng preview.";
      caption.title = missingCore.length ? `Ch\u01b0a ch\u1ecdn: ${missingCore.join(", ")}` : "C\u1ea5u h\u00ecnh \u0111\u00e3 c\u00f3 c\u00e1c linh ki\u1ec7n ch\u00ednh.";
    }
  }

  function enablePreviewControls() {
    if (!preview || !previewCase) return;
    applyTransform();

    preview.addEventListener("pointerdown", (event) => {
      dragging = true;
      startX = event.clientX;
      startY = event.clientY;
      preview.setPointerCapture?.(event.pointerId);
      preview.classList.add("is-dragging");
    });

    preview.addEventListener("pointermove", (event) => {
      if (!dragging) return;
      const dx = event.clientX - startX;
      const dy = event.clientY - startY;
      startX = event.clientX;
      startY = event.clientY;
      rotateY = normalizeDegrees(rotateY + dx * 0.28);
      rotateX = clamp(rotateX - dy * 0.18, -58, 58);
      applyTransform();
    });

    const stopDragging = (event) => {
      dragging = false;
      preview.releasePointerCapture?.(event.pointerId);
      preview.classList.remove("is-dragging");
    };

    preview.addEventListener("pointerup", stopDragging);
    preview.addEventListener("pointercancel", stopDragging);
    preview.addEventListener("wheel", (event) => {
      event.preventDefault();
      scale = clamp(scale + (event.deltaY > 0 ? -0.05 : 0.05), 0.76, 1.32);
      applyTransform();
    }, { passive: false });

    preview.addEventListener("dblclick", () => {
      rotateX = 6;
      rotateY = -18;
      scale = 1;
      applyTransform();
    });
  }

  function applyTransform() {
    previewCase.style.setProperty("--rx", `${rotateX}deg`);
    previewCase.style.setProperty("--ry", `${rotateY}deg`);
    previewCase.style.setProperty("--preview-scale", String(scale));
  }

  function escapeHtml(value) {
    return String(value || "").replace(/[&<>"']/g, (ch) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" })[ch]);
  }

  function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
  }

  function normalizeDegrees(value) {
    return ((value % 360) + 360) % 360;
  }
})();
