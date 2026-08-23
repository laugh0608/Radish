part of 'smoke_test.dart';

NativeAuthController _buildAuthController(
  SessionController sessionController, {
  NativeAuthCallbackPayload? pendingCallback,
  AuthSession? nextSession,
  String? exchangeFailureMessage,
}) {
  const restoredState = 'restored-native-oidc-state';
  final restoredCallback = pendingCallback == null
      ? null
      : NativeAuthCallbackPayload(
          type: pendingCallback.type,
          code: pendingCallback.code,
          state: pendingCallback.state ?? restoredState,
          error: pendingCallback.error,
          errorDescription: pendingCallback.errorDescription,
        );
  return NativeAuthController(
    environment: const AppEnvironment.development(),
    sessionController: sessionController,
    gateway: InMemoryNativeAuthGateway(
      initialPendingCallback: restoredCallback,
      initialAuthorizationAttempt: pendingCallback == null
          ? null
          : NativeOidcAuthorizationAttempt(
              state: restoredState,
              codeVerifier:
                  'test-verifier-abcdefghijklmnopqrstuvwxyz-1234567890',
              redirectUri:
                  const AppEnvironment.development().nativeOidcRedirectUri,
              startedAt: DateTime.now().toUtc(),
            ),
    ),
    exchangeService: _FakeAuthorizationCodeExchangeService(
      nextSession: nextSession,
      failureMessage: exchangeFailureMessage,
    ),
  );
}

class _FakeDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverFeedPage> getFeed({
    required int pageSize,
    String? cursor,
  }) async {
    return _smokeDiscoverPage(items: const []);
  }
}

class _SeededDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverFeedPage> getFeed({
    required int pageSize,
    String? cursor,
  }) async {
    return _smokeDiscoverPage(
      items: [
        DiscoverFeedItem(
          key: 'post:1',
          kind: DiscoverItemKind.post,
          occurredAtUtc: DateTime.utc(2026, 5, 31, 8),
          title: 'Native discover',
          summary: 'Use discover to jump into real tabs.',
          actor: const DiscoverActor(
            publicId: 'user-9',
            displayName: 'luobo',
          ),
          target: const DiscoverTarget(
            kind: DiscoverTargetKind.forumPost,
            postPublicId: 'post-1',
            requiresAuthentication: false,
          ),
        ),
      ],
    );
  }
}

class _SeededShopRepository implements ShopRepository {
  const _SeededShopRepository();

  @override
  Future<ShopProductPage> getProductPage({
    required int pageIndex,
    required int pageSize,
  }) async {
    return const ShopProductPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      products: [
        ShopProductSummary(
          id: '4001',
          name: 'Profile Rename Card',
          productType: '消耗品',
          price: 120,
          originalPrice: 180,
          hasDiscount: true,
          soldCount: 3,
          durationDisplay: '永久',
          inStock: true,
        ),
      ],
    );
  }

  @override
  Future<ShopProductDetail> getProductDetail({
    required String productId,
  }) async {
    return const ShopProductDetail(
      id: '4001',
      name: 'Profile Rename Card',
      description: 'Use this read-only detail to confirm the item scope.',
      categoryName: 'Profile tools',
      productType: '消耗品',
      benefitValue: 'rename-card',
      price: 120,
      originalPrice: 180,
      hasDiscount: true,
      stockType: 'Unlimited',
      stock: 0,
      soldCount: 3,
      limitPerUser: 1,
      inStock: true,
      durationDisplay: '永久',
      isOnSale: true,
      isEnabled: true,
    );
  }

  @override
  Future<ShopProductBuyCheckResult> checkCanBuy({
    required String accessToken,
    required String productId,
    int quantity = 1,
  }) async {
    return const ShopProductBuyCheckResult(canBuy: true);
  }

  @override
  Future<ShopPurchaseResult> purchaseProduct({
    required String accessToken,
    required String productId,
    required String paymentPassword,
    required String idempotencyKey,
    int quantity = 1,
  }) async {
    return const ShopPurchaseResult(
      success: true,
      orderId: '9001',
      orderNo: 'RO202605310001',
      deductedCoins: 120,
      remainingBalance: 880,
    );
  }

  @override
  Future<ShopOrderPage> getMyOrders({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const ShopOrderPage(
      page: 1,
      pageSize: 20,
      dataCount: 1,
      pageCount: 1,
      orders: [
        ShopOrderSummary(
          id: '9001',
          orderNo: 'RO202605310001',
          productName: 'Profile Rename Card',
          quantity: 1,
          totalPrice: 120,
          status: 'Completed',
          statusDisplay: '已完成',
          createTime: '2026-05-31T08:00:00Z',
        ),
      ],
    );
  }

  @override
  Future<ShopOrderDetail> getOrderDetail({
    required String accessToken,
    required String orderId,
  }) async {
    return const ShopOrderDetail(
      id: '9001',
      orderNo: 'RO202605310001',
      productId: '4001',
      productName: 'Profile Rename Card',
      productType: 'Consumable',
      productTypeDisplay: '消耗品',
      quantity: 1,
      unitPrice: 120,
      totalPrice: 120,
      status: 'Completed',
      statusDisplay: '已完成',
      coinTransactionId: 'coin-2',
      durationDisplay: '永久',
      createTime: '2026-05-31T08:00:00Z',
      paidTime: '2026-05-31T08:00:30Z',
      completedTime: '2026-05-31T08:01:00Z',
    );
  }

  @override
  Future<List<ShopUserBenefit>> getMyBenefits({
    required String accessToken,
  }) async {
    return const [
      ShopUserBenefit(
        id: 'benefit-1',
        benefitType: 'Badge',
        benefitTypeDisplay: '徽章',
        benefitName: '早鸟徽章',
        sourceType: 'Purchase',
        sourceTypeDisplay: '购买',
        sourceOrderId: '9001',
        sourceProductId: '4001',
        durationDisplay: '永久',
        isActive: true,
        isExpired: false,
        createTime: '2026-05-31T08:05:00Z',
      ),
    ];
  }

  @override
  Future<List<ShopInventoryItem>> getMyInventory({
    required String accessToken,
  }) async {
    return const [
      ShopInventoryItem(
        id: 'inventory-1',
        consumableType: 'RenameCard',
        consumableTypeDisplay: '改名卡',
        itemName: 'Profile Rename Card',
        itemValue: 'rename-card',
        sourceProductId: '4001',
        quantity: 1,
        createTime: '2026-05-31T08:06:00Z',
      ),
    ];
  }
}

class _SeededWalletRepository implements WalletRepository {
  const _SeededWalletRepository();

  @override
  Future<CoinBalance> getBalance({
    required String accessToken,
  }) async {
    return const CoinBalance(
      userId: 'user-42',
      balance: 1200,
      balanceDisplay: '1.200',
      frozenBalance: 100,
      frozenBalanceDisplay: '0.100',
      totalEarned: 1800,
      totalSpent: 600,
      totalTransferredIn: 0,
      totalTransferredOut: 0,
      createTime: '2026-05-30T08:00:00Z',
      modifyTime: '2026-05-31T09:00:00Z',
    );
  }

  @override
  Future<CoinTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
    String? transactionType,
    String? status,
    String? businessType,
    String? businessId,
  }) async {
    const transactions = [
      CoinTransaction(
        id: 'coin-1',
        transactionNo: 'CT202605310001',
        fromUserId: null,
        fromUserName: null,
        toUserId: 'user-42',
        toUserName: 'user-42',
        amount: 1800,
        amountDisplay: '1.800',
        fee: 0,
        feeDisplay: '0.000',
        transactionType: 'SYSTEM_GRANT',
        transactionTypeDisplay: '系统赠送',
        status: 'SUCCESS',
        statusDisplay: '成功',
        remark: '新账号奖励',
        createTime: '2026-05-31T08:00:00Z',
      ),
      CoinTransaction(
        id: 'coin-2',
        transactionNo: 'CT202605310002',
        fromUserId: 'user-42',
        fromUserName: 'user-42',
        toUserId: null,
        toUserName: null,
        amount: 600,
        amountDisplay: '0.600',
        fee: 0,
        feeDisplay: '0.000',
        transactionType: 'CONSUME',
        transactionTypeDisplay: '商城消费',
        status: 'SUCCESS',
        statusDisplay: '成功',
        businessType: 'Order',
        businessId: '9001',
        remark: '购买 Profile Rename Card',
        createTime: '2026-05-31T08:30:00Z',
      ),
    ];
    final filteredTransactions = transactions.where((transaction) {
      if (transactionType != null &&
          transactionType.trim().isNotEmpty &&
          transaction.transactionType != transactionType.trim()) {
        return false;
      }
      if (status != null &&
          status.trim().isNotEmpty &&
          transaction.status != status.trim()) {
        return false;
      }
      if (businessType != null &&
          businessType.trim().isNotEmpty &&
          transaction.businessType != businessType.trim()) {
        return false;
      }
      if (businessId != null &&
          businessId.trim().isNotEmpty &&
          transaction.businessId != businessId.trim()) {
        return false;
      }

      return true;
    }).toList();

    return CoinTransactionPage(
      page: 1,
      pageSize: 20,
      dataCount: filteredTransactions.length,
      pageCount: 1,
      transactions: filteredTransactions,
    );
  }
}

class _SeededExperienceRepository implements ExperienceRepository {
  const _SeededExperienceRepository();

  @override
  Future<UserExperience> getMyExperience({
    required String accessToken,
  }) async {
    return const UserExperience(
      userId: 'user-42',
      userName: 'user-42',
      currentLevel: 3,
      currentLevelName: '练气',
      currentExp: 240,
      totalExp: 1240,
      expToNextLevel: 260,
      nextLevel: 4,
      nextLevelName: '筑基',
      levelProgress: 0.48,
      expFrozen: false,
      levelUpAt: '2026-05-30T08:00:00Z',
      rank: 12,
    );
  }

  @override
  Future<ExperienceTransactionPage> getTransactions({
    required String accessToken,
    required int pageIndex,
    required int pageSize,
  }) async {
    return const ExperienceTransactionPage(
      page: 1,
      pageSize: 20,
      dataCount: 2,
      pageCount: 1,
      transactions: [
        ExperienceTransaction(
          id: 'exp-1',
          userId: 'user-42',
          userName: 'user-42',
          operatorId: '0',
          operatorName: 'system',
          expType: 'POST_CREATE',
          expTypeDisplay: '发帖奖励',
          expAmount: 20,
          businessType: 'Post',
          businessId: 'post-1',
          remark: '发布公开帖子',
          expBefore: 1220,
          expAfter: 1240,
          levelBefore: 3,
          levelAfter: 3,
          isLevelUp: false,
          createTime: '2026-05-31T08:00:00Z',
        ),
        ExperienceTransaction(
          id: 'exp-2',
          userId: 'user-42',
          userName: 'user-42',
          operatorId: '0',
          operatorName: 'system',
          expType: 'COMMENT_CREATE',
          expTypeDisplay: '评论奖励',
          expAmount: 10,
          businessType: 'Comment',
          businessId: 'comment-1',
          remark: '发布公开评论',
          expBefore: 1210,
          expAfter: 1220,
          levelBefore: 2,
          levelAfter: 3,
          isLevelUp: true,
          createTime: '2026-05-31T07:30:00Z',
        ),
      ],
    );
  }
}

class _SeededDocumentDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverFeedPage> getFeed({
    required int pageSize,
    String? cursor,
  }) async {
    return _smokeDiscoverPage(
      items: [
        DiscoverFeedItem(
          key: 'docs:1',
          kind: DiscoverItemKind.memberActivity,
          occurredAtUtc: DateTime.utc(2026, 4, 20, 8),
          title: 'Radish Flutter docs scope',
          summary: 'Read-only docs route handoff.',
          target: const DiscoverTarget(
            kind: DiscoverTargetKind.docs,
            documentSlug: 'flutter-docs-scope',
            requiresAuthentication: false,
          ),
        ),
      ],
    );
  }
}

class _SeededBigIdDiscoverRepository implements DiscoverRepository {
  @override
  Future<DiscoverFeedPage> getFeed({
    required int pageSize,
    String? cursor,
  }) async {
    return _smokeDiscoverPage(
      items: [
        DiscoverFeedItem(
          key: 'post:big-id',
          kind: DiscoverItemKind.post,
          occurredAtUtc: DateTime.utc(2026, 5, 31, 8),
          title: 'Native discover wiring plan',
          summary: 'Use discover to jump into real tabs.',
          target: const DiscoverTarget(
            kind: DiscoverTargetKind.forumPost,
            postPublicId: '2042219067430928384',
            requiresAuthentication: false,
          ),
        ),
      ],
    );
  }
}

DiscoverFeedPage _smokeDiscoverPage({
  required List<DiscoverFeedItem> items,
}) {
  return DiscoverFeedPage(
    items: items,
    pulse: DiscoverPulse(
      windowStartedAtUtc: DateTime.utc(2026, 5, 30, 8),
      windowEndedAtUtc: DateTime.utc(2026, 5, 31, 8),
      discoverableChannelCount: '2',
      eligibleItemCount: items.length.toString(),
      knowledgeContributionCount: '1',
    ),
    hasMore: false,
    generatedAtUtc: DateTime.utc(2026, 5, 31, 8),
  );
}
