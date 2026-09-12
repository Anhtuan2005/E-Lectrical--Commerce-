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
    loadDistricts(selectedAddressCode(province), "").then(updateCheckoutShippingFee);
  });

  district.addEventListener("change", function () {
    currentDistrict = district.value;
    currentWard = "";
    loadWards(selectedAddressCode(district), "").then(updateCheckoutShippingFee);
  });

  ward.addEventListener("change", updateCheckoutShippingFee);

  clearDistricts();
  loadProvinces().then(function () {
    return loadDistricts(selectedAddressCode(province), currentDistrict);
  }).then(function () {
    return loadWards(selectedAddressCode(district), currentWard);
  }).then(updateCheckoutShippingFee);
}

function initProfileAddressFill() {
  var button = document.querySelector("[data-profile-address]");
  if (!button) return;
  button.addEventListener("click", function () {
    var parts = button.dataset.profileAddress.split(",").map(function (part) { return part.trim(); }).filter(Boolean);
    var street = document.getElementById("Street");
    if (window.techvoraAddressPicker && typeof window.techvoraAddressPicker.applyParts === "function") {
      window.techvoraAddressPicker.applyParts(parts).then(function () {
        updateCheckoutShippingFee();
        showToast("Đã điền địa chỉ đã lưu.", "success");
      });
    } else {
      if (street) street.value = button.dataset.profileAddress;
      updateCheckoutShippingFee();
      showToast("Đã điền địa chỉ đã lưu.", "success");
    }
  });
}

