function initAiChatbot() {
  var root = document.querySelector("[data-ai-chatbot]");
  if (!root) return;

  var toggle = root.querySelector("[data-ai-chat-toggle]");
  var panel = root.querySelector("[data-ai-chat-panel]");
  var close = root.querySelector("[data-ai-chat-close]");
  var form = root.querySelector("[data-ai-chat-form]");
  var input = root.querySelector("[data-ai-chat-input]");
  var send = root.querySelector("[data-ai-chat-send]");
  var messages = root.querySelector("[data-ai-chat-messages]");
  var transcriptKey = "techvoraAiChatTranscript";
  var history = [];
  var transcript = loadTranscript();
  var isSending = false;

  function setOpen(open) {
    if (!panel || !toggle) return;
    panel.hidden = !open;
    root.classList.toggle("is-open", open);
    toggle.setAttribute("aria-expanded", String(open));
    sessionStorage.setItem("techvoraAiChatOpen", open ? "true" : "false");
    if (open && input) {
      setTimeout(function () {
        input.focus();
        scrollMessages();
      }, 40);
    }
    refreshIcons();
  }

  function scrollMessages() {
    if (messages) messages.scrollTop = messages.scrollHeight;
  }

  function autoSizeInput() {
    if (!input) return;
    input.style.height = "auto";
    input.style.height = Math.min(input.scrollHeight, 112) + "px";
  }

  function setBusy(busy) {
    isSending = busy;
    if (send) send.disabled = busy;
    if (input) input.disabled = busy;
    root.classList.toggle("is-sending", busy);
  }

  function normalizeProducts(products) {
    return (products || []).filter(Boolean).slice(0, 4).map(function (product) {
      return {
        id: product.id,
        name: product.name || "",
        category: product.category || "",
        price: product.price || "",
        url: product.url || (product.id ? "/Product/Detail/" + product.id : "")
      };
    });
  }

  function loadTranscript() {
    try {
      var raw = sessionStorage.getItem(transcriptKey);
      if (!raw) return [];
      var saved = JSON.parse(raw);
      if (!Array.isArray(saved)) return [];
      return saved
        .filter(function (item) {
          return item && (item.role === "user" || item.role === "model") && String(item.text || "").trim();
        })
        .slice(-24)
        .map(function (item) {
          return {
            role: item.role,
            text: String(item.text || "").slice(0, 4000),
            products: normalizeProducts(item.products),
            keepInHistory: item.keepInHistory !== false
          };
        });
    } catch (_) {
      sessionStorage.removeItem(transcriptKey);
      return [];
    }
  }

  function syncHistory() {
    history = transcript
      .filter(function (item) { return item.keepInHistory !== false; })
      .map(function (item) { return { role: item.role, text: item.text }; })
      .slice(-8);
  }

  function saveTranscript() {
    transcript = transcript.slice(-24);
    try {
      sessionStorage.setItem(transcriptKey, JSON.stringify(transcript));
    } catch (_) {
      transcript = transcript.slice(-12);
    }
    syncHistory();
  }

  function restoreTranscript() {
    transcript.forEach(function (item) {
      appendMessage(item.role, item.text, item.products, { persist: false });
    });
    syncHistory();
  }

  function appendMessage(role, text, products, options) {
    if (!messages) return null;
    options = options || {};
    var item = document.createElement("div");
    item.className = "ai-message " + (role === "user" ? "ai-message-user" : "ai-message-bot");
    item.innerHTML = "<p>" + formatAiMessage(text, products) + "</p>";
    if (role !== "user" && products && products.length) {
      item.appendChild(renderProductLinks(products));
    }
    messages.appendChild(item);
    if (options.persist !== false) {
      transcript.push({
        role: role === "user" ? "user" : "model",
        text: String(text || "").trim(),
        products: role === "user" ? [] : normalizeProducts(products),
        keepInHistory: options.history !== false
      });
      saveTranscript();
    }
    scrollMessages();
    return item;
  }

  function appendTyping() {
    if (!messages) return null;
    var item = document.createElement("div");
    item.className = "ai-message ai-message-bot ai-message-typing";
    item.innerHTML = '<i data-lucide="sparkles" aria-hidden="true"></i><span>Đang tư vấn...</span>';
    messages.appendChild(item);
    scrollMessages();
    refreshIcons();
    return item;
  }

  function submitPrompt(text) {
    var prompt = String(text || "").trim();
    if (!prompt || isSending) return;

    appendMessage("user", prompt);
    if (input) {
      input.value = "";
      autoSizeInput();
    }

    var typing = appendTyping();
    setBusy(true);

    fetch("/AiChat/Ask", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "RequestVerificationToken": antiForgeryToken()
      },
      body: JSON.stringify({
        message: prompt,
        history: history.slice(0, -1)
      })
    })
      .then(function (response) {
        if (response.status === 429) throw new Error("rate-limit");
        return response.json().then(function (data) {
          if (!response.ok || !data.success) {
            throw new Error(data.message || "request-failed");
          }
          return data;
        });
      })
      .then(function (data) {
        if (typing) typing.remove();
        var reply = data.reply || "Mình chưa có câu trả lời phù hợp. Bạn cho mình thêm nhu cầu nhé.";
        appendMessage("model", reply, data.products || []);
      })
      .catch(function (error) {
        if (typing) typing.remove();
        var message = error.message === "rate-limit"
          ? "Bạn gửi hơi nhanh. Mình đợi vài giây rồi hỏi tiếp nhé."
          : "Mình chưa thể trả lời lúc này. Bạn thử lại sau một chút nhé.";
        appendMessage("model", message, [], { history: false });
      })
      .finally(function () {
        setBusy(false);
        if (input) input.focus();
      });
  }

  function renderProductLinks(products) {
    var list = document.createElement("div");
    list.className = "ai-product-links";
    products.slice(0, 4).forEach(function (product) {
      var link = document.createElement("a");
      link.className = "ai-product-link";
      link.href = product.url || ("/Product/Detail/" + product.id);
      link.innerHTML =
        '<span class="ai-product-name">' + escapeHtml(product.name || "Xem sản phẩm") + "</span>" +
        '<span class="ai-product-meta">' + escapeHtml(product.category || "Sản phẩm") + " · " + escapeHtml(product.price || "") + "</span>";
      list.appendChild(link);
    });
    return list;
  }

  function formatAiMessage(text, products) {
    var productIds = {};
    var placeholders = [];
    (products || []).forEach(function (product) {
      productIds[String(product.id)] = product.url || ("/Product/Detail/" + product.id);
    });

    var html = escapeHtml(text)
      .replace(/\[([^\]]+)\]\((\/Product\/Detail\/(\d+))\)/g, function (_, label, url) {
        var key = "@@AI_LINK_" + placeholders.length + "@@";
        placeholders.push('<a href="' + url + '">' + label + "</a>");
        return key;
      })
      .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
      .replace(/(\/Product\/Detail\/(\d+))/g, '<a href="$1">$1</a>')
      .replace(/#(\d+)/g, function (match, id) {
        var url = productIds[String(id)] || ("/Product/Detail/" + id);
        return '<a href="' + url + '">' + match + "</a>";
      })
      .replace(/\n/g, "<br>");

    placeholders.forEach(function (link, index) {
      html = html.replace("@@AI_LINK_" + index + "@@", link);
    });

    return html;
  }

  if (toggle) {
    toggle.addEventListener("click", function () {
      setOpen(panel ? panel.hidden : true);
    });
  }

  if (close) {
    close.addEventListener("click", function () {
      setOpen(false);
    });
  }

  if (form) {
    form.addEventListener("submit", function (event) {
      event.preventDefault();
      submitPrompt(input ? input.value : "");
    });
  }

  if (input) {
    input.addEventListener("input", autoSizeInput);
    input.addEventListener("keydown", function (event) {
      if (event.key === "Enter" && !event.shiftKey) {
        event.preventDefault();
        submitPrompt(input.value);
      }
    });
  }

  root.querySelectorAll("[data-ai-prompt]").forEach(function (button) {
    button.addEventListener("click", function () {
      setOpen(true);
      submitPrompt(button.dataset.aiPrompt);
    });
  });

  restoreTranscript();
  setOpen(sessionStorage.getItem("techvoraAiChatOpen") === "true");
  autoSizeInput();
}

var drawerTrigger = null;

