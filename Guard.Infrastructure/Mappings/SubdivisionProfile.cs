using AutoMapper;
using Guard.Domain.Entities;
using Guard.Infrastructure.Dtos;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Guard.Infrastructure.Mappings
{
  /// <summary>
  /// Профиль AutoMapper для модели Subdivision.
  /// </summary>
  public class SubdivisionProfile : Profile
  {
    /// <summary>
    /// Конструктор профиля.
    /// </summary>
    public SubdivisionProfile()
    {
      // Определение маппинга между Subdivision и SubdivisionDto
      CreateMap<Subdivision, SubdivisionDto>()
          .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
          .ForMember(dest => dest.Корневой, opt => opt.MapFrom(src => src.Корневой))
          .ForMember(dest => dest.Региональный, opt => opt.MapFrom(src => src.Региональный))
          .ForMember(dest => dest.Территориальный, opt => opt.MapFrom(src => src.Территориальный))
          .ForMember(dest => dest.Субтерриториальный, opt => opt.MapFrom(src => src.Субтерриториальный))
          .ForMember(dest => dest.Адрес, opt => opt.MapFrom(src => src.Адрес))
          .ForMember(dest => dest.Уровень, opt => opt.MapFrom(src => src.Уровень))
          .ForMember(dest => dest.Наименование, opt => opt.MapFrom(src => src.Наименование));

      // Определение маппинга между SubdivisionDto и Subdivision
      CreateMap<SubdivisionDto, Subdivision>()
          .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
          .ForMember(dest => dest.Корневой, opt => opt.MapFrom(src => src.Корневой))
          .ForMember(dest => dest.Региональный, opt => opt.MapFrom(src => src.Региональный))
          .ForMember(dest => dest.Территориальный, opt => opt.MapFrom(src => src.Территориальный))
          .ForMember(dest => dest.Субтерриториальный, opt => opt.MapFrom(src => src.Субтерриториальный))
          .ForMember(dest => dest.Адрес, opt => opt.MapFrom(src => src.Адрес))
          .ForMember(dest => dest.Уровень, opt => opt.MapFrom(src => src.Уровень))
          .ForMember(dest => dest.Наименование, opt => opt.MapFrom(src => src.Наименование));
    }
  }
}