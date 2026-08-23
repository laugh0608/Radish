part of 'smoke_test.dart';

class _FakeDocsRepository implements DocsRepository {
  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) async {
    return const DocsDocumentPage(
      page: 1,
      pageSize: 20,
      dataCount: 0,
      pageCount: 1,
      documents: [],
    );
  }

  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) async {
    return DocsDocumentDetail(
      id: 'doc-$slug',
      title: 'Doc $slug',
      slug: slug,
      markdownContent: '# $slug',
      sourceType: 'Markdown',
      visibility: 1,
      status: 1,
      createTime: '2026-04-20T08:00:00Z',
    );
  }
}

class _SearchableDocsRepository extends _FakeDocsRepository {
  static const _documents = [
    DocsDocumentSummary(
      id: 'doc-flutter-docs-scope',
      title: 'Radish Flutter docs scope',
      slug: 'flutter-docs-scope',
      summary: 'Native Flutter docs wiring.',
    ),
    DocsDocumentSummary(
      id: 'doc-public-docs-reading-boundary',
      title: 'Public docs reading boundary',
      slug: 'public-docs-reading-boundary',
      summary: 'Keep editing outside the native docs search batch.',
    ),
  ];

  @override
  Future<DocsDocumentPage> getDocumentPage({
    required int pageIndex,
    required int pageSize,
    String? keyword,
  }) async {
    final normalizedKeyword = keyword?.trim().toLowerCase();
    final documents = _documents.where((document) {
      if (normalizedKeyword == null || normalizedKeyword.isEmpty) {
        return true;
      }

      return document.title.toLowerCase().contains(normalizedKeyword) ||
          document.slug.toLowerCase().contains(normalizedKeyword) ||
          (document.summary?.toLowerCase().contains(normalizedKeyword) ??
              false);
    }).toList();

    return DocsDocumentPage(
      page: pageIndex,
      pageSize: pageSize,
      dataCount: documents.length,
      pageCount: 1,
      documents: documents,
    );
  }
}

class _LinkedDocsRepository extends _FakeDocsRepository {
  @override
  Future<DocsDocumentDetail> getDocumentDetail({
    required String slug,
  }) async {
    if (slug == 'flutter-docs-scope') {
      return DocsDocumentDetail(
        id: 'doc-$slug',
        title: 'Doc $slug',
        slug: slug,
        markdownContent:
            '# $slug\n继续阅读 [公开阅读边界](/docs/public-docs-reading-boundary)',
        sourceType: 'Markdown',
        visibility: 1,
        status: 1,
        createTime: '2026-04-20T08:00:00Z',
      );
    }

    return super.getDocumentDetail(slug: slug);
  }
}
