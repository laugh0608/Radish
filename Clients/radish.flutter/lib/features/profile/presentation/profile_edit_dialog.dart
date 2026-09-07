import 'package:flutter/material.dart';

import '../../../core/layout/radish_window_class.dart';
import '../../../core/theme/radish_theme.dart';
import '../../../shared/widgets/radish_state_slot.dart';
import '../data/profile_repository.dart';
import 'profile_edit_controller.dart';

class ProfileEditDialog extends StatefulWidget {
  const ProfileEditDialog({
    required this.repository,
    required this.accessToken,
    super.key,
  });

  final ProfileRepository repository;
  final String accessToken;

  @override
  State<ProfileEditDialog> createState() => _ProfileEditDialogState();
}

class _ProfileEditDialogState extends State<ProfileEditDialog> {
  final _formKey = GlobalKey<FormState>();
  late ProfileEditController _controller;

  @override
  void initState() {
    super.initState();
    _controller = ProfileEditController(
      repository: widget.repository,
      accessToken: widget.accessToken,
    );
    _controller.load();
  }

  @override
  void didUpdateWidget(covariant ProfileEditDialog oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository == widget.repository &&
        oldWidget.accessToken == widget.accessToken) {
      return;
    }
    _controller.dispose();
    _controller = ProfileEditController(
      repository: widget.repository,
      accessToken: widget.accessToken,
    );
    _controller.load();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _requestClose() async {
    if (_controller.isSaving) {
      return;
    }
    if (!_controller.isDirty) {
      Navigator.of(context).pop(false);
      return;
    }

    final discard = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (context) => AlertDialog(
        title: const Text('丢弃未保存更改？'),
        content: const Text('当前资料尚未保存，退出后本次修改会丢失。'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('继续编辑'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('丢弃更改'),
          ),
        ],
      ),
    );
    if (discard == true && mounted) {
      Navigator.of(context).pop(false);
    }
  }

  Future<void> _save() async {
    final form = _formKey.currentState;
    if (form == null || !form.validate()) {
      return;
    }
    final saved = await _controller.save();
    if (saved && mounted) {
      Navigator.of(context).pop(true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final task = AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        final windowClass = RadishWindowClassResolution.fromWidth(
          MediaQuery.sizeOf(context).width,
        );
        return PopScope<bool>(
          canPop: !_controller.isSaving && !_controller.isDirty,
          onPopInvokedWithResult: (didPop, result) {
            if (!didPop) {
              _requestClose();
            }
          },
          child: windowClass == RadishWindowClass.compact
              ? _buildCompactTask()
              : _buildBoundedTask(),
        );
      },
    );
    if (Theme.of(context).extension<RadishThemeTokens>() == null) {
      return Theme(
        data: buildRadishTheme(RadishThemeId.defaultTheme),
        child: task,
      );
    }
    return task;
  }

  Widget _buildCompactTask() {
    return Dialog.fullscreen(
      key: const Key('profile-edit-task-compact'),
      child: Scaffold(
        appBar: AppBar(
          leading: IconButton(
            key: const Key('profile-edit-close'),
            tooltip: '关闭资料编辑',
            onPressed: _controller.isSaving ? null : _requestClose,
            icon: const Icon(Icons.close),
          ),
          title: const Text('编辑个人资料'),
        ),
        body: SafeArea(
          top: false,
          child: _ProfileEditTaskBody(
            controller: _controller,
            formKey: _formKey,
            onRetry: _controller.load,
            onSave: _save,
          ),
        ),
      ),
    );
  }

  Widget _buildBoundedTask() {
    final maxHeight =
        (MediaQuery.sizeOf(context).height - 64).clamp(420.0, 800.0).toDouble();
    return Dialog(
      key: const Key('profile-edit-task-bounded'),
      insetPadding: const EdgeInsets.all(RadishSpacing.xLarge),
      clipBehavior: Clip.antiAlias,
      child: ConstrainedBox(
        constraints: BoxConstraints(maxWidth: 680, maxHeight: maxHeight),
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(
                RadishSpacing.xLarge,
                RadishSpacing.large,
                RadishSpacing.medium,
                RadishSpacing.large,
              ),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      '编辑个人资料',
                      style: Theme.of(context).textTheme.titleLarge,
                    ),
                  ),
                  IconButton(
                    key: const Key('profile-edit-close'),
                    tooltip: '关闭资料编辑',
                    onPressed: _controller.isSaving ? null : _requestClose,
                    icon: const Icon(Icons.close),
                  ),
                ],
              ),
            ),
            const Divider(height: 1),
            Expanded(
              child: _ProfileEditTaskBody(
                controller: _controller,
                formKey: _formKey,
                onRetry: _controller.load,
                onSave: _save,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ProfileEditTaskBody extends StatelessWidget {
  const _ProfileEditTaskBody({
    required this.controller,
    required this.formKey,
    required this.onRetry,
    required this.onSave,
  });

  final ProfileEditController controller;
  final GlobalKey<FormState> formKey;
  final VoidCallback onRetry;
  final VoidCallback onSave;

  @override
  Widget build(BuildContext context) {
    if (controller.isLoading) {
      return const Center(
        child: RadishStateSlot(
          kind: RadishStateKind.loading,
          title: '正在加载个人资料',
          message: '读取可编辑资料权威快照。',
        ),
      );
    }
    if (controller.isUnavailable) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(RadishSpacing.large),
          child: RadishStateSlot(
            kind: RadishStateKind.unavailable,
            title: '加载个人资料失败',
            message: controller.loadIssue?.message ?? '暂时无法读取个人资料。',
            action: FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('重试'),
            ),
          ),
        ),
      );
    }

    final profile = controller.profile;
    return Column(
      children: [
        if (controller.saveIssue != null)
          Padding(
            padding: const EdgeInsets.fromLTRB(
              RadishSpacing.xLarge,
              RadishSpacing.large,
              RadishSpacing.xLarge,
              0,
            ),
            child: RadishStateSlot(
              kind: RadishStateKind.error,
              title: '保存个人资料失败',
              message: controller.saveIssue!.message,
              compact: true,
            ),
          ),
        Expanded(
          child: SingleChildScrollView(
            key: const Key('profile-edit-scroll'),
            keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
            padding: const EdgeInsets.all(RadishSpacing.xLarge),
            child: Form(
              key: formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (profile != null) ...[
                    Text(
                      '用户 ID：${profile.userId}',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                    const SizedBox(height: RadishSpacing.large),
                  ],
                  TextFormField(
                    key: const Key('profile-edit-user-name'),
                    controller: controller.userNameController,
                    enabled: !controller.isSaving,
                    decoration: const InputDecoration(
                      labelText: '展示名',
                      helperText: '公开资料、帖子、评论和艾特搜索会显示该名称。',
                    ),
                    validator: _validateUserName,
                  ),
                  const SizedBox(height: RadishSpacing.medium),
                  TextFormField(
                    key: const Key('profile-edit-email'),
                    controller: controller.emailController,
                    enabled: !controller.isSaving,
                    decoration: const InputDecoration(labelText: '邮箱'),
                    keyboardType: TextInputType.emailAddress,
                    validator: _validateEmail,
                  ),
                  const SizedBox(height: RadishSpacing.medium),
                  TextFormField(
                    key: const Key('profile-edit-age'),
                    controller: controller.ageController,
                    enabled: !controller.isSaving,
                    decoration: const InputDecoration(labelText: '年龄'),
                    keyboardType: TextInputType.number,
                    validator: _validateAge,
                  ),
                  const SizedBox(height: RadishSpacing.medium),
                  TextFormField(
                    key: const Key('profile-edit-address'),
                    controller: controller.addressController,
                    enabled: !controller.isSaving,
                    decoration: const InputDecoration(labelText: '地址'),
                    minLines: 2,
                    maxLines: 4,
                    validator: _validateAddress,
                  ),
                  const SizedBox(height: RadishSpacing.xLarge),
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton(
                          onPressed: controller.isSaving
                              ? null
                              : () =>
                                  Navigator.of(context).maybePop<bool>(false),
                          child: const Text('取消'),
                        ),
                      ),
                      const SizedBox(width: RadishSpacing.medium),
                      Expanded(
                        child: FilledButton.icon(
                          onPressed: controller.isSaving ? null : onSave,
                          icon: controller.isSaving
                              ? const SizedBox.square(
                                  dimension: 16,
                                  child:
                                      CircularProgressIndicator(strokeWidth: 2),
                                )
                              : const Icon(Icons.save_outlined),
                          label: Text(controller.isSaving ? '正在保存' : '保存'),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }
}

String? _validateUserName(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) return '请输入展示名';
  if (text.length < 2 || text.length > 50) return '展示名长度为 2-50 个字符';
  return null;
}

String? _validateEmail(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) return '请输入邮箱';
  if (!RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(text)) {
    return '请输入有效的邮箱地址';
  }
  if (text.length > 200) return '邮箱长度不能超过 200 个字符';
  return null;
}

String? _validateAge(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) return null;
  final age = int.tryParse(text);
  if (age == null || age < 0) return '年龄必须是非负整数';
  return null;
}

String? _validateAddress(String? value) {
  if ((value?.trim() ?? '').length > 2000) return '地址长度不能超过 2000 个字符';
  return null;
}
