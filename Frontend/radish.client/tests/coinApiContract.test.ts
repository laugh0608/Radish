import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const sourceRoot = path.resolve(testDirectory, '../src');
const coinApiSource = fs.readFileSync(path.join(sourceRoot, 'api/coin.ts'), 'utf8');
const paymentApiSource = fs.readFileSync(path.join(sourceRoot, 'api/paymentPassword.ts'), 'utf8');
const pitRoot = path.join(sourceRoot, 'apps/radish-pit');

function readSourceTree(directory: string): string {
  return fs.readdirSync(directory, { withFileTypes: true })
    .flatMap((entry) => {
      const file = path.join(directory, entry.name);
      if (entry.isDirectory()) return [readSourceTree(file)];
      return /\.(ts|tsx)$/.test(entry.name) ? [fs.readFileSync(file, 'utf8')] : [];
    })
    .join('\n');
}

const consumerSource = [
  readSourceTree(pitRoot),
  fs.readFileSync(path.join(sourceRoot, 'me/MeApp.tsx'), 'utf8'),
  fs.readFileSync(path.join(sourceRoot, 'me/MeAssetsPage.tsx'), 'utf8'),
  fs.readFileSync(path.join(sourceRoot, 'apps/profile/components/CoinWallet.tsx'), 'utf8'),
  fs.readFileSync(path.join(sourceRoot, 'apps/profile/components/CoinTransactionList.tsx'), 'utf8'),
  fs.readFileSync(path.join(sourceRoot, 'apps/profile/components/UserInfoCard.tsx'), 'utf8'),
  fs.readFileSync(path.join(sourceRoot, 'desktop/components/CoinBalance.tsx'), 'utf8'),
].join('\n');

test('当前萝卜与支付口令 API 应抛出结构化 ApiResponseError 并要求本地化 fallback', () => {
  for (const source of [coinApiSource, paymentApiSource]) {
    assert.match(source, /createApiResponseError/);
    assert.match(source, /t: TFunction/);
    assert.doesNotMatch(source, /throw new Error/);
  }

  assert.match(coinApiSource, /export type CoinAmount = LongId/);
  assert.match(coinApiSource, /voTransactionNo: string/);
  assert.match(coinApiSource, /getBalance\(t: TFunction\)/);
  assert.match(paymentApiSource, /getSecurityLogs\([\s\S]*t: TFunction/);
});

test('萝卜域实际消费者不得使用服务端展示字段参与控制或本地化', () => {
  assert.doesNotMatch(
    consumerSource,
    /voBalanceDisplay|voFrozenBalanceDisplay|voAmountDisplay|voFeeDisplay|voTransactionTypeDisplay|voStatusDisplay|voAction|voStrengthLevelDisplay|voLastUsedTimeDisplay|voLastModifiedTimeDisplay|voCreatedAtDisplay/,
  );
  assert.match(consumerSource, /formatTransactionType\([^,]+\.voTransactionType, t\)/);
  assert.match(consumerSource, /formatSecurityLogType\(logItem\.voType, t\)/);
});

test('Radish Pit 不得继续维护模拟通知中心或按错误消息识别口令升级', () => {
  assert.doesNotMatch(consumerSource, /useNotifications|NotificationItem|mockNotifications/);
  assert.doesNotMatch(consumerSource, /isPaymentPasscodeUpgradeRequiredError\(\{[^}]*message:/);
  assert.match(consumerSource, /voTransactionNo/);
});
