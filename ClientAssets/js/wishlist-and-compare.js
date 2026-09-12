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

function initProductCompare() {
  var storageKey = "techvora-compare-products-v2";
  var tray = document.querySelector("[data-compare-tray]");
  var count = tray ? tray.querySelector("[data-compare-count]") : null;
  var hint = tray ? tray.querySelector("[data-compare-hint]") : null;
  var itemsRoot = tray ? tray.querySelector("[data-compare-items]") : null;
  var clearButton = tray ? tray.querySelector("[data-compare-clear]") : null;
  var openLink = tray ? tray.querySelector("[data-compare-open]") : null;

  function normalize(items) {
    if (!Array.isArray(items)) return [];
    var seen = {};
    return items.filter(function (item) {
      var id = Number(item && item.id);
      if (!id || seen[id]) return false;
      seen[id] = true;
      item.id = id;
      item.name = String(item.name || "Sản phẩm");
      item.image = String(item.image || "/images/placeholder.svg");
      item.categoryId = Number(item.categoryId) || 0;
      item.categoryName = String(item.categoryName || "danh mục này");
      return true;
    }).slice(0, 4);
  }

  function readSelection() {
    try {
      return normalize(JSON.parse(window.localStorage.getItem(storageKey) || "[]"));
    } catch (error) {
      return [];
    }
  }

  var selected = readSelection();
  if (selected.length === 0) {
    selected = normalize([].slice.call(document.querySelectorAll("[data-compare-seed]")).map(function (element) {
      return {
        id: element.dataset.compareSeed,
        name: element.dataset.compareName,
        image: element.dataset.compareImage,
        categoryId: element.dataset.compareCategoryId,
        categoryName: element.dataset.compareCategoryName
      };
    }));
  }

  function writeSelection() {
    try {
      window.localStorage.setItem(storageKey, JSON.stringify(selected));
    } catch (error) {
      // The compare page still works through its query string if storage is unavailable.
    }
  }

  function buildCompareUrl() {
    var params = new URLSearchParams();
    selected.forEach(function (item) { params.append("ids", String(item.id)); });
    return "/Product/Compare?" + params.toString();
  }

  function renderItems() {
    if (!itemsRoot) return;
    itemsRoot.replaceChildren();
    for (var index = 0; index < 4; index++) {
      var item = selected[index];
      if (!item) {
        var empty = document.createElement("span");
        empty.className = "compare-tray-slot is-empty";
        empty.setAttribute("aria-hidden", "true");
        itemsRoot.appendChild(empty);
        continue;
      }

      var slot = document.createElement("span");
      slot.className = "compare-tray-slot";
      slot.title = item.name;

      var image = document.createElement("img");
      image.src = item.image;
      image.alt = "";
      image.loading = "lazy";
      image.decoding = "async";
      slot.appendChild(image);

      var remove = document.createElement("button");
      remove.type = "button";
      remove.dataset.compareRemove = String(item.id);
      remove.setAttribute("aria-label", "Bỏ " + item.name + " khỏi so sánh");
      remove.innerHTML = '<i data-lucide="x" aria-hidden="true"></i>';
      slot.appendChild(remove);
      itemsRoot.appendChild(slot);
    }
  }

  function render() {
    document.querySelectorAll("[data-compare-product]").forEach(function (button) {
      var active = selected.some(function (item) { return item.id === Number(button.dataset.compareProduct); });
      button.classList.toggle("active", active);
      button.setAttribute("aria-pressed", String(active));
      button.title = active ? "Bỏ khỏi so sánh" : "Thêm vào so sánh";
    });

    if (!tray) return;
    tray.hidden = selected.length === 0;
    document.body.classList.toggle("compare-tray-visible", selected.length > 0);
    if (count) count.textContent = String(selected.length);
    if (hint) {
      var categoryName = selected[0] ? selected[0].categoryName : "";
      hint.textContent = selected.length < 2
        ? "Chọn thêm trong " + categoryName
        : "Cùng danh mục " + categoryName;
    }
    if (openLink) {
      var disabled = selected.length < 2;
      openLink.href = buildCompareUrl();
      openLink.classList.toggle("disabled", disabled);
      openLink.setAttribute("aria-disabled", String(disabled));
      openLink.tabIndex = disabled ? -1 : 0;
    }
    renderItems();
    refreshIcons();
  }

  document.querySelectorAll("[data-compare-product]").forEach(function (button) {
    button.addEventListener("click", function (event) {
      event.preventDefault();
      event.stopPropagation();
      var id = Number(button.dataset.compareProduct);
      var existingIndex = selected.findIndex(function (item) { return item.id === id; });
      if (existingIndex >= 0) {
        selected.splice(existingIndex, 1);
      } else if (selected.length >= 4) {
        showToast("Bạn chỉ có thể so sánh tối đa 4 sản phẩm.", "error");
        return;
      } else {
        var categoryId = Number(button.dataset.compareCategoryId);
        if (selected.length > 0 && selected[0].categoryId !== categoryId) {
          showToast("Chỉ có thể so sánh sản phẩm cùng danh mục " + selected[0].categoryName + ".", "error");
          return;
        }
        selected.push({
          id: id,
          name: button.dataset.compareName || "Sản phẩm",
          image: button.dataset.compareImage || "/images/placeholder.svg",
          categoryId: categoryId,
          categoryName: button.dataset.compareCategoryName || "danh mục này"
        });
      }
      writeSelection();
      render();
    });
  });

  if (itemsRoot) {
    itemsRoot.addEventListener("click", function (event) {
      var remove = event.target.closest("[data-compare-remove]");
      if (!remove) return;
      selected = selected.filter(function (item) { return item.id !== Number(remove.dataset.compareRemove); });
      writeSelection();
      render();
    });
  }

  document.querySelectorAll("[data-compare-remove-id]").forEach(function (link) {
    link.addEventListener("click", function () {
      selected = selected.filter(function (item) { return item.id !== Number(link.dataset.compareRemoveId); });
      writeSelection();
    });
  });

  if (clearButton) {
    clearButton.addEventListener("click", function () {
      selected = [];
      writeSelection();
      render();
    });
  }

  document.querySelectorAll("[data-compare-clear-link]").forEach(function (link) {
    link.addEventListener("click", function () {
      selected = [];
      writeSelection();
    });
  });

  if (openLink) {
    openLink.addEventListener("click", function (event) {
      if (selected.length >= 2) return;
      event.preventDefault();
      showToast("Chọn ít nhất 2 sản phẩm để so sánh.", "error");
    });
  }

  writeSelection();
  render();
}

