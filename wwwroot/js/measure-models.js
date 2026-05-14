import * as THREE from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";

const loader = new GLTFLoader();

const models = [
  "/models/case.glb",
  "/models/mainboard.glb",
  "/models/cpu.glb",
  "/models/ram-rgb.glb",
  "/models/vga-triple.glb",
  "/models/vga-dual.glb",
  "/models/ssd.glb",
  "/models/psu.glb",
  "/models/cooler.glb",
  "/models/case-fan.glb"
];

async function measure() {
  const results = {};

  for (const path of models) {
    try {
      const response = await fetch(path, { cache: "no-store" });
      if (!response.ok) { results[path] = "NOT FOUND " + response.status; continue; }
      const buffer = await response.arrayBuffer();

      const gltf = await new Promise((resolve, reject) => {
        loader.parse(buffer, path.slice(0, path.lastIndexOf("/") + 1), resolve, reject);
      });

      const scene = gltf.scene || gltf.scenes?.[0];
      if (!scene) { results[path] = "NO SCENE"; continue; }

      const box = new THREE.Box3().setFromObject(scene);
      const size = box.getSize(new THREE.Vector3());
      const center = box.getCenter(new THREE.Vector3());

      results[path] = {
        size: [+size.x.toFixed(3), +size.y.toFixed(3), +size.z.toFixed(3)],
        center: [+center.x.toFixed(3), +center.y.toFixed(3), +center.z.toFixed(3)],
        min: [+box.min.x.toFixed(3), +box.min.y.toFixed(3), +box.min.z.toFixed(3)],
        max: [+box.max.x.toFixed(3), +box.max.y.toFixed(3), +box.max.z.toFixed(3)]
      };
    } catch (err) {
      results[path] = "ERROR: " + err.message;
    }
  }

  // Display results on page
  const pre = document.createElement("pre");
  pre.id = "modelMeasureOutput";
  pre.style.cssText = "position:fixed;inset:0;z-index:99999;background:#000;color:#0f0;padding:20px;overflow:auto;font-size:14px;white-space:pre-wrap;";
  
  let text = "=== GLB MODEL MEASUREMENTS ===\n\n";
  for (const [path, data] of Object.entries(results)) {
    const name = path.split("/").pop();
    if (typeof data === "string") {
      text += `${name}: ${data}\n\n`;
    } else {
      text += `${name}:\n`;
      text += `  size:   [${data.size.join(", ")}]  (W x H x D)\n`;
      text += `  center: [${data.center.join(", ")}]\n`;
      text += `  min:    [${data.min.join(", ")}]\n`;
      text += `  max:    [${data.max.join(", ")}]\n\n`;
    }
  }
  
  pre.textContent = text;
  document.body.appendChild(pre);
  console.log(text);
}

measure();
