using AutoMapper;
using EkstraSim.Shared.DTOs;

namespace EkstraSim.Backend.Database.Entities;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        CreateMap<League, LeagueDTO>()
            .ForMember(dest => dest.Seasons, opt => opt.Ignore())
            .ForMember(dest => dest.Matches, opt => opt.Ignore());

        CreateMap<Season, SeasonDTO>()
            .ForMember(dest => dest.Matches, opt => opt.Ignore());

        CreateMap<Match, MatchDTO>();

        CreateMap<SimulatedRound, SimulatedRoundDTO>();

        CreateMap<SimulatedMatchResult, SimulatedMatchResultDTO>()
            .ForMember(dest => dest.SimulatedRound, opt => opt.Ignore());

        CreateMap<Team, TeamDTO>();

    }
}