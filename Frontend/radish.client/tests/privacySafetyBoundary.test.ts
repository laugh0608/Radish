import assert from 'node:assert/strict';
import test from 'node:test';
import {
  privacyBoundaryAudienceOrder,
  privacyBoundaryEntries,
  safetyResponseEntries,
} from '../src/privacy/privacySafetyBoundaryData.ts';

test('privacySafetyBoundary 应覆盖正式版隐私可见边界', () => {
  assert.deepEqual(privacyBoundaryAudienceOrder, ['public', 'private', 'console', 'restricted']);
  assert.equal(privacyBoundaryEntries.length, 4);

  const audiences = new Set(privacyBoundaryEntries.map((entry) => entry.audience));
  for (const audience of privacyBoundaryAudienceOrder) {
    assert.equal(audiences.has(audience), true);
  }

  for (const entry of privacyBoundaryEntries) {
    assert.ok(entry.id);
    assert.ok(entry.icon.startsWith('mdi:'));
    assert.ok(entry.titleKey.startsWith('privacySafety.boundary.'));
    assert.ok(entry.descriptionKey.startsWith('privacySafety.boundary.'));
    assert.ok(entry.exampleKeys.length >= 3);
  }
});

test('privacySafetyBoundary 应覆盖反骚扰与隐私泄露响应路径', () => {
  assert.deepEqual(
    safetyResponseEntries.map((entry) => entry.id),
    ['report-context', 'preserve-evidence', 'account-assets', 'urgent-risk'],
  );

  for (const entry of safetyResponseEntries) {
    assert.ok(entry.icon.startsWith('mdi:'));
    assert.ok(entry.titleKey.startsWith('privacySafety.response.'));
    assert.ok(entry.descriptionKey.startsWith('privacySafety.response.'));
  }
});
