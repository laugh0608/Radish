namespace Radish.Model.ViewModels;

/// <summary>公开商品评价。</summary>
public sealed class ProductReviewVo
{
    public long VoId { get; set; }
    public long VoProductId { get; set; }
    public long VoUserId { get; set; }
    public string? VoAuthorPublicId { get; set; }
    public long? VoAuthorPublicIndex { get; set; }
    public string VoAuthorDisplayName { get; set; } = string.Empty;
    public string? VoAuthorDisplayHandle { get; set; }
    public int VoRating { get; set; }
    public string? VoComment { get; set; }
    public bool VoVerifiedPurchase { get; set; } = true;
    public int VoVersion { get; set; }
    public DateTime VoCreateTime { get; set; }
    public DateTime? VoModifyTime { get; set; }
}

/// <summary>商品公开综合评分与五星分布。</summary>
public sealed class ProductReviewSummaryVo
{
    public decimal VoAverageRating { get; set; }
    public int VoReviewCount { get; set; }
    public int VoFiveStarCount { get; set; }
    public int VoFourStarCount { get; set; }
    public int VoThreeStarCount { get; set; }
    public int VoTwoStarCount { get; set; }
    public int VoOneStarCount { get; set; }
}

/// <summary>商品公开评价分页。</summary>
public sealed class ProductReviewPageVo
{
    public ProductReviewSummaryVo VoSummary { get; set; } = new();
    public List<ProductReviewVo> VoItems { get; set; } = [];
    public int VoTotal { get; set; }
    public int VoPageIndex { get; set; }
    public int VoPageSize { get; set; }
}

/// <summary>当前用户对商品的评价资格与 CAS 写入基线。</summary>
public sealed class MyProductReviewVo
{
    public bool VoCanReview { get; set; }
    public string? VoUnavailableReason { get; set; }
    public int VoExpectedVersion { get; set; }
    public ProductReviewVo? VoReview { get; set; }
}
