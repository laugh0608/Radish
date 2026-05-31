import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/features/experience/data/experience_models.dart';

void main() {
  test('parses user experience metadata', () {
    final experience = UserExperience.fromJson({
      'voUserId': 42,
      'voUserName': 'user-42',
      'voCurrentLevel': 3,
      'voCurrentLevelName': '练气',
      'voCurrentExp': 240,
      'voTotalExp': 1240,
      'voExpToNextLevel': 260,
      'voNextLevel': 4,
      'voNextLevelName': '筑基',
      'voLevelProgress': 0.48,
      'voThemeColor': '#FFC107',
      'voLevelUpAt': '2026-05-30T08:00:00Z',
      'voRank': 12,
      'voExpFrozen': false,
    });

    expect(experience.userId, '42');
    expect(experience.userName, 'user-42');
    expect(experience.currentLevel, 3);
    expect(experience.currentLevelName, '练气');
    expect(experience.currentExp, 240);
    expect(experience.totalExp, 1240);
    expect(experience.expToNextLevel, 260);
    expect(experience.nextLevelName, '筑基');
    expect(experience.levelProgress, 0.48);
    expect(experience.rank, 12);
    expect(experience.expFrozen, isFalse);
  });

  test('parses experience transaction page', () {
    final page = ExperienceTransactionPage.fromJson({
      'page': 1,
      'pageSize': 20,
      'dataCount': 2,
      'pageCount': 1,
      'data': [
        {
          'voId': 'exp-1',
          'voUserId': 42,
          'voUserName': 'user-42',
          'voOperatorId': 0,
          'voOperatorName': 'system',
          'voExpType': 'POST_CREATE',
          'voExpTypeDisplay': '发帖奖励',
          'voExpAmount': 20,
          'voBusinessType': 'Post',
          'voBusinessId': 'post-1',
          'voRemark': '发布公开帖子',
          'voExpBefore': 1220,
          'voExpAfter': 1240,
          'voLevelBefore': 3,
          'voLevelAfter': 3,
          'voCreateTime': '2026-05-31T08:00:00Z',
        },
        {
          'voId': 2,
          'voUserId': '42',
          'voOperatorId': '0',
          'voExpType': 'COMMENT_CREATE',
          'voExpTypeDisplay': '评论奖励',
          'voExpAmount': '10',
          'voExpBefore': '1210',
          'voExpAfter': '1220',
          'voLevelBefore': 2,
          'voLevelAfter': 3,
          'voIsLevelUp': true,
        },
      ],
    });

    expect(page.page, 1);
    expect(page.dataCount, 2);
    expect(page.hasMore, isFalse);
    expect(page.transactions, hasLength(2));
    expect(page.transactions.first.id, 'exp-1');
    expect(page.transactions.first.userId, '42');
    expect(page.transactions.first.expTypeDisplay, '发帖奖励');
    expect(page.transactions.first.isLevelUp, isFalse);
    expect(page.transactions.last.id, '2');
    expect(page.transactions.last.expAmount, 10);
    expect(page.transactions.last.isLevelUp, isTrue);
  });
}
