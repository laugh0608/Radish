import { describe, it } from 'node:test';
import assert from 'node:assert/strict';

import {
  rewriteDesktopOidcReturnToBrowserPath,
  rewriteOidcLoopbackToBrowserPath,
  rewriteRadishDeepLinkToBrowserPath,
  TAURI_DESKTOP_ENTRY_PATH,
} from '../src/platform/tauriBridge.ts';

describe('tauriBridge', () => {
  it('rewrites OIDC callback deep links to the existing browser callback route', () => {
    const path = rewriteRadishDeepLinkToBrowserPath(
      'radish://oidc/callback?code=abc&state=xyz#done',
    );

    assert.equal(path, '/oidc/callback?code=abc&state=xyz#done');
  });

  it('rewrites logout completion deep links to the desktop entry', () => {
    const path = rewriteRadishDeepLinkToBrowserPath('radish://oidc/logout-complete');

    assert.equal(path, TAURI_DESKTOP_ENTRY_PATH);
  });

  it('ignores unrelated URLs', () => {
    assert.equal(rewriteRadishDeepLinkToBrowserPath('https://radishx.com/docs'), null);
    assert.equal(rewriteRadishDeepLinkToBrowserPath('radish://forum/post/1'), null);
    assert.equal(rewriteRadishDeepLinkToBrowserPath('not a url'), null);
  });

  it('rewrites OIDC loopback callbacks to the existing browser callback route', () => {
    const path = rewriteOidcLoopbackToBrowserPath(
      'http://127.0.0.1:48801/oidc/callback?code=abc&state=xyz',
    );

    assert.equal(path, '/oidc/callback?code=abc&state=xyz');
  });

  it('rewrites OIDC loopback logout completion to the desktop entry', () => {
    const path = rewriteOidcLoopbackToBrowserPath('http://127.0.0.1:48801/oidc/logout-complete');

    assert.equal(path, TAURI_DESKTOP_ENTRY_PATH);
  });

  it('supports both legacy deep link and loopback OIDC returns', () => {
    assert.equal(
      rewriteDesktopOidcReturnToBrowserPath('radish://oidc/callback?code=abc'),
      '/oidc/callback?code=abc',
    );
    assert.equal(
      rewriteDesktopOidcReturnToBrowserPath('http://localhost:48801/oidc/callback?code=abc'),
      '/oidc/callback?code=abc',
    );
  });
});
