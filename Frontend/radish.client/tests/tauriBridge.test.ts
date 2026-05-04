import { describe, it } from 'node:test';
import assert from 'node:assert/strict';

import { rewriteRadishDeepLinkToBrowserPath } from '../src/platform/tauriBridge.ts';

describe('tauriBridge', () => {
  it('rewrites OIDC callback deep links to the existing browser callback route', () => {
    const path = rewriteRadishDeepLinkToBrowserPath(
      'radish://oidc/callback?code=abc&state=xyz#done',
    );

    assert.equal(path, '/oidc/callback?code=abc&state=xyz#done');
  });

  it('rewrites logout completion deep links to the shell root', () => {
    const path = rewriteRadishDeepLinkToBrowserPath('radish://oidc/logout-complete');

    assert.equal(path, '/');
  });

  it('ignores unrelated URLs', () => {
    assert.equal(rewriteRadishDeepLinkToBrowserPath('https://radishx.com/docs'), null);
    assert.equal(rewriteRadishDeepLinkToBrowserPath('radish://forum/post/1'), null);
    assert.equal(rewriteRadishDeepLinkToBrowserPath('not a url'), null);
  });
});
