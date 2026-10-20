import { spawnSync } from 'node:child_process';
import fs from 'node:fs';

const metadata = spawnSync('cargo', ['metadata', '--manifest-path', 'Lib/radish.lib/Cargo.toml', '--locked', '--format-version', '1'],
  { encoding: 'utf8', maxBuffer: 8 * 1024 * 1024 });
if (metadata.error || metadata.status !== 0) throw new Error('Cannot read locked native dependency graph');
const packages = JSON.parse(metadata.stdout).packages.filter(pkg => pkg.source?.startsWith('registry+'));
async function query(endpoint, body) {
  const response = await fetch(`https://api.osv.dev/v1/${endpoint}`, {
    method: body ? 'POST' : 'GET', headers: body ? { 'Content-Type': 'application/json' } : {},
    body: body ? JSON.stringify(body) : undefined, signal: AbortSignal.timeout(60000),
  });
  if (!response.ok) throw new Error(`OSV request failed (${response.status})`);
  return response.json();
}
const report = await query('querybatch', { queries: packages.map(pkg =>
  ({ package: { name: pkg.name, ecosystem: 'crates.io' }, version: pkg.version })) });
if (!Array.isArray(report.results) || report.results.length !== packages.length) throw new Error('OSV result count mismatch');
const findings = [];
for (let i = 0; i < packages.length; i++) {
  for (const finding of report.results[i].vulns ?? []) {
    const advisory = await query(`vulns/${encodeURIComponent(finding.id)}`);
    if (advisory.withdrawn) continue;
    const affected = advisory.affected?.find(item => item.package?.name === packages[i].name);
    const informational = affected?.database_specific?.informational;
    findings.push({ package: packages[i].name, version: packages[i].version, id: advisory.id,
      summary: advisory.summary, informational: informational ?? null,
      url: `https://osv.dev/vulnerability/${advisory.id}` });
  }
}
fs.mkdirSync('artifacts', { recursive: true });
fs.writeFileSync('artifacts/native-security-audit.json', `${JSON.stringify({ checkedAt: new Date().toISOString(),
  packages: packages.length, source: 'OSV / crates.io', findings,
  boundary: 'Vendored C components also require version/source review; this query covers Rust advisories.' }, null, 2)}\n`);
for (const finding of findings) console.log(`[native-security] ${finding.informational ?? 'VULNERABILITY'}: ${finding.package}@${finding.version} ${finding.id}`);
if (findings.some(finding => !finding.informational)) process.exit(1);
console.log(`[native-security] ${packages.length} locked packages checked; ${findings.length} informational advisories`);
