import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDir = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDir, '..');

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
  const source = readFileSync(resolve(clientRoot, 'src/i18n.ts'), 'utf8');

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

test('公开个人页和发现页账号动作文案应指向私域 Web 而不是工作台', () => {
  const source = readFileSync(resolve(clientRoot, 'src/i18n.ts'), 'utf8');

  assert.match(source, /Creation and account-only actions such as orders and inventory stay outside this public entry/);
  assert.match(source, /Orders, inventory, assets, and other account actions stay in private Web routes/);
  assert.match(source, /创作以及订单、背包等账号私域动作不在这里开放/);
  assert.match(source, /订单、背包、资产和其他账号动作继续留在私域 Web 路由/);
  assert.doesNotMatch(source, /Inventory, orders, and other workspace-only account actions remain outside this shell/);
  assert.doesNotMatch(source, /Creation, orders, inventory, and other workspace actions stay outside/);
  assert.doesNotMatch(source, /背包、订单和其他工作台专属账号动作继续留在壳层之外/);
  assert.doesNotMatch(source, /创作、订单、背包和其他工作台动作不在这里开放/);
});

test('公开个人页应保持只读边界，不直接暴露关注写操作', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/profile/PublicProfileApp.tsx'), 'utf8');

  assert.doesNotMatch(source, /from '@\/api\/userFollow'/);
  assert.doesNotMatch(source, /followUser\(/);
  assert.doesNotMatch(source, /unfollowUser\(/);
  assert.doesNotMatch(source, /getFollowStatus\(/);
});

test('公开个人页内容查询不应依赖内部用户 ID', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/profile/PublicProfileApp.tsx'), 'utf8');

  assert.doesNotMatch(source, /voUserId/);
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

test('公开社区发现页应为跨入口导航提供公开链接并保留壳层导航拦截', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/discover/PublicDiscoverApp.tsx'), 'utf8');
  const feedSource = readFileSync(resolve(clientRoot, 'src/public/discover/PublicDiscoverFeed.tsx'), 'utf8');

  assert.match(source, /buildPublicDocsPath/);
  assert.match(source, /buildPublicLeaderboardPath/);
  assert.match(source, /buildPublicShopPath/);
  assert.match(source, /shouldHandlePublicDiscoverLink\(event\)/);
  assert.match(source, /href=\{forumListHref\}/);
  assert.match(source, /href=\{docsSearchHref\}/);
  assert.match(source, /href=\{shopProductsHref\}/);
  assert.match(source, /href=\{item\.href\}/);
  assert.match(source, /href=\{buildPublicForumPath\(tagRoute\)\}/);
  assert.match(source, /href=\{buildPublicDocsPath\(\{ kind: 'detail', slug: document\.voSlug \}\)\}/);
  assert.match(source, /const leaderboardHref = buildPublicLeaderboardPath\(leaderboardRoute\);/);
  assert.match(source, /href=\{leaderboardHref\}/);
  assert.match(source, /const productHref = buildPublicShopPath\(productRoute\);/);
  assert.match(source, /href=\{productHref\}/);
  assert.match(source, /handlePublicDiscoverLinkClick\(event,/);
  assert.match(feedSource, /href=\{buildPublicForumPath\(\{ kind: 'list', categoryId: null, sortBy: 'newest', page: 1 \}\)\}/);
  assert.match(feedSource, /href=\{buildPublicDocsPath\(\{ kind: 'list' \}\)\}/);
  assert.match(feedSource, /href=\{buildPublicShopPath\(\{ kind: 'home' \}\)\}/);
  assert.match(feedSource, /handleFeedLinkClick\(event, onOpenForum\)/);
});

test('公开壳层头部发现动作应提供真实链接并保留普通点击处理', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/components/PublicShellHeader.tsx'), 'utf8');
  const shellSource = readFileSync(resolve(clientRoot, 'src/components/web-shell/WebShellHeader.tsx'), 'utf8');

  assert.match(source, /discoverHref = '\/discover'/);
  assert.match(source, /href: discoverHref/);
  assert.match(source, /onClick: onNavigateToDiscover/);
  assert.match(shellSource, /function shouldHandleShellLinkClick/);
  assert.match(shellSource, /href=\{item\.href\}/);
  assert.match(shellSource, /event\.preventDefault\(\);/);
  assert.match(shellSource, /item\.onClick\(\);/);
  assert.doesNotMatch(source, /<button[\s\S]*onClick=\{onNavigateToDiscover\}/);
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

test('文档作者正式 Web 入口应独立于公开 SEO 壳层且不承载治理动作', () => {
  const mainSource = readFileSync(resolve(clientRoot, 'src/main.tsx'), 'utf8');
  const entryRouteSource = readFileSync(resolve(clientRoot, 'src/bootstrap/entryRoute.ts'), 'utf8');
  const docsAuthorSource = readFileSync(resolve(clientRoot, 'src/docs/DocsAuthorApp.tsx'), 'utf8');

  assert.match(mainSource, /const DocsAuthorEntry = lazy/);
  assert.match(mainSource, /: isDocsAuthorRoute[\s\S]*\? DocsAuthorEntry[\s\S]*: isPublicContentRoute/);
  assert.match(entryRouteSource, /pathname\.startsWith\('\/docs\/'\) && !isDocsAuthorPathname\(pathname\)/);
  assert.match(docsAuthorSource, /buildDocsAuthorMineReturnPath/);
  assert.match(docsAuthorSource, /createWikiDocument/);
  assert.match(docsAuthorSource, /updateWikiDocument/);
  assert.match(docsAuthorSource, /getWikiRevisionList/);
  assert.match(docsAuthorSource, /const treeRef = useRef<WikiDocumentTreeNodeVo\[\]>\(\[\]\);/);
  assert.match(docsAuthorSource, /treeRef\.current = collectionState\.tree;/);
  assert.match(docsAuthorSource, /onClick=\{\(event\) => onNavigate\(event, \{ kind: 'revisions', documentId: route\.documentId \}\)\}/);
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
  const source = readFileSync(resolve(clientRoot, 'src/public/PublicEntry.tsx'), 'utf8');

  assert.match(source, /applyPublicStructuredData/);
  assert.match(source, /buildPublicRouteStructuredData\(route\)/);
  assert.match(source, /return removePublicStructuredData/);
});

test('公开用户承诺页应进入公共壳层并提供导航入口', () => {
  const entryRouteSource = readFileSync(resolve(clientRoot, 'src/bootstrap/entryRoute.ts'), 'utf8');
  const publicEntrySource = readFileSync(resolve(clientRoot, 'src/public/PublicEntry.tsx'), 'utf8');
  const publicShellSource = readFileSync(resolve(clientRoot, 'src/public/components/PublicShellHeader.tsx'), 'utf8');
  const webShellSource = readFileSync(resolve(clientRoot, 'src/components/web-shell/WebShellHeader.tsx'), 'utf8');
  const headSource = readFileSync(resolve(clientRoot, 'src/public/publicHead.ts'), 'utf8');

  assert.match(entryRouteSource, /isPublicLegalPathname\(pathname\)/);
  assert.match(publicEntrySource, /parsePublicLegalRoute\(window\.location\.pathname\)/);
  assert.match(publicEntrySource, /<PublicCommitmentsApp/);
  assert.match(publicShellSource, /href: '\/legal'/);
  assert.match(webShellSource, /pathname === '\/legal'/);
  assert.match(headSource, /用户承诺与社区边界/);
});

test('公开论坛详情加载后应刷新详情 head 并复用同一个 canonical', () => {
  const source = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumDetail.tsx'), 'utf8');

  assert.match(source, /applyPublicHead/);
  assert.match(source, /buildForumPostPublicHead/);
  assert.match(source, /const postHead = buildForumPostPublicHead\(post, commentId, coverImageUrl\);/);
  assert.match(source, /applyPublicHead\(postHead\);/);
  assert.match(source, /canonicalPath: postHead\.canonicalPath/);
});

test('公开论坛浏览入口应提供公开链接并保留壳层导航拦截', () => {
  const linksSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumLinks.tsx'), 'utf8');
  const handlersSource = readFileSync(resolve(clientRoot, 'src/public/forum/publicForumLinkHandlers.ts'), 'utf8');
  const statusSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicStatusCard.tsx'), 'utf8');
  const appSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumApp.tsx'), 'utf8');
  const detailSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumDetail.tsx'), 'utf8');
  const listSource = readFileSync(resolve(clientRoot, 'src/public/forum/PublicForumList.tsx'), 'utf8');
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
  assert.match(detailSource, /secondaryAction=\{\{[\s\S]*label: backLabel,[\s\S]*href: backHref,[\s\S]*onClick: onBack[\s\S]*\}\}/);
  assert.match(listSource, /PublicForumPagination/);
  assert.match(listSource, /route=\{createDefaultSearchRoute\(\)\}/);
  assert.match(listSource, /route=\{buildListRoute\(1, selectedCategoryId, 'hottest'\)\}/);
  assert.match(listSource, /route=\{buildListRoute\(1, nextCategoryId\)\}/);
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
