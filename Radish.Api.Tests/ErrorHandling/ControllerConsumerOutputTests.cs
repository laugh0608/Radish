using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Resources;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class ControllerConsumerOutputTests
{
    [Theory]
    [MemberData(nameof(ApplicationOperationLoggingTests.Routes), MemberType = typeof(ApplicationOperationLoggingTests))]
    public async Task ActualActions_ShouldKeepResponsesAndRejectPollutedScopes(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var pollution = ControllerFinalOutputTests.Pollution(capture);
        var current = Current();
        var failure = new InvalidOperationException(HttpBoundaryCapture.Secret);
        var coin = new Mock<ICoinService>();
        coin.Setup(s => s.GetBalanceAsync(7)).ThrowsAsync(failure);
        var coinController = new CoinController(coin.Object, current, Localizer());
        var coinResponse = await coinController.GetBalanceByUserId(7);
        Assert.Equal(400, coinResponse.StatusCode);
        Assert.False(coinResponse.IsSuccess);
        Check(capture, "coin.balance_query_rejected", "invalid-operation");
        coin.Verify(s => s.GetBalanceAsync(7), Times.Once);

        var payment = new Mock<IPaymentPasswordService>();
        payment.Setup(s => s.SetPaymentPasswordAsync(7, It.IsAny<SetPaymentPasswordRequest>())).ThrowsAsync(ServerFailure());
        var paymentResponse = await new PaymentPasswordController(payment.Object, current, Localizer()).SetPassword(new SetPaymentPasswordRequest());
        Assert.Equal(503, paymentResponse.StatusCode);
        Assert.False(paymentResponse.IsSuccess);
        Check(capture, "http.failed", "other", "statusCode");
        payment.Verify(s => s.SetPaymentPasswordAsync(7, It.IsAny<SetPaymentPasswordRequest>()), Times.Once);

        var order = new Mock<IOrderService>();
        order.Setup(s => s.CancelOrderAsync(7, 9, null)).ThrowsAsync(failure);
        var shop = new ShopController(Mock.Of<IProductService>(), order.Object, Mock.Of<IUserBenefitService>(), Mock.Of<IUserInventoryService>(),
            Mock.Of<IUserBrowseHistoryService>(), current, Localizer(), Mock.Of<IProductReviewService>());
        Assert.False((await shop.CancelOrder(9)).IsSuccess);
        Check(capture, "order.cancellation_rejected", "invalid-operation");
        order.Verify(s => s.CancelOrderAsync(7, 9, null), Times.Once);

        var quick = new Mock<IPostQuickReplyService>();
        quick.Setup(s => s.GetRecentByPostIdAsync(9, 30)).ThrowsAsync(failure);
        var quickResponse = await new PostQuickReplyController(quick.Object, Mock.Of<IContentModerationService>(), current).GetRecentByPostId(9);
        Assert.Equal(404, quickResponse.StatusCode);
        Assert.Equal(HttpBoundaryCapture.Secret, quickResponse.MessageInfo);
        Check(capture, "quick_reply.query_rejected", "invalid-operation");

        var posts = new Mock<IPostService>();
        posts.Setup(s => s.SetTopAsync(9, true, 7, "tester")).ThrowsAsync(failure);
        var post = new PostController(posts.Object, Mock.Of<IUserService>(), Mock.Of<IContentModerationService>(), null, null,
            Mock.Of<IUserBrowseHistoryService>(), current, Mock.Of<IForumContentWriteService>(), Localizer());
        var postResponse = await post.SetTop(new SetPostTopDto { PostId = 9, IsTop = true });
        Assert.Equal(404, postResponse.StatusCode);
        Assert.Equal(HttpBoundaryCapture.Secret, postResponse.MessageInfo);
        Check(capture, "post.top_failed", "invalid-operation");
        posts.Verify(s => s.SetTopAsync(9, true, 7, "tester"), Times.Once);

        var comments = new Mock<ICommentService>();
        comments.Setup(s => s.ToggleLikeAsync(7, "tester", 9)).ThrowsAsync(failure);
        var comment = new CommentController(comments.Object, Mock.Of<IPostService>(), Mock.Of<IUserService>(), Mock.Of<IContentModerationService>(),
            current, null!, Mock.Of<IForumContentWriteService>());
        var commentResponse = await comment.ToggleLike(9);
        Assert.Equal(400, commentResponse.StatusCode);
        Assert.Equal(HttpBoundaryCapture.Secret, commentResponse.MessageInfo);
        Check(capture, "comment.like_failed", "invalid-operation");
        comments.Verify(s => s.ToggleLikeAsync(7, "tester", 9), Times.Once);
    }

    [Theory]
    [MemberData(nameof(ApplicationOperationLoggingTests.Routes), MemberType = typeof(ApplicationOperationLoggingTests))]
    public void ActualPrivateConsumers_ShouldKeepResponseConstructionAndSafeOutput(bool candidate, string environment, bool audit)
    {
        // 定向调用真实消费方法，不将反射调用当成 HTTP 端到端测试。
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var pollution = ControllerFinalOutputTests.Pollution(capture);
        foreach (var source in new[] { typeof(ReactionController), typeof(PollController), typeof(LotteryController), typeof(QuestionController) })
        {
            var parameter = source == typeof(QuestionController) ? typeof(Exception) : typeof(BusinessException);
            var response = Assert.IsType<MessageModel>(Invoke(source, "BuildErrorResponse", parameter, ServerFailure()));
            Assert.Equal(503, response.StatusCode);
            Assert.Equal(HttpBoundaryCapture.Secret, response.MessageInfo);
            Check(capture, "http.failed", "other", "statusCode");
            response = Assert.IsType<MessageModel>(Invoke(source, "BuildErrorResponse", parameter, new BusinessException("rejected", 409)));
            Assert.Equal(409, response.StatusCode);
            Assert.Empty(capture.Events);
            var secondary = Assert.Throws<TargetInvocationException>(() => Invoke(source, "BuildErrorResponse", parameter, new UnreadableBusiness()));
            Assert.IsType<IOException>(secondary.InnerException);
            Assert.Empty(capture.Events);
        }
        foreach (var (source, code) in new[] { (typeof(PollController), "poll.request_failed"), (typeof(LotteryController), "lottery.request_failed"), (typeof(QuestionController), "question.request_failed") })
        {
            var parameter = source == typeof(QuestionController) ? typeof(Exception) : typeof(ArgumentException);
            var response = Assert.IsType<MessageModel>(Invoke(source, "BuildErrorResponse", parameter, new ArgumentException(HttpBoundaryCapture.Secret)));
            Assert.Equal(400, response.StatusCode);
            Check(capture, code, "argument");
        }
        foreach (var (source, code) in new[] { (typeof(TagController), "tag.request_failed"), (typeof(CategoryController), "category.request_failed") })
        {
            var response = Assert.IsType<MessageModel>(Invoke(source, "BuildErrorResponse", typeof(InvalidOperationException), new InvalidOperationException(HttpBoundaryCapture.Secret)));
            Assert.False(response.IsSuccess);
            Assert.Equal(HttpBoundaryCapture.Secret, response.MessageInfo);
            Check(capture, code, "invalid-operation");
        }
        Invoke(typeof(StickerController), "LogConsumedFailure", typeof(Exception), new InvalidOperationException(HttpBoundaryCapture.Secret));
        Check(capture, "sticker.request_failed", "invalid-operation");

        var attachment = Attachment(capture);
        typeof(AttachmentController).GetMethod("LogUploadFailure", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(attachment, [ServerFailure()]);
        Check(capture, "http.failed", "other", "statusCode");
        foreach (var operation in new[] { "quota-complete", "quota-release" })
        {
            typeof(AttachmentController).GetMethod("LogUploadAccountingFailure", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(attachment, [operation, new IOException(HttpBoundaryCapture.Secret)]);
            Check(capture, "upload.cleanup.failed", "io", "cleanupOperation");
        }
        foreach (var code in new[] { "order.remark_rejected", "benefit.activation_rejected", "benefit.deactivation_rejected", "benefit.revocation_rejected", "product.management_rejected" })
        {
            typeof(ShopController).GetMethod("LogHandledShopFailure", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [new InvalidOperationException(HttpBoundaryCapture.Secret), code]);
            Check(capture, code, "invalid-operation");
        }
        foreach (var code in new[] { "inventory.use_failed", "inventory.rename_failed" })
        {
            typeof(ShopController).GetMethod("LogConsumedInventoryFailure", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [new InvalidOperationException(HttpBoundaryCapture.Secret), code]);
            Check(capture, code, "invalid-operation");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualConsumer_ShouldPreserveWriteToAndAuditToFailureBehavior(bool audit)
    {
        using var capture = new HttpBoundaryCapture(false, "Production", audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        capture.Sink.EmitFailure = new IOException(HttpBoundaryCapture.Secret);
        var calls = 0;
        var quick = new Mock<IPostQuickReplyService>();
        quick.Setup(s => s.GetRecentByPostIdAsync(9, 30)).Callback(() => calls++).ThrowsAsync(new InvalidOperationException(HttpBoundaryCapture.Secret));
        var controller = new PostQuickReplyController(quick.Object, Mock.Of<IContentModerationService>(), Current());
        if (audit) await Assert.ThrowsAnyAsync<Exception>(() => controller.GetRecentByPostId(9));
        else Assert.Equal(404, (await controller.GetRecentByPostId(9)).StatusCode);
        Assert.Equal(1, calls);
        Assert.Empty(capture.Events);
    }

    [Fact]
    public async Task CandidateOutputFailure_ShouldKeepConsumerResponseAndUseSafeEmergency()
    {
        using var writer = new HangfireBrokenWriter();
        using var capture = new HttpBoundaryCapture(true, "Production", output: writer);
        using var global = new ApplicationLogScope(capture.Logger);
        var quick = new Mock<IPostQuickReplyService>();
        quick.Setup(s => s.GetRecentByPostIdAsync(9, 30)).ThrowsAsync(new InvalidOperationException(HttpBoundaryCapture.Secret));
        var controller = new PostQuickReplyController(quick.Object, Mock.Of<IContentModerationService>(), Current());
        Assert.Equal(404, (await controller.GetRecentByPostId(9)).StatusCode);
        Assert.Equal(404, (await controller.GetRecentByPostId(9)).StatusCode);
        Assert.Contains("pipeline.output_failed", capture.Emergency.ToString());
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, capture.Emergency.ToString());
        Assert.Single(capture.Emergency.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    private static object? Invoke(Type type, string method, Type parameter, Exception exception) =>
        type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic, [parameter])!.Invoke(null, [exception]);

    private sealed class UnreadableBusiness() : BusinessException("private", 503)
    {
        public override string Message => throw new IOException(HttpBoundaryCapture.Secret);
    }

    private static BusinessException ServerFailure() => new(HttpBoundaryCapture.Secret, 503, "System.UnexpectedError", "error.system.unexpected_error");
    private static ICurrentUserAccessor Current() => Mock.Of<ICurrentUserAccessor>(a => a.Current == new CurrentUser { UserId = 7, UserName = "tester", Roles = new[] { "Admin" } });
    private static IStringLocalizer<Errors> Localizer()
    {
        var localizer = new Mock<IStringLocalizer<Errors>>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key, true));
        localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string key, object[] _) => new LocalizedString(key, key, true));
        return localizer.Object;
    }
    private static AttachmentController Attachment(HttpBoundaryCapture capture) => new(Mock.Of<IAttachmentService>(), Current(), Mock.Of<IUserService>(),
        Mock.Of<IUploadRateLimitService>(), Options.Create(new UploadRateLimitOptions()), Mock.Of<IFileAccessTokenService>(),
        new ConfigurationBuilder().Build(), Localizer(), capture.Factory.CreateLogger<AttachmentController>());
    private static void Check(HttpBoundaryCapture capture, string code, string kind, string? extra = null)
    {
        Assert.Equal(code, Assert.Single(capture.Codes));
        Assert.Equal(kind, Assert.Single(capture.Events).FailureKind);
        ControllerFinalOutputTests.AssertFields(capture, extra == null ? ["failureKind"] : ["failureKind", extra]);
        capture.AssertSafe(); capture.Clear();
    }
}
