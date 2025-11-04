using AutoMapper;
using HotelSystem.Domain.Entities;
using ReserHotel.Models.DTOs;

namespace ReserHotel.Mapping;

public class AutoMapperProfiles : Profile
{
 public AutoMapperProfiles()
 {
 CreateMap<Habitacion, HabitacionDto>().ReverseMap();
 CreateMap<TarifaHabitacion, TarifaHabitacionDto>().ReverseMap();
 }
}