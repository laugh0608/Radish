import 'package:flutter/material.dart';

import '../../../core/config/app_environment.dart';
import '../../../core/layout/radish_window_class.dart';
import '../data/docs_models.dart';
import '../data/docs_repository.dart';
import 'docs_detail_controller.dart';
import 'docs_reader_surface.dart';

class DocsReaderRoutePage extends StatefulWidget {
  const DocsReaderRoutePage({
    required this.environment,
    required this.repository,
    required this.target,
    this.onRecordDocumentTarget,
    super.key,
  });

  final AppEnvironment environment;
  final DocsRepository repository;
  final DocsDetailHandoffTarget target;
  final ValueChanged<DocsDetailHandoffTarget>? onRecordDocumentTarget;

  @override
  State<DocsReaderRoutePage> createState() => _DocsReaderRoutePageState();
}

class _DocsReaderRoutePageState extends State<DocsReaderRoutePage> {
  late DocsDetailController _controller;

  @override
  void initState() {
    super.initState();
    _controller = DocsDetailController(repository: widget.repository);
    _controller.openTarget(widget.target);
  }

  @override
  void didUpdateWidget(covariant DocsReaderRoutePage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository != widget.repository ||
        oldWidget.target.normalizedSlug != widget.target.normalizedSlug) {
      _controller.dispose();
      _controller = DocsDetailController(repository: widget.repository);
      _controller.openTarget(widget.target);
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        return Scaffold(
          appBar: AppBar(
            title: Text(
              _controller.state.detail?.title ??
                  widget.target.normalizedInitialTitle ??
                  '文档详情',
            ),
          ),
          body: LayoutBuilder(
            builder: (context, constraints) {
              final windowClass = RadishWindowClassResolution.fromWidth(
                constraints.maxWidth,
              );
              return RadishContentFrame(
                maxWidth: windowClass == RadishWindowClass.expanded ? 968 : 920,
                child: ListView(
                  key: Key('docs-reader-route-${windowClass.name}'),
                  children: [
                    ConstrainedBox(
                      key: windowClass == RadishWindowClass.expanded
                          ? const Key('docs-reader-route-axis-904')
                          : null,
                      constraints: const BoxConstraints(maxWidth: 904),
                      child: DocsReaderSurface(
                        environment: widget.environment,
                        state: _controller.state,
                        onRefresh: _controller.refresh,
                        onBack: () => Navigator.of(context).maybePop(),
                        backLabel: '返回来源',
                        onOpenDocumentSlug: _openLinkedDocument,
                      ),
                    ),
                  ],
                ),
              );
            },
          ),
        );
      },
    );
  }

  void _openLinkedDocument(String slug) {
    final target = DocsDetailHandoffTarget(
      slug: slug,
      source: DocsDetailHandoffSource.docsLink,
    );
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
