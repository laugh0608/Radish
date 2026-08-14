using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class TagServiceTest
{
    [Fact]
    public async Task GetTagPageAsync_Should_Pass_IncludeDeleted_To_Repository()
    {
        var tag = new Tag("已删除标签")
        {
            Id = 42,
            Slug = "deleted-tag",
            IsDeleted = true
        };
        var tagVo = new TagVo
        {
            VoId = tag.Id,
            VoName = tag.Name,
            VoSlug = tag.Slug,
            VoIsDeleted = true
        };
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var tagRepository = new Mock<IBaseRepository<Tag>>(MockBehavior.Strict);
        var discoveryRepository = new Mock<ITagDiscoveryRepository>(MockBehavior.Strict);

        tagRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<Tag, bool>>?>(),
                1,
                20,
                It.IsAny<Expression<Func<Tag, object>>>(),
                OrderByType.Asc,
                It.IsAny<Expression<Func<Tag, object>>>(),
                OrderByType.Asc,
                true))
            .ReturnsAsync(([tag], 1));
        mapper
            .Setup(instance => instance.Map<List<TagVo>>(It.IsAny<object>()))
            .Returns([tagVo]);

        var service = new TagService(
            mapper.Object,
            tagRepository.Object,
            discoveryRepository.Object);

        var page = await service.GetTagPageAsync(includeDeleted: true);

        Assert.Single(page.Data);
        Assert.True(page.Data[0].VoIsDeleted);
        tagRepository.VerifyAll();
        mapper.VerifyAll();
    }

    [Fact]
    public async Task RestoreTagAsync_Should_Query_Deleted_Tag_Before_Restoring()
    {
        var deletedTag = new Tag("待恢复标签")
        {
            Id = 43,
            Slug = "restorable-tag",
            IsDeleted = true
        };
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var tagRepository = new Mock<IBaseRepository<Tag>>(MockBehavior.Strict);
        var discoveryRepository = new Mock<ITagDiscoveryRepository>(MockBehavior.Strict);

        tagRepository
            .Setup(repository => repository.QueryByIdAsync(deletedTag.Id, true))
            .ReturnsAsync(deletedTag);
        tagRepository
            .Setup(repository => repository.RestoreByIdAsync(deletedTag.Id))
            .ReturnsAsync(true);
        tagRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Tag, Tag>>>(),
                It.IsAny<Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(1);

        var service = new TagService(
            mapper.Object,
            tagRepository.Object,
            discoveryRepository.Object);

        var restored = await service.RestoreTagAsync(deletedTag.Id, 20001, "Admin");

        Assert.True(restored);
        tagRepository.VerifyAll();
    }
}
