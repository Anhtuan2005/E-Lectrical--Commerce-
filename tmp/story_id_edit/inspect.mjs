import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "E:/T4_B1_Nhom3.xlsx";
const previewDir = "E:/E-Lectrical--Commerce-/tmp/story_id_edit/previews_before";

await fs.mkdir(previewDir, { recursive: true });
const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(inputPath));

const summary = await workbook.inspect({
  kind: "workbook,sheet,table,definedName,drawing",
  maxChars: 20000,
  tableMaxRows: 15,
  tableMaxCols: 15,
  tableMaxCellChars: 160,
});
console.log("===WORKBOOK SUMMARY===");
console.log(summary.ndjson);

for (const sheet of workbook.worksheets.items) {
  const used = sheet.getUsedRange();
  console.log(`===SHEET ${sheet.name} USED ${used?.address ?? "(none)"}===`);
  if (used) {
    const region = await workbook.inspect({
      kind: "table,formula",
      sheetId: sheet.name,
      range: used.address,
      include: "values,formulas",
      maxChars: 30000,
      tableMaxRows: 100,
      tableMaxCols: 30,
      tableMaxCellChars: 200,
      options: { maxResults: 500 },
    });
    console.log(region.ndjson);
    const preview = await workbook.render({
      sheetName: sheet.name,
      autoCrop: "all",
      scale: 1,
      format: "png",
    });
    const safeName = sheet.name.replace(/[<>:"/\\|?*]/g, "_");
    await fs.writeFile(path.join(previewDir, `${safeName}.png`), new Uint8Array(await preview.arrayBuffer()));
  }
}
