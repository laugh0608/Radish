import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import 'docs_models.dart';

abstract class DocsRepository {
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
  });

  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  });
}

class HttpDocsRepository implements DocsRepository {
  const HttpDocsRepository({
    required this.apiClient,
    required this.endpoints,
  });

  final RadishApiClient apiClient;
  final RadishApiEndpoints endpoints;

  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
  }) {
    final uri = endpoints.resolveApi(
      '/api/v1/Wiki/GetList',
      queryParameters: {
        'pageIndex': pageIndex.toString(),
        'pageSize': pageSize.toString(),
      },
    );

    return apiClient.get(
      uri: uri,
      decode: DocsDocumentPage.fromJson,
    );
  }

  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) {
    final uri = endpoints.resolveApi(
      '/api/v1/Wiki/GetBySlug/${Uri.encodeComponent(slug)}',
    );

    return apiClient.get(
      uri: uri,
      decode: DocsDocumentDetail.fromJson,
    );
  }
}
