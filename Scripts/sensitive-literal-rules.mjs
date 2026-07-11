const compactJwtPattern = new RegExp(
  String.raw`\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{20,}\b`,
  'gu'
);

const literalBearerPattern = new RegExp(
  String.raw`\bBearer[ \t]+(?!\$|\{|<|\(|invalid_token\b|example\b|your[_-]?token\b)[A-Za-z0-9._~+/-]{24,}`,
  'giu'
);

const privateKeyHeaderPattern = new RegExp(
  ['-----BEGIN ', '(?:RSA |EC |DSA |OPENSSH )?', 'PRIVATE KEY-----'].join(''),
  'gu'
);

export const SENSITIVE_LITERAL_RULES = [
  {
    id: 'compact-jwt',
    description: '完整 JWT 字面量',
    pattern: compactJwtPattern,
  },
  {
    id: 'literal-bearer-token',
    description: '硬编码 Bearer Token',
    pattern: literalBearerPattern,
  },
  {
    id: 'private-key-header',
    description: '私钥 PEM 头',
    pattern: privateKeyHeaderPattern,
  },
];

export function findSensitiveLiteralMatches(filePath, content) {
  const matches = [];
  const lines = content.split(/\r?\n/u);

  for (let lineIndex = 0; lineIndex < lines.length; lineIndex += 1) {
    const line = lines[lineIndex];

    for (const rule of SENSITIVE_LITERAL_RULES) {
      rule.pattern.lastIndex = 0;
      if (rule.pattern.test(line)) {
        matches.push({
          filePath,
          lineNumber: lineIndex + 1,
          ruleId: rule.id,
          description: rule.description,
        });
      }
    }
  }

  return matches;
}
