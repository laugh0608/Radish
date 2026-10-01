import { readFileSync } from 'node:fs';

const policyUrl = new URL('../../Radish.Common/LogTool/Contracts/runtime-log-policy.v1.json', import.meta.url);

function luaLiteral(value) {
  if (typeof value === 'string') {
    return `"${value.replace(/[\\"\x00-\x1f\x7f]/gu, (character) => `\\${character.charCodeAt(0).toString().padStart(3, '0')}`)}"`;
  }
  if (typeof value === 'boolean' || (typeof value === 'number' && Number.isFinite(value))) return String(value);
  if (Array.isArray(value)) return `{${value.map(luaLiteral).join(',')}}`;
  if (value && typeof value === 'object') return `{${Object.entries(value).map(([key, item]) => `[${luaLiteral(key)}]=${luaLiteral(item)}`).join(',')}}`;
  throw new Error('Unsupported logging policy value.');
}

/** 将唯一 JSON 策略作为数据注入 Lua；不复制另一套级别或属性白名单。 */
export function renderCollectorPolicy() {
  const policy = JSON.parse(readFileSync(policyUrl, 'utf8'));
  return `-- Generated from runtime-log-policy.v1.json. Do not edit.\nreturn ${luaLiteral(policy)}\n`;
}
