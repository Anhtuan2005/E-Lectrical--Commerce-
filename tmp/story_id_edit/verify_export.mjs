import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "E:/E-Lectrical--Commerce-/outputs/01a0cd18-8168-7500-afd2-3a7d684a3928/T4_B1_Nhom3_story_id_random.xlsx";
const previewDir = "E:/E-Lectrical--Commerce-/tmp/story_id_edit/previews_after";
await fs.mkdir(previewDir, { recursive: true });

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(inputPath));
const summary = await workbook.inspect({
  kind: "workbook,sheet,table,definedName,drawing",
  maxChars: 10000,
  tableMaxRows: 5,
  tableMaxCols: 12,
});
console.log(summary.ndjson);

for (const sheet of workbook.worksheets.items) {
  const preview = await workbook.render({
    sheetName: sheet.name,
    autoCrop: "all",
    scale: 1,
    format: "png",
  });
  const safeName = sheet.name.replace(/[<>:"/\\|?*]/g, "_");
  const outputPath = path.join(previewDir, `${safeName}.png`);
  await fs.writeFile(outputPath, new Uint8Array(await preview.arrayBuffer()));
  console.log(outputPath);
}
