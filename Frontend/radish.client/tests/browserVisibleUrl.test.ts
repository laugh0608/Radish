import assert from 'node:assert/strict';
import test from 'node:test';
import { normalizeBrowserVisibleUrl } from '../src/utils/browserVisibleUrl.ts';

test('normalizeBrowserVisibleUrl 应把 HTTPS Gateway 下的本地 HTTP 资源收口到当前 origin', () => {
  assert.equal(
    normalizeBrowserVisibleUrl('http://localhost:5100/uploads/DefaultIco/bailuobo.ico', 'https://localhost:5000'),
    'https://localhost:5000/uploads/DefaultIco/bailuobo.ico'
  );

  assert.equal(
    normalizeBrowserVisibleUrl('http://127.0.0.1:5200/Account/Login?returnUrl=%2F', 'https://localhost:5000'),
    'https://localhost:5000/Account/Login?returnUrl=%2F'
  );
});

test('normalizeBrowserVisibleUrl 不应改写公网、非 HTTPS 当前入口或非本地资源', () => {
  assert.equal(
    normalizeBrowserVisibleUrl('https://cdn.example.com/a.png', 'https://localhost:5000'),
    'https://cdn.example.com/a.png'
  );
  assert.equal(
    normalizeBrowserVisibleUrl('http://api.example.com/a.png', 'https://localhost:5000'),
    'http://api.example.com/a.png'
  );
  assert.equal(
    normalizeBrowserVisibleUrl('http://localhost:5100/a.png', 'http://localhost:3000'),
    'http://localhost:5100/a.png'
  );
});
