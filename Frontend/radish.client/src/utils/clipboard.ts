function copyWithTextarea(text: string): boolean {
  if (typeof document === 'undefined') {
    return false;
  }

  const textarea = document.createElement('textarea');
  textarea.value = text;
  textarea.style.position = 'fixed';
  textarea.style.left = '-9999px';
  textarea.style.top = '0';
  document.body.appendChild(textarea);
  textarea.focus();
  textarea.select();

  try {
    return document.execCommand('copy');
  } finally {
    document.body.removeChild(textarea);
  }
}

export async function copyToClipboard(text: string): Promise<void> {
  // textarea 复制必须尽量贴近用户点击的同步阶段执行；先尝试它可以覆盖
  // 本地调试、嵌入浏览器和权限策略导致 navigator.clipboard 异步拒绝的场景。
  if (copyWithTextarea(text)) {
    return;
  }

  if (typeof navigator !== 'undefined' && navigator.clipboard && navigator.clipboard.writeText) {
    await navigator.clipboard.writeText(text);
    return;
  }

  throw new Error('Clipboard is unavailable in the current environment.');
}
