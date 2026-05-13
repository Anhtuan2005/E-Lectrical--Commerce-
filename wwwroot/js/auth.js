(function () {
  "use strict";

  document.querySelectorAll("[data-toggle-password]").forEach(function (button) {
    button.addEventListener("click", function () {
      var input = button.parentElement.querySelector("[data-password-input]");
      if (!input) return;
      var isHidden = input.type === "password";
      input.type = isHidden ? "text" : "password";
      button.textContent = isHidden ? "Ẩn" : "Hiện";
    });
  });

  document.querySelectorAll("[data-password-strength]").forEach(function (input) {
    input.addEventListener("input", function () {
      var score = 0;
      if (input.value.length >= 6) score++;
      if (/[A-Z]/.test(input.value)) score++;
      if (/[0-9]/.test(input.value)) score++;
      if (/[^A-Za-z0-9]/.test(input.value)) score++;

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

  document.querySelectorAll("[data-auth-switch]").forEach(function (link) {
    link.addEventListener("click", function (event) {
      if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      event.preventDefault();
      var page = document.querySelector("[data-auth-page]");
      if (page) page.classList.add("auth-leaving");
      window.setTimeout(function () {
        window.location.href = link.href;
      }, 260);
    });
  });
})();
