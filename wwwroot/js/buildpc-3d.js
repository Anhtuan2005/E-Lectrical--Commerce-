import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { RoomEnvironment } from "three/addons/environments/RoomEnvironment.js";

const viewport = document.getElementById("pc3dViewport");
const fallback = document.getElementById("pc3dFallback");
const tooltip = document.getElementById("pc3dTooltip");
const partList = document.getElementById("pc3dPartList");
const totalEl = document.getElementById("pc3dTotal");
const emptyEl = document.getElementById("pc3dEmpty");
const hintEl = document.querySelector(".pc3d-hint");

const slotLabels = {
  Case: "V\u1ecf case",
  Mainboard: "Mainboard",
  CPU: "CPU",
  RAM: "RAM",
  VGA: "Card \u0111\u1ed3 h\u1ecda",
  SSD: "SSD",
  PSU: "Ngu\u1ed3n",
  Cooling: "T\u1ea3n nhi\u1ec7t"
};

const slotOrder = ["Case", "Mainboard", "CPU", "RAM", "VGA", "SSD", "PSU", "Cooling"];

const slotSpecs = {
  Case: { size: [30, 50, 58], position: [0, 0, 0], rotation: [0, 0, 0], model: "/models/case.glb", always: true },
  Mainboard: { size: [22, 30, 1.1], position: [0, 2, -12], rotation: [0, 0, 0], model: "/models/mainboard.glb", always: true },
  CPU: { size: [4.8, 4.8, 0.8], position: [0, 7, -10.8], rotation: [0, 0, 0], model: "/models/cpu.glb" },
  RAM: { size: [1.2, 12, 1.2], positions: [[-5.3, 8, -10.2], [-3.4, 8, -10.2]], rotation: [0, 0, 0], model: "/models/ram-rgb.glb" },
  VGA: { size: [21, 4.8, 7.2], position: [0, -7.5, -5.6], rotation: [0, 0, 0], model: resolveVgaModel },
  SSD: { size: [5.2, 9, 0.8], position: [7.4, 2, -10.5], rotation: [0, 0, 0], model: "/models/ssd.glb" },
  PSU: { size: [14, 8.5, 13], position: [0, -18.5, 7.5], rotation: [0, 0, 0], model: "/models/psu.glb" },
  Cooling: { size: [8, 8, 5], position: [0, 9, -7.5], rotation: [0, 0, 0], model: "/models/cooler.glb" }
};

const loader = new GLTFLoader();
const modelCache = new Map();
const minimumGlbBytes = 1024;
const modelLoadTimeoutMs = 4500;

let renderer;
let scene;
let camera;
let controls;
let resizeObserver;
let raycaster;
let pointer;
let animationId = 0;
let renderVersion = 0;
let pcGroup;
let componentGroup;
let worldGuideGroup;
let currentBuild = {};
let componentMeshes = new Map();
let modelFallbackCount = 0;
let modelFallbackSlots = new Set();
let lastInteraction = performance.now();
let canvasCheckTimer = 0;

function openPage(buildState) {
  currentBuild = buildState || {};
  renderBuildSummary(currentBuild);

  if (!viewport) return;
  if (!Object.keys(currentBuild).length && emptyEl) emptyEl.hidden = false;

  if (!supportsWebGL()) {
    renderCssPreview(currentBuild, "Trinh duyet dang tat WebGL, Techvora chuyen sang preview tuong thich.");
    return;
  }

  if (!initScene()) {
    renderCssPreview(currentBuild, "Khong the khoi tao WebGL renderer.");
    return;
  }

  renderBuildModels(currentBuild);
  resize();
  startLoop();
}

async function renderBuildModels(buildState) {
  const version = ++renderVersion;
  modelFallbackCount = 0;
  modelFallbackSlots = new Set();
  setHint("Dang tai model 3D...");
  clearGroup(componentGroup);
  componentMeshes = new Map();

  const slotsToRender = slotOrder.filter((slot) => slotSpecs[slot]?.always || buildState[slot]);
  const renderItems = [];

  for (const slot of slotsToRender) {
    const spec = slotSpecs[slot];
    const product = buildState[slot] || null;

    if (slot === "RAM" && spec.positions?.length) {
      for (let index = 0; index < spec.positions.length; index += 1) {
        renderItems.push({ slot, spec, product, position: spec.positions[index], key: `${slot}-${index + 1}`, tooltipProduct: product || { name: "RAM placeholder" } });
      }
      continue;
    }

    renderItems.push({ slot, spec, product, position: spec.position, key: slot, tooltipProduct: product });
  }

  renderItems.forEach((item) => addFallbackToScene(item.slot, item.spec, item.product, item.position, item.key, item.tooltipProduct));
  frameScene();
  setHint("Dang hien preview tam thoi, model that se tu thay vao neu file GLB hop le.");
  scheduleCanvasVisibilityCheck(version, 900);

  await Promise.allSettled(renderItems.map((item) => hydrateModel(item, version)));

  if (version !== renderVersion) return;
  frameScene();
  setHint(buildModelHint());
  scheduleCanvasVisibilityCheck(version, 1200);
}

function addFallbackToScene(slot, spec, product, position, key, tooltipProduct) {
  const object = createFallbackModel(slot, spec);
  normalizeObject(object, spec.size);
  tintFallback(object, slot, true);

  const holder = new THREE.Group();
  holder.name = `pc3d-${key}`;
  holder.userData = { slot, product: tooltipProduct, usedFallback: true };
  holder.position.fromArray(position || [0, 0, 0]);
  holder.rotation.set(...(spec.rotation || [0, 0, 0]));
  holder.add(object);

  componentGroup.add(holder);
  componentMeshes.set(key, holder);
  return holder;
}

async function hydrateModel(item, version) {
  const { slot, spec, product, key } = item;
  try {
    const source = await loadModel(resolveModelPath(slot, spec, product));
    if (version !== renderVersion) return;
    const object = cloneModel(source);
    normalizeObject(object, spec.size);
    enhanceLoadedModel(object, slot);
    replaceHolderObject(key, slot, spec, object);
    frameScene();
  } catch (error) {
    modelFallbackCount += 1;
    modelFallbackSlots.add(slotLabels[slot] || slot);
    if (!error?.isPc3dPlaceholder) {
      console.warn(`[pc3d] Cannot load GLB for ${slot}. Using placeholder.`, error);
    }
  }
}

function replaceHolderObject(key, slot, spec, object) {
  const holder = componentMeshes.get(key);
  if (!holder) return;

  holder.children.forEach((child) => disposeObject(child));
  holder.clear();
  holder.add(object);

  holder.userData.usedFallback = false;
}

function initScene() {
  if (renderer) return true;

  viewport.hidden = false;
  viewport.innerHTML = "";
  removeStablePreview();
  if (fallback) fallback.hidden = true;

  scene = new THREE.Scene();
  scene.background = new THREE.Color(0x0b1624);
  scene.fog = new THREE.Fog(0x0b1624, 140, 280);

  camera = new THREE.PerspectiveCamera(42, 1, 0.1, 1000);
  camera.position.set(38, 28, 82);

  try {
    renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, powerPreference: "high-performance" });
  } catch {
    disposeScene();
    return false;
  }

  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.setSize(Math.max(1, viewport.clientWidth), Math.max(1, viewport.clientHeight), false);
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.45;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  viewport.appendChild(renderer.domElement);

  const pmrem = new THREE.PMREMGenerator(renderer);
  scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
  pmrem.dispose();

  controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.enablePan = false;
  controls.minDistance = 34;
  controls.maxDistance = 140;
  controls.minPolarAngle = 0.18;
  controls.maxPolarAngle = Math.PI - 0.18;
  controls.addEventListener("start", markInteraction);
  controls.addEventListener("change", markInteraction);
  renderer.domElement.addEventListener("dblclick", resetCamera);

  raycaster = new THREE.Raycaster();
  pointer = new THREE.Vector2();
  renderer.domElement.addEventListener("pointermove", onPointerMove);
  renderer.domElement.addEventListener("pointerleave", hideTooltip);

  scene.add(new THREE.AmbientLight(0xffffff, 0.9));
  scene.add(new THREE.HemisphereLight(0xd9e7ff, 0x111827, 1.9));
  const keyLight = new THREE.DirectionalLight(0xffffff, 4.4);
  keyLight.position.set(34, 58, 54);
  keyLight.castShadow = true;
  keyLight.shadow.mapSize.set(2048, 2048);
  scene.add(keyLight);

  const rimLight = new THREE.DirectionalLight(0x55c8ff, 2.5);
  rimLight.position.set(-40, 24, -32);
  scene.add(rimLight);

  const rgbGlow = new THREE.PointLight(0x00c8ff, 70, 120, 2);
  rgbGlow.position.set(0, 5, 18);
  scene.add(rgbGlow);

  pcGroup = new THREE.Group();
  componentGroup = new THREE.Group();
  pcGroup.add(componentGroup);
  scene.add(pcGroup);
  worldGuideGroup = createWorldGuide();
  worldGuideGroup.visible = false;
  scene.add(worldGuideGroup);
  scene.add(createStudioFloor());

  resizeObserver = new ResizeObserver(resize);
  resizeObserver.observe(viewport);
  return true;
}

function createStudioFloor() {
  const floor = new THREE.Group();
  const plane = new THREE.Mesh(
    new THREE.CircleGeometry(42, 96),
    new THREE.MeshStandardMaterial({
      color: 0x07111d,
      roughness: 0.62,
      metalness: 0.18
    })
  );
  plane.rotation.x = -Math.PI / 2;
  plane.position.y = -26;
  plane.receiveShadow = true;
  floor.add(plane);

  const grid = new THREE.GridHelper(86, 24, 0x0ea5e9, 0x123146);
  grid.position.y = -25.8;
  grid.material.transparent = true;
  grid.material.opacity = 0.22;
  floor.add(grid);
  return floor;
}

function createWorldGuide() {
  const group = new THREE.Group();
  group.name = "pc3d-world-guide";

  const backdrop = new THREE.Mesh(
    new THREE.PlaneGeometry(92, 72),
    new THREE.MeshBasicMaterial({
      color: 0x123a55,
      transparent: true,
      opacity: 0.62,
      side: THREE.DoubleSide,
      depthTest: false,
      depthWrite: false
    })
  );
  backdrop.position.set(0, 0, -34);
  backdrop.renderOrder = 850;
  group.add(backdrop);

  const targetRing = new THREE.Mesh(
    new THREE.TorusGeometry(15, 0.65, 16, 96),
    new THREE.MeshBasicMaterial({
      color: 0xffd34d,
      depthTest: false,
      depthWrite: false
    })
  );
  targetRing.position.set(0, 0, 30);
  targetRing.renderOrder = 1100;
  group.add(targetRing);

  const caseSize = slotSpecs.Case.size;
  const caseEdges = new THREE.LineSegments(
    new THREE.EdgesGeometry(new THREE.BoxGeometry(caseSize[0], caseSize[1], caseSize[2])),
    new THREE.LineBasicMaterial({
      color: 0x45e5ff,
      transparent: true,
      opacity: 0.92,
      depthTest: false,
      depthWrite: false
    })
  );
  caseEdges.renderOrder = 1000;
  group.add(caseEdges);

  const boardSpec = slotSpecs.Mainboard;
  const board = new THREE.Mesh(
    new THREE.BoxGeometry(boardSpec.size[0], boardSpec.size[1], 0.35),
    new THREE.MeshBasicMaterial({
      color: 0x22c55e,
      transparent: true,
      opacity: 0.42,
      depthTest: false,
      depthWrite: false
    })
  );
  board.position.fromArray(boardSpec.position);
  board.renderOrder = 1001;
  group.add(board);

  const beacon = new THREE.Mesh(
    new THREE.SphereGeometry(4.2, 32, 16),
    new THREE.MeshBasicMaterial({
      color: 0xff3b30,
      depthTest: false,
      depthWrite: false
    })
  );
  beacon.position.set(0, 0, 31);
  beacon.renderOrder = 1002;
  group.add(beacon);

  return group;
}

function createFallbackModel(slot, spec) {
  switch (slot) {
    case "Case": return createFallbackCase(spec);
    case "Mainboard": return createFallbackBox(spec.size, 0x16a34a, 0.72);
    case "CPU": return createFallbackCpu();
    case "RAM": return createFallbackRam();
    case "VGA": return createFallbackGpu();
    case "SSD": return createFallbackBox(spec.size, 0x0891b2, 0.9);
    case "PSU": return createFallbackPsu();
    case "Cooling": return createFallbackCooler();
    default: return createFallbackBox(spec.size, 0x38bdf8, 0.9);
  }
}

function createFallbackCase(spec) {
  const group = new THREE.Group();
  const [x, y, z] = spec.size;
  const shell = new THREE.BoxGeometry(x, y, z);
  const edges = new THREE.LineSegments(
    new THREE.EdgesGeometry(shell),
    new THREE.LineBasicMaterial({ color: 0x7dd3fc, transparent: true, opacity: 0.86 })
  );
  group.add(edges);

  const back = new THREE.Mesh(
    new THREE.BoxGeometry(x, y, 0.5),
    new THREE.MeshStandardMaterial({ color: 0x0b1725, metalness: 0.35, roughness: 0.5, transparent: true, opacity: 0.84 })
  );
  back.position.z = -z / 2;
  back.receiveShadow = true;
  group.add(back);

  const glass = new THREE.Mesh(
    new THREE.BoxGeometry(x * 0.94, y * 0.94, 0.28),
    new THREE.MeshPhysicalMaterial({
      color: 0x38bdf8,
      transmission: 0.2,
      transparent: true,
      opacity: 0.13,
      roughness: 0.08,
      metalness: 0.02,
      depthWrite: false
    })
  );
  glass.position.z = z / 2;
  group.add(glass);
  return group;
}

function createFallbackCpu() {
  const group = createFallbackBox([5, 5, 0.8], 0xd4af37, 1);
  const mark = new THREE.Mesh(
    new THREE.BoxGeometry(3.4, 3.4, 0.12),
    new THREE.MeshStandardMaterial({ color: 0xf8fafc, roughness: 0.42 })
  );
  mark.position.z = 0.48;
  group.add(mark);
  return group;
}

function createFallbackRam() {
  const group = createFallbackBox([1.25, 12, 1], 0x8b5cf6, 1);
  const glow = new THREE.Mesh(
    new THREE.BoxGeometry(0.18, 11, 1.2),
    new THREE.MeshBasicMaterial({ color: 0x00c8ff })
  );
  glow.position.x = -0.7;
  group.add(glow);
  return group;
}

function createFallbackGpu() {
  const group = createFallbackBox([21, 4.8, 7.2], 0x2563eb, 1);
  [-5.8, 0, 5.8].forEach((x) => {
    const fan = new THREE.Mesh(
      new THREE.CylinderGeometry(1.65, 1.65, 0.28, 48),
      new THREE.MeshStandardMaterial({ color: 0x020617, metalness: 0.35, roughness: 0.34 })
    );
    fan.rotation.x = Math.PI / 2;
    fan.position.set(x, 0, 3.74);
    group.add(fan);
  });
  return group;
}

function createFallbackPsu() {
  const group = createFallbackBox([14, 8.5, 13], 0x64748b, 1);
  const fan = new THREE.Mesh(
    new THREE.CylinderGeometry(3.1, 3.1, 0.24, 64),
    new THREE.MeshStandardMaterial({ color: 0x0f172a, roughness: 0.45, metalness: 0.45 })
  );
  fan.rotation.x = Math.PI / 2;
  fan.position.z = 6.65;
  group.add(fan);
  return group;
}

function createFallbackCooler() {
  const group = new THREE.Group();
  const fan = new THREE.Mesh(
    new THREE.CylinderGeometry(3.8, 3.8, 1.1, 64),
    new THREE.MeshStandardMaterial({ color: 0xdbeafe, metalness: 0.18, roughness: 0.28 })
  );
  fan.rotation.x = Math.PI / 2;
  fan.castShadow = true;
  group.add(fan);
  const ring = new THREE.Mesh(
    new THREE.TorusGeometry(3.1, 0.18, 12, 64),
    new THREE.MeshBasicMaterial({ color: 0x00c8ff })
  );
  ring.position.z = 0.62;
  group.add(ring);
  return group;
}

function createFallbackBox(size, color, opacity) {
  const group = new THREE.Group();
  const mesh = new THREE.Mesh(
    new THREE.BoxGeometry(size[0], size[1], size[2]),
    new THREE.MeshStandardMaterial({
      color,
      metalness: 0.25,
      roughness: 0.36,
      transparent: opacity < 1,
      opacity
    })
  );
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  group.add(mesh);
  const edges = new THREE.LineSegments(
    new THREE.EdgesGeometry(mesh.geometry),
    new THREE.LineBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.72, depthTest: false })
  );
  edges.renderOrder = 24;
  group.add(edges);
  return group;
}

function createVisibilityCage(size) {
  const cage = new THREE.LineSegments(
    new THREE.EdgesGeometry(new THREE.BoxGeometry(size[0] * 1.04, size[1] * 1.04, size[2] * 1.04)),
    new THREE.LineBasicMaterial({
      color: 0x56e8ff,
      transparent: true,
      opacity: 0.92,
      depthTest: false,
      depthWrite: false
    })
  );
  cage.name = "pc3d-case-visibility-cage";
  cage.renderOrder = 999;
  return cage;
}

function resolveModelPath(slot, spec, product) {
  if (product?.model3DUrl) return product.model3DUrl;
  if (product?.modelUrl) return product.modelUrl;
  if (typeof spec.model === "function") return spec.model(product);
  return spec.model;
}

function resolveVgaModel(product) {
  const name = String(product?.name || "").toLowerCase();
  if (name.includes("dual") || name.includes("2x") || name.includes("2 fan")) return "/models/vga-dual.glb";
  return "/models/vga-triple.glb";
}

async function loadModel(path) {
  if (!path) return Promise.reject(new Error("Missing model path"));
  if (modelCache.has(path)) return Promise.resolve(cloneModel(modelCache.get(path)));

  const response = await fetch(path, { cache: "no-store" });
  if (!response.ok) {
    throw createModelPlaceholderError(`Model ${path} returned ${response.status}.`);
  }
  const arrayBuffer = await response.arrayBuffer();
  assertLikelyGltfPayload(path, arrayBuffer);

  return withModelTimeout(new Promise((resolve, reject) => {
    loader.parse(
      arrayBuffer,
      getModelBasePath(path),
      (gltf) => {
        const sceneRoot = gltf.scene || gltf.scenes?.[0];
        if (!sceneRoot) {
          reject(new Error(`No scene in ${path}`));
          return;
        }
        prepareLoadedModel(sceneRoot);
        modelCache.set(path, sceneRoot);
        resolve(cloneModel(sceneRoot));
      },
      (error) => {
        if (isLocalModelPath(path)) error.isPc3dPlaceholder = true;
        reject(error);
      }
    );
  }), path);
}

function assertLikelyGltfPayload(path, arrayBuffer) {
  if (!arrayBuffer || arrayBuffer.byteLength < minimumGlbBytes) {
    throw createModelPlaceholderError(`Model ${path} is only ${arrayBuffer?.byteLength || 0} bytes.`);
  }

  const magic = readAscii(arrayBuffer, 0, 4);
  const isBinaryGlb = magic === "glTF";
  const isJsonGltf = readAscii(arrayBuffer, 0, 1).trim() === "{";
  if (!isBinaryGlb && !isJsonGltf) {
    throw createModelPlaceholderError(`Model ${path} is not a GLB/GLTF payload.`);
  }
}

function getModelBasePath(path) {
  const cleanPath = path.split("?")[0];
  return cleanPath.slice(0, cleanPath.lastIndexOf("/") + 1);
}

function createModelPlaceholderError(message) {
  const error = new Error(`${message} Using placeholder.`);
  error.isPc3dPlaceholder = true;
  return error;
}

function withModelTimeout(promise, path) {
  let timeoutId;
  const timeout = new Promise((_, reject) => {
    timeoutId = window.setTimeout(() => {
      reject(createModelPlaceholderError(`Model ${path} took too long to parse.`));
    }, modelLoadTimeoutMs);
  });

  return Promise.race([promise, timeout]).finally(() => window.clearTimeout(timeoutId));
}

function readAscii(arrayBuffer, start, length) {
  return String.fromCharCode(...new Uint8Array(arrayBuffer, start, length));
}

function isLocalModelPath(path) {
  return typeof path === "string" && path.startsWith("/models/");
}

function cloneModel(source) {
  const clone = source.clone(true);
  clone.traverse((node) => {
    if (node.isMesh) {
      node.castShadow = true;
      node.receiveShadow = true;
      if (node.material) {
        node.material = Array.isArray(node.material)
          ? node.material.map((material) => material.clone())
          : node.material.clone();
      }
    }
  });
  return clone;
}

function prepareLoadedModel(object) {
  object.traverse((node) => {
    if (!node.isMesh) return;
    node.castShadow = true;
    node.receiveShadow = true;
    if (node.material) {
      const materials = Array.isArray(node.material) ? node.material : [node.material];
      materials.forEach((material) => {
        material.side = THREE.DoubleSide;
        material.needsUpdate = true;
      });
    }
  });
}

function enhanceLoadedModel(object, slot) {
  if (slot !== "Case") return;

  object.traverse((node) => {
    if (!node.isMesh || !node.material) return;
    node.renderOrder = 8;
    const materials = Array.isArray(node.material) ? node.material : [node.material];
    materials.forEach((material) => {
      if (material.color) {
        const hsl = {};
        material.color.getHSL(hsl);
        if (hsl.l < 0.34) material.color.setHSL(hsl.h, Math.min(hsl.s, 0.24), 0.42);
      }
      if ("emissive" in material) {
        material.emissive = new THREE.Color(0x071522);
        material.emissiveIntensity = 0.08;
      }
      if ("roughness" in material) material.roughness = Math.max(material.roughness ?? 0.5, 0.48);
      material.needsUpdate = true;
    });
  });
}

function normalizeObject(object, targetSize) {
  const box = new THREE.Box3().setFromObject(object);
  const size = box.getSize(new THREE.Vector3());
  const center = box.getCenter(new THREE.Vector3());

  const target = new THREE.Vector3(...targetSize);
  const scale = Math.min(
    safeRatio(target.x, size.x),
    safeRatio(target.y, size.y),
    safeRatio(target.z, size.z)
  );
  if (Number.isFinite(scale) && scale > 0) {
    object.scale.multiplyScalar(scale);
    object.position.copy(center).multiplyScalar(-scale);
  } else {
    object.position.copy(center).multiplyScalar(-1);
  }
}

function tintFallback(object, slot, usedFallback) {
  if (!usedFallback) return;
  object.traverse((node) => {
    if ((!node.isMesh && !node.isLineSegments) || !node.material) return;
    node.userData.slot = slot;
    node.renderOrder = slot === "Case" ? 10 : 22;
    node.material.depthTest = slot === "Case";
    node.material.depthWrite = slot === "Case";
    if ("emissive" in node.material) {
      node.material.emissive = new THREE.Color(slot === "RAM" ? 0x211044 : 0x061625);
      node.material.emissiveIntensity = 0.22;
    }
  });
}

function safeRatio(target, current) {
  return current > 0.0001 ? target / current : Number.POSITIVE_INFINITY;
}

function startLoop() {
  if (animationId) return;
  const tick = () => {
    animationId = requestAnimationFrame(tick);
    if (controls) {
      controls.autoRotate = performance.now() - lastInteraction > 4500;
      controls.autoRotateSpeed = 0.35;
      controls.update();
    }
    if (renderer && scene && camera) renderer.render(scene, camera);
  };
  tick();
}

function scheduleCanvasVisibilityCheck(version, delayMs) {
  window.clearTimeout(canvasCheckTimer);
  canvasCheckTimer = window.setTimeout(() => {
    if (version !== renderVersion || !renderer) return;
    if (isCanvasStillBlank()) {
      console.warn("[pc3d] WebGL canvas is still too dark after rendering models. Switching to compatible preview.");
      renderCssPreview(currentBuild, "WebGL dang chay nhung canvas khong ve duoc model, Techvora chuyen sang preview tuong thich.");
    }
  }, delayMs);
}

function buildModelHint() {
  if (!modelFallbackCount) {
    return "Keo de xoay 360 do, scroll de zoom. Double click de reset goc nhin.";
  }

  const slots = [...modelFallbackSlots].join(", ");
  return `${slots} chua co GLB hop le, dang dung placeholder 3D. Keo de xoay, scroll de zoom.`;
}

function isCanvasStillBlank() {
  try {
    if (renderer && scene && camera) renderer.render(scene, camera);
    const gl = renderer.getContext();
    const width = gl.drawingBufferWidth;
    const height = gl.drawingBufferHeight;
    if (width < 2 || height < 2) return true;

    const samplePoints = [
      [0.5, 0.5],
      [0.42, 0.5],
      [0.58, 0.5],
      [0.5, 0.38],
      [0.5, 0.62]
    ];

    let brightest = 0;
    const pixel = new Uint8Array(4);
    samplePoints.forEach(([x, y]) => {
      gl.readPixels(
        Math.floor(width * x),
        Math.floor(height * y),
        1,
        1,
        gl.RGBA,
        gl.UNSIGNED_BYTE,
        pixel
      );
      brightest = Math.max(brightest, pixel[0], pixel[1], pixel[2]);
    });

    return brightest < 44;
  } catch {
    return false;
  }
}

function frameScene() {
  if (!pcGroup || !camera || !controls) return;
  const box = getExpectedComponentBox();
  if (box.isEmpty()) return;

  const center = box.getCenter(new THREE.Vector3());
  const size = box.getSize(new THREE.Vector3());
  const maxDim = Math.max(size.x, size.y, size.z, 40);
  const fov = THREE.MathUtils.degToRad(camera.fov);
  const distance = (maxDim / (2 * Math.tan(fov / 2))) * 0.98;

  controls.target.copy(center);
  camera.position.set(center.x + maxDim * 0.34, center.y + maxDim * 0.22, center.z + distance);
  camera.near = 0.1;
  camera.far = Math.max(500, distance * 5);
  camera.lookAt(center);
  camera.updateProjectionMatrix();
  controls.update();
}

function getExpectedComponentBox() {
  const box = new THREE.Box3();
  componentMeshes.forEach((holder) => {
    const spec = slotSpecs[holder.userData?.slot];
    if (!spec?.size) return;

    const half = new THREE.Vector3(spec.size[0] / 2, spec.size[1] / 2, spec.size[2] / 2);
    const center = holder.position.clone();
    box.expandByPoint(center.clone().sub(half));
    box.expandByPoint(center.clone().add(half));
  });
  return box;
}

function resetCamera() {
  frameScene();
  markInteraction();
}

function onPointerMove(event) {
  if (!renderer || !camera || !raycaster || !tooltip) return;
  const rect = renderer.domElement.getBoundingClientRect();
  pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
  pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
  raycaster.setFromCamera(pointer, camera);

  const targets = [];
  componentMeshes.forEach((group) => group.traverse((child) => {
    if (child.isMesh) targets.push(child);
  }));
  const hit = raycaster.intersectObjects(targets, false)[0];
  if (!hit) {
    hideTooltip();
    return;
  }

  const group = findComponentGroup(hit.object);
  const data = group?.userData || hit.object.userData;
  tooltip.innerHTML = `<strong>${escapeHtml(slotLabels[data.slot] || data.slot || "Model")}</strong>${escapeHtml(data.product?.name || "")}<br>${data.usedFallback ? "Placeholder 3D" : escapeHtml(data.product?.price || "")}`;
  tooltip.hidden = false;
  tooltip.style.left = `${event.clientX - rect.left + 16}px`;
  tooltip.style.top = `${event.clientY - rect.top + 16}px`;
}

function findComponentGroup(object) {
  let current = object;
  while (current) {
    if (current.userData?.slot && ("product" in current.userData || "usedFallback" in current.userData)) return current;
    current = current.parent;
  }
  return null;
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
      : "<li>Chua co linh kien nao.</li>";
  }
  const total = entries.reduce((sum, [, product]) => sum + Number(product.priceRaw || 0), 0);
  if (totalEl) totalEl.textContent = `${total.toLocaleString("vi-VN")} \u0111`;
}

function renderCssPreview(buildState, message) {
  if (!fallback) return;
  viewport.hidden = true;
  fallback.hidden = false;
  const entries = Object.entries(buildState || {});
  fallback.innerHTML = `
    <div class="pc3d-css-preview" role="img" aria-label="Preview cau hinh PC dang tuong thich">
      <div class="pc3d-css-note">${escapeHtml(message || "Che do preview tuong thich dang bat.")}</div>
      <div class="pc3d-css-case">
        <div class="pc3d-css-shell"></div>
        ${entries.map(([slot, product]) => `<div class="pc3d-css-part pc3d-slot-${escapeHtml(slot.toLowerCase())}" title="${escapeHtml(product.name)}"><span>${escapeHtml(slot)}</span></div>`).join("")}
      </div>
    </div>`;
}

function removeStablePreview() {
  document.getElementById("pc3dDomPreview")?.remove();
  document.getElementById("pc3dStablePreviewStyles")?.remove();
}

function resize() {
  if (!renderer || !camera || !viewport) return;
  const width = Math.max(1, viewport.clientWidth);
  const height = Math.max(1, viewport.clientHeight);
  renderer.setSize(width, height, false);
  camera.aspect = width / height;
  camera.updateProjectionMatrix();
}

function clearGroup(group) {
  if (!group) return;
  [...group.children].forEach((child) => {
    group.remove(child);
    disposeObject(child);
  });
}

function readSavedBuild() {
  try {
    return JSON.parse(sessionStorage.getItem("techvoraBuildPcPreview") || "{}") || {};
  } catch {
    return {};
  }
}

function supportsWebGL() {
  try {
    const canvas = document.createElement("canvas");
    return Boolean(canvas.getContext("webgl2") || canvas.getContext("webgl") || canvas.getContext("experimental-webgl"));
  } catch {
    return false;
  }
}

function setHint(text) {
  if (hintEl) hintEl.textContent = text;
}

function hideTooltip() {
  if (tooltip) tooltip.hidden = true;
}

function disposeScene() {
  window.clearTimeout(canvasCheckTimer);
  canvasCheckTimer = 0;
  cancelAnimationFrame(animationId);
  animationId = 0;
  if (renderer?.domElement) {
    renderer.domElement.removeEventListener("pointermove", onPointerMove);
    renderer.domElement.removeEventListener("pointerleave", hideTooltip);
    renderer.domElement.removeEventListener("dblclick", resetCamera);
  }
  resizeObserver?.disconnect();
  controls?.dispose();
  if (scene) disposeObject(scene);
  renderer?.dispose();
  if (viewport) viewport.innerHTML = "";
  renderer = null;
  scene = null;
  camera = null;
  controls = null;
  resizeObserver = null;
  raycaster = null;
  pointer = null;
  pcGroup = null;
  componentGroup = null;
  worldGuideGroup = null;
  componentMeshes = new Map();
}

function disposeObject(object) {
  object.traverse((child) => {
    child.geometry?.dispose?.();
    if (child.material) {
      if (Array.isArray(child.material)) child.material.forEach((material) => material.dispose?.());
      else child.material.dispose?.();
    }
  });
}

function markInteraction() {
  lastInteraction = performance.now();
}

function escapeHtml(value) {
  return String(value || "").replace(/[&<>"']/g, (ch) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" })[ch]);
}

window.addEventListener("beforeunload", disposeScene);
window.buildpc3d = { openPage, update: openPage, close: disposeScene, debug: getDebugState };

if (viewport) openPage(readSavedBuild());

function getDebugState() {
  const expectedBox = getExpectedComponentBox();
  const actualBox = pcGroup ? new THREE.Box3().setFromObject(pcGroup) : null;
  return {
    hasRenderer: Boolean(renderer),
    viewport: viewport ? {
      clientWidth: viewport.clientWidth,
      clientHeight: viewport.clientHeight,
      rect: viewport.getBoundingClientRect().toJSON?.() || viewport.getBoundingClientRect()
    } : null,
    canvas: renderer?.domElement ? {
      width: renderer.domElement.width,
      height: renderer.domElement.height,
      cssWidth: renderer.domElement.style.width,
      cssHeight: renderer.domElement.style.height
    } : null,
    camera: camera ? {
      position: camera.position.toArray(),
      near: camera.near,
      far: camera.far,
      aspect: camera.aspect
    } : null,
    components: [...componentMeshes.entries()].map(([key, holder]) => ({
      key,
      slot: holder.userData?.slot,
      usedFallback: holder.userData?.usedFallback,
      children: holder.children.length,
      position: holder.position.toArray()
    })),
    expectedBox: expectedBox.isEmpty() ? null : {
      min: expectedBox.min.toArray(),
      max: expectedBox.max.toArray(),
      size: expectedBox.getSize(new THREE.Vector3()).toArray()
    },
    actualBox: actualBox?.isEmpty() ? null : {
      min: actualBox.min.toArray(),
      max: actualBox.max.toArray(),
      size: actualBox.getSize(new THREE.Vector3()).toArray()
    },
    worldGuide: worldGuideGroup ? {
      children: worldGuideGroup.children.length,
      visible: worldGuideGroup.visible
    } : null,
    canvasStillBlank: renderer ? isCanvasStillBlank() : null
  };
}
