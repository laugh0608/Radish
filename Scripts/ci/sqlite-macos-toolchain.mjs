import fs from 'node:fs';
import path from 'node:path';

const requireCondition = (condition, message) => { if (!condition) throw new Error(message); };

export async function selectMacToolchain(execution) {
  const developer = execution.environment.DEVELOPER_DIR || (await execution.run('xcode-select', ['-p'], 'developer-directory')).trim();
  requireCondition(path.isAbsolute(developer) && fs.statSync(developer).isDirectory(), 'Missing Developer Directory');
  const developerDirectory = fs.realpathSync(developer);
  execution.environment.DEVELOPER_DIR = developerDirectory;
  const xcrun = async (args, name) => (await execution.run('xcrun', ['--sdk', 'macosx', ...args], name)).trim();
  const sdkPath = fs.realpathSync(await xcrun(['--show-sdk-path'], 'macos-sdk'));
  requireCondition(fs.statSync(sdkPath).isDirectory() && sdkPath.startsWith(`${developerDirectory}${path.sep}`), 'SDK outside selected Developer Directory');
  const sdkVersion = await xcrun(['--show-sdk-version'], 'macos-sdk-version');
  requireCondition(/^\d+(?:\.\d+)+$/u.test(sdkVersion), 'Invalid macOS SDK version');
  const compiler = await xcrun(['--find', 'clang'], 'clang-path');
  const cxx = await xcrun(['--find', 'clang++'], 'clangxx-path');
  for (const [tool, name] of [[compiler, 'clang'], [cxx, 'clang++']]) {
    requireCondition(path.isAbsolute(tool) && path.basename(tool) === name && fs.statSync(tool).isFile()
      && fs.realpathSync(tool).startsWith(`${developerDirectory}${path.sep}`), `Invalid selected ${name} tool`);
  }
  requireCondition(path.dirname(compiler) === path.dirname(cxx), 'C/C++ toolchain mismatch');
  execution.environment.SDKROOT = sdkPath;
  execution.environment.MACOSX_DEPLOYMENT_TARGET = '13.0';
  const xcode = (await execution.run('xcodebuild', ['-version'], 'xcode-version')).trim();
  requireCondition(xcode.length > 0, 'Missing Xcode identity');
  return { compiler, cxx, sdk: { developerDirectory, sdkPath, sdkVersion, xcode, deploymentTarget: '13.0' } };
}

export async function compileMacIdentity(execution, directory, toolchain, architecture) {
  requireCondition(['x64', 'arm64'].includes(architecture), 'Unsupported macOS identity architecture');
  const outputs = [];
  for (const [language, compiler, contents] of [
    ['c', toolchain.compiler, '#include <stdio.h>\n#include <stdlib.h>\nint main(void) { char *p = getenv("PATH"); return puts(p ? p : "path"); }\n'],
    ['cpp', toolchain.cxx, '#include <iostream>\n#include <string>\nint main(int argc, char **argv) { std::string text(argc > 1 ? argv[1] : "identity"); std::cout << text << std::endl; return 0; }\n'],
  ]) {
    const source = path.join(directory, `identity.${language}`); const binary = path.join(directory, `identity-${language}`);
    fs.writeFileSync(source, contents);
    await execution.run(compiler, ['-isysroot', toolchain.sdk.sdkPath, '-mmacosx-version-min=13.0', '-arch', architecture === 'x64' ? 'x86_64' : 'arm64', source, '-o', binary], `compiler-identity-${language}`);
    const bytes = fs.readFileSync(binary);
    requireCondition(bytes.length >= 8 && bytes.readUInt32LE(0) === 0xfeedfacf
      && bytes.readUInt32LE(4) === (architecture === 'x64' ? 0x01000007 : 0x0100000c), 'Wrong macOS identity architecture');
    outputs.push({ language, source, binary, architecture, executed: false });
  }
  return outputs;
}
