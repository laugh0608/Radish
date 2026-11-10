# Windows runner image 上游询证草稿

> 2026-10-31（Asia/Shanghai）。**已获项目所有者批准并提交为 [actions/runner-images#15201](https://github.com/actions/runner-images/issues/15201)。** 以下保留已审阅的英文标题和正文；实际提交仅在正文末尾追加证据 ZIP 链接，按 Bug Report 字段组织。[中文审阅说明与下一步方案](./hangfire-windows-runner-image-inquiry-review-2026-10-31.md)；事实基线为[已提交的诊断记录](./hangfire-windows-razor-inventory-diagnostic-2026-10-31.md)。

## 拟提交标题

`[Windows Server 2022] Image files report 20261025.346.1 but Machine/Process ImageVersion report 20261026.347.1`

## 拟提交正文（自下方 Description 起）

### Description

On a GitHub-hosted standard `windows-2022` runner, the startup log and both image metadata files report `20261025.346.1`, while the machine and process `ImageVersion` environment variables report `20261026.347.1`. The user-scoped variables are unset.

We are asking which image revision was actually provisioned for the run below, why these identity sources disagree, and whether this is a known image metadata or provisioning issue. We are not assuming either version is authoritative.

Our diagnostic job deliberately fails when these sources disagree. Its 20 PowerShell contract checks passed and tool inventory collection completed before that assertion failed. This is not a report of a Razor, MSBuild compilation, or application runtime failure: no upstream build task was executed.

### Platforms affected

- [x] GitHub Actions - Standard Runners
- [ ] GitHub Actions - Larger Runners
- [ ] Azure DevOps

### Runner images affected

- [x] Windows Server 2022

### Image version and build link

- [Diagnostic run 41193259788, attempt 1](https://github.com/laugh0608/Radish/actions/runs/41193259788)
- [Windows job 124514190507](https://github.com/laugh0608/Radish/actions/runs/41193259788/job/124514190507)
- Event: `workflow_dispatch`; runner label: `windows-2022`; run created at `2026-10-31T10:33:49Z`.
- Repository and workflow commit: `75a9e33f8a3d1856d1fa0ffaa7f96c091824d456`.
- Runner version: `2.338.2`; OS reported in the startup log: Windows Server 2022 Datacenter, `10.0.20348`.
- Provisioner: `20261021.618`, commit `21505ad2541d5be6135ed84fc33697146cf75b8e`; region: `westus3`, cloud: Azure.
- Provisioner Worker ID from the public job log: `{a4edfe5c-573f-42e9-b585-d79966125ca8}`.

| Source | Observed ImageVersion | Image identity |
| --- | --- | --- |
| Job startup “Runner Image” section | `20261025.346.1` | windows-2022 |
| `C:\actions-runner\cached\2.338.2\.setup_info` | `20261025.346.1` | windows-2022 |
| `C:\imagedata.json` | `20261025.346.1` | windows-2022 |
| `[Environment]::GetEnvironmentVariable('ImageVersion', 'Machine')` | `20261026.347.1` | ImageOS = win22 |
| `[Environment]::GetEnvironmentVariable('ImageVersion', 'Process')` | `20261026.347.1` | ImageOS = win22 |
| `[Environment]::GetEnvironmentVariable('ImageVersion', 'User')` | null | ImageOS also null |

The four image lines in the startup log match the selected `Runner Image` detail in both files exactly. The software and release links in all three sources point to `win22/20261025.346`.

| File | LastWriteTimeUtc observed | Bytes | SHA256 of source bytes |
| --- | --- | --- | --- |
| `.setup_info` | `2026-10-31T10:33:51.9126576Z` | 776 | `e4eb6bfd3794c182cd29565152665e956e90551a2f3070b6e1963d7589cbeb36` |
| `C:\imagedata.json` | `2026-10-25T21:42:20.3679383Z` | 426 | `c2ec31491057e5d8b58218369093a3240d73ad1d15269ae74e943d00fad1eda0` |

Files were sampled at `2026-10-31T10:34:06Z`. We retained source-byte hashes and only the image identity fields, not the full source files or internal setup groups. File timestamps do not by themselves prove a build or provisioning sequence.

Selected tool observations: VS Enterprise 2022 `17.14.37824.6`; PowerShell `7.6.9`; Full amd64 MSBuild version and FileVersion `17.14.60.43110`, executable SHA256 `7da5f86d7dfa627c4071edc9563fda7ace7ec0aaadd8b2aaccb92d2a823535c0`. These observations do not establish the identity of the entire installed image.

### Is it regression?

Unknown: we do not have a verified last-known-good run for consistency across all these sources.

An [earlier run](https://github.com/laugh0608/Radish/actions/runs/41191661741) also had the startup-log/environment discrepancy but did not capture the two files and environment scopes. It succeeded before this consistency assertion was added; it must not be treated as a known-good image identity or as proof of the same underlying cause.

### Expected behavior

The image metadata files, startup image information and `ImageVersion` should identify the same deployed image revision, or their different meanings and an authoritative mapping should be documented.

The public Windows [build template](https://github.com/actions/runner-images/blob/36de032a97840ee900185b6168c9d63983e8db5a/images/windows/templates/build.windows-2022.pkr.hcl#L63) passes the same `IMAGE_VERSION` input to the image-data and system-environment setup scripts. We use that as background for the question, not proof of the actual provisioning sequence for this VM.

### Actual behavior

The two file sources agree with each other and with the startup log, while Machine and Process agree on a different version. User overrides are absent. Our diagnostic report contains exactly these two issues:

```text
files.setupInfo.versionMismatch
files.imageData.versionMismatch
```

The final failure is our explicit `Consistent image identity required; inspect image-identity.json` assertion. Cleanup and evidence upload completed successfully.

### Repro steps

This describes the already-executed workflow, not a newly executed minimal reproducer. We cannot guarantee that a newly scheduled hosted runner will reproduce it.

1. The pinned [workflow](https://github.com/laugh0608/Radish/blob/75a9e33f8a3d1856d1fa0ffaa7f96c091824d456/.github/workflows/native-images.yml) ran with `validation_scope=hangfire-razor`, `razor_mode=inventory`, and `expected_head_sha=75a9e33f8a3d1856d1fa0ffaa7f96c091824d456`. Only its Windows Razor job ran; the other five jobs were skipped.
2. After checkout, the [collector](https://github.com/laugh0608/Radish/blob/75a9e33f8a3d1856d1fa0ffaa7f96c091824d456/Scripts/hangfire/windows/ImageIdentity.ps1) read only `ImageOS` / `ImageVersion` at Process, Machine and User scope, located `.setup_info` through the actual `Runner.Worker.exe` ancestor, and read `C:\imagedata.json`.
3. It bounded file reads, retained hashes and whitelisted image fields, and compared the sources. This happened before the contract fixtures and tool discovery. The workflow does not assign `ImageVersion` or modify these source files, registry values or profiles.
4. Contract checks and VS / MSBuild inventory completed, then the consistency assertion rejected the conflicting identity. No SDK, package feed or upstream source download, Razor generation, application startup or machine-wide tool installation was performed.

### Questions for triage

1. Can you identify the actual deployed image revision for this run and Worker ID, and confirm which software manifest applies?
2. Is disagreement between the persisted image-data files and Machine `ImageVersion` a known issue? Can you explain which metadata is stale or incorrect, rather than only recommending that one source be ignored?
3. If this is being fixed, which image release or deployment should contain the fix, and what metadata can a fresh diagnostic run use to verify it?
4. If these sources intentionally describe different revisions, where is the supported mapping documented? If investigation belongs to the hosted-runner provisioning team, could you route this report accordingly?

### Evidence

- [Original Actions artifact](https://github.com/laugh0608/Radish/actions/runs/41193259788/artifacts/13039112970), `hangfire-razor-41193259788-1`; expires at `2026-11-07T10:34:11Z` according to the artifact API. Download access may require GitHub sign-in. A local verified copy has been retained.
- Original artifact ZIP SHA256: `1fea6d5f479e9b1b7f8d7c283a7870df7b5a9b5cfc10bd59df73bb8b4dd224b6`.
- The prepared attachment `windows-2022-image-identity-evidence-41193259788.zip` is a separately identified subset: unchanged `image-identity.json`, contract / result / cleanup reports, a startup-log excerpt, and a manifest explaining provenance. It is not the original artifact and does not include full runner configuration, credentials or a complete environment dump.
- The original report says `startupLogCompared=false` because the collector did not read Actions logs. A subsequent archive review compared the startup image lines with both selected file details and found them identical; the original report was not modified.

We have not retried this diagnostic run or proceeded to build verification with either image version.
