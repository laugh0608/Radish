using AutoMapper;
using Radish.Books;

namespace Radish;

public class RadishApplicationAutoMapperProfile : Profile
{
    public RadishApplicationAutoMapperProfile()
    {
        CreateMap<Book, BookDto>();
        CreateMap<CreateUpdateBookDto, Book>();
        /* You can configure your AutoMapper mapping configuration here.
         * Alternatively, you can split your mapping configurations
         * into multiple profile classes for a better organization. */
    }
}
