using AutoMapper;
using EkstraSim.Shared.DTOs;

namespace EkstraSim.Backend.Database.Entities;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        CreateMap<League, LeagueDTO>();

        CreateMap<Season, SeasonDTO>();

        CreateMap<Match, MatchDTO>();

        CreateMap<SimulatedRound, SimulatedRoundDTO>();

        CreateMap<SimulatedMatchResultDTO, SimulatedMatchResultDTO>();

        CreateMap<Team, TeamDTO>();
    }
}