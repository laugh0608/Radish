namespace Radish.Shared.Constants;

/// <summary>
/// 萝卜币 client 业务域稳定错误码。
/// </summary>
public static class CoinErrorCodes
{
    public const string TransactionNotFound = "Coin.TransactionNotFound";
    public const string TransferSelfRejected = "Coin.TransferSelfRejected";
    public const string TransferAmountInvalid = "Coin.TransferAmountInvalid";
    public const string TransferInsufficientBalance = "Coin.TransferInsufficientBalance";
    public const string TransferAccountUnavailable = "Coin.TransferAccountUnavailable";
    public const string TransferConcurrencyConflict = "Coin.TransferConcurrencyConflict";
    public const string TransferProcessing = "Coin.TransferProcessing";
    public const string TransferIdempotencyConflict = "Coin.TransferIdempotencyConflict";
    public const string TransferIdempotencyInvalid = "Coin.TransferIdempotencyInvalid";
    public const string TransferReplayUnavailable = "Coin.TransferReplayUnavailable";
}
