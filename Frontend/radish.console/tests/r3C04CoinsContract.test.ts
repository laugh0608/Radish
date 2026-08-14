import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  buildCoinAdminSearchParams,
  parseCoinAdminUrlState,
} from '../src/pages/Coins/coinAdminUrlState.ts';

const readSource = (relativePath: string) => readFileSync(
  new URL(`../${relativePath}`, import.meta.url),
  'utf8',
);

test('R3-C04-F Coins 查询与筛选应由 URL 权威状态驱动', () => {
  const state = parseCoinAdminUrlState(new URLSearchParams(
    'userId=2042219067430928384&transactionType=ADMIN_ADJUST&status=SUCCESS&businessType=Admin&businessId=2042219067430928385&pageIndex=3&pageSize=50&returnTo=%2Forders%3Fpage%3D2',
  ));

  assert.deepEqual(state, {
    userId: '2042219067430928384',
    transactionType: 'ADMIN_ADJUST',
    status: 'SUCCESS',
    businessType: 'Admin',
    businessId: '2042219067430928385',
    pageIndex: 3,
    pageSize: 50,
    returnTo: '/orders?page=2',
  });
  assert.equal(
    buildCoinAdminSearchParams(state).toString(),
    'userId=2042219067430928384&transactionType=ADMIN_ADJUST&status=SUCCESS&businessType=Admin&businessId=2042219067430928385&pageIndex=3&pageSize=50&returnTo=%2Forders%3Fpage%3D2',
  );

  const invalid = parseCoinAdminUrlState(new URLSearchParams(
    'userId=9.1&transactionType=UNKNOWN&pageIndex=0&pageSize=101',
  ));
  assert.equal(invalid.userId, undefined);
  assert.equal(invalid.transactionType, undefined);
  assert.equal(invalid.pageIndex, 1);
  assert.equal(invalid.pageSize, 10);
});

test('R3-C04-F 调账必须绑定权威余额版本与稳定幂等键', () => {
  const api = readSource('src/api/coinAdminApi.ts');
  const page = readSource('src/pages/Coins/CoinAdminPage.tsx');
  const dto = readSource('../../Radish.Model/DtoModels/CoinDto.cs');
  const service = readSource('../../Radish.Service/CoinService.AdminAdjustments.cs');

  assert.match(api, /expectedVersion: number;/);
  assert.match(api, /idempotencyKey: string;/);
  assert.match(dto, /class AdminAdjustBalanceDto[\s\S]*?ExpectedVersion[\s\S]*?IdempotencyKey/);
  assert.match(page, /userId: balance\.voUserId/);
  assert.match(page, /expectedVersion: balance\.voVersion/);
  assert.match(page, /idempotencyKey: idempotencyKey\.current/);
  assert.doesNotMatch(page, /name="userId"/);
  assert.match(page, /Modal\.confirm\(\{[\s\S]*?coins\.confirm\.target[\s\S]*?coins\.confirm\.amount/);
  assert.match(service, /\[UseTran\(Propagation = Propagation\.Required\)\][\s\S]*?public async Task<string> AdminAdjustBalanceAsync/);
  assert.match(service, /OperationIdempotencyOperationTypes\.CoinAdminAdjustment/);
  assert.match(service, /balance\.Version == expectedVersion/);
});

test('R3-C04-F 非权威状态应冻结写入并以同一流水快照承载 PC 与 Mobile', () => {
  const page = readSource('src/pages/Coins/CoinAdminPage.tsx');

  assert.match(page, /requestGeneration !== balanceRequestGeneration\.current/);
  assert.match(page, /requestGeneration !== transactionRequestGeneration\.current/);
  assert.match(page, /setTransactionState\('stale'\)/);
  assert.match(page, /setTransactionState\('unavailable'\)/);
  assert.match(page, /const actionsAreAuthoritative = balanceState === 'ready'/);
  assert.match(page, /window\.addEventListener\('beforeunload'/);
  assert.match(page, /<ConsoleResourceList/);
  assert.match(page, /console-resource-mobile-card coin-admin-mobile-transaction/);
  assert.match(page, /<BottomSheet/);
});
