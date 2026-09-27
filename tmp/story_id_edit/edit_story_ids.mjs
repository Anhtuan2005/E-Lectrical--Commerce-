import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "E:/E-Lectrical--Commerce-/outputs/01a0cd18-8168-7500-afd2-3a7d684a3928/T4_B1_Nhom3_story_id_random.xlsx";
const outputDir = "E:/E-Lectrical--Commerce-/outputs/01a0cd18-8168-7500-afd2-3a7d684a3928";
const outputPath = path.join(outputDir, "T4_B1_Nhom3_backlog_remapped.xlsx");
const previewDir = "E:/E-Lectrical--Commerce-/tmp/story_id_edit/remapped_previews";

const storyById = new Map([
  ["US-01", ["Áp dụng và kiểm tra điều kiện voucher", 3]],
  ["US-02", ["Đăng ký, đăng nhập, đăng xuất và quản lý hồ sơ", 5]],
  ["US-03", ["Xem xác nhận, lịch sử và chi tiết đơn hàng", 3]],
  ["US-04", ["So sánh giá và thông số sản phẩm", 3]],
  ["US-05", ["Quản lý tồn kho và lịch sử nhập xuất", 5]],
  ["US-06", ["Tìm kiếm, lọc, sắp xếp và phân trang sản phẩm", 5]],
  ["US-07", ["Xem trang chủ, danh mục và sản phẩm nổi bật", 2]],
  ["US-08", ["Đặt hàng và thanh toán khi nhận hàng COD", 3]],
  ["US-09", ["Quản lý danh sách sản phẩm yêu thích", 2]],
  ["US-10", ["Quản lý đơn hàng, trạng thái, hoàn tiền, hóa đơn và xuất CSV", 5]],
  ["US-11", ["Xem chi tiết, hình ảnh, thông số, giá và tồn kho sản phẩm", 3]],
  ["US-12", ["Checkout với địa chỉ giao hàng và phí vận chuyển", 5]],
  ["US-13", ["Hủy đơn chưa giao và mua lại đơn hàng cũ", 3]],
  ["US-14", ["Quản lý giỏ hàng, mua ngay và ghép giỏ khi đăng nhập", 5]],
  ["US-15", ["Thanh toán qua cổng thanh toán trực tuyến", 5]],
  ["US-16", ["Quản lý danh mục, sản phẩm, ảnh, thông số và banner", 5]],
  ["US-17", ["Tạo, đồng bộ, theo dõi và hủy vận đơn GHN", 5]],
  ["US-18", ["Xử lý yêu cầu đổi trả và bảo hành", 3]],
  ["US-19", ["Theo dõi tiến trình giao hàng và nhận thông báo", 3]],
  ["US-20", ["Quản lý người dùng, vai trò và khóa tài khoản", 5]],
  ["US-21", ["Đánh giá sản phẩm đã mua kèm hình ảnh", 3]],
  ["US-22", ["Xem dashboard và thông báo đơn mới theo thời gian thực", 3]],
  ["US-23", ["Gửi yêu cầu đổi trả hoặc bảo hành", 5]],
  ["US-24", ["Duyệt, từ chối và xóa đánh giá", 3]],
  ["US-25", ["Trao đổi với chatbot tư vấn sản phẩm", 3]],
  ["US-26", ["Quản lý voucher, khuyến mãi và phân khúc khách hàng", 5]],
  ["US-27", ["Bảo mật, ghi log, health check và kiểm thử tự động", 5]],
  ["US-28", ["Nhận gợi ý sản phẩm cá nhân hóa", 3]],
  ["US-29", ["Xem báo cáo doanh thu, sản phẩm và danh mục", 5]],
  ["US-30", ["Hiển thị sản phẩm mua kèm và nhắc giỏ hàng bỏ quên", 3]],
]);

const productGroups = [
  { range: "B5:C20", ids: Array.from({ length: 16 }, (_, i) => `US-${String(i + 1).padStart(2, "0")}`) },
  { range: "B22:C29", ids: Array.from({ length: 8 }, (_, i) => `US-${String(i + 17).padStart(2, "0")}`) },
  { range: "B31:C36", ids: Array.from({ length: 6 }, (_, i) => `US-${String(i + 25).padStart(2, "0")}`) },
];

const sprintPlans = [
  {
    range: "A7:C17",
    allColumnsRange: "A7:J17",
    ids: ["US-07", "US-06", "US-11", "US-02", "US-09", "US-14", "US-16", "US-12", "US-01", "US-08", "US-03"],
    expectedPoints: 41,
  },
  {
    range: "A20:C30",
    allColumnsRange: "A20:J30",
    ids: ["US-04", "US-15", "US-13", "US-05", "US-10", "US-19", "US-21", "US-23", "US-22", "US-17", "US-24"],
    expectedPoints: 43,
  },
  {
    range: "A33:C40",
    allColumnsRange: "A33:J40",
    ids: ["US-18", "US-20", "US-26", "US-29", "US-28", "US-30", "US-25", "US-27"],
    expectedPoints: 32,
  },
];

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(inputPath));
const productSheet = workbook.worksheets.getItem("Product backlog");
const sprintSheet = workbook.worksheets.getItem("Sprint backlog");

const untouchedSnapshots = new Map();
for (const sheetName of ["Project Information", "Team"]) {
  const sheet = workbook.worksheets.getItem(sheetName);
  const used = sheet.getUsedRange();
  untouchedSnapshots.set(sheetName, JSON.stringify({ values: used.values, formulas: used.formulas }));
}

const originalProductIds = [
  ...productSheet.getRange("A5:A20").values.flat(),
  ...productSheet.getRange("A22:A29").values.flat(),
  ...productSheet.getRange("A31:A36").values.flat(),
];
const expectedIds = Array.from({ length: 30 }, (_, i) => `US-${String(i + 1).padStart(2, "0")}`);
if (JSON.stringify(originalProductIds) !== JSON.stringify(expectedIds)) {
  throw new Error("Product Backlog Story ID list is not the expected unchanged US-01 to US-30 sequence.");
}

const originalStories = [
  ...productSheet.getRange("B5:C20").values,
  ...productSheet.getRange("B22:C29").values,
  ...productSheet.getRange("B31:C36").values,
];
const canonicalize = (rows) => rows.map(([name, points]) => `${name}\u0000${points}`).sort();
if (JSON.stringify(canonicalize(originalStories)) !== JSON.stringify(canonicalize([...storyById.values()]))) {
  throw new Error("The remap must preserve every original function and its story points exactly once.");
}

const sprintOtherColumnsBefore = new Map();
for (const plan of sprintPlans) {
  const firstRow = Number(plan.range.match(/\d+/)[0]);
  const lastRow = Number(plan.range.match(/:(?:[A-Z]+)(\d+)/)[1]);
  sprintOtherColumnsBefore.set(plan.range, JSON.stringify(sprintSheet.getRange(`D${firstRow}:J${lastRow}`).values));
}

for (const group of productGroups) {
  productSheet.getRange(group.range).values = group.ids.map((id) => storyById.get(id));
}

for (const plan of sprintPlans) {
  const rows = plan.ids.map((id) => [id, ...storyById.get(id)]);
  const totalPoints = rows.reduce((sum, row) => sum + row[2], 0);
  if (totalPoints !== plan.expectedPoints) {
    throw new Error(`Unexpected point total for ${plan.range}: ${totalPoints}`);
  }
  sprintSheet.getRange(plan.range).values = rows;
  sprintSheet.getRange(plan.allColumnsRange).format.autofitRows();
}

workbook.recalculate();

const finalProductIds = [
  ...productSheet.getRange("A5:A20").values.flat(),
  ...productSheet.getRange("A22:A29").values.flat(),
  ...productSheet.getRange("A31:A36").values.flat(),
];
if (JSON.stringify(finalProductIds) !== JSON.stringify(originalProductIds)) {
  throw new Error("Product Backlog Story IDs changed, but only their mapped functions were allowed to change.");
}
if (productSheet.getRange("B11").values[0][0] !== "Xem trang chủ, danh mục và sản phẩm nổi bật") {
  throw new Error("Required mapping failed: the homepage/catalog story must be US-07.");
}

const seenSprintIds = [];
for (const plan of sprintPlans) {
  const rows = sprintSheet.getRange(plan.range).values;
  const ids = rows.map((row) => row[0]);
  if (JSON.stringify(ids) !== JSON.stringify(plan.ids)) {
    throw new Error(`Sprint order mismatch in ${plan.range}.`);
  }
  for (const [id, name, points] of rows) {
    const mapped = storyById.get(id);
    if (!mapped || mapped[0] !== name || mapped[1] !== points) {
      throw new Error(`Sprint Backlog does not match Product Backlog for ${id}.`);
    }
    seenSprintIds.push(id);
  }
  const firstRow = Number(plan.range.match(/\d+/)[0]);
  const lastRow = Number(plan.range.match(/:(?:[A-Z]+)(\d+)/)[1]);
  const otherColumnsAfter = JSON.stringify(sprintSheet.getRange(`D${firstRow}:J${lastRow}`).values);
  if (otherColumnsAfter !== sprintOtherColumnsBefore.get(plan.range)) {
    throw new Error(`Unexpected change in Sprint operational columns for ${plan.range}.`);
  }
}
if (seenSprintIds.length !== 30 || new Set(seenSprintIds).size !== 30 || expectedIds.some((id) => !seenSprintIds.includes(id))) {
  throw new Error("Sprint Backlog must contain each Product Backlog Story ID exactly once.");
}

for (const [sheetName, snapshot] of untouchedSnapshots) {
  const sheet = workbook.worksheets.getItem(sheetName);
  const used = sheet.getUsedRange();
  if (JSON.stringify({ values: used.values, formulas: used.formulas }) !== snapshot) {
    throw new Error(`Unexpected change detected in untouched sheet: ${sheetName}`);
  }
}

const productCheck = await workbook.inspect({
  kind: "table",
  sheetId: "Product backlog",
  range: "A3:C36",
  include: "values,formulas",
  maxChars: 16000,
  tableMaxRows: 40,
  tableMaxCols: 4,
});
console.log("===PRODUCT BACKLOG CHECK===");
console.log(productCheck.ndjson);

const sprintCheck = await workbook.inspect({
  kind: "table,formula",
  sheetId: "Sprint backlog",
  range: "A4:J41",
  include: "values,formulas",
  maxChars: 24000,
  tableMaxRows: 45,
  tableMaxCols: 12,
  options: { maxResults: 100 },
});
console.log("===SPRINT BACKLOG CHECK===");
console.log(sprintCheck.ndjson);

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!",
  options: { useRegex: true, maxResults: 300 },
  maxChars: 2000,
  summary: "final formula error scan",
});
console.log("===FORMULA ERROR SCAN===");
console.log(errors.ndjson);

await fs.mkdir(outputDir, { recursive: true });
await fs.mkdir(previewDir, { recursive: true });
for (const [sheetName, fileName, range] of [
  ["Product backlog", "Product_backlog.png", "A1:C36"],
  ["Sprint backlog", "Sprint_backlog.png", "A1:J41"],
]) {
  const preview = await workbook.render({ sheetName, range, scale: 1, format: "png" });
  await fs.writeFile(path.join(previewDir, fileName), new Uint8Array(await preview.arrayBuffer()));
}

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);

const saved = await SpreadsheetFile.importXlsx(await FileBlob.load(outputPath));
const savedProduct = saved.worksheets.getItem("Product backlog");
const savedSprint = saved.worksheets.getItem("Sprint backlog");
if (savedProduct.getRange("A11:C11").values[0][0] !== "US-07" || savedProduct.getRange("A11:C11").values[0][1] !== "Xem trang chủ, danh mục và sản phẩm nổi bật") {
  throw new Error("Saved workbook failed the US-07 homepage mapping check.");
}
for (const plan of sprintPlans) {
  const rows = savedSprint.getRange(plan.range).values;
  for (const [id, name, points] of rows) {
    const mapped = storyById.get(id);
    if (!mapped || mapped[0] !== name || mapped[1] !== points) {
      throw new Error(`Saved workbook mismatch for ${id}.`);
    }
  }
}
if (savedSprint.getRange("C18").values[0][0] !== 41 || savedSprint.getRange("C31").values[0][0] !== 43 || savedSprint.getRange("C41").values[0][0] !== 32) {
  throw new Error("Saved Sprint totals are incorrect.");
}

console.log(JSON.stringify({
  outputPath,
  homepageId: "US-07",
  productIdsPreserved: finalProductIds.length,
  sprintIdsMatched: seenSprintIds.length,
  sprintTotals: [41, 43, 32],
}));
