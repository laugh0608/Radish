import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../data/docs_models.dart';
import '../data/docs_repository.dart';
import 'docs_catalog_surface.dart';
import 'docs_detail_controller.dart';
import 'docs_feed_controller.dart';
import 'docs_reader_route.dart';
import 'docs_reader_surface.dart';

class DocsPage extends StatefulWidget {
  const DocsPage({
    required this.environment,
    required this.repository,
    this.handoffTarget,
    this.onConsumeHandoffTarget,
    this.onRecordDocumentTarget,
    this.onInlineDetailBackHandlerChanged,
    super.key,
  });

  final AppEnvironment environment;
  final DocsRepository repository;
  final DocsDetailHandoffTarget? handoffTarget;
  final VoidCallback? onConsumeHandoffTarget;
  final ValueChanged<DocsDetailHandoffTarget>? onRecordDocumentTarget;
  final ValueChanged<VoidCallback?>? onInlineDetailBackHandlerChanged;

  @override
  State<DocsPage> createState() => _DocsPageState();
}

class _DocsPageState extends State<DocsPage> {
  late DocsFeedController _feedController;
  late DocsDetailController _detailController;
  late TextEditingController _searchController;
  late ScrollController _feedScrollController;
  late ScrollController _readerScrollController;
  RadishWindowClass _windowClass = RadishWindowClass.compact;
  double _feedScrollOffset = 0;
  bool _reportedInlineDetailMode = false;
  String? _handledHandoffSignature;

  @override
  void initState() {
    super.initState();
    _createOwners();
    _feedController.loadInitial();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _openHandoffTargetIfNeeded();
    });
  }

  @override
  void didUpdateWidget(covariant DocsPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository != widget.repository) {
      _disposeRemoteOwners();
      _feedController = DocsFeedController(repository: widget.repository);
      _detailController = DocsDetailController(repository: widget.repository)
        ..addListener(_handleInlineDetailBackHandlerChanged);
      _searchController.text = '';
      _feedScrollOffset = 0;
      _reportedInlineDetailMode = false;
      _reportInlineDetailBackHandler(force: true);
      _handledHandoffSignature = null;
      _feedController.loadInitial();
    }
    if (oldWidget.onInlineDetailBackHandlerChanged !=
        widget.onInlineDetailBackHandlerChanged) {
      _reportInlineDetailBackHandler(force: true);
    }
    if (oldWidget.handoffTarget != null && widget.handoffTarget == null) {
      _handledHandoffSignature = null;
    }
    if (oldWidget.handoffTarget != widget.handoffTarget) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        _openHandoffTargetIfNeeded();
      });
    }
  }

  @override
  void dispose() {
    _disposeRemoteOwners();
    _searchController.dispose();
    _feedScrollController.dispose();
    _readerScrollController.dispose();
    widget.onInlineDetailBackHandlerChanged?.call(null);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: Listenable.merge([_feedController, _detailController]),
      builder: (context, child) {
        return LayoutBuilder(
          builder: (context, constraints) {
            _windowClass = RadishWindowClassResolution.fromWidth(
              constraints.maxWidth,
            );
            final isDetailMode = !_detailController.state.isIdle;
            return PopScope<void>(
              canPop: !isDetailMode,
              onPopInvokedWithResult: (didPop, result) {
                if (!didPop && isDetailMode) {
                  _closeInlineDetail();
                }
              },
              child: Material(
                type: MaterialType.transparency,
                child: RadishContentFrame(
                  child: LayoutBuilder(
                    builder: (context, frameConstraints) {
                      return SizedBox(
                        height: frameConstraints.maxHeight,
                        child: switch (_windowClass) {
                          RadishWindowClass.compact => _buildCompact(),
                          RadishWindowClass.medium => _buildSplit(
                              frameConstraints,
                              isExpanded: false,
                            ),
                          RadishWindowClass.expanded => _buildSplit(
                              frameConstraints,
                              isExpanded: true,
                            ),
                        },
                      );
                    },
                  ),
                ),
              ),
            );
          },
        );
      },
    );
  }

  Widget _buildCompact() {
    final detailState = _detailController.state;
    if (!detailState.isIdle) {
      return ListView(
        key: const Key('docs-layout-compact-reader'),
        controller: _readerScrollController,
        children: [
          DocsReaderSurface(
            environment: widget.environment,
            state: detailState,
            onRefresh: _detailController.refresh,
            onBack: _closeInlineDetail,
            onOpenDocumentSlug: _openLinkedDocumentFromTab,
          ),
        ],
      );
    }
    return ListView(
      key: const Key('docs-layout-compact-catalog'),
      controller: _feedScrollController,
      children: [_buildCatalog(isPane: false)],
    );
  }

  Widget _buildSplit(
    BoxConstraints constraints, {
    required bool isExpanded,
  }) {
    final canUseFixedExpandedAxes = isExpanded && constraints.maxWidth >= 1208;
    final catalogWidth = canUseFixedExpandedAxes
        ? 280.0
        : (constraints.maxWidth * 0.32).clamp(200.0, 280.0);
    final reader = ListView(
      key: const Key('docs-reader-pane'),
      controller: _readerScrollController,
      children: [
        if (_detailController.state.isIdle)
          const DocsReaderEmptySurface()
        else
          DocsReaderSurface(
            environment: widget.environment,
            state: _detailController.state,
            onRefresh: _detailController.refresh,
            onBack: _closeInlineDetail,
            onOpenDocumentSlug: _openLinkedDocumentFromTab,
          ),
      ],
    );
    return Row(
      key: Key(isExpanded ? 'docs-layout-expanded' : 'docs-layout-medium'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        SizedBox(
          key: canUseFixedExpandedAxes
              ? const Key('docs-catalog-axis-280')
              : null,
          width: catalogWidth,
          child: ListView(
            controller: _feedScrollController,
            children: [_buildCatalog(isPane: true)],
          ),
        ),
        SizedBox(
          width: canUseFixedExpandedAxes
              ? RadishSpacing.xLarge
              : RadishSpacing.large,
        ),
        if (canUseFixedExpandedAxes)
          SizedBox(
            key: const Key('docs-reader-main-axis-904'),
            width: 904,
            child: reader,
          )
        else
          Expanded(child: reader),
      ],
    );
  }

  Widget _buildCatalog({required bool isPane}) {
    final state = _feedController.state;
    return DocsCatalogSurface(
      state: state,
      searchController: _searchController,
      selectedSlug: _detailController.state.slug,
      isPane: isPane,
      onSearch: _searchDocs,
      onClearSearch: _clearDocsSearch,
      onRefresh: _feedController.refresh,
      onOpenDocument: _openDocumentFromList,
      onPreviousPage: state.hasPreviousPage
          ? () => _goToDocsPage(state.pageIndex - 1)
          : null,
      onNextPage:
          state.hasNextPage ? () => _goToDocsPage(state.pageIndex + 1) : null,
    );
  }

  void _createOwners() {
    _feedController = DocsFeedController(repository: widget.repository);
    _detailController = DocsDetailController(repository: widget.repository)
      ..addListener(_handleInlineDetailBackHandlerChanged);
    _searchController = TextEditingController();
    _feedScrollController = ScrollController();
    _readerScrollController = ScrollController();
  }

  void _disposeRemoteOwners() {
    _feedController.dispose();
    _detailController.removeListener(_handleInlineDetailBackHandlerChanged);
    _detailController.dispose();
  }

  void _openDocumentFromList(DocsDocumentSummary document) {
    if (document.slug.trim().isEmpty) {
      return;
    }
    _rememberFeedScrollOffset();
    final target = DocsDetailHandoffTarget(
      slug: document.slug,
      source: DocsDetailHandoffSource.docsList,
      initialTitle: document.title,
    );
    widget.onRecordDocumentTarget?.call(target);
    _detailController.openTarget(target);
    _scrollReaderToTopAfterFrame();
  }

  void _openLinkedDocumentFromTab(String slug) {
    final target = DocsDetailHandoffTarget(
      slug: slug,
      source: DocsDetailHandoffSource.docsLink,
    );
    widget.onRecordDocumentTarget?.call(target);
    _detailController.openTarget(target);
    _scrollReaderToTopAfterFrame();
  }

  void _searchDocs() {
    final keyword = _searchController.text.trim();
    _syncSearchControllerText(keyword);
    _feedController.search(keyword);
    _scrollFeedToTopAfterFrame();
  }

  void _clearDocsSearch() {
    _searchController.clear();
    _feedController.clearSearch();
    _scrollFeedToTopAfterFrame();
  }

  void _goToDocsPage(int pageIndex) {
    _feedController.goToPage(pageIndex);
    _scrollFeedToTopAfterFrame();
  }

  void _closeInlineDetail() {
    _detailController.close();
    _restoreFeedScrollOffset();
  }

  void _handleInlineDetailBackHandlerChanged() {
    _reportInlineDetailBackHandler();
  }

  void _reportInlineDetailBackHandler({bool force = false}) {
    final isDetailMode = !_detailController.state.isIdle;
    if (!force && _reportedInlineDetailMode == isDetailMode) {
      return;
    }
    _reportedInlineDetailMode = isDetailMode;
    widget.onInlineDetailBackHandlerChanged?.call(
      isDetailMode ? _closeInlineDetail : null,
    );
  }

  void _rememberFeedScrollOffset() {
    if (_feedScrollController.hasClients) {
      _feedScrollOffset = _feedScrollController.offset;
    }
  }

  void _restoreFeedScrollOffset() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || !_feedScrollController.hasClients) {
        return;
      }
      final max = _feedScrollController.position.maxScrollExtent;
      _feedScrollController.jumpTo(_feedScrollOffset.clamp(0.0, max));
    });
  }

  void _scrollFeedToTopAfterFrame() {
    _feedScrollOffset = 0;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && _feedScrollController.hasClients) {
        _feedScrollController.jumpTo(0);
      }
    });
  }

  void _scrollReaderToTopAfterFrame() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && _readerScrollController.hasClients) {
        _readerScrollController.jumpTo(0);
      }
    });
  }

  void _syncSearchControllerText(String keyword) {
    if (_searchController.text == keyword) {
      return;
    }
    _searchController.value = TextEditingValue(
      text: keyword,
      selection: TextSelection.collapsed(offset: keyword.length),
    );
  }

  void _openHandoffTargetIfNeeded() {
    if (!mounted) {
      return;
    }
    final target = widget.handoffTarget;
    if (target == null || !target.hasValidSlug) {
      return;
    }
    final signature = '${target.source.name}:${target.normalizedSlug}';
    if (_handledHandoffSignature == signature) {
      return;
    }
    _handledHandoffSignature = signature;
    widget.onConsumeHandoffTarget?.call();
    widget.onRecordDocumentTarget?.call(target);
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => DocsReaderRoutePage(
          environment: widget.environment,
          repository: widget.repository,
          target: target,
          onRecordDocumentTarget: widget.onRecordDocumentTarget,
        ),
      ),
    );
  }
}
