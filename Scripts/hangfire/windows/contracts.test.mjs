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
  assert.equal(job['runs-on'], 'windows-2022');assert.equal(job['timeout-minutes'], 60);assert.deepEqual(workflow.permissions, { contents: 'read' });assert.equal(job.services, undefined);
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
  const all = ['Common', 'Cleanup', 'Acquire', 'Sources', 'Verification', 'Inventory', 'Contracts'].map(script).join('\n');assert.ok(!/taskkill|build-server shutdown|dotnet run|npm (?:install|ci)|Set-ExecutionPolicy|Set-ItemProperty|Start-Service/u.test(all));
});
test('generation calls the explicit CLR4 task and snapshots the loaded engine instead of using stale output', () => {
  const source = script('Sources');assert.match(source, /Razor\.Verification\.proj/u);assert.match(source, /\/t:PrecompileRazorFiles/u);assert.match(source, /AfterTargets="PrecompileRazorFiles"/u);assert.match(source, /Assert-RazorSnapshot/u);assert.match(source, /Loaded Razor assembly hash differs/u);
  const capture = read('CaptureRazorAssemblies.cs');assert.match(capture, /AppDomain\.CurrentDomain\.GetAssemblies/u);assert.match(capture, /Environment\.Version/u);
  assert.ok(!source.includes("'/t:GenerateRazorClasses'"));assert.ok(!read('Tools/Sources.cs').includes('NoWarn ='));
});

test('PowerShell command identifiers cannot collide with environment iteration variables', () => {
  const invoke = script('Common').split('function Invoke-OwnedCommand(')[1];
  assert.ok(invoke);
  assert.match(invoke, /\[string\]\$CommandName/u);
  assert.match(invoke, /foreach \(\$environmentKey in \$childEnv\.Keys\)/u);
  assert.match(invoke, /name = \$CommandName/u);
  assert.match(invoke, /Command failed: \$CommandName/u);
  assert.match(invoke, /Command timed out: \$CommandName/u);
  assert.ok(!/\$name\b/iu.test(invoke));
});

test('identity and incomplete discovery evidence precede every inventory acceptance gate', () => {
  const inventory = script('Inventory');
  const initial = inventory.split('function Read-VisualStudioInventory(')[0];
  for (const field of ['GITHUB_RUN_ID', 'GITHUB_RUN_ATTEMPT', 'GITHUB_SHA', 'GITHUB_WORKFLOW_SHA', 'GITHUB_WORKFLOW_REF', 'ImageOS', 'ImageVersion']) assert.ok(initial.includes(`$env:${field}`), field);
  assert.match(initial, /complete = \$false/u);
  assert.match(initial, /inventory-progress\.json/u);
  const selection = inventory.split('function Read-VisualStudioInventory(')[1].split('function Get-WindowsInventory(')[0];
  assert.ok(selection.indexOf('Write-Json $ProgressPath $Inventory') < selection.indexOf('ConvertFrom-Json'));
  assert.ok(selection.indexOf('$Inventory.vswhere.instances = $instances') < selection.indexOf('$instances.Count -eq 1'));
  assert.match(selection, /\$version -ge \[version\]'17\.0' -and \$version -lt \[version\]'18\.0'/u);
  const collect = inventory.split('function Get-WindowsInventory(')[1].split('function Assert-ReviewedInventory(')[0];
  assert.ok(collect.indexOf('Write-Json $progress $Inventory') < collect.indexOf('Test-Path -LiteralPath $vswhere'));
  assert.match(collect, /'-version', '\[17\.0,18\.0\)'/u);
  assert.match(collect, /MSBuild\/Current\/Bin\/amd64\/MSBuild\.exe/u);
  assert.ok(collect.indexOf("'inventory.json'") > collect.indexOf('$Inventory.frameworkRelease -ge 528040'));
  assert.match(collect, /\$completed = \$Inventory\.Clone\(\)/u);
  assert.match(collect, /catch \{\s*\$Inventory\.error = \$_\.Exception\.Message\s*Write-Json \$progress \$Inventory\s*throw/u);
  assert.match(inventory, /Assert-Condition \(\$Inventory\.complete -eq \$true\)/u);
});

test('CI entry runs owned PowerShell contracts after identity capture and before actual inventory', () => {
  const entry = script('Invoke-Validation');
  assert.match(entry, /\. "\$PSScriptRoot\/Contracts\.ps1"/u);
  const start = entry.indexOf('$progress = New-WindowsInventory');
  const check = entry.indexOf('Invoke-PowerShellContracts');
  const inventory = entry.indexOf('Get-WindowsInventory $progress');
  assert.ok(start > entry.indexOf("$script:Owner.mode = $Phase") && start < check && check < inventory);
  assert.match(entry, /finally \{\s*Save-Owner\s*Complete-OwnedCleanup/u);
  const step = job.steps.find(item => item.run?.includes('./Scripts/hangfire/windows/Invoke-Validation.ps1 -Phase $env:RAZOR_MODE'));
  assert.ok(step);assert.match(step.name, /PowerShell contracts/u);
  assert.equal(step.if, undefined);
  const contracts = script('Contracts');
  for (const name of ['command-success-name-and-environment', 'command-failure-name-and-exit-code', 'zero-vs', 'one-vs', 'two-vs', 'wrong-vs-major', 'invalid-json-evidence', 'incomplete-inventory-rejected-for-verify']) assert.ok(contracts.includes(`'${name}'`), name);
  assert.match(contracts, /fixtureOnly = \$true/u);
  assert.match(contracts, /Assert-OwnedPath \(Join-Path \$script:Work 'powershell-contracts'\)/u);
  assert.match(contracts, /Remove-Owned \$fixtureRoot/u);
  assert.match(contracts, /finally \{ Write-Json \$reportPath \$report \}/u);
  assert.ok(!/Invoke-WindowsVerification|Initialize-VerificationInputs|Invoke-RazorGeneration|Get-WindowsInventory|Invoke-WebRequest|Install-Module/u.test(contracts));
});
