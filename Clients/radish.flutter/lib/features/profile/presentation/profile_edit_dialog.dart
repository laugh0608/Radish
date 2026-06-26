import 'package:flutter/material.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/profile_models.dart';
import '../data/profile_repository.dart';

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
  final _userNameController = TextEditingController();
  final _emailController = TextEditingController();
  final _ageController = TextEditingController();
  final _addressController = TextEditingController();

  MyProfileInfo? _profile;
  bool _loading = true;
  bool _saving = false;
  String? _errorMessage;
  String? _saveErrorMessage;

  @override
  void initState() {
    super.initState();
    _loadProfile();
  }

  @override
  void dispose() {
    _userNameController.dispose();
    _emailController.dispose();
    _ageController.dispose();
    _addressController.dispose();
    super.dispose();
  }

  Future<void> _loadProfile() async {
    setState(() {
      _loading = true;
      _errorMessage = null;
    });

    try {
      final profile = await widget.repository.getMyProfile(
        accessToken: widget.accessToken,
      );
      if (!mounted) {
        return;
      }

      _profile = profile;
      _userNameController.text = profile.userName;
      _emailController.text = profile.userEmail;
      _ageController.text = profile.age > 0 ? profile.age.toString() : '';
      _addressController.text = profile.address;
      setState(() {
        _loading = false;
      });
    } catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _loading = false;
        _errorMessage = _formatErrorMessage(error, '加载个人资料失败');
      });
    }
  }

  Future<void> _saveProfile() async {
    final form = _formKey.currentState;
    if (form == null || !form.validate()) {
      return;
    }

    final ageText = _ageController.text.trim();
    final age = ageText.isEmpty ? null : int.parse(ageText);
    final request = UpdateMyProfileRequest(
      userName: _userNameController.text.trim(),
      userEmail: _emailController.text.trim(),
      age: age,
      address: _emptyToNull(_addressController.text),
    );

    setState(() {
      _saving = true;
      _saveErrorMessage = null;
    });

    try {
      await widget.repository.updateMyProfile(
        request: request,
        accessToken: widget.accessToken,
      );
      if (!mounted) {
        return;
      }

      Navigator.of(context).pop(true);
    } catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _saving = false;
        _saveErrorMessage = _formatErrorMessage(error, '保存个人资料失败');
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final profile = _profile;

    return AlertDialog(
      title: const Text('编辑个人资料'),
      content: SizedBox(
        width: 520,
        child: _loading
            ? const _ProfileEditLoading()
            : _errorMessage != null
                ? _ProfileEditError(
                    message: _errorMessage!,
                    onRetry: _loadProfile,
                  )
                : Form(
                    key: _formKey,
                    child: SingleChildScrollView(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          if (profile != null) ...[
                            Text(
                              '用户 ID：${profile.userId}',
                              style: Theme.of(context).textTheme.bodySmall,
                            ),
                            const SizedBox(height: 12),
                          ],
                          TextFormField(
                            key: const Key('profile-edit-user-name'),
                            controller: _userNameController,
                            enabled: !_saving,
                            decoration: const InputDecoration(
                              labelText: '展示名',
                              helperText: '公开资料、帖子、评论和艾特搜索会显示该名称。',
                            ),
                            validator: _validateUserName,
                          ),
                          const SizedBox(height: 12),
                          TextFormField(
                            key: const Key('profile-edit-email'),
                            controller: _emailController,
                            enabled: !_saving,
                            decoration: const InputDecoration(
                              labelText: '邮箱',
                            ),
                            keyboardType: TextInputType.emailAddress,
                            validator: _validateEmail,
                          ),
                          const SizedBox(height: 12),
                          TextFormField(
                            key: const Key('profile-edit-age'),
                            controller: _ageController,
                            enabled: !_saving,
                            decoration: const InputDecoration(
                              labelText: '年龄',
                            ),
                            keyboardType: TextInputType.number,
                            validator: _validateAge,
                          ),
                          const SizedBox(height: 12),
                          TextFormField(
                            key: const Key('profile-edit-address'),
                            controller: _addressController,
                            enabled: !_saving,
                            decoration: const InputDecoration(
                              labelText: '地址',
                            ),
                            minLines: 2,
                            maxLines: 4,
                            validator: _validateAddress,
                          ),
                          if (_saveErrorMessage != null) ...[
                            const SizedBox(height: 16),
                            Text(
                              _saveErrorMessage!,
                              style: TextStyle(
                                color: Theme.of(context).colorScheme.error,
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
                  ),
      ),
      actions: [
        TextButton(
          onPressed: _saving ? null : () => Navigator.of(context).pop(false),
          child: const Text('取消'),
        ),
        FilledButton.icon(
          onPressed: _loading || _errorMessage != null || _saving
              ? null
              : _saveProfile,
          icon: _saving
              ? const SizedBox.square(
                  dimension: 16,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Icon(Icons.save_outlined),
          label: Text(_saving ? '正在保存' : '保存'),
        ),
      ],
    );
  }
}

class _ProfileEditLoading extends StatelessWidget {
  const _ProfileEditLoading();

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.symmetric(vertical: 32),
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            CircularProgressIndicator(),
            SizedBox(height: 16),
            Text('正在加载个人资料...'),
          ],
        ),
      ),
    );
  }
}

class _ProfileEditError extends StatelessWidget {
  const _ProfileEditError({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(message),
        const SizedBox(height: 16),
        FilledButton.icon(
          onPressed: onRetry,
          icon: const Icon(Icons.refresh),
          label: const Text('重试'),
        ),
      ],
    );
  }
}

String? _validateUserName(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) {
    return '请输入展示名';
  }

  if (text.length < 2 || text.length > 50) {
    return '展示名长度为 2-50 个字符';
  }

  return null;
}

String? _validateEmail(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) {
    return '请输入邮箱';
  }

  if (!RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(text)) {
    return '请输入有效的邮箱地址';
  }

  if (text.length > 200) {
    return '邮箱长度不能超过 200 个字符';
  }

  return null;
}

String? _validateAge(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) {
    return null;
  }

  final age = int.tryParse(text);
  if (age == null || age < 0) {
    return '年龄必须是非负整数';
  }

  return null;
}

String? _validateAddress(String? value) {
  final text = value?.trim() ?? '';
  if (text.length > 2000) {
    return '地址长度不能超过 2000 个字符';
  }

  return null;
}

String? _emptyToNull(String value) {
  final text = value.trim();
  return text.isEmpty ? null : text;
}

String _formatErrorMessage(Object error, String fallback) {
  if (error is RadishApiClientException) {
    return error.message;
  }

  final text = error.toString();
  if (text.isEmpty) {
    return fallback;
  }

  return text;
}
