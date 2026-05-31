class CoinBalance {
  const CoinBalance({
    required this.userId,
    required this.balance,
    required this.balanceDisplay,
    required this.frozenBalance,
    required this.frozenBalanceDisplay,
    required this.totalEarned,
    required this.totalSpent,
    required this.totalTransferredIn,
    required this.totalTransferredOut,
    this.createTime,
    this.modifyTime,
  });

  factory CoinBalance.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return CoinBalance(
      userId: _readString(map['voUserId']) ?? '',
      balance: _readInt(map['voBalance']) ?? 0,
      balanceDisplay: _readString(map['voBalanceDisplay']) ?? '0.000',
      frozenBalance: _readInt(map['voFrozenBalance']) ?? 0,
      frozenBalanceDisplay:
          _readString(map['voFrozenBalanceDisplay']) ?? '0.000',
      totalEarned: _readInt(map['voTotalEarned']) ?? 0,
      totalSpent: _readInt(map['voTotalSpent']) ?? 0,
      totalTransferredIn: _readInt(map['voTotalTransferredIn']) ?? 0,
      totalTransferredOut: _readInt(map['voTotalTransferredOut']) ?? 0,
      createTime: _readString(map['voCreateTime']),
      modifyTime: _readString(map['voModifyTime']),
    );
  }

  final String userId;
  final int balance;
  final String balanceDisplay;
  final int frozenBalance;
  final String frozenBalanceDisplay;
  final int totalEarned;
  final int totalSpent;
  final int totalTransferredIn;
  final int totalTransferredOut;
  final String? createTime;
  final String? modifyTime;
}

class CoinTransactionPage {
  const CoinTransactionPage({
    required this.page,
    required this.pageSize,
    required this.dataCount,
    required this.pageCount,
    required this.transactions,
  });

  factory CoinTransactionPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['data'];

    return CoinTransactionPage(
      page: _readInt(map['page']) ?? _readInt(map['pageIndex']) ?? 1,
      pageSize: _readInt(map['pageSize']) ?? 20,
      dataCount: _readInt(map['dataCount']) ?? 0,
      pageCount: _readInt(map['pageCount']) ?? 1,
      transactions: data is List
          ? data.map(CoinTransaction.fromJson).toList()
          : const <CoinTransaction>[],
    );
  }

  final int page;
  final int pageSize;
  final int dataCount;
  final int pageCount;
  final List<CoinTransaction> transactions;

  bool get hasMore => page < pageCount;
}

class CoinTransaction {
  const CoinTransaction({
    required this.id,
    required this.transactionNo,
    required this.amount,
    required this.amountDisplay,
    required this.fee,
    required this.feeDisplay,
    required this.transactionType,
    required this.transactionTypeDisplay,
    required this.status,
    required this.statusDisplay,
    this.fromUserId,
    this.fromUserName,
    this.toUserId,
    this.toUserName,
    this.businessType,
    this.businessId,
    this.remark,
    this.createTime,
  });

  factory CoinTransaction.fromJson(Object? json) {
    final map = _readJsonMap(json);

    return CoinTransaction(
      id: _readString(map['voId']) ?? '',
      transactionNo: _readString(map['voTransactionNo']) ?? '',
      fromUserId: _readString(map['voFromUserId']),
      fromUserName: _readString(map['voFromUserName']),
      toUserId: _readString(map['voToUserId']),
      toUserName: _readString(map['voToUserName']),
      amount: _readInt(map['voAmount']) ?? 0,
      amountDisplay: _readString(map['voAmountDisplay']) ?? '0.000',
      fee: _readInt(map['voFee']) ?? 0,
      feeDisplay: _readString(map['voFeeDisplay']) ?? '0.000',
      transactionType: _readString(map['voTransactionType']) ?? 'UNKNOWN',
      transactionTypeDisplay:
          _readString(map['voTransactionTypeDisplay']) ?? '未知类型',
      status: _readString(map['voStatus']) ?? 'UNKNOWN',
      statusDisplay: _readString(map['voStatusDisplay']) ?? '未知状态',
      businessType: _readString(map['voBusinessType']),
      businessId: _readString(map['voBusinessId']),
      remark: _readString(map['voRemark']),
      createTime: _readString(map['voCreateTime']),
    );
  }

  final String id;
  final String transactionNo;
  final String? fromUserId;
  final String? fromUserName;
  final String? toUserId;
  final String? toUserName;
  final int amount;
  final String amountDisplay;
  final int fee;
  final String feeDisplay;
  final String transactionType;
  final String transactionTypeDisplay;
  final String status;
  final String statusDisplay;
  final String? businessType;
  final String? businessId;
  final String? remark;
  final String? createTime;
}

Map<String, Object?> _readJsonMap(Object? json) {
  if (json is Map) {
    return Map<String, Object?>.from(json.cast<Object?, Object?>());
  }

  throw const FormatException('Expected a JSON object.');
}

String? _readString(Object? value) {
  if (value == null) {
    return null;
  }

  final text = value.toString().trim();
  return text.isEmpty ? null : text;
}

int? _readInt(Object? value) {
  if (value is int) {
    return value;
  }

  if (value is double) {
    return value.round();
  }

  return int.tryParse(value?.toString() ?? '');
}
