import assert from 'node:assert/strict';
import test from 'node:test';
import { copyToClipboard } from '../src/utils/clipboard.ts';

interface ClipboardDocumentMock {
  appended: unknown[];
  copiedText: string | null;
  createElement: (tagName: string) => unknown;
  execCommand: (command: string) => boolean;
  body: {
    appendChild: (element: unknown) => void;
    removeChild: (element: unknown) => void;
  };
}

function setGlobalValue(name: 'document' | 'navigator', value: unknown): void {
  Object.defineProperty(globalThis, name, {
    configurable: true,
    value,
  });
}

function restoreGlobalValue(name: 'document' | 'navigator', descriptor: PropertyDescriptor | undefined): void {
  if (descriptor) {
    Object.defineProperty(globalThis, name, descriptor);
    return;
  }

  Reflect.deleteProperty(globalThis, name);
}

function createDocumentMock(copyResult: boolean): ClipboardDocumentMock {
  const mock: ClipboardDocumentMock = {
    appended: [],
    copiedText: null,
    createElement: () => ({
      focus: () => undefined,
      select: () => undefined,
      style: {},
      value: '',
    }),
    execCommand: () => copyResult,
    body: {
      appendChild: (element) => {
        mock.appended.push(element);
        mock.copiedText = String((element as { value?: string }).value ?? '');
      },
      removeChild: (element) => {
        mock.appended = mock.appended.filter((item) => item !== element);
      },
    },
  };
  return mock;
}

test('copyToClipboard 应优先使用同步 textarea 复制', async () => {
  const documentDescriptor = Object.getOwnPropertyDescriptor(globalThis, 'document');
  const navigatorDescriptor = Object.getOwnPropertyDescriptor(globalThis, 'navigator');
  const documentMock = createDocumentMock(true);
  let clipboardWriteCount = 0;

  setGlobalValue('document', documentMock);
  setGlobalValue('navigator', {
    clipboard: {
      writeText: async () => {
        clipboardWriteCount += 1;
      },
    },
  });

  try {
    await copyToClipboard('https://radishx.com/u/20001');

    assert.equal(documentMock.copiedText, 'https://radishx.com/u/20001');
    assert.equal(documentMock.appended.length, 0);
    assert.equal(clipboardWriteCount, 0);
  } finally {
    restoreGlobalValue('document', documentDescriptor);
    restoreGlobalValue('navigator', navigatorDescriptor);
  }
});

test('copyToClipboard 应在 textarea 不可用时回退到 navigator.clipboard', async () => {
  const documentDescriptor = Object.getOwnPropertyDescriptor(globalThis, 'document');
  const navigatorDescriptor = Object.getOwnPropertyDescriptor(globalThis, 'navigator');
  const documentMock = createDocumentMock(false);
  let clipboardText = '';

  setGlobalValue('document', documentMock);
  setGlobalValue('navigator', {
    clipboard: {
      writeText: async (text: string) => {
        clipboardText = text;
      },
    },
  });

  try {
    await copyToClipboard('https://radishx.com/forum/post/1');

    assert.equal(documentMock.copiedText, 'https://radishx.com/forum/post/1');
    assert.equal(documentMock.appended.length, 0);
    assert.equal(clipboardText, 'https://radishx.com/forum/post/1');
  } finally {
    restoreGlobalValue('document', documentDescriptor);
    restoreGlobalValue('navigator', navigatorDescriptor);
  }
});
