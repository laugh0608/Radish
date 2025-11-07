using AutoMapper;
using Radish.Books;

namespace Radish;

public class RadishApplicationAutoMapperProfile : Profile
{
    /// <summary>实体模型与 Dto 视图模型的映射</summary>
    public RadishApplicationAutoMapperProfile()
    {
        CreateMap<Book, BookDto>();
        CreateMap<CreateUpdateBookDto, Book>();
        /* You can configure your AutoMapper mapping configuration here.
         * Alternatively, you can split your mapping configurations
         * into multiple profile classes for a better organization. */
    }
}
