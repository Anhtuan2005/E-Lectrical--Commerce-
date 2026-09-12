function initReviewForm() {
  var form = document.getElementById("reviewForm");
  if (!form) return;
  form.addEventListener("submit", function (event) {
    event.preventDefault();
    var body = new FormData(form);
    fetch("/Review/Submit", {
      method: "POST",
      headers: {
        "RequestVerificationToken": antiForgeryToken()
      },
      body: body
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
        applyReviewFilter();
        showToast(data.message, "success");
      });
  });
}

function initReviewImagePreview() {
  var input = document.querySelector("[data-review-images]");
  var preview = document.querySelector("[data-review-preview]");
  if (!input || !preview) return;

  input.addEventListener("change", function () {
    preview.innerHTML = "";
    var files = Array.prototype.slice.call(input.files || []);
    if (files.length > 4) {
      showToast("Bạn chỉ có thể tải tối đa 4 ảnh cho mỗi đánh giá.", "error");
      input.value = "";
      return;
    }

    files.forEach(function (file) {
      var item = document.createElement("span");
      var image = document.createElement("img");
      var url = URL.createObjectURL(file);
      image.src = url;
      image.alt = file.name;
      image.decoding = "async";
      image.onload = function () { URL.revokeObjectURL(url); };
      item.appendChild(image);
      preview.appendChild(item);
    });
  });
}

function appendReview(review) {
  var list = document.getElementById("reviewList");
  if (!list) return;
  var empty = document.querySelector("[data-review-empty]");
  if (empty) empty.remove();
  var item = document.createElement("article");
  item.className = "review-item new";
  item.dataset.reviewItem = "";
  item.dataset.rating = String(review.rating);
  var initial = review.user ? review.user.substring(0, 1).toUpperCase() : "K";
  var stars = '<span class="star-meter" aria-hidden="true">';
  for (var i = 1; i <= 5; i++) {
    stars += '<span class="star-meter-star" style="--fill:' + (i <= review.rating ? 100 : 0) + '%"><span>★</span></span>';
  }
  stars += "</span>";
  item.innerHTML =
    '<div class="review-avatar">' + escapeHtml(initial) + "</div>" +
    '<div class="review-copy">' +
    '<div class="review-meta"><strong>' + escapeHtml(review.user) + "</strong><span>" + escapeHtml(review.date) + "</span></div>" +
    stars +
    "<p>" + escapeHtml(review.comment) + "</p>" +
    renderReviewImages(review.images) +
    "</div>";
  list.prepend(item);
  incrementReviewFilterCount("all");
  incrementReviewFilterCount(String(review.rating));
}

function renderReviewImages(images) {
  if (!images || !images.length) return "";
  var html = '<div class="review-photo-grid">';
  images.forEach(function (url) {
    html += '<a class="review-photo-thumb" href="' + escapeHtml(url) + '" target="_blank" rel="noopener">' +
      '<img src="' + escapeHtml(url) + '" alt="Ảnh đánh giá" loading="lazy" decoding="async" />' +
      "</a>";
  });
  return html + "</div>";
}

function incrementReviewFilterCount(filter) {
  var count = document.querySelector('[data-review-filter="' + filter + '"] span');
  if (!count) return;
  count.textContent = String(parseLocalizedNumber(count.textContent, 0) + 1);
}

