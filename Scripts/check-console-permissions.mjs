import { readFileSync, readdirSync } from 'node:fs';
import { dirname, extname, join, resolve, relative } from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(scriptDir, '..');

const frontendPermissionsPath = join(repoRoot, 'Frontend/radish.console/src/constants/permissions.ts');
const routeMetaPath = join(repoRoot, 'Frontend/radish.console/src/router/routeMeta.ts');
const consoleSourceRoot = join(repoRoot, 'Frontend/radish.console/src');
const backendPermissionsPath = join(repoRoot, 'Radish.Common/PermissionTool/ConsolePermissions.cs');
const identitySeederPath = join(repoRoot, 'Radish.DbMigrate/InitialDataSeeder.Identity.cs');

function readText(filePath) {
  return readFileSync(filePath, 'utf8');
}

function getAllSourceFiles(rootPath) {
  const files = [];
  const stack = [rootPath];

  while (stack.length > 0) {
    const current = stack.pop();
    if (!current) {
      continue;
    }

    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const fullPath = join(current, entry.name);
      if (entry.isDirectory()) {
        stack.push(fullPath);
        continue;
      }

      const extension = extname(entry.name);
      if (extension === '.ts' || extension === '.tsx') {
        files.push(fullPath);
      }
    }
  }

  return files.sort();
}

function unique(values) {
  return [...new Set(values)];
}

function sortStrings(values) {
  return [...values].sort((left, right) => left.localeCompare(right, 'zh-CN'));
}

function difference(leftValues, rightValues) {
  const rightSet = new Set(rightValues);
  return leftValues.filter((value) => !rightSet.has(value));
}

function parseFrontendPermissions(fileContent) {
  const objectMatch = fileContent.match(/export const CONSOLE_PERMISSIONS = \{([\s\S]*?)\} as const;/);
  if (!objectMatch) {
    throw new Error('未找到前端 CONSOLE_PERMISSIONS 定义。');
  }

  const permissions = new Map();
  const entryPattern = /^\s*([A-Za-z0-9]+):\s*'([^']+)',?$/gm;

  for (const match of objectMatch[1].matchAll(entryPattern)) {
    permissions.set(match[1], match[2]);
  }

  return permissions;
}

function parseRouteMeta(fileContent, frontendPermissions) {
  const arrayMatch = fileContent.match(/export const consoleRouteMeta:[\s\S]*?= \[([\s\S]*?)\] as const;/);
  if (!arrayMatch) {
    throw new Error('未找到 consoleRouteMeta 定义。');
  }

  const routes = [];
  const objectPattern = /\{([\s\S]*?)\}/g;

  for (const match of arrayMatch[1].matchAll(objectPattern)) {
    const block = match[1];
    const key = block.match(/key:\s*'([^']+)'/)?.[1];
    const path = block.match(/path:\s*'([^']+)'/)?.[1];
    const permissionKey = block.match(/requiredPermission:\s*CONSOLE_PERMISSIONS\.([A-Za-z0-9]+)/)?.[1];
    const authOnly = /authOnly:\s*true/.test(block);

    if (!key || !path) {
      continue;
    }

    routes.push({
      key,
      path,
      authOnly,
      permissionKey,
      permissionValue: permissionKey ? frontendPermissions.get(permissionKey) : undefined,
    });
  }

  return routes;
}

function parseBackendPermissions(fileContent) {
  const permissions = new Map();
  const constPattern = /public const string ([A-Za-z0-9]+) = "([^"]+)";/g;

  for (const match of fileContent.matchAll(constPattern)) {
    permissions.set(match[1], match[2]);
  }

  return permissions;
}

function parseApiPermissionMappings(fileContent, backendPermissions) {
  const mappings = [];
  const mappingPattern = /\["([^"]+)"\]\s*=\s*new\[\]\s*\{([\s\S]*?)\}/g;

  for (const match of fileContent.matchAll(mappingPattern)) {
    const url = match[1];
    const permissionNames = match[2]
      .split(',')
      .map((item) => item.trim())
      .filter(Boolean);

    const unresolvedNames = permissionNames.filter((name) => !backendPermissions.has(name));
    const permissionValues = permissionNames
      .map((name) => backendPermissions.get(name))
      .filter((value) => typeof value === 'string');

    mappings.push({
      url,
      permissionNames,
      permissionValues,
      unresolvedNames,
    });
  }

  return mappings;
}

function parseSeedUrls(fileContent) {
  return unique([...fileContent.matchAll(/LinkUrl\s*=\s*"([^"]+)"/g)].map((match) => match[1]));
}

function parseUsePermissionReferences(filePaths, frontendPermissions) {
  const references = [];
  const pattern = /usePermission\(\s*CONSOLE_PERMISSIONS\.([A-Za-z0-9]+)\s*\)/g;

  for (const filePath of filePaths) {
    const relativePath = relative(repoRoot, filePath);
    const content = readText(filePath);

    for (const match of content.matchAll(pattern)) {
      const permissionKey = match[1];
      references.push({
        filePath: relativePath,
        permissionKey,
        permissionValue: frontendPermissions.get(permissionKey),
      });
    }
  }

  return references;
}

function printSection(title, values) {
  if (values.length <= 0) {
    return;
  }

  console.log(`\n${title}`);
  for (const value of values) {
    console.log(`- ${value}`);
  }
}

try {
  const frontendPermissions = parseFrontendPermissions(readText(frontendPermissionsPath));
  const backendPermissions = parseBackendPermissions(readText(backendPermissionsPath));
  const routes = parseRouteMeta(readText(routeMetaPath), frontendPermissions);
  const sourceFiles = getAllSourceFiles(consoleSourceRoot);
  const usePermissionReferences = parseUsePermissionReferences(sourceFiles, frontendPermissions);
  const mappings = parseApiPermissionMappings(readText(backendPermissionsPath), backendPermissions);
  const seedUrls = parseSeedUrls(readText(identitySeederPath));

  const frontendPermissionValues = unique([...frontendPermissions.values()]);
  const backendPermissionValues = unique([...backendPermissions.values()]);
  const routePermissionValues = unique(routes.filter((route) => !route.authOnly && route.permissionValue).map((route) => route.permissionValue));
  const referencedPermissionValues = unique([
    ...routePermissionValues,
    ...usePermissionReferences.map((reference) => reference.permissionValue).filter(Boolean),
  ]);
  const mappingPermissionValues = unique(mappings.flatMap((mapping) => mapping.permissionValues));
  const mappingUrls = unique(mappings.map((mapping) => mapping.url));
  const authOnlyPaths = routes.filter((route) => route.authOnly).map((route) => route.path);

  const errors = [];
  const warnings = [];

  const frontendOnlyPermissions = sortStrings(difference(frontendPermissionValues, backendPermissionValues));
  const backendOnlyPermissions = sortStrings(difference(backendPermissionValues, frontendPermissionValues));
  const missingSeedUrls = sortStrings(difference(mappingUrls, seedUrls));
  const referencedButNotMappedPermissions = sortStrings(difference(referencedPermissionValues, mappingPermissionValues));
  const mappedButNotFrontendPermissions = sortStrings(difference(mappingPermissionValues, frontendPermissionValues));
  const unusedFrontendPermissions = sortStrings(difference(frontendPermissionValues, referencedPermissionValues));

  const routesMissingPermission = routes
    .filter((route) => !route.authOnly && !route.permissionKey)
    .map((route) => `${route.path} 未声明 requiredPermission`);

  const routesWithUnknownPermission = routes
    .filter((route) => route.permissionKey && !route.permissionValue)
    .map((route) => `${route.path} 引用了未知前端权限键 CONSOLE_PERMISSIONS.${route.permissionKey}`);

  const usePermissionUnknownKeys = usePermissionReferences
    .filter((reference) => !reference.permissionValue)
    .map((reference) => `${reference.filePath} 引用了未知前端权限键 CONSOLE_PERMISSIONS.${reference.permissionKey}`);

  const unresolvedMappingPermissions = mappings.flatMap((mapping) =>
    mapping.unresolvedNames.map((name) => `${mapping.url} 引用了未知后端权限常量 ${name}`)
  );

  if (frontendOnlyPermissions.length > 0) {
    errors.push(`前端权限常量存在后端未定义项：${frontendOnlyPermissions.join('、')}`);
  }

  if (backendOnlyPermissions.length > 0) {
    errors.push(`后端权限常量存在前端未定义项：${backendOnlyPermissions.join('、')}`);
  }

  errors.push(...routesMissingPermission);
  errors.push(...routesWithUnknownPermission);
  errors.push(...usePermissionUnknownKeys);
  errors.push(...unresolvedMappingPermissions);

  if (mappedButNotFrontendPermissions.length > 0) {
    errors.push(`后端资源映射引用了前端不存在的权限值：${mappedButNotFrontendPermissions.join('、')}`);
  }

  if (referencedButNotMappedPermissions.length > 0) {
    errors.push(`前端已引用但后端资源映射未覆盖的权限值：${referencedButNotMappedPermissions.join('、')}`);
  }

  if (missingSeedUrls.length > 0) {
    errors.push(`后端资源映射缺少 DbMigrate 种子 LinkUrl：${missingSeedUrls.join('、')}`);
  }

  if (unusedFrontendPermissions.length > 0) {
    warnings.push(`前端权限常量当前未被 routeMeta 或 usePermission 引用：${unusedFrontendPermissions.join('、')}`);
  }

  console.log('[Console 权限扫描]');
  console.log(`- 路由元数据：${routes.length} 条（authOnly ${authOnlyPaths.length} 条）`);
  console.log(`- 前端权限常量：${frontendPermissionValues.length} 项`);
  console.log(`- 后端权限常量：${backendPermissionValues.length} 项`);
  console.log(`- 页面 usePermission 引用：${usePermissionReferences.length} 处，去重后 ${unique(usePermissionReferences.map((reference) => reference.permissionValue).filter(Boolean)).length} 项`);
  console.log(`- 后端资源映射：${mappingUrls.length} 条 URL，${mappingPermissionValues.length} 项权限值`);
  console.log(`- DbMigrate 种子 LinkUrl：${seedUrls.length} 条`);

  printSection('authOnly 路由', sortStrings(authOnlyPaths));
  printSection('警告', warnings);

  if (errors.length > 0) {
    printSection('错误', errors);
    process.exitCode = 1;
  } else {
    console.log('\n结果：通过。四层对象已完成基础对齐。');
  }
} catch (error) {
  console.error('[Console 权限扫描] 执行失败');
  console.error(error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
}
