import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";

const viewport = document.getElementById("pc3dViewport");
const fallback = document.getElementById("pc3dFallback");
const tooltip = document.getElementById("pc3dTooltip");
const partList = document.getElementById("pc3dPartList");
const totalEl = document.getElementById("pc3dTotal");
const emptyEl = document.getElementById("pc3dEmpty");

const slotLabels = {
  CPU: "CPU",
  VGA: "Card đồ họa",
  RAM: "RAM",
  SSD: "SSD",
  Mainboard: "Mainboard",
  PSU: "Nguồn",
  Case: "Vỏ case",
  Cooling: "Tản nhiệt"
};

const componentSpecs = {
  Mainboard: { label: "Mainboard", geometry: [20, 0.8, 24], color: 0x1a472a, position: [0, 0, -3] },
  CPU: { label: "CPU", geometry: [8, 2, 8], color: 0xe5a100, position: [0, 2, -5] },
  VGA: { label: "VGA", geometry: [25, 4, 12], color: 0x1a1a3e, position: [0, -8, 3] },
  RAM: { label: "RAM", geometry: [1.5, 12, 4], color: 0x2e1a5e, positions: [[-6, 4.5, -5], [-3.7, 4.5, -3.7]] },
  SSD: { label: "SSD", geometry: [6, 0.5, 10], color: 0x1e3a5f, position: [6, 1.1, 0] },
  PSU: { label: "PSU", geometry: [15, 8.5, 14], color: 0x2a2a2a, position: [-2, -17, 10] },
  Cooling: { label: "Cooling", cylinder: [5, 5, 6, 16], color: 0x444444, position: [0, 6.2, -5] }
};

let renderer;
let scene;
let camera;
let controls;
let caseGroup;
let componentGroup;
let raycaster;
let pointer;
let resizeObserver;
let animationId = 0;
let lastInteraction = performance.now();
let currentBuild = {};
let componentMeshes = new Map();
let animations = [];
let labelEls = [];

/**
 * Render a full-page 3D preview from the Build PC state saved by the builder page.
 * Falls back to a CSS 3D schematic when WebGL is disabled by the browser/webview.
 * @param {Record<string, {id:number,name:string,price:string,priceRaw:number}>} buildState
 */
function openPage(buildState) {
  currentBuild = buildState || {};
  renderBuildSummary(currentBuild);

  if (!Object.keys(currentBuild).length) {
    if (emptyEl) emptyEl.hidden = false;
    renderCssPreview(currentBuild, "Chưa có linh kiện nào để dựng preview.");
    return;
  }

  if (!supportsWebGL()) {
    renderCssPreview(currentBuild, "Trình duyệt đang tắt WebGL, Techvora chuyển sang chế độ preview tương thích.");
    return;
  }

  if (!initScene()) {
    renderCssPreview(currentBuild, "Không thể khởi tạo WebGL renderer, Techvora chuyển sang chế độ preview tương thích.");
    return;
  }

  update(currentBuild);
  resize();
  startLoop();
}

/**
 * Sync scene meshes with the supplied Build PC state.
 * @param {Record<string, {id:number,name:string,price:string,priceRaw:number}>} buildState
 */
function update(buildState) {
  currentBuild = buildState || {};
  if (!scene || !componentGroup) {
    if (fallback && !fallback.hidden) renderCssPreview(currentBuild);
    return;
  }

  Object.keys(componentSpecs).forEach((slot) => {
    if (currentBuild[slot] && !componentMeshes.has(slot)) addComponent(slot, currentBuild[slot]);
    if (currentBuild[slot] && componentMeshes.has(slot)) componentMeshes.get(slot).userData.product = currentBuild[slot];
    if (!currentBuild[slot] && componentMeshes.has(slot)) removeComponent(slot);
  });

  renderLabels();
}

/**
 * Dispose the renderer, controls, meshes and DOM overlays used by the preview page.
 */
function close() {
  cancelAnimationFrame(animationId);
  animationId = 0;
  hideTooltip();
  clearLabels();
  if (resizeObserver) resizeObserver.disconnect();
  resizeObserver = null;
  disposeScene();
}

function initScene() {
  if (!viewport) return false;
  if (renderer) return true;
  viewport.innerHTML = "";
  if (fallback) fallback.hidden = true;

  scene = new THREE.Scene();
  scene.background = new THREE.Color(0x060a10);

  camera = new THREE.PerspectiveCamera(45, 1, 0.1, 500);
  camera.position.set(0, 20, 70);

  try {
    renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: "high-performance" });
  } catch {
    disposeScene();
    return false;
  }

  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  viewport.appendChild(renderer.domElement);

  controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.minDistance = 30;
  controls.maxDistance = 150;
  controls.addEventListener("start", markInteraction);
  controls.addEventListener("change", markInteraction);

  raycaster = new THREE.Raycaster();
  pointer = new THREE.Vector2();

  /* r155+ uses physically-correct lights by default – intensities need
     to be much higher than the old legacy model expected. */
  scene.add(new THREE.AmbientLight(0xc8d8f0, 1.8));
  const hemi = new THREE.HemisphereLight(0xc8e0ff, 0x1a2030, 1.5);
  scene.add(hemi);
  const dir = new THREE.DirectionalLight(0xffffff, 4);
  dir.position.set(30, 60, 50);
  scene.add(dir);
  const dir2 = new THREE.DirectionalLight(0x88aaff, 2);
  dir2.position.set(-20, 30, -40);
  scene.add(dir2);
  const spot = new THREE.SpotLight(0xffffff, 120, 300, Math.PI / 5, 0.4, 2);
  spot.position.set(0, 80, 40);
  scene.add(spot);
  const cyan = new THREE.PointLight(0x00c8ff, 60, 150, 2);
  cyan.position.set(0, 8, 4);
  scene.add(cyan);

  caseGroup = new THREE.Group();
  componentGroup = new THREE.Group();
  scene.add(caseGroup);
  scene.add(componentGroup);
  createCase();

  renderer.domElement.addEventListener("pointermove", onPointerMove);
  renderer.domElement.addEventListener("pointerleave", hideTooltip);

  resizeObserver = new ResizeObserver(resize);
  resizeObserver.observe(viewport);
  return true;
}

function createCase() {
  const body = new THREE.BoxGeometry(20, 45, 50);
  const edges = new THREE.EdgesGeometry(body);
  const line = new THREE.LineSegments(
    edges,
    new THREE.LineBasicMaterial({ color: 0x00c8ff, transparent: true, opacity: 0.3 })
  );
  caseGroup.add(line);

  const floor = new THREE.Mesh(
    new THREE.BoxGeometry(18.5, 0.4, 48),
    new THREE.MeshStandardMaterial({ color: 0x0d1522, metalness: 0.25, roughness: 0.45 })
  );
  floor.position.set(0, -22.7, 0);
  caseGroup.add(floor);
}

function addComponent(slot, product) {
  const spec = componentSpecs[slot];
  const holder = new THREE.Group();
  holder.userData = { slot, product };

  if (slot === "RAM") {
    spec.positions.forEach((position) => {
      const mesh = makeBox(spec.geometry, spec.color, slot, product);
      mesh.position.set(position[0], position[1], position[2]);
      holder.add(mesh);
    });
  } else {
    const mesh = spec.cylinder ? makeCylinder(spec.cylinder, spec.color, slot, product) : makeBox(spec.geometry, spec.color, slot, product);
    holder.add(mesh);
  }

  const position = spec.position || [0, 0, 0];
  holder.position.set(position[0], position[1] + 30, position[2]);
  holder.userData.target = new THREE.Vector3(position[0], position[1], position[2]);
  componentGroup.add(holder);
  componentMeshes.set(slot, holder);
  animations.push({ group: holder, from: holder.position.clone(), to: holder.userData.target.clone(), start: performance.now(), duration: 600, remove: false });
}

function removeComponent(slot) {
  const group = componentMeshes.get(slot);
  if (!group) return;
  componentMeshes.delete(slot);
  animations.push({
    group,
    from: group.position.clone(),
    to: group.position.clone().add(new THREE.Vector3(0, 30, 0)),
    start: performance.now(),
    duration: 420,
    remove: true
  });
}

function makeBox(size, color, slot, product) {
  const mesh = new THREE.Mesh(
    new THREE.BoxGeometry(size[0], size[1], size[2]),
    new THREE.MeshStandardMaterial({ color, metalness: 0.25, roughness: 0.38 })
  );
  mesh.userData = { slot, product };
  return mesh;
}

function makeCylinder(size, color, slot, product) {
  const mesh = new THREE.Mesh(
    new THREE.CylinderGeometry(size[0], size[1], size[2], size[3]),
    new THREE.MeshStandardMaterial({ color, metalness: 0.18, roughness: 0.42 })
  );
  mesh.rotation.x = Math.PI / 2;
  mesh.userData = { slot, product };
  return mesh;
}

function startLoop() {
  if (animationId) return;
  const tick = () => {
    animationId = requestAnimationFrame(tick);
    animateComponents(performance.now());
    if (controls) {
      controls.autoRotate = performance.now() - lastInteraction > 3000;
      controls.autoRotateSpeed = 0.55;
      controls.update();
    }
    renderLabels();
    if (renderer && scene && camera) renderer.render(scene, camera);
  };
  tick();
}

function animateComponents(now) {
  animations = animations.filter((item) => {
    const progress = Math.min(1, (now - item.start) / item.duration);
    const eased = easeOutBack(progress);
    item.group.position.set(
      lerp(item.from.x, item.to.x, eased),
      lerp(item.from.y, item.to.y, eased),
      lerp(item.from.z, item.to.z, eased)
    );
    if (progress >= 1 && item.remove) {
      componentGroup.remove(item.group);
      disposeObject(item.group);
      return false;
    }
    return progress < 1;
  });
}

function onPointerMove(event) {
  if (!renderer || !camera || !raycaster || !tooltip) return;
  const rect = renderer.domElement.getBoundingClientRect();
  pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
  pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
  raycaster.setFromCamera(pointer, camera);
  const targets = [];
  componentMeshes.forEach((group) => group.traverse((child) => { if (child.isMesh) targets.push(child); }));
  const hit = raycaster.intersectObjects(targets, false)[0];
  if (!hit) {
    hideTooltip();
    return;
  }
  const data = hit.object.userData;
  tooltip.innerHTML = `<strong>${escapeHtml(data.slot)}</strong>${escapeHtml(data.product?.name || "")}<br>${escapeHtml(data.product?.price || "")}`;
  tooltip.hidden = false;
  tooltip.style.left = `${event.clientX - rect.left + 16}px`;
  tooltip.style.top = `${event.clientY - rect.top + 16}px`;
}

function renderLabels() {
  if (!renderer || !camera || !viewport) return;
  clearLabels();
  componentMeshes.forEach((group, slot) => {
    const vector = group.position.clone().project(camera);
    if (vector.z < -1 || vector.z > 1) return;
    const label = document.createElement("span");
    label.className = "pc3d-label";
    label.textContent = slot;
    label.style.left = `${(vector.x * 0.5 + 0.5) * viewport.clientWidth}px`;
    label.style.top = `${(-vector.y * 0.5 + 0.5) * viewport.clientHeight}px`;
    viewport.appendChild(label);
    labelEls.push(label);
  });
}

function renderCssPreview(buildState, message) {
  if (!fallback) return;
  const entries = Object.entries(buildState || {});
  fallback.hidden = false;
  fallback.innerHTML = `
    <div class="pc3d-css-preview" role="img" aria-label="Preview cấu hình PC dạng tương thích">
      <div class="pc3d-css-note">${escapeHtml(message || "Chế độ preview tương thích đang bật.")}</div>
      <div class="pc3d-css-case">
        <div class="pc3d-css-shell"></div>
        ${entries.map(([slot, product]) => `<div class="pc3d-css-part pc3d-slot-${escapeHtml(slot.toLowerCase())}" title="${escapeHtml(product.name)}"><span>${escapeHtml(slot)}</span></div>`).join("")}
      </div>
    </div>`;
}

function renderBuildSummary(buildState) {
  const entries = Object.entries(buildState || {});
  if (partList) {
    partList.innerHTML = entries.length
      ? entries.map(([slot, product]) => `<li><span>${escapeHtml(slotLabels[slot] || slot)}</span><strong>${escapeHtml(product.name)}</strong><em>${escapeHtml(product.price)}</em></li>`).join("")
      : "<li>Chưa có linh kiện nào.</li>";
  }
  const total = entries.reduce((sum, [, product]) => sum + Number(product.priceRaw || 0), 0);
  if (totalEl) totalEl.textContent = total.toLocaleString("vi-VN") + " ₫";
}

function readSavedBuild() {
  try {
    return JSON.parse(sessionStorage.getItem("techvoraBuildPcPreview") || "{}") || {};
  } catch {
    return {};
  }
}

function clearLabels() {
  labelEls.forEach((label) => label.remove());
  labelEls = [];
}

function resize() {
  if (!renderer || !camera || !viewport) return;
  const width = Math.max(1, viewport.clientWidth);
  const height = Math.max(1, viewport.clientHeight);
  renderer.setSize(width, height, false);
  camera.aspect = width / height;
  camera.updateProjectionMatrix();
}

function supportsWebGL() {
  try {
    const canvas = document.createElement("canvas");
    return !!(canvas.getContext("webgl2") || canvas.getContext("webgl") || canvas.getContext("experimental-webgl"));
  } catch {
    return false;
  }
}

function hideTooltip() {
  if (tooltip) tooltip.hidden = true;
}

function disposeScene() {
  if (renderer?.domElement) {
    renderer.domElement.removeEventListener("pointermove", onPointerMove);
    renderer.domElement.removeEventListener("pointerleave", hideTooltip);
  }
  if (controls) controls.dispose();
  if (scene) disposeObject(scene);
  if (renderer) {
    renderer.dispose();
    renderer.forceContextLoss?.();
  }
  if (viewport) viewport.innerHTML = "";
  renderer = null;
  scene = null;
  camera = null;
  controls = null;
  caseGroup = null;
  componentGroup = null;
  raycaster = null;
  pointer = null;
  componentMeshes = new Map();
  animations = [];
}

function disposeObject(object) {
  object.traverse((child) => {
    if (child.geometry) child.geometry.dispose();
    if (child.material) {
      if (Array.isArray(child.material)) child.material.forEach((material) => material.dispose());
      else child.material.dispose();
    }
  });
}

function markInteraction() {
  lastInteraction = performance.now();
}

function lerp(from, to, t) {
  return from + (to - from) * t;
}

function easeOutBack(t) {
  const c1 = 1.70158;
  const c3 = c1 + 1;
  return 1 + c3 * Math.pow(t - 1, 3) + c1 * Math.pow(t - 1, 2);
}

function escapeHtml(value) {
  return String(value || "").replace(/[&<>"']/g, (ch) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" })[ch]);
}

window.addEventListener("beforeunload", close);
window.buildpc3d = { openPage, update, close };

if (viewport) openPage(readSavedBuild());
