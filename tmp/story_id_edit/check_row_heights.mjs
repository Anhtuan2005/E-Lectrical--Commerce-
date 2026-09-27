import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load("E:/T4_B1_Nhom3.xlsx"));
const sheet = workbook.worksheets.getItem("Sprint backlog");
for (const row of [7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 20, 24, 28, 33, 40]) {
  const range = sheet.getRange(`A${row}:J${row}`);
  console.log(JSON.stringify({ row, rowHeight: range.format.rowHeight, values: range.values[0].slice(0, 3) }));
}
