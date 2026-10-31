import assert from 'node:assert/strict';
import fs from 'node:fs';
import { createHash } from 'node:crypto';
import { test } from 'node:test';
import yaml from 'js-yaml';

const read = name => fs.readFileSync(new URL(name, import.meta.url), 'utf8');
const contract = JSON.parse(read('inputs.json'));
const workflow = yaml.load(read('../../../.github/workflows/native-images.yml'));
const job = workflow.jobs['hangfire-windows-razor'];
const script = name => read(`${name}.ps1`);

test('Razor inputs are the reviewed eleven versions, including all archive/content hashes', () => {
  assert.equal(contract.packages.length, 11);assert.equal(new Set(contract.packages.map(item => `${item.package}/${item.version}`)).size, 11);
  for (const item of contract.packages) { assert.match(item.archiveSha256, /^[a-f0-9]{64}$/u);assert.equal(Buffer.from(item.contentHash, 'base64').length, 64);assert.ok(item.url.endsWith(`/${item.package}.${item.version}.nupkg`)); }
  assert.equal(contract.sdk.version, '10.0.403');assert.match(contract.sdk.sha512, /^[a-f0-9]{128}$/u);
});
test('generation inventory fixes all 23 template/output pairs and all 24 payload DLL hashes', () => {
  assert.equal(contract.pairs.length, 23);assert.equal(new Set(contract.pairs.map(pair => pair.input)).size, 23);
  for (const pair of contract.pairs) { assert.equal(pair.output, pair.input + '.cs');assert.notEqual(pair.lfSha256, pair.windowsCrlfSha256);assert.match(pair.gitBlob, /^[a-f0-9]{40}$/u); }
  assert.equal(contract.payload.filter(item => item.path.endsWith('.dll')).length, 24);
});
test('existing five native/SQLite jobs are structurally unchanged', () => {
  const baseline = JSON.parse(read('workflow-baseline.json'));
  for (const [name, expected] of Object.entries(baseline.jobs)) assert.equal(createHash('sha256').update(JSON.stringify(workflow.jobs[name])).digest('hex'), expected, name);
  assert.equal(Object.keys(workflow.jobs).length, 6);
  assert.equal(workflow.on.workflow_dispatch.inputs.validation_scope.default, 'native-images');
});
test('new job is a separate manual scope, uses reviewed actions, read permissions and no services', () => {
  assert.equal(job.if, "${{ github.event_name == 'workflow_dispatch' && inputs.validation_scope == 'hangfire-razor' }}");
  assert.equal(job['runs-on'], 'windows-2025');assert.equal(job['timeout-minutes'], 60);assert.deepEqual(workflow.permissions, { contents: 'read' });assert.equal(job.services, undefined);
  const uses = job.steps.filter(step => step.uses).map(step => step.uses);
  assert.deepEqual(uses, [`actions/checkout@${contract.checkoutSha}`, `actions/upload-artifact@${contract.uploadArtifactSha}`]);
  assert.equal(job.steps.find(step => step.id === 'checkout').with['persist-credentials'], false);
  assert.equal(job.steps.find(step => step.id === 'checkout').with.ref, '${{ github.sha }}');
  assert.ok(!/secrets\.|npm |rustup|docker |gh workflow|--push/u.test(JSON.stringify(job)));
});
test('inventory is default; approved commit and four tool identities gate verify', () => {
  assert.equal(workflow.on.workflow_dispatch.inputs.razor_mode.default, 'inventory');
  const entry = script('Invoke-Validation');assert.ok(entry.indexOf('Assert-ReviewedInventory') < entry.indexOf('Invoke-WindowsVerification $inventory'));
  assert.match(entry, /if \(\$Phase -eq 'Verify'\)/u);
  const inventory = script('Inventory');for (const field of ['imageVersion', 'visualStudio.version', 'msbuild.version', 'msbuild.sha256']) assert.ok(inventory.includes(field));
  assert.match(script('Common'), /ExpectedCommit -ceq \$env:GITHUB_SHA/u);
  assert.match(job.steps[0].run, /EXPECTED_HEAD_SHA -cne \$env:GITHUB_SHA/u);
});
test('failure cleanup still publishes reviewed evidence and never requires verification success', () => {
  const cleanup = job.steps.find(step => step.id === 'cleanup');assert.match(cleanup.if, /^always\(\)/u);
  const upload = job.steps.find(step => step.uses?.startsWith('actions/upload-artifact'));
  assert.equal(upload.if, "always() && steps.cleanup.outputs.publish_safe == 'true'");assert.equal(upload.with['retention-days'], 7);assert.equal(upload.with['if-no-files-found'], 'error');
  const code = script('Cleanup');assert.match(code, /Reparse|Assert-OwnedPath/u);assert.match(code, /result\.json/u);assert.match(code, /cleanup\.json/u);assert.match(code, /manifest\.json/u);assert.match(code, /\.json', '\.log', '\.xml', '\.cs', '\.patch', '\.binlog'/u);
  assert.ok(!code.includes("'.dll'"));assert.ok(!code.includes("'.pdb'"));
});
test('child processes are isolated from Actions tokens, have timeouts and verified PID cleanup', () => {
  const common = script('Common');assert.match(common, /\.Environment\.Clear\(\)/u);assert.match(common, /ArgumentList\.Add/u);assert.match(common, /WaitForExit\(\$TimeoutSeconds/u);assert.match(common, /creationTime/u);assert.match(common, /Process executable differs/u);
  assert.match(common, /DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'/u);assert.match(common, /ReparsePoint/u);
  const all = ['Common', 'Cleanup', 'Acquire', 'Sources', 'Verification', 'Inventory'].map(script).join('\n');assert.ok(!/taskkill|build-server shutdown|dotnet run|npm (?:install|ci)|Set-ExecutionPolicy|Set-ItemProperty|Start-Service/u.test(all));
});
test('generation calls the explicit CLR4 task and snapshots the loaded engine instead of using stale output', () => {
  const source = script('Sources');assert.match(source, /Razor\.Verification\.proj/u);assert.match(source, /\/t:PrecompileRazorFiles/u);assert.match(source, /AfterTargets="PrecompileRazorFiles"/u);assert.match(source, /Assert-RazorSnapshot/u);assert.match(source, /Loaded Razor assembly hash differs/u);
  const capture = read('CaptureRazorAssemblies.cs');assert.match(capture, /AppDomain\.CurrentDomain\.GetAssemblies/u);assert.match(capture, /Environment\.Version/u);
  assert.ok(!source.includes("'/t:GenerateRazorClasses'"));assert.ok(!read('Tools/Sources.cs').includes('NoWarn ='));
});
