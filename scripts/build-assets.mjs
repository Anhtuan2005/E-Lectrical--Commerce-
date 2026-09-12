import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { transform } from 'esbuild';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const manifest = JSON.parse(fs.readFileSync(path.join(root, 'ClientAssets/manifest.json'), 'utf8'));
const check = process.argv.includes('--check');
let stale = false;

async function write(relative, content) {
  const filename = path.join(root, relative);
  if (check) {
    if (!fs.existsSync(filename) || fs.readFileSync(filename, 'utf8').replace(/\r\n/g, '\n') !== content) {
      console.error(`Outdated asset: ${relative}. Run npm run build:assets.`);
      stale = true;
    }
  } else fs.writeFileSync(filename, content);
}

for (const [extension, files] of Object.entries(manifest)) {
  // Preserve the CSS cascade and global function visibility for existing Razor event handlers.
  const source = files.map(file => fs.readFileSync(path.join(root, file), 'utf8').replace(/\r\n/g, '\n')).join('');
  await write(`wwwroot/${extension}/site.${extension}`, source);
  const result = await transform(source, { loader: extension, minifyWhitespace: true,
    minifySyntax: true, minifyIdentifiers: false, target: 'es2020', legalComments: 'none' });
  await write(`wwwroot/${extension}/site.min.${extension}`, result.code);
}
for (const [extension, name] of [['css', 'admin'], ['js', 'admin'], ['js', 'buildpc']]) {
  const source = fs.readFileSync(path.join(root, `wwwroot/${extension}/${name}.${extension}`), 'utf8');
  const result = await transform(source, { loader: extension, minifyWhitespace: true,
    minifySyntax: true, minifyIdentifiers: false, target: 'es2020', legalComments: 'none' });
  await write(`wwwroot/${extension}/${name}.min.${extension}`, result.code);
}
if (stale) process.exitCode = 1;
