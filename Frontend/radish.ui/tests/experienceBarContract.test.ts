import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const experienceBarSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/components/ExperienceBar/ExperienceBar.tsx'),
  'utf8',
);
const lineChartSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/components/Charts/LineChart.tsx'),
  'utf8',
);
const pieChartSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/components/Charts/PieChart.tsx'),
  'utf8',
);

test('ExperienceBar 应由宿主提供词元和 locale formatter', () => {
  assert.match(experienceBarSource, /presentation: ExperienceBarPresentation/);
  assert.match(experienceBarSource, /presentation\.formatNumber/);
  assert.match(experienceBarSource, /presentation\.formatPercentage/);
  assert.match(experienceBarSource, /presentation\.formatDateTime/);
  assert.doesNotMatch(
    experienceBarSource,
    /排名 #|当前进度|下一等级|还需 .*经验值|总经验值|经验值已冻结|原因：|上次升级：/,
  );
});

test('经验图表使用的共享组件应允许宿主格式化数字和百分比', () => {
  assert.match(lineChartSource, /valueFormatter\?: \(value: number\) => string/);
  assert.match(lineChartSource, /tickFormatter=\{valueFormatter\}/);
  assert.match(pieChartSource, /percentageFormatter\?: \(value: number\) => string/);
  assert.match(pieChartSource, /percentageFormatter\(ratio\)/);
});
