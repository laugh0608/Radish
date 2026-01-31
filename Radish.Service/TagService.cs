using AutoMapper;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service.Base;

namespace Radish.Service;

/// <summary>标签服务实现</summary>
public class TagService : BaseService<Tag, TagVo>, ITagService
{
    private readonly IBaseRepository<Tag> _tagRepository;

    public TagService(IMapper mapper, IBaseRepository<Tag> baseRepository)
        : base(mapper, baseRepository)
    {
        _tagRepository = baseRepository;
    }

    /// <summary>
    /// 根据标签名称获取或创建标签
    /// </summary>
    public async Task<Tag> GetOrCreateTagAsync(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException("标签名称不能为空", nameof(tagName));
        }

        // 尝试获取现有标签
        var tags = await _tagRepository.QueryAsync(t => t.Name == tagName.Trim());
        var existingTag = tags.FirstOrDefault();

        if (existingTag != null)
        {
            return existingTag;
        }

        // 创建新标签
        var newTag = new Tag(tagName.Trim())
        {
            IsEnabled = true,
            IsDeleted = false
        };

        var tagId = await _tagRepository.AddAsync(newTag);
        newTag.Id = tagId;

        return newTag;
    }
}
