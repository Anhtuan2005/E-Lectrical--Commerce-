function initSupportRequestValidation() {
  var form = document.querySelector("[data-support-form]");
  if (!form) return;
  if (form.dataset.supportValidationReady === "true") return;
  form.dataset.supportValidationReady = "true";

  var attempted = !!form.querySelector(".field-validation-error, .input-validation-error, [data-support-item-error]:not([hidden]), [data-valmsg-summary=true] li");
  form.noValidate = true;

  function collectErrors() {
    var errors = [];
    var type = form.querySelector('input[name="Type"]');
    var items = form.querySelector(".support-item-list");
    var name = form.querySelector('[name="ContactName"]');
    var phone = form.querySelector('[name="ContactPhone"]');
    var reason = form.querySelector('[name="Reason"]');
    var description = form.querySelector('[name="Description"]');
    var images = form.querySelector('[name="Images"]');

    function add(field, target, key, message) {
      errors.push({ field: field, target: target || field, key: key, message: message });
    }

    if (type && !form.querySelector('input[name="Type"]:checked')) {
      add(type, form.querySelector(".support-type-grid"), "Type", "Vui lòng chọn loại yêu cầu.");
    }

    if (items) {
      var selected = items.querySelectorAll('input[type="checkbox"][name$=".Selected"]:checked');
      if (!selected.length) {
        add(items.querySelector('input[type="checkbox"]'), items, "Items", "Vui lòng chọn ít nhất một sản phẩm cần hỗ trợ.");
      }
      selected.forEach(function (checkbox) {
        var row = checkbox.closest(".support-item-row");
        var quantity = row && row.querySelector('input[type="number"]');
        if (quantity && (!quantity.value || Number(quantity.value) < 1 || Number(quantity.value) > Number(quantity.max))) {
          add(quantity, quantity, quantity.name, "Số lượng phải từ 1 đến " + quantity.max + ".");
        }
      });
    }

    if (name && !name.value.trim()) add(name, name, "ContactName", "Vui lòng nhập người liên hệ.");
    else if (name && name.value.trim().length > 120) add(name, name, "ContactName", "Người liên hệ tối đa 120 ký tự.");
    if (phone) {
      var phoneValue = phone.value.trim();
      if (!phoneValue) add(phone, phone, "ContactPhone", "Vui lòng nhập số điện thoại.");
      else if (!/^(03|07|08|09)\d{8}$/.test(phoneValue)) {
        add(phone, phone, "ContactPhone", "Số điện thoại phải có 10 chữ số và bắt đầu bằng 03, 07, 08 hoặc 09.");
      }
    }
    if (reason && !reason.value.trim()) add(reason, reason, "Reason", "Vui lòng chọn lý do.");
    if (description) {
      var length = description.value.trim().length;
      if (!length) add(description, description, "Description", "Vui lòng mô tả tình trạng sản phẩm (ít nhất 20 ký tự).");
      else if (length < 20) add(description, description, "Description", "Mô tả chưa đủ 20 ký tự (hiện có " + length + ").");
      else if (length > 1200) add(description, description, "Description", "Mô tả không được dài quá 1200 ký tự.");
    }
    if (images && images.files) {
      if (images.files.length > 5) add(images, images, "Images", "Chỉ được tải tối đa 5 ảnh minh chứng.");
      else if (Array.from(images.files).some(function (file) { return file.size > 5 * 1024 * 1024; })) {
        add(images, images, "Images", "Mỗi ảnh minh chứng phải dưới 5 MB.");
      } else if (Array.from(images.files).some(function (file) { return !/\.(jpe?g|png|webp)$/i.test(file.name) || !/^image\//i.test(file.type); })) {
        add(images, images, "Images", "Chỉ hỗ trợ ảnh JPG, PNG hoặc WEBP.");
      }
    }
    return errors;
  }

  function renderErrors(errors, flash) {
    form.querySelectorAll(".support-invalid").forEach(function (element) {
      element.classList.remove("support-invalid");
    });
    form.querySelectorAll('[aria-invalid="true"]').forEach(function (element) {
      element.removeAttribute("aria-invalid");
    });
    form.querySelectorAll("[data-valmsg-for]").forEach(function (element) {
      element.textContent = "";
      element.classList.remove("field-validation-error");
    });
    var itemError = form.querySelector("[data-support-item-error]");
    if (itemError) {
      itemError.hidden = true;
      itemError.textContent = "";
    }
    var imageError = form.querySelector("[data-support-image-error]");
    if (imageError) {
      imageError.hidden = true;
      imageError.textContent = "";
    }

    errors.forEach(function (error) {
      if (error.target) error.target.classList.add("support-invalid");
      if (error.field && error.field.matches("input, select, textarea")) error.field.setAttribute("aria-invalid", "true");
      var inline = form.querySelector('[data-valmsg-for="' + error.key + '"]');
      if (inline) {
        inline.textContent = error.message;
        inline.classList.add("field-validation-error");
      } else if (error.key === "Items" && itemError) {
        itemError.textContent = error.message;
        itemError.hidden = false;
      } else if (error.key === "Images" && imageError) {
        imageError.textContent = error.message;
        imageError.hidden = false;
      }
    });

    if (flash && errors.length) {
      var first = errors[0];
      if (first.field && first.field.focus) first.field.focus({ preventScroll: true });
      if (first.target) {
        first.target.scrollIntoView({
          behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth",
          block: "center"
        });
      }
    }
  }

  form.addEventListener("submit", function (event) {
    attempted = true;
    var errors = collectErrors();
    if (!errors.length) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    renderErrors(errors, true);
  }, true);

  function refreshErrors() {
    if (attempted) renderErrors(collectErrors(), false);
  }
  form.addEventListener("input", refreshErrors);
  form.addEventListener("change", refreshErrors);
}

if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", initSupportRequestValidation);
} else {
  initSupportRequestValidation();
}
