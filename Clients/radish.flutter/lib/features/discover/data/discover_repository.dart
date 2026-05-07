import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import '../../docs/data/docs_models.dart';
import '../../docs/data/docs_repository.dart';
import '../../forum/data/forum_models.dart';
import 'discover_models.dart';

abstract class DiscoverRepository {
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  });
}

class HttpDiscoverRepository implements DiscoverRepository {
  HttpDiscoverRepository({
    required this.apiClient,
    required this.endpoints,
  }) : _docsRepository = HttpDocsRepository(
          apiClient: apiClient,
          endpoints: endpoints,
        );

  final RadishApiClient apiClient;
  final RadishApiEndpoints endpoints;
  final HttpDocsRepository _docsRepository;

  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    final forumPostsFuture = _loadSection(
      section: DiscoverSection.forum,
      load: () => _getForumPosts(pageSize),
    );
    final documentsFuture = _loadSection(
      section: DiscoverSection.docs,
      load: () => _getDocuments(pageSize),
    );
    final productsFuture = _loadSection(
      section: DiscoverSection.shop,
      load: () => _getProducts(pageSize),
    );

    final forumPostsResult = await forumPostsFuture;
    final documentsResult = await documentsFuture;
    final productsResult = await productsFuture;

    return DiscoverSnapshot(
      forumPosts: forumPostsResult.items,
      documents: documentsResult.items,
      products: productsResult.items,
      sectionIssues: [
        if (forumPostsResult.issue != null) forumPostsResult.issue!,
        if (documentsResult.issue != null) documentsResult.issue!,
        if (productsResult.issue != null) productsResult.issue!,
      ],
    );
  }

  Future<_DiscoverSectionResult<T>> _loadSection<T>({
    required DiscoverSection section,
    required Future<List<T>> Function() load,
  }) async {
    try {
      return _DiscoverSectionResult<T>(
        items: await load(),
      );
    } on RadishApiClientException catch (error) {
      return _DiscoverSectionResult<T>(
        items: const [],
        issue: DiscoverSectionIssue(
          section: section,
          message: error.message,
        ),
      );
    } on FormatException catch (error) {
      return _DiscoverSectionResult<T>(
        items: const [],
        issue: DiscoverSectionIssue(
          section: section,
          message: '返回格式异常：${error.message}',
        ),
      );
    }
  }

  Future<List<ForumPostSummary>> _getForumPosts(int pageSize) async {
    final uri = endpoints.resolveApi(
      '/api/v1/Post/GetList',
      queryParameters: {
        'pageIndex': '1',
        'pageSize': pageSize.toString(),
        'sortBy': ForumFeedSort.newest.apiValue,
        'postType': 'all',
      },
    );

    final page = await apiClient.get(
      uri: uri,
      decode: ForumPostPage.fromJson,
    );
    return page.posts;
  }

  Future<List<DocsDocumentSummary>> _getDocuments(int pageSize) async {
    final page = await _docsRepository.getDocumentPage(
      pageIndex: 1,
      pageSize: pageSize,
    );

    return page.documents;
  }

  Future<List<DiscoverProductSummary>> _getProducts(int pageSize) async {
    final uri = endpoints.resolveApi(
      '/api/v1/Shop/GetProducts',
      queryParameters: {
        'pageIndex': '1',
        'pageSize': pageSize.toString(),
      },
    );

    final page = await apiClient.get(
      uri: uri,
      decode: DiscoverProductPage.fromJson,
    );
    return page.products;
  }
}

class _DiscoverSectionResult<T> {
  const _DiscoverSectionResult({
    required this.items,
    this.issue,
  });

  final List<T> items;
  final DiscoverSectionIssue? issue;
}
