// Exercise wasm-ld with the ABI of both C# imports, without rebuilding the game.
// Usage: node Tools/check_metrica_linkage.cjs <Unity Emscripten/llvm directory>
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const assert = require('node:assert/strict');
const {spawnSync} = require('node:child_process');
const root = path.resolve(__dirname, '..');
const llvm = process.argv[2];
assert.ok(llvm, 'Pass the Unity Emscripten/llvm directory');
const scratch = fs.mkdtempSync(path.join(os.tmpdir(), 'sandMetrica'));
function run(tool, args) {
  const result = spawnSync(path.join(llvm, tool + '.exe'), args, {cwd: scratch, encoding: 'utf8'});
  if (result.error) throw result.error;
  assert.equal(result.status, 0, result.stderr);
}
try {
  const imports = [
    ['Assets/Scripts/SDK/YourGamesAnalyticsPlatform.cs', 'InitSandMetrica'],
    ['Assets/PluginYourGames/Modules/Metrica/Scripts/Metrica_interface.cs', 'InitYandexMetrica'],
  ];
  for (const [index, [file, method]] of imports.entries()) {
    const source = fs.readFileSync(path.join(root, file), 'utf8');
    const declaration = source.match(new RegExp(`extern (void|bool|int) ${method}\\(string counterId\\)`));
    assert.ok(declaration, `Missing import in ${file}`);
    // IL2CPP marshals the plugin's C# bool return as a native int32_t.
    const type = declaration[1] === 'void' ? 'void' : 'int';
    fs.writeFileSync(path.join(scratch, `bridge${index}.c`),
      `extern ${type} InitYandexMetrica(char*);\nvoid call${index}(char* id) { InitYandexMetrica(id); }\n`);
    run('clang', ['--target=wasm32', '-c', `bridge${index}.c`, '-o', `bridge${index}.o`]);
  }
  run('wasm-ld', ['--no-entry', '--allow-undefined', '--fatal-warnings',
    '--export=call0', '--export=call1', 'bridge0.o', 'bridge1.o', '-o', 'bridge.wasm']);
  console.log('PASS: both InitYandexMetrica imports link with the same WebGL ABI');
} finally {
  assert.equal(path.dirname(path.resolve(scratch)), path.resolve(os.tmpdir()));
  assert.ok(path.basename(scratch).startsWith('sandMetrica'));
  fs.rmSync(scratch, {recursive: true, force: true});
}
