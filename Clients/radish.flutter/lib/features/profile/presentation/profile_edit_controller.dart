import 'package:flutter/material.dart';

import '../../../core/network/radish_api_client.dart';
import '../data/profile_models.dart';
import '../data/profile_repository.dart';
import 'profile_controller.dart';

enum ProfileEditStatus { loading, ready, unavailable }

class ProfileEditController extends ChangeNotifier {
  ProfileEditController({
    required ProfileRepository repository,
    required String accessToken,
  })  : _repository = repository,
        _accessToken = accessToken.trim() {
    userNameController.addListener(_handleDraftChanged);
    emailController.addListener(_handleDraftChanged);
    ageController.addListener(_handleDraftChanged);
    addressController.addListener(_handleDraftChanged);
  }

  final ProfileRepository _repository;
  final String _accessToken;
  final TextEditingController userNameController = TextEditingController();
  final TextEditingController emailController = TextEditingController();
  final TextEditingController ageController = TextEditingController();
  final TextEditingController addressController = TextEditingController();

  ProfileEditStatus _status = ProfileEditStatus.loading;
  MyProfileInfo? _profile;
  UpdateMyProfileRequest? _baseline;
  ProfileIssue? _loadIssue;
  ProfileIssue? _saveIssue;
  bool _isSaving = false;
  bool _isHydrating = false;
  int _requestVersion = 0;

  ProfileEditStatus get status => _status;
  MyProfileInfo? get profile => _profile;
  ProfileIssue? get loadIssue => _loadIssue;
  ProfileIssue? get saveIssue => _saveIssue;
  bool get isLoading => _status == ProfileEditStatus.loading;
  bool get isReady => _status == ProfileEditStatus.ready;
  bool get isUnavailable => _status == ProfileEditStatus.unavailable;
  bool get isSaving => _isSaving;

  bool get isDirty {
    final baseline = _baseline;
    if (!isReady || baseline == null) {
      return false;
    }
    return !_requestsEqual(baseline, buildRequest());
  }

  Future<void> load() async {
    final version = ++_requestVersion;
    _status = ProfileEditStatus.loading;
    _loadIssue = null;
    _saveIssue = null;
    notifyListeners();

    try {
      final profile = await _repository.getMyProfile(
        accessToken: _accessToken,
      );
      if (version != _requestVersion) {
        return;
      }
      _hydrate(profile);
      _profile = profile;
      _baseline = buildRequest();
      _status = ProfileEditStatus.ready;
      notifyListeners();
    } on RadishApiClientException catch (error) {
      _rejectLoad(version, ProfileIssue.fromApi(error));
    } on FormatException catch (error) {
      _rejectLoad(
        version,
        ProfileIssue.invalidResponse(error, resourceLabel: '个人资料'),
      );
    } catch (error) {
      _rejectLoad(version, _unexpectedIssue(error, '加载个人资料失败'));
    }
  }

  UpdateMyProfileRequest buildRequest() {
    final ageText = ageController.text.trim();
    return UpdateMyProfileRequest(
      userName: userNameController.text.trim(),
      userEmail: emailController.text.trim(),
      age: ageText.isEmpty ? null : int.tryParse(ageText),
      address: addressController.text.trim(),
    );
  }

  Future<bool> save() async {
    if (!isReady || _isSaving || !isDirty) {
      return false;
    }

    _isSaving = true;
    _saveIssue = null;
    notifyListeners();
    try {
      await _repository.updateMyProfile(
        request: buildRequest(),
        accessToken: _accessToken,
      );
      _isSaving = false;
      notifyListeners();
      return true;
    } on RadishApiClientException catch (error) {
      _saveIssue = ProfileIssue.fromApi(error);
    } on FormatException catch (error) {
      _saveIssue = ProfileIssue.invalidResponse(error, resourceLabel: '资料保存结果');
    } catch (error) {
      _saveIssue = _unexpectedIssue(error, '保存个人资料失败');
    }
    _isSaving = false;
    notifyListeners();
    return false;
  }

  void _hydrate(MyProfileInfo profile) {
    _isHydrating = true;
    userNameController.text = profile.userName;
    emailController.text = profile.userEmail;
    ageController.text = profile.age > 0 ? profile.age.toString() : '';
    addressController.text = profile.address;
    _isHydrating = false;
  }

  void _handleDraftChanged() {
    if (!_isHydrating && isReady) {
      _saveIssue = null;
      notifyListeners();
    }
  }

  void _rejectLoad(int version, ProfileIssue issue) {
    if (version != _requestVersion) {
      return;
    }
    _status = ProfileEditStatus.unavailable;
    _loadIssue = issue;
    notifyListeners();
  }

  @override
  void dispose() {
    _requestVersion++;
    userNameController.dispose();
    emailController.dispose();
    ageController.dispose();
    addressController.dispose();
    super.dispose();
  }
}

bool _requestsEqual(
  UpdateMyProfileRequest left,
  UpdateMyProfileRequest right,
) {
  return left.userName == right.userName &&
      left.userEmail == right.userEmail &&
      left.sex == right.sex &&
      left.age == right.age &&
      left.birth == right.birth &&
      left.address == right.address;
}

ProfileIssue _unexpectedIssue(Object error, String fallback) {
  final message = error.toString().trim();
  return ProfileIssue(
    kind: ProfileIssueKind.request,
    message: message.isEmpty ? fallback : message,
    code: 'Profile.Unexpected',
  );
}
