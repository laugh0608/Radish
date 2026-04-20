import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import '../../forum/data/forum_models.dart';
import 'discover_models.dart';

abstract class DiscoverRepository {
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  });
}

class HttpDiscoverRepository implements DiscoverRepository {
  const HttpDiscoverRepository({
    required this.apiClient,
    required this.endpoints,
  });

  final RadishApiClient apiClient;
  final RadishApiEndpoints endpoints;

  @override
  Future<DiscoverSnapshot> getSnapshot({
    required int pageSize,
  }) async {
    final forumPostsFuture = _getForumPosts(pageSize);
    final documentsFuture = _getDocuments(pageSize);
    final productsFuture = _getProducts(pageSize);

    return DiscoverSnapshot(
      forumPosts: await forumPostsFuture,
      documents: await documentsFuture,
      products: await productsFuture,
    );
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

  Future<List<DiscoverDocumentSummary>> _getDocuments(int pageSize) async {
    final uri = endpoints.resolveApi(
      '/api/v1/Wiki/GetList',
      queryParameters: {
        'pageIndex': '1',
        'pageSize': pageSize.toString(),
      },
    );

    final page = await apiClient.get(
      uri: uri,
      decode: DiscoverDocumentPage.fromJson,
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
