import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDir = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDir, '..');

function readLocaleResources(): string {
  const domainNames = ['core', 'shell', 'discover', 'community', 'account', 'commerce', 'docs'];
  return ['en', 'zh']
    .flatMap((language) => domainNames.map((domain) =>
      readFileSync(resolve(clientRoot, `src/locales/${language}/${domain}.ts`), 'utf8')))
    .join('\n');
}

function readTypeScriptSources(directory: string, prefix = ''): Array<{ path: string; source: string }> {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const entryPath = resolve(directory, entry.name);
    const relativePath = prefix ? `${prefix}/${entry.name}` : entry.name;
    if (entry.isDirectory()) {
      return readTypeScriptSources(entryPath, relativePath);
    }

    return /\.tsx?$/.test(entry.name)
      ? [{ path: relativePath, source: readFileSync(entryPath, 'utf8') }]
      : [];
  });
}

test('robots.txt 应开放公开入口并指向公开 sitemap', () => {
  const robots = readFileSync(resolve(clientRoot, 'public/robots.txt'), 'utf8');

  assert.match(robots, /^User-agent: \*/m);
  assert.match(robots, /^Allow: \/discover$/m);
  assert.match(robots, /^Allow: \/forum$/m);
  assert.match(robots, /^Allow: \/docs$/m);
  assert.match(robots, /^Allow: \/u\/$/m);
  assert.match(robots, /^Disallow: \/connect\/$/m);
  assert.match(robots, /^Sitemap: https:\/\/radishx\.com\/sitemap\.xml$/m);
});

test('sitemap.xml 应提供第一批公开入口 seed', () => {
  const sitemap = readFileSync(resolve(clientRoot, 'public/sitemap.xml'), 'utf8');

  assert.match(sitemap, /<urlset xmlns="http:\/\/www\.sitemaps\.org\/schemas\/sitemap\/0\.9">/);
  assert.match(sitemap, /<loc>https:\/\/radishx\.com\/discover<\/loc>/);
  assert.match(sitemap, /<loc>https:\/\/radishx\.com\/forum<\/loc>/);
  assert.match(sitemap, /<loc>https:\/\/radishx\.com\/docs<\/loc>/);
  assert.match(sitemap, /<loc>https:\/\/radishx\.com\/shop\/products<\/loc>/);
});

test('公开商城详情购买入口应指向正式 Web 购买回流路径', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/shop/PublicShopApp.tsx'), 'utf8');
  const detailSource = readFileSync(resolve(clientRoot, 'src/public/shop/PublicShopViews.tsx'), 'utf8');

  assert.match(source, /buildShopProductPurchaseReturnPath/);
  assert.match(source, /const purchaseReturnPath = buildShopProductPurchaseReturnPath\(selectedProduct\.voId\);/);
  assert.match(detailSource, /href=\{purchaseReturnPath\}/);
  assert.match(source, /redirectToLogin\(\{ returnPath \}\)/);
  assert.match(source, /buildShopOrderReturnPath\(result\.data\.orderId\) \?\? buildShopOrdersReturnPath\(\)/);
  assert.match(source, /<PurchaseModal/);
  assert.doesNotMatch(source, /buildDesktopShopProductReturnPath/);
  assert.doesNotMatch(source, /desktopProductEntryUrl/);
  assert.doesNotMatch(source, /className=\{styles\.primaryLink\} href="\/"/);
});

test('公开商品榜单文案应指向商品详情购买而不是阻断购买能力', () => {
  const source = readLocaleResources();

  assert.match(
    source,
    /'leaderboard\.public\.productGuide\.focusBoundaryValue': 'Leaderboard browsing; purchase continues from product details after sign-in'/
  );
  assert.match(
    source,
    /'leaderboard\.public\.productGuide\.focusBoundaryValue': '榜单只做展示比较，购买从商品详情登录后继续'/
  );
  assert.doesNotMatch(
    source,
    /'leaderboard\.public\.productGuide\.focusBoundaryValue': 'Public browsing only, not purchase or account workflows'/
  );
  assert.doesNotMatch(
    source,
    /'leaderboard\.public\.productGuide\.focusBoundaryValue': '只读浏览，不带购买或账号流程'/
  );
  assert.doesNotMatch(source, /shop\.public\.openDesktop/);
  assert.doesNotMatch(source, /shop shell/);
  assert.doesNotMatch(source, /商城壳层/);
});

test('公开个人页账号动作文案应指向个人页面而不是工作台', () => {
  const source = readLocaleResources();

  assert.match(source, /Orders, inventory, assets, and other account actions stay on signed-in personal pages/);
  assert.match(source, /订单、背包、资产和其他账号动作继续留在登录后的个人页面/);
  assert.doesNotMatch(source, /private Web routes/);
  assert.doesNotMatch(source, /私域 Web 路由/);
  assert.doesNotMatch(source, /Inventory, orders, and other workspace-only account actions remain outside this shell/);
  assert.doesNotMatch(source, /Creation, orders, inventory, and other workspace actions stay outside/);
  assert.doesNotMatch(source, /背包、订单和其他工作台专属账号动作继续留在壳层之外/);
  assert.doesNotMatch(source, /创作、订单、背包和其他工作台动作不在这里开放/);
});

test('公开个人页应提供受控登录回流与关注状态切换', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/profile/PublicProfileApp.tsx'), 'utf8');
  const i18nSource = readLocaleResources();

  assert.match(source, /from '@\/api\/userFollow'/);
  assert.match(source, /getFollowStatus\(profile\.voUserId, t\)/);
  assert.match(source, /followUser\(profile\.voUserId, t\)/);
  assert.match(source, /unfollowUser\(profile\.voUserId, t\)/);
  assert.match(source, /buildPublicProfileFollowReturnPath\(profileRouteIdentifier\)/);
  assert.match(source, /redirectToLogin\(\{ returnPath:/);
  assert.match(source, /aria-pressed=\{loggedIn \? followStatus\?\.voIsFollowing === true : undefined\}/);
  assert.match(i18nSource, /当前查看的是用户公开主页，可浏览公开内容并关注作者/);
  assert.match(i18nSource, /公开资料与关系动作，不带出私有账号工具/);
  assert.doesNotMatch(i18nSource, /当前查看的是用户主页，只提供公开内容浏览/);
  assert.doesNotMatch(i18nSource, /只读资料上下文，不带出账号内动作/);
});

test('公开个人页内容查询不应依赖内部用户 ID', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/profile/PublicProfileApp.tsx'), 'utf8');

  assert.doesNotMatch(source, /GetUserStats/);
  assert.doesNotMatch(source, /GetUserPosts/);
  assert.doesNotMatch(source, /GetUserComments/);
  assert.match(source, /getPublicUserStats\(route\.userId\)/);
  assert.match(source, /getPublicUserPosts\(profileRouteIdentifier, route\.page, 10\)/);
  assert.match(source, /getPublicUserComments\(profileRouteIdentifier, route\.page, 10\)/);
});

test('公开个人页返回、tab 和分页应提供公开链接并保留壳层导航拦截', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/profile/PublicProfileApp.tsx'), 'utf8');
  const stylesSource = readFileSync(resolve(clientRoot, 'src/public/profile/PublicProfileApp.module.css'), 'utf8');

  assert.match(source, /href\?: string;/);
  assert.match(source, /function shouldHandleProfileLinkInternally/);
  assert.match(source, /const backHref = backAction\?\.href/);
  assert.match(source, /href: primaryAction\.href/);
  assert.match(source, /href: secondaryAction\.href/);
  assert.match(source, /href=\{backHref\}/);
  assert.match(source, /href=\{buildPublicProfilePath\(\{ kind: 'detail', userId: profileRouteIdentifier, tab: 'posts', page: 1 \}\)\}/);
  assert.match(source, /href=\{buildPublicProfilePath\(\{ kind: 'detail', userId: profileRouteIdentifier, tab: 'comments', page: 1 \}\)\}/);
  assert.match(source, /page: route\.page - 1/);
  assert.match(source, /page: route\.page \+ 1/);
  assert.match(source, /handleProfileRouteLinkClick\(event,/);
  assert.match(stylesSource, /\.summaryBackLink[\s\S]*text-decoration: none;/);
  assert.match(stylesSource, /\.tabButton[\s\S]*white-space: nowrap;/);
});

test('公开社区发现页应使用统一公开分享入口', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/discover/PublicDiscoverApp.tsx'), 'utf8');

  assert.match(source, /usePublicShareLink/);
  assert.match(source, /buildPublicShareUrl\(buildPublicDiscoverPath\(route\)\)/);
  assert.match(source, /discover\.public\.shareAction/);
});

test('公开发现和论坛列表不应渲染教学式阅读说明卡', () => {
  const discoverSource = readFileSync(resolve(clientRoot, 'src/public/discover/PublicDiscoverApp.tsx'), 'utf8');
  const discoverStylesSource = readFileSync(resolve(clientRoot, 'src/public/discover/PublicDiscoverApp.module.css'), 'utf8');
  const forumListSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumList.tsx'), 'utf8');
  const forumUtilsSource = readFileSync(resolve(clientRoot, 'src/public/forum/publicForumUtils.ts'), 'utf8');
  const i18nSource = readLocaleResources();

  assert.doesNotMatch(discoverSource, /discoverGuideItems|heroTitleRow|heroGuideGrid|pulseKicker|discussionKicker/);
  assert.doesNotMatch(discoverSource, /summaryCards|routeGuideCards|routeGuideMap|PublicDiscoverFeed/);
  assert.doesNotMatch(discoverSource, /featuredLeaderboardConfigs|forumSectionCueDefinitions|docsSectionCueDefinitions|shopSectionCueDefinitions/);
  assert.doesNotMatch(discoverStylesSource, /heroGuide|streamBoundary|streamKicker/);
  assert.doesNotMatch(forumListSource, /PublicReadingGuide|readingGuide|forum\.public\.guide\.label|listRailRulesTitle|sideRuleList/);
  assert.doesNotMatch(forumUtilsSource, /listGuideDefinition|categoryGuideDefinition/);
  assert.doesNotMatch(i18nSource, /discover\.public\.heroGuide|discover\.public\.streamInteraction|forum\.public\.listGuide|forum\.public\.categoryGuide|forum\.public\.listRailRule/);
  assert.doesNotMatch(i18nSource, /公开论坛列表阅读提示|列表阅读规则|先看正在变化的内容|从活跃讨论开始|正在被讨论/);
});

test('公开社区发现页应为跨入口导航提供公开链接并保留壳层导航拦截', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/discover/PublicDiscoverApp.tsx'), 'utf8');

  assert.match(source, /buildPublicDocsPath/);
  assert.match(source, /buildPublicLeaderboardPath/);
  assert.match(source, /buildPublicShopPath/);
  assert.match(source, /shouldHandlePublicDiscoverLink\(event\)/);
  assert.match(source, /href=\{forumListHref\}/);
  assert.match(source, /href=\{forumSearchHref\}/);
  assert.match(source, /href=\{forumQuestionHref\}/);
  assert.match(source, /href=\{forumHotHref\}/);
  assert.match(source, /href=\{buildPublicForumPath\(tagRoute\)\}/);
  assert.match(source, /featuredDocument \? buildPublicDocsPath\(\{ kind: 'detail', slug: featuredDocument\.voSlug \}\) : docsListHref/);
  assert.match(source, /featuredProduct \? buildPublicShopPath\(\{ kind: 'detail', productId: String\(featuredProduct\.voId\) \}\) : shopProductsHref/);
  assert.match(source, /href=\{leaderboardHref\}/);
  assert.match(source, /handlePublicDiscoverLinkClick\(event,/);
});

test('公开社区发现页应保持单一社区内容主流并只加载少量辅助内容', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/discover/PublicDiscoverApp.tsx'), 'utf8');

  assert.match(source, /getPostList\(null, t, 1, 8, 'newest'\)/);
  assert.match(source, /const spotlightPosts = forumPosts\.slice\(0, 5\)/);
  assert.match(source, /getPublicWikiList\(\{ pageIndex: 1, pageSize: 1 \}\)/);
  assert.match(source, /getProducts\(t, undefined, undefined, undefined, 1, 1\)/);
  assert.equal((source.match(/<PostCard/g) ?? []).length, 1);
  assert.doesNotMatch(source, /docs\.map|products\.map|featuredLeaderboardConfigs\.map/);
  assert.doesNotMatch(source, /sectionRefs|scrollToSection|previewSection|rememberSection/);
});

test('纯 Web 壳层应使用统一产品级导航而不是 public / private 两套入口', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/components/PublicShellHeader.tsx'), 'utf8');
  const shellSource = readFileSync(resolve(clientRoot, 'src/components/web-shell/WebShellHeader.tsx'), 'utf8');
  const shellStylesSource = readFileSync(resolve(clientRoot, 'src/components/web-shell/WebShellHeader.module.css'), 'utf8');
  const i18nSource = readLocaleResources();

  assert.match(shellSource, /function shouldHandleShellLinkClick/);
  assert.match(shellSource, /function navigateToShellPath/);
  assert.match(shellSource, /function getDefaultNavItems/);
  assert.match(shellSource, /key: 'discover'[\s\S]*key: 'forum'[\s\S]*key: 'chat'[\s\S]*key: 'more'/);
  assert.match(shellSource, /function getDefaultMobileNavItems/);
  assert.match(shellSource, /key: 'discover'[\s\S]*key: 'forum'[\s\S]*key: 'chat'[\s\S]*key: 'more'[\s\S]*key: 'me'/);
  assert.match(shellSource, /aria-label=\{t\('public\.shell\.navLabel'\)\}/);
  assert.match(shellSource, /aria-label=\{t\('public\.shell\.mobileNavLabel'\)\}/);
  assert.match(shellSource, /hideMobileNav/);
  assert.match(shellSource, /href=\{item\.href\}/);
  assert.match(shellSource, /event\.preventDefault\(\);/);
  assert.match(shellSource, /item\.onClick\(\);/);
  assert.match(shellSource, /if \(navigateToShellPath\(item\.href\)\) \{/);
  assert.match(source, /function buildShellActionItems\(authAction: WebShellNavItem, notificationsLabel: string\): WebShellNavItem\[\]/);
  assert.match(source, /key: 'notifications'[\s\S]*href: '\/notifications'/);
  assert.match(source, /key: 'me'[\s\S]*href: '\/me'/);
  assert.match(source, /avatarUrl: resolveMediaUrl\(avatarUrl\)/);
  assert.doesNotMatch(shellSource, /publicNavItems|privateNavItems|publicMobileNavItems|privateMobileNavItems/);
  assert.doesNotMatch(shellSource, /key: 'docs'[\s\S]*productNavItems|key: 'shop'[\s\S]*productNavItems|label: '工作台'|label: '消息'/);
  assert.doesNotMatch(source, /discover-action|circle-action|workbench-action|me-action|login-action/);
  assert.doesNotMatch(source, /discoverHref|discoverLabel|circleHref|circleLabel|showCircleAction|desktopHref|desktopLabel|myStatusLabel/);
  assert.doesNotMatch(source, /buildPublicNavItems|buildPublicMobileNavItems|buildActionItems/);
  assert.doesNotMatch(shellStylesSource, /publicActionRail|publicActionItem|publicActionPrimary/);
  assert.doesNotMatch(i18nSource, /public\.shell\.(discoverAction|circleAction|desktopAction|myStatusAction)/);
});

test('正式 Web 主题入口应由共享 Header 覆盖桌面与移动视图', () => {
  const publicShellSource = readFileSync(resolve(clientRoot, 'src/public/components/PublicShellHeader.tsx'), 'utf8');
  const shellSource = readFileSync(resolve(clientRoot, 'src/components/web-shell/WebShellHeader.tsx'), 'utf8');
  const shellStylesSource = readFileSync(resolve(clientRoot, 'src/components/web-shell/WebShellHeader.module.css'), 'utf8');
  const switcherSource = readFileSync(resolve(clientRoot, 'src/theme/ThemeSwitcher.tsx'), 'utf8');

  assert.match(publicShellSource, /<LanguageSwitcher \/>[\s\S]*<ThemeSwitcher \/>/);
  assert.match(shellSource, /actionSlot\?: ReactNode/);
  assert.match(shellSource, /\{actionSlot\}/);
  assert.match(switcherSource, /themeOptions\.map/);
  assert.match(switcherSource, /redirectToLogin/);
  assert.match(switcherSource, /buildPublicShopPath/);
  assert.match(switcherSource, /keyword: '主题'/);
  assert.match(shellStylesSource, /@media \(max-width: 1080px\)[\s\S]*\.actionRail[\s\S]*padding: 4px/);
  assert.match(shellStylesSource, /@media \(max-width: 720px\)[\s\S]*\.actionRail[\s\S]*display: inline-flex/);
});

test('正式 Web 壳层切换应复用当前 React 入口而不是整页重载', () => {
  const mainSource = readFileSync(resolve(clientRoot, 'src/main.tsx'), 'utf8');
  const browserRouterSource = readFileSync(resolve(clientRoot, 'src/bootstrap/BrowserAppRouter.tsx'), 'utf8');
  const shellSource = readFileSync(resolve(clientRoot, 'src/components/web-shell/WebShellHeader.tsx'), 'utf8');
  const publicShellSource = readFileSync(resolve(clientRoot, 'src/public/components/PublicShellHeader.tsx'), 'utf8');

  assert.match(browserRouterSource, /export function BrowserAppRouter\(\)/);
  assert.match(browserRouterSource, /const \[entryKind, setEntryKind\] = useState<BrowserEntryKind>/);
  assert.match(browserRouterSource, /window\.addEventListener\('popstate', handlePopState\);/);
  assert.match(browserRouterSource, /startTransition\(\(\) => \{/);
  assert.match(browserRouterSource, /resolveEntryComponent\(entryKind\)/);
  assert.match(mainSource, /<BrowserAppRouter \/>/);
  assert.match(shellSource, /window\.history\.pushState\(\{\}, '', nextPath\);/);
  assert.match(shellSource, /window\.dispatchEvent\(new PopStateEvent\('popstate'/);
  assert.match(publicShellSource, /buildShellActionItems\(authAction, t\('public\.shell\.nav\.notifications'\)\)/);
  assert.doesNotMatch(publicShellSource, /window\.location\.href = href/);
});

test('登录态正式 Web 页面应使用应用内滚动容器', () => {
  const stylePaths = [
    'src/me/MeApp.module.css',
    'src/messages/MessagesApp.module.css',
    'src/notifications/NotificationsApp.module.css',
    'src/pet/PetApp.module.css',
    'src/workbench/WorkbenchApp.module.css',
    'src/docs/DocsAuthorApp.module.css',
    'src/circle/CircleApp.module.css',
  ];

  for (const stylePath of stylePaths) {
    const source = readFileSync(resolve(clientRoot, stylePath), 'utf8');
    assert.match(source, /\.page[\s\S]*height: 100dvh;/, stylePath);
  }

  const bodyScrollStylePaths = [
    'src/me/MeApp.module.css',
    'src/notifications/NotificationsApp.module.css',
    'src/pet/PetApp.module.css',
    'src/workbench/WorkbenchApp.module.css',
    'src/docs/DocsAuthorApp.module.css',
  ];

  for (const stylePath of bodyScrollStylePaths) {
    const source = readFileSync(resolve(clientRoot, stylePath), 'utf8');
    assert.match(source, /\.page[\s\S]*overflow-y: auto;/, stylePath);
  }

  const messagesSource = readFileSync(resolve(clientRoot, 'src/messages/MessagesApp.module.css'), 'utf8');
  const chatSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatApp.module.css'), 'utf8');
  assert.match(messagesSource, /\.page[\s\S]*overflow: hidden;/);
  assert.match(chatSource, /\.messageViewport[\s\S]*overflow-y: auto;/);
});

test('聊天正式 Web 页面应把聊天窗口作为主体而不是仪表盘卡片', () => {
  const messagesSource = readFileSync(resolve(clientRoot, 'src/messages/MessagesApp.tsx'), 'utf8');
  const messagesStylesSource = readFileSync(resolve(clientRoot, 'src/messages/MessagesApp.module.css'), 'utf8');
  const chatSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatApp.tsx'), 'utf8');
  const chatSidebarSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatChannelSidebar.tsx'), 'utf8');
  const chatTypesSource = readFileSync(resolve(clientRoot, 'src/types/chat.ts'), 'utf8');

  assert.match(messagesSource, /activeKey="chat"/);
  assert.match(messagesSource, /hideMobileNav=\{route\.channelId !== undefined\}/);
  assert.match(messagesSource, /messages\.web\.chatWorkspaceLabel/);
  assert.doesNotMatch(messagesSource, /summaryPanel|messageRail|summaryCards|channelQueue/);
  assert.match(messagesStylesSource, /width: min\(1560px, calc\(100vw - 32px\)\)/);
  assert.match(messagesStylesSource, /height: calc\(100dvh - var\(--rx-shell-header-height\) - 40px\)/);
  assert.match(chatSource, /useState\(true\)/);
  assert.match(chatSource, /chatAppFocused/);
  assert.match(chatSidebarSource, /key: 'mutual'[\s\S]*key: 'stranger'[\s\S]*key: 'group'[\s\S]*key: 'public'/);
  assert.match(chatSidebarSource, /voConversationKind/);
  assert.match(chatTypesSource, /voConversationKind\?: 'public' \| 'mutual' \| 'stranger' \| 'group' \| null;/);
});

test('正式 Web 页头登录态账号应展示用户名而不是公开句柄', () => {
  const publicShellSource = readFileSync(resolve(clientRoot, 'src/public/components/PublicShellHeader.tsx'), 'utf8');
  const userStoreSource = readFileSync(resolve(clientRoot, 'src/stores/userStore.ts'), 'utf8');
  const authBootstrapSource = readFileSync(resolve(clientRoot, 'src/services/authBootstrap.ts'), 'utf8');

  assert.match(publicShellSource, /const userLabel = userName\?\.trim\(\) \|\| displayName\?\.trim\(\) \|\| t\('public\.shell\.nav\.me'\);/);
  assert.doesNotMatch(publicShellSource, /const userLabel = displayHandle/);
  assert.match(userStoreSource, /const userName = user\.userName\?\.trim\(\) \|\| displayName;/);
  assert.match(authBootstrapSource, /const userName = user\.voUserName\.trim\(\);/);
  assert.match(authBootstrapSource, /userName: userName \|\| displayName/);
});

test('我的状态首页不应承载规则说明型隐私边界大卡片', () => {
  const meSource = readFileSync(resolve(clientRoot, 'src/me/MeApp.tsx'), 'utf8');
  const meStylesSource = readFileSync(resolve(clientRoot, 'src/me/MeApp.module.css'), 'utf8');
  const legalSource = readFileSync(resolve(clientRoot, 'src/public/legal/PublicCommitmentsApp.tsx'), 'utf8');

  assert.doesNotMatch(meSource, /PrivacySafetyBoundaryPanel/);
  assert.doesNotMatch(meSource, /privacySafety/);
  assert.doesNotMatch(meStylesSource, /privacySafetyPanel/);
  assert.match(legalSource, /<PrivacySafetyBoundaryPanel \/>/);
});

test('公开规则页应使用应用内滚动容器而不是依赖 body 滚动', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/legal/PublicCommitmentsApp.tsx'), 'utf8');
  const stylesSource = readFileSync(resolve(clientRoot, 'src/public/legal/PublicCommitmentsApp.module.css'), 'utf8');

  assert.match(source, /const pageRef = useRef<HTMLDivElement \| null>\(null\);/);
  assert.match(source, /<div className=\{styles\.page\} ref=\{pageRef\}>/);
  assert.match(source, /pageRef\.current\?\.scrollTo\(\{ top: 0, behavior: 'smooth' \}\)/);
  assert.match(stylesSource, /\.page[\s\S]*height: 100dvh;/);
  assert.match(stylesSource, /\.page[\s\S]*overflow-y: auto;/);
  assert.match(stylesSource, /\.main[\s\S]*flex: 1 0 auto;/);
});

test('公开文档浏览和详情返回应提供公开链接并保留壳层导航拦截', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/docs/PublicDocsApp.tsx'), 'utf8');
  const stylesSource = readFileSync(resolve(clientRoot, 'src/public/docs/PublicDocsApp.module.css'), 'utf8');

  assert.match(source, /buildDocsAuthorPath/);
  assert.match(source, /canUseDocsAuthorTools/);
  assert.match(source, /const authorHref = buildDocsAuthorPath\(\{ kind: 'mine' \}\);/);
  assert.match(source, /href=\{authorHref\}/);
  assert.match(source, /href=\{editHref\}/);
  assert.match(source, /function handlePublicDocsLinkClick/);
  assert.match(source, /const detailBackHref = detailBackAction\?\.href \?\? buildPublicDocsPath\(fallbackBrowseRoute\);/);
  assert.match(source, /href=\{searchHref\}/);
  assert.match(source, /const href = buildPublicDocsPath\(\{ kind: 'detail', slug: row\.slug \}\);/);
  assert.match(source, /const href = buildPublicDocsPath\(\{ kind: 'detail', slug: document\.voSlug \}\);/);
  assert.match(source, /href=\{browseDirectoryHref\}/);
  assert.match(source, /href=\{buildPublicDocsPath\(\{ \.\.\.route, page: route\.page - 1 \}\)\}/);
  assert.match(source, /href=\{buildPublicDocsPath\(\{ \.\.\.route, page: route\.page \+ 1 \}\)\}/);
  assert.match(source, /href=\{backHref\}/);
  assert.match(stylesSource, /\.directoryItem[\s\S]*text-decoration: none;/);
  assert.match(stylesSource, /\.docCard[\s\S]*text-decoration: none;/);
});

test('公开文档数据源应固定使用服务端公开契约且不携带认证信息', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/docs/publicDocsApi.ts'), 'utf8');

  assert.match(source, /Wiki\/PublicGetList/);
  assert.match(source, /Wiki\/PublicGetTree/);
  assert.match(source, /Wiki\/PublicGetBySlug/);
  assert.match(source, /withAuth: false/g);
  assert.doesNotMatch(source, /tokenService/);
  assert.doesNotMatch(source, /resolveReadWithAuth/);
  assert.doesNotMatch(source, /filterPublicReadableTree/);
  assert.doesNotMatch(source, /normalizePublicWikiPage/);
});

test('文档作者正式 Web 入口应独立于公开 SEO 壳层且不承载治理动作', () => {
  const mainSource = readFileSync(resolve(clientRoot, 'src/main.tsx'), 'utf8');
  const browserRouterSource = readFileSync(resolve(clientRoot, 'src/bootstrap/BrowserAppRouter.tsx'), 'utf8');
  const entryRouteSource = readFileSync(resolve(clientRoot, 'src/bootstrap/entryRoute.ts'), 'utf8');
  const docsAuthorSource = readFileSync(resolve(clientRoot, 'src/docs/DocsAuthorApp.tsx'), 'utf8');

  assert.match(mainSource, /import \{ BrowserAppRouter \} from '@\/bootstrap\/BrowserAppRouter';/);
  assert.match(browserRouterSource, /const DocsAuthorEntry = lazy/);
  assert.match(browserRouterSource, /case 'docs-author':\s*return DocsAuthorEntry;[\s\S]*case 'public':\s*return PublicEntry;/);
  assert.match(entryRouteSource, /if \(isDocsAuthorPathname\(pathname\)\) \{\s*return 'docs-author';\s*\}[\s\S]*if \(isPublicContentPathname\(pathname\)\) \{\s*return 'public';/);
  assert.match(entryRouteSource, /pathname\.startsWith\('\/docs\/'\) && !isDocsAuthorPathname\(pathname\)/);
  assert.match(docsAuthorSource, /buildDocsAuthorMineReturnPath/);
  assert.match(docsAuthorSource, /createWikiDocument/);
  assert.match(docsAuthorSource, /updateWikiDocument/);
  assert.match(docsAuthorSource, /getWikiRevisionList/);
  assert.match(docsAuthorSource, /const treeRef = useRef<WikiDocumentTreeNodeVo\[\]>\(\[\]\);/);
  assert.match(docsAuthorSource, /treeRef\.current = collectionState\.tree;/);
  assert.match(docsAuthorSource, /preventNavigationWhileUploading\(event\);[\s\S]*onNavigate\(event, \{ kind: 'revisions', documentId: route\.documentId \}\);/);
  assert.match(docsAuthorSource, /const publicReadHref = state\.document && !state\.document\.voIsDeleted && state\.document\.voSlug\.trim\(\)/);
  assert.match(docsAuthorSource, /href=\{publicReadHref\}/);
  assert.doesNotMatch(docsAuthorSource, /publishWikiDocument/);
  assert.doesNotMatch(docsAuthorSource, /unpublishWikiDocument/);
  assert.doesNotMatch(docsAuthorSource, /archiveWikiDocument/);
  assert.doesNotMatch(docsAuthorSource, /rollbackWikiRevision/);
  assert.doesNotMatch(docsAuthorSource, /deleteWikiDocument/);
  assert.doesNotMatch(docsAuthorSource, /restoreWikiDocument/);
});

test('公开商城复用的商品列表组件应提供公开链接能力', () => {
  const shopSource = readFileSync(resolve(clientRoot, 'src/public/shop/PublicShopApp.tsx'), 'utf8');
  const shopDetailSource = readFileSync(resolve(clientRoot, 'src/public/shop/PublicShopViews.tsx'), 'utf8');
  const shopHomeSource = readFileSync(resolve(clientRoot, 'src/apps/shop/pages/ShopHome.tsx'), 'utf8');
  const productListSource = readFileSync(resolve(clientRoot, 'src/apps/shop/pages/ProductList.tsx'), 'utf8');
  const publicEntrySource = readFileSync(resolve(clientRoot, 'src/public/PublicEntry.tsx'), 'utf8');

  assert.match(shopHomeSource, /getCategoryHref\?: \(categoryId: string\) => string;/);
  assert.match(shopHomeSource, /getProductHref\?: \(productId: LongId\) => string;/);
  assert.match(shopHomeSource, /viewAllProductsHref\?: string;/);
  assert.match(shopHomeSource, /href=\{categoryHref\}/);
  assert.match(shopHomeSource, /href=\{productHref\}/);
  assert.match(productListSource, /backHref\?: string;/);
  assert.match(productListSource, /getCategoryHref\?: \(categoryId\?: string\) => string;/);
  assert.match(productListSource, /getPageHref\?: \(page: number\) => string;/);
  assert.match(productListSource, /href=\{backHref\}/);
  assert.match(productListSource, /href=\{getCategoryHref\(String\(category\.voId\)\)\}/);
  assert.match(productListSource, /href=\{getPageHref\(pageNum\)\}/);
  assert.match(productListSource, /href=\{productHref\}/);
  assert.match(shopSource, /getCategoryHref=\{\(categoryId\) => buildPublicShopPath\(\{ kind: 'products', categoryId, page: 1 \}\)\}/);
  assert.match(shopSource, /getPageHref=\{\(page\) => buildPublicShopPath\(\{/);
  assert.match(shopDetailSource, /href=\{detailBackHref\}/);
  assert.match(publicEntrySource, /href: buildPublicPath\(routeSourceState\.shopDetailSourceRoute\)/);
});

test('正式 Web 商城交易入口应提供订单与库存真实链接', () => {
  const shopWebSource = readFileSync(resolve(clientRoot, 'src/shop/ShopWebApp.tsx'), 'utf8');
  const orderListSource = readFileSync(resolve(clientRoot, 'src/apps/shop/pages/OrderList.tsx'), 'utf8');
  const orderDetailSource = readFileSync(resolve(clientRoot, 'src/apps/shop/pages/OrderDetail.tsx'), 'utf8');
  const inventorySource = readFileSync(resolve(clientRoot, 'src/apps/shop/pages/Inventory.tsx'), 'utf8');

  assert.match(shopWebSource, /parseShopRoute\(window\.location\.pathname\)/);
  assert.match(shopWebSource, /buildShopPath\(\{ kind: 'order-detail', orderId \}\)/);
  assert.match(shopWebSource, /getSourceOrderHref=\{\(orderId\) => buildShopPath\(\{ kind: 'order-detail', orderId \}\)\}/);
  assert.match(shopWebSource, /getSourceProductHref=\{\(productId\) => buildPublicShopPath\(\{ kind: 'detail', productId: String\(productId\) \}\)\}/);
  assert.match(orderListSource, /getOrderHref\?: \(orderId: LongId\) => string;/);
  assert.match(orderListSource, /href=\{orderHref\}/);
  assert.match(orderDetailSource, /productHref\?: string;/);
  assert.match(orderDetailSource, /href=\{productHref\}/);
  assert.match(inventorySource, /getSourceOrderHref\?: \(orderId: LongId\) => string;/);
  assert.match(inventorySource, /href=\{sourceOrderHref\}/);
  assert.match(inventorySource, /href=\{sourceProductHref\}/);
});

test('公开入口应为所有公开路由应用通用 JSON-LD', () => {
  const entrySource = readFileSync(resolve(clientRoot, 'src/public/PublicEntry.tsx'), 'utf8');
  const lifecycleSource = readFileSync(resolve(clientRoot, 'src/public/PublicHeadLifecycle.tsx'), 'utf8');

  assert.match(entrySource, /<PublicHeadLifecycleOwner route=\{route\} routeKey=\{buildPublicPath\(route\)\}>/);
  assert.doesNotMatch(entrySource, /applyPublic(?:Head|StructuredData)/);
  assert.match(lifecycleSource, /applyPublicHead\(resolvedHead\)/);
  assert.match(lifecycleSource, /applyPublicStructuredData\(resolvedStructuredData\)/);
  assert.match(lifecycleSource, /buildPublicRouteStructuredData\(route, \{ head: resolvedHead \}\)/);
  assert.match(lifecycleSource, /removePublicStructuredData\(\)/);
  assert.match(lifecycleSource, /resetPublicHead\(\)/);
});

test('公开壳层的 head DOM 写入应只由 lifecycle owner 调用 helper', () => {
  const sources = readTypeScriptSources(resolve(clientRoot, 'src/public'));
  const titleWriters = sources
    .filter(({ source }) => /(?:globalThis\.)?document\.title\s*=/.test(source))
    .map(({ path }) => path);
  const headAppliers = sources
    .filter(({ source }) => /applyPublicHead/.test(source))
    .map(({ path }) => path)
    .sort();
  const structuredDataAppliers = sources
    .filter(({ source }) => /applyPublicStructuredData/.test(source))
    .map(({ path }) => path)
    .sort();

  assert.deepEqual(titleWriters, ['publicHead.ts']);
  assert.deepEqual(headAppliers, ['PublicHeadLifecycle.tsx', 'publicHead.ts']);
  assert.deepEqual(structuredDataAppliers, ['PublicHeadLifecycle.tsx', 'publicStructuredData.ts']);
});

test('公开详情页应只提交 head 快照，不再直接写 DOM', () => {
  const detailSources = [
    'src/public/forum/PublicForumDetail.tsx',
    'src/public/docs/PublicDocsApp.tsx',
    'src/public/profile/PublicProfileApp.tsx',
    'src/public/shop/PublicShopApp.tsx',
  ].map((path) => readFileSync(resolve(clientRoot, path), 'utf8'));

  for (const source of detailSources) {
    assert.match(source, /usePublicHeadSnapshot\(publicHeadSnapshot\)/);
    assert.doesNotMatch(source, /applyPublicHead|applyPublicStructuredData|removePublicStructuredData/);
    assert.doesNotMatch(source, /(?:globalThis\.)?document\.title/);
  }
});

test('Docs 与 Forum 详情应以 keyed remount 和实体身份校验形成 head 双保险', () => {
  const docsSource = readFileSync(resolve(clientRoot, 'src/public/docs/PublicDocsApp.tsx'), 'utf8');
  const forumAppSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumApp.tsx'), 'utf8');
  const forumDetailSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumDetail.tsx'), 'utf8');

  assert.equal(docsSource.includes("key={`docs-${route.slug}-${route.anchor ?? 'root'}`}"), true);
  assert.match(docsSource, /isCurrentDocsHeadSource\(route, documentDetail\)/);
  assert.match(docsSource, /buildLocalizedPublicRouteHead\(\{ app: 'docs', route: canonicalRoute \}, t\)/);
  assert.match(docsSource, /buildPublicDocsHeadSnapshot\(documentDetail, route\.anchor, \{/);
  assert.equal(forumAppSource.includes("key={`detail-${route.postId}-${route.commentId ?? 'none'}-${route.intent ?? 'read'}`}"), true);
  assert.match(forumDetailSource, /isCurrentForumPostHeadSource\(postId, post\)/);
  assert.match(forumDetailSource, /buildLocalizedPublicRouteHead\(\{/);
});

test('公开集合页应由唯一 owner 应用本地化基线并保留数据原文快照', () => {
  const lifecycleSource = readFileSync(resolve(clientRoot, 'src/public/PublicHeadLifecycle.tsx'), 'utf8');
  const forumListSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumList.tsx'), 'utf8');
  const forumTagSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumTag.tsx'), 'utf8');
  const leaderboardSource = readFileSync(resolve(clientRoot, 'src/public/leaderboard/PublicLeaderboardApp.tsx'), 'utf8');
  const shopSource = readFileSync(resolve(clientRoot, 'src/public/shop/PublicShopApp.tsx'), 'utf8');

  assert.match(lifecycleSource, /buildLocalizedPublicRouteHead\(route, t\)/);
  assert.match(forumListSource, /title: `\$\{selectedCategory\.voName\} · \$\{t\('desktop\.apps\.forum\.name'\)\}`/);
  assert.match(forumTagSource, /title: `\$\{selectedTag\.voName\} · \$\{t\('desktop\.apps\.forum\.name'\)\}`/);
  assert.match(leaderboardSource, /title: `\$\{activeTypeConfig\.voName\} · \$\{t\('desktop\.apps\.leaderboard\.name'\)\}`/);
  assert.match(shopSource, /title: `\$\{selectedCategory\.voName\} · \$\{t\('desktop\.apps\.shop\.name'\)\}`/);

  for (const source of [forumListSource, forumTagSource, leaderboardSource, shopSource]) {
    assert.match(source, /usePublicHeadSnapshot\(publicHeadSnapshot\)/);
    assert.doesNotMatch(source, /(?:globalThis\.)?document\.title|applyPublicHead/);
  }
});

test('Browser React 根应只挂载一个 ToastContainer', () => {
  const toastOwners = readTypeScriptSources(resolve(clientRoot, 'src'))
    .filter(({ source }) => /<ToastContainer\s*\/>/.test(source))
    .map(({ path }) => path);

  assert.deepEqual(toastOwners, ['main.tsx']);
});

test('公开用户承诺页应进入公共壳层并归属工作台入口', () => {
  const entryRouteSource = readFileSync(resolve(clientRoot, 'src/bootstrap/entryRoute.ts'), 'utf8');
  const publicEntrySource = readFileSync(resolve(clientRoot, 'src/public/PublicEntry.tsx'), 'utf8');
  const publicShellSource = readFileSync(resolve(clientRoot, 'src/public/components/PublicShellHeader.tsx'), 'utf8');
  const webShellSource = readFileSync(resolve(clientRoot, 'src/components/web-shell/WebShellHeader.tsx'), 'utf8');
  const headSource = readFileSync(resolve(clientRoot, 'src/public/publicHead.ts'), 'utf8');
  const workbenchSource = readFileSync(resolve(clientRoot, 'src/workbench/WorkbenchApp.tsx'), 'utf8');

  assert.match(entryRouteSource, /isPublicLegalPathname\(pathname\)/);
  assert.match(publicEntrySource, /parsePublicLegalRoute\(window\.location\.pathname\)/);
  assert.match(publicEntrySource, /<PublicCommitmentsApp/);
  assert.doesNotMatch(publicShellSource, /href: '\/legal'/);
  assert.match(webShellSource, /pathname === '\/legal'[\s\S]*return 'more';/);
  assert.match(workbenchSource, /titleKey: 'workbench\.item\.legal\.title'[\s\S]*href: '\/legal'/);
  assert.match(headSource, /用户承诺与社区边界/);
});

test('公开论坛详情加载后应向唯一 head owner 提交共用 canonical 快照', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumDetail.tsx'), 'utf8');

  assert.match(source, /usePublicHeadSnapshot\(publicHeadSnapshot\)/);
  assert.match(source, /buildForumPostPublicHead/);
  assert.match(source, /const postHead = buildForumPostPublicHead\(post, commentId, coverImageUrl, \{/);
  assert.match(source, /appName: t\('desktop\.apps\.forum\.name'\)/);
  assert.match(source, /fallbackDescription: routeHead\.description/);
  assert.doesNotMatch(source, /applyPublicHead/);
  assert.doesNotMatch(source, /document\.title/);
  assert.match(source, /canonicalPath: postHead\.canonicalPath/);
});

test('公开论坛浏览入口应提供公开链接并保留壳层导航拦截', () => {
  const linksSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumLinks.tsx'), 'utf8');
  const handlersSource = readFileSync(resolve(clientRoot, 'src/public/forum/publicForumLinkHandlers.ts'), 'utf8');
  const statusSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicStatusCard.tsx'), 'utf8');
  const appSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumApp.tsx'), 'utf8');
  const detailSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumDetail.tsx'), 'utf8');
  const listSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumList.tsx'), 'utf8');
  const stylesSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumApp.module.css'), 'utf8');
  const searchSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumSearch.tsx'), 'utf8');
  const tagSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumTag.tsx'), 'utf8');
  const typeSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumTypeFeed.tsx'), 'utf8');

  assert.match(linksSource, /href=\{buildPublicForumPath\(route\)\}/);
  assert.match(linksSource, /handlePublicForumLinkClick\(event, onNavigate\)/);
  assert.match(handlersSource, /event\.button === 0/);
  assert.match(statusSource, /href: primaryAction\.href/);
  assert.match(statusSource, /href: secondaryAction\.href/);
  assert.match(appSource, /const detailBackHref = detailBackAction\?\.href \?\? buildPublicForumPath\(fallbackBrowseRoute\);/);
  assert.match(detailSource, /href=\{backHref\}/);
  assert.match(detailSource, /secondaryAction=\{\{[\s\S]*label: backLabel,[\s\S]*href: backHref,[\s\S]*onClick: handleBackWhileEditorIdle[\s\S]*\}\}/);
  assert.match(listSource, /PublicForumPagination/);
  assert.match(listSource, /route=\{createDefaultSearchRoute\(\)\}/);
  assert.match(listSource, /route=\{buildListRoute\(1, selectedCategoryId, 'hottest'\)\}/);
  assert.match(listSource, /route=\{buildListRoute\(1, nextCategoryId\)\}/);
  assert.match(listSource, /styles\.sidePanelAction/);
  assert.match(stylesSource, /\.workspaceActionButtons \.workspaceActionButton \{\s*flex: 1 1 150px;/);
  assert.doesNotMatch(stylesSource, /\n {2}\.workspaceActionButton \{\n {4}flex:/);
  assert.match(searchSource, /route=\{backToListRoute\}/);
  assert.match(searchSource, /route=\{defaultSearchRoute\}/);
  assert.match(searchSource, /route=\{buildSearchRoute\(1, sortBy, value\)\}/);
  assert.match(tagSource, /route=\{searchTagRoute\}/);
  assert.match(tagSource, /href: buildPublicForumPath\(backToListRoute\)/);
  assert.match(typeSource, /route=\{buildTypeRoute\(1, option\.value\)\}/);
  assert.match(typeSource, /PublicForumPagination/);
});

test('公开论坛发帖入口应使用正式 Web 路径和统一论坛发布器', () => {
  const composeSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumCompose.tsx'), 'utf8');
  const appSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumApp.tsx'), 'utf8');
  const publicEntrySource = readFileSync(resolve(clientRoot, 'src/public/PublicEntry.tsx'), 'utf8');
  const publishModalSource = readFileSync(resolve(clientRoot, 'src/apps/forum/components/PublishPostModal.tsx'), 'utf8');

  assert.match(composeSource, /buildPublicForumComposeReturnPath/);
  assert.match(composeSource, /publishPost\(/);
  assert.match(composeSource, /buildPostSubmissionFingerprint/);
  assert.match(composeSource, /loginReturnPath=\{loginReturnPath\}/);
  assert.match(composeSource, /onNavigate\(\{ kind: 'detail', postId: publishedPostId \}, \{ replace: true \}\)/);
  assert.doesNotMatch(composeSource, /buildDesktopForumReturnPath/);
  assert.match(appSource, /<PublicForumCompose/);
  assert.match(appSource, /route\.kind === 'compose'/);
  assert.match(publicEntrySource, /parsedRoute\.kind === 'detail' \|\| parsedRoute\.kind === 'compose'/);
  assert.match(publicEntrySource, /nextRoute\.route\.kind !== 'detail' && nextRoute\.route\.kind !== 'compose'/);
  assert.match(publishModalSource, /loginReturnPath\?: string \| null;/);
  assert.match(publishModalSource, /returnPath: loginReturnPath \?\? buildDesktopForumReturnPath\(\)/);
});

test('公开论坛详情作者态应使用正式 Web intent 和共享幂等指纹', () => {
  const detailSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumDetail.tsx'), 'utf8');
  const postDetailSource = readFileSync(resolve(clientRoot, 'src/apps/forum/components/PostDetail.tsx'), 'utf8');

  assert.match(detailSource, /intent: 'answer'/);
  assert.match(detailSource, /intent: 'edit'/);
  assert.match(detailSource, /intent: 'history'/);
  assert.match(detailSource, /href=\{answerReturnPath\}/);
  assert.match(detailSource, /href=\{quickReplyReturnPath\}/);
  assert.match(detailSource, /href=\{editReturnPath\}/);
  assert.match(detailSource, /href=\{historyReturnPath\}/);
  assert.match(detailSource, /href=\{commentReturnPath\}/);
  assert.match(detailSource, /handlePublicForumLinkClick\(event, handleAnswerAction\)/);
  assert.match(detailSource, /handlePublicForumLinkClick\(event, handleQuickReplyAction\)/);
  assert.match(detailSource, /handlePublicForumLinkClick\(event, \(\) => void handleEditPostAction\(\)\)/);
  assert.match(detailSource, /handlePublicForumLinkClick\(event, \(\) => void handleViewPostHistory\(\)\)/);
  assert.match(detailSource, /handlePublicForumLinkClick\(event, handleCommentAction\)/);
  assert.match(detailSource, /answerQuestion\(/);
  assert.match(detailSource, /acceptQuestionAnswer\(/);
  assert.match(detailSource, /updatePost\(/);
  assert.match(detailSource, /getPostEditHistory\(/);
  assert.match(detailSource, /buildAnswerSubmissionFingerprint/);
  assert.match(detailSource, /buildPostEditSubmissionFingerprint/);
  assert.doesNotMatch(detailSource, /buildDesktopForumPostReturnPath/);
  assert.doesNotMatch(detailSource, /openApp/);
  assert.match(postDetailSource, /answerAutoFocusKey\?: string \| null;/);
  assert.match(postDetailSource, /!isReadOnly && onAnswerQuestion/);
  assert.match(postDetailSource, /!isReadOnly && onLike/);
});

test('公开论坛详情应复用统一举报组件覆盖帖子、轻回应和评论', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumDetail.tsx'), 'utf8');

  assert.match(source, /import\('@\/components\/ContentReportModal'\)/);
  assert.match(source, /handleOpenReport\('Post', targetId\)/);
  assert.match(source, /handleOpenReport\('PostQuickReply', targetId\)/);
  assert.match(source, /handleOpenReport\('Comment', targetId\)/);
  assert.match(source, /toast\.error\(t\('report\.loginRequired'\)\)/);
  assert.match(source, /<ContentReportModal/);
});

test('登录态私域入口生成公开链接前应复用 PublicId 校验', () => {
  const circleSource = readFileSync(resolve(clientRoot, 'src/circle/CircleApp.tsx'), 'utf8');
  const meSource = readFileSync(resolve(clientRoot, 'src/me/MeApp.tsx'), 'utf8');
  const leaderboardSource = readFileSync(resolve(clientRoot, 'src/public/leaderboard/PublicLeaderboardApp.tsx'), 'utf8');
  const publicIdSource = readFileSync(resolve(clientRoot, 'src/public/publicId.ts'), 'utf8');

  assert.match(circleSource, /resolvePublicPostRouteIdentifier/);
  assert.match(circleSource, /resolvePublicUserRouteIdentifier/);
  assert.match(circleSource, /const href = buildCirclePath\(\{ tab, page: 1 \}\);/);
  assert.match(circleSource, /href=\{href\}/);
  assert.match(circleSource, /href=\{buildCirclePath\(\{ \.\.\.route, page: route\.page - 1 \}\)\}/);
  assert.match(circleSource, /href=\{buildCirclePath\(\{ \.\.\.route, page: route\.page \+ 1 \}\)\}/);
  assert.doesNotMatch(circleSource, /voPublicId\?\.trim\(\)/);
  assert.match(meSource, /resolvePublicUserRouteIdentifier/);
  assert.match(meSource, /href=\{buildMePath\(\{ kind: 'assets-transactions' \}\)\}/);
  assert.match(meSource, /function isPublicDocsDetailPath/);
  assert.match(meSource, /if \(isPublicDocsDetailPath\(pathname\)\) \{/);
  assert.match(meSource, /rememberPublicRouteSourceTransfer\(href, sourceState\)/);
  assert.doesNotMatch(meSource, /href="\/desktop\?app=radish-pit"/);
  assert.doesNotMatch(meSource, /voPublicId\?\.trim\(\)/);
  assert.match(leaderboardSource, /resolvePublicUserRouteIdentifier/);
  assert.doesNotMatch(leaderboardSource, /voUserPublicId\?\.trim\(\)/);
  assert.match(publicIdSource, /export function resolvePublicUserRouteIdentifier\(/);
  assert.match(publicIdSource, /const publicId = normalizePublicUserId\(user\?\.voPublicId\);/);
  assert.doesNotMatch(publicIdSource, /string \| number/);
});

test('个人中心内容与浏览历史正式页应提供真实公开链接', () => {
  const meSource = readFileSync(resolve(clientRoot, 'src/me/MeApp.tsx'), 'utf8');
  const postListSource = readFileSync(resolve(clientRoot, 'src/apps/profile/components/UserPostList.tsx'), 'utf8');
  const commentListSource = readFileSync(resolve(clientRoot, 'src/apps/profile/components/UserCommentList.tsx'), 'utf8');
  const quickReplyListSource = readFileSync(resolve(clientRoot, 'src/apps/profile/components/UserQuickReplyList.tsx'), 'utf8');
  const browseHistoryListSource = readFileSync(resolve(clientRoot, 'src/apps/profile/components/UserBrowseHistoryList.tsx'), 'utf8');

  assert.match(meSource, /getPostHref=\{\(postId, postPublicId\) => buildForumDetailHref\(postId, postPublicId\)\}/);
  assert.match(
    meSource,
    /getCommentHref=\{\(postId, commentId, postPublicId\) => buildForumDetailHref\(postId, postPublicId, commentId\)\}/
  );
  assert.match(meSource, /getItemHref=\{\(postId, postPublicId\) => buildForumDetailHref\(postId, postPublicId\)\}/);
  assert.match(meSource, /getItemHref=\{buildBrowseHistoryHref\}/);
  assert.match(meSource, /onItemLinkClick=\{\(event, href\) => rememberSourceForPublicLink\(event, href\)\}/);
  assert.doesNotMatch(meSource, /onPostClick=\{/);
  assert.doesNotMatch(meSource, /onCommentClick=\{/);

  assert.match(postListSource, /href=\{href\}/);
  assert.match(postListSource, /onPostLinkClick\?\.\(event, href, post\.voId, post\.voPublicId\)/);
  assert.match(commentListSource, /href=\{href\}/);
  assert.match(commentListSource, /onCommentLinkClick\?\.\(/);
  assert.match(quickReplyListSource, /href=\{href\}/);
  assert.match(quickReplyListSource, /onItemLinkClick\?\.\(event, href, item\.voPostId, item\.voPostPublicId\)/);
  assert.match(browseHistoryListSource, /href=\{href\}/);
  assert.match(browseHistoryListSource, /onItemLinkClick\?\.\(event, href, item\)/);
});

test('公开榜单条目应提供公开详情链接并保留壳层导航拦截', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/leaderboard/PublicLeaderboardApp.tsx'), 'utf8');

  assert.match(source, /buildPublicProfilePath\(\{ kind: 'detail', userId: profileIdentifier, tab: 'posts', page: 1 \}\)/);
  assert.match(source, /buildPublicShopPath\(\{ kind: 'detail', productId \}\)/);
  assert.match(source, /href=\{profileHref\}/);
  assert.match(source, /href=\{productHref\}/);
  assert.match(source, /href=\{buildPublicLeaderboardPath\(typeRoute\)\}/);
  assert.match(source, /href=\{buildPublicLeaderboardPath\(\{ kind: 'list', typeSlug: route\.typeSlug, page: route\.page - 1 \}\)\}/);
  assert.match(source, /href=\{buildPublicLeaderboardPath\(\{ kind: 'list', typeSlug: route\.typeSlug, page \}\)\}/);
  assert.match(source, /href=\{buildPublicLeaderboardPath\(\{ kind: 'list', typeSlug: route\.typeSlug, page: route\.page \+ 1 \}\)\}/);
  assert.match(source, /handleUserProfileLinkClick\(event, profileIdentifier\)/);
  assert.match(source, /handleProductDetailLinkClick\(event, productId\)/);
  assert.match(source, /handlePublicLeaderboardLinkClick\(event,/);
  assert.match(source, /event\.preventDefault\(\);/);
  assert.doesNotMatch(source, /onClick=\{\(\) => handleOpenUserProfile\(item\)\}/);
  assert.doesNotMatch(source, /onClick=\{\(\) => handleOpenProductDetail\(item\)\}/);
});
