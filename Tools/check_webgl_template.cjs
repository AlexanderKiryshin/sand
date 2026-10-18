// Run with: node Tools/check_webgl_template.cjs <Unity BuildTools/Preprocess.js>
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const {spawnSync} = require('node:child_process');
const preprocessor = process.argv[2];
assert.ok(preprocessor && fs.existsSync(preprocessor), 'Pass the installed Unity Preprocess.js path');
const template = path.resolve(__dirname, '../Assets/WebGLTemplates/MirraGamesTemplate-1.0.3');
const settings = fs.readFileSync(path.resolve(__dirname, '../ProjectSettings/ProjectSettings.asset'), 'utf8');
const tagsBlock = settings.match(/^  m_TemplateCustomTags:\r?\n((?:^    .*\r?\n)*)/m);
assert.ok(tagsBlock, 'PlayerSettings must register the custom WebGL template variables');
const tags = Object.fromEntries([...tagsBlock[1].matchAll(/^    (\w+): *(.*?)\r?$/gm)]
  .map(([, key, value]) => [key, value.replace(/^['"]|['"]$/g, '')]));
const scratch = fs.mkdtempSync(path.join(os.tmpdir(), 'sandWebGL'));
const optional = {
  PORTRAIT_ONLY: 'true', LANDSCAPE_ONLY: 'false',
  MOBILE_PORTRAIT_ASPECT_RATIO: '0.5625', MOBILE_LANDSCAPE_ASPECT_RATIO: '1.777',
  DESKTOP_ASPECT_RATIO: '1.6', MATCH_WEBGL_TO_CANVAS_SIZE: 'false',
  AUTO_SYNC_PERSISTENT_DATA_PATH: 'true', DEVICE_PIXEL_RATIO: '1.5',
};
const internal = {
  USE_THREADS: false, USE_WASM: true, SYMBOLS_FILENAME: false, SHOW_DIAGNOSTICS: false,
  LOADER_FILENAME: 'sand.loader.js', DATA_FILENAME: 'sand.data',
  FRAMEWORK_FILENAME: 'sand.framework.js', CODE_FILENAME: 'sand.wasm',
  COMPANY_NAME: 'Sand', PRODUCT_NAME: 'Sand', PRODUCT_VERSION: '1.0',
};
function preprocess(values) {
  const args = [path.resolve(preprocessor), "locals.inputPath='unityApp.js'", "locals.outputPath='output.js'",
    ...Object.entries({...internal, ...values}).map(([key, value]) =>
      `${key}=${typeof value === 'string' ? `'${value}'` : value}`)];
  const result = spawnSync(process.execPath, args, {cwd: scratch, encoding: 'utf8'});
  assert.equal(result.status, 0, result.stderr);
  const output = fs.readFileSync(path.join(scratch, 'output.js'), 'utf8');
  assert.ok(!output.includes('{{{'), 'All macros must be expanded');
  const syntax = spawnSync(process.execPath, ['--check', 'output.js'], {cwd: scratch, encoding: 'utf8'});
  assert.equal(syntax.status, 0, syntax.stderr);
  return output;
}
try {
  fs.copyFileSync(path.join(template, 'unityApp.js'), path.join(scratch, 'unityApp.js'));
  for (const key of Object.keys(optional)) {
    assert.ok(Object.hasOwn(tags, key), `PlayerSettings is missing ${key}`);
  }
  preprocess(tags);
  console.log('PASS: original template preprocesses with project custom settings');
  const output = preprocess(optional);
  for (const value of Object.values(optional)) {
    assert.ok(output.includes(`"${value}"`), `Configured value ${value} was lost`);
  }
  console.log('PASS: configured custom settings survive preprocessing; generated JavaScript parses');
} finally {
  assert.equal(path.dirname(path.resolve(scratch)), path.resolve(os.tmpdir()));
  assert.ok(path.basename(scratch).startsWith('sandWebGL'));
  fs.rmSync(scratch, {recursive: true, force: true});
}
