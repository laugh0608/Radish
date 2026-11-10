import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { selectMacToolchain, compileMacIdentity } from './sqlite-macos-toolchain.mjs';

async function fixture(action) {
  const root = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'radish-sdk-contract-')));
  const developer = path.join(root, 'Developer'); const bin = path.join(developer, 'Toolchains/bin'); const sdk = path.join(developer, 'SDKs/MacOSX.sdk');
  fs.mkdirSync(bin, { recursive: true }); fs.mkdirSync(sdk, { recursive: true });
  const compiler = path.join(bin, 'clang'); const cxx = path.join(bin, 'clang++'); fs.writeFileSync(compiler, 'compiler');
  // Exercise driver aliases even on Windows, where symlink privileges may be absent.
  if (process.platform === 'win32') fs.copyFileSync(compiler, cxx); else fs.symlinkSync('clang', cxx);
  const calls = [];
  const execution = { environment: {}, run: async (command, args) => {
    calls.push({ command, args, environment: { ...execution.environment } });
    if (command === 'xcode-select') return developer;
    assert.equal(execution.environment.DEVELOPER_DIR, developer);
    if (command === 'xcodebuild') return 'Xcode fixture';
    assert.equal(command, 'xcrun'); assert.deepEqual(args.slice(0, 2), ['--sdk', 'macosx']);
    return { '--show-sdk-path': sdk, '--show-sdk-version': '26.1', '--find': args[3] === 'clang' ? compiler : cxx }[args[2]];
  } };
  try { await action({ root, developer, bin, sdk, compiler, cxx, execution, calls }); }
  finally { fs.rmSync(root, { recursive: true, force: true }); }
}

test('selects one developer/SDK and preserves clang++ invocation instead of its realpath', async () => fixture(async ({ execution, sdk, compiler, cxx, calls }) => {
  const selected = await selectMacToolchain(execution);
  assert.equal(selected.compiler, compiler); assert.equal(selected.cxx, cxx);
  assert.equal(execution.environment.SDKROOT, sdk); assert.equal(execution.environment.MACOSX_DEPLOYMENT_TARGET, '13.0');
  assert.ok(calls.filter(call => call.command === 'xcrun').every(call => call.environment.DEVELOPER_DIR));
}));

test('missing SDK or compiler fails without selecting another toolchain', async () => {
  for (const missing of ['sdk', 'compiler']) await fixture(async ({ execution, sdk, compiler, calls }) => {
    fs.rmSync(missing === 'sdk' ? sdk : compiler, { recursive: true });
    await assert.rejects(selectMacToolchain(execution));
    assert.equal(calls.filter(call => call.command === 'xcode-select').length, 1);
  });
});

test('both compiler samples link with the selected sysroot and reject wrong architecture', async () => fixture(async ({ execution, root }) => {
  const selected = await selectMacToolchain(execution); const commands = [];
  let wrong = false;
  execution.run = async (command, args) => {
    commands.push({ command, args });
    const bytes = Buffer.alloc(8); bytes.writeUInt32LE(0xfeedfacf, 0); bytes.writeUInt32LE(wrong ? 0x0100000c : 0x01000007, 4);
    fs.writeFileSync(args.at(-1), bytes);
  };
  const samples = await compileMacIdentity(execution, root, selected, 'x64');
  assert.equal(samples.length, 2);
  assert.deepEqual(commands.map(call => call.command), [selected.compiler, selected.cxx]);
  assert.ok(commands.every(call => call.args.includes(selected.sdk.sdkPath) && call.args.includes('-isysroot')));
  assert.match(fs.readFileSync(samples[0].source, 'utf8'), /#include <stdio.h>/u);
  assert.match(fs.readFileSync(samples[1].source, 'utf8'), /std::cout/u);
  wrong = true; await assert.rejects(compileMacIdentity(execution, root, selected, 'x64'), /architecture/u);
}));
