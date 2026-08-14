using System.Linq;
using System.Reflection;
using Radish.Common.AttributeTool;
using Radish.Model;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ForumQuestionContractTest
{
    [Fact]
    public void Mutations_ShouldShareTransactionBoundary()
    {
        var mutationNames = new[]
        {
            nameof(ForumQuestionService.CreateAnswerAsync),
            nameof(ForumQuestionService.UpdateAnswerAsync),
            nameof(ForumQuestionService.DeleteAnswerAsync),
            nameof(ForumQuestionService.RestoreAnswerRevisionAsync),
            nameof(ForumQuestionService.AcceptAnswerAsync),
            nameof(ForumQuestionService.RevokeAcceptanceAsync)
        };

        foreach (var name in mutationNames)
        {
            var method = typeof(ForumQuestionService)
                .GetMethods()
                .Single(item => item.Name == name && item.IsPublic);
            Assert.NotNull(method.GetCustomAttribute<UseTranAttribute>());
        }
    }

    [Fact]
    public void NotificationsAndModeration_ShouldExposeAnswerLifecycle()
    {
        Assert.Equal(6, (int)ContentReportTargetTypeEnum.PostAnswer);
        Assert.Equal(7, (int)ContentReportTargetTypeEnum.ProductReview);
        Assert.True(NotificationDefinitionRegistry.GetRequired(NotificationType.QuestionAnswered).IsProducerActive);
        Assert.True(NotificationDefinitionRegistry.GetRequired(NotificationType.AnswerAccepted).IsProducerActive);
        Assert.True(NotificationDefinitionRegistry.GetRequired(NotificationType.AnswerAcceptanceRevoked).IsProducerActive);
        Assert.Contains(
            NotificationTargetKind.ForumPost,
            NotificationDefinitionRegistry.GetRequired(NotificationType.AnswerAccepted).AllowedTargetKinds);
    }
}
