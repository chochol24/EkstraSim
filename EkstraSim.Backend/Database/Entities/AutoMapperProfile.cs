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

        CreateMap<SimulatedFinalLeague, SimulatedFinalLeagueDTO>();

        CreateMap<SimulatedTeamInFinalTable, SimulatedTeamInFinalTableDTO>()
            .ForMember(dest => dest.SimulatedFinalLeague, opt => opt.Ignore());

        CreateMap<ModelEvaluationRun, ModelEvaluationRunDTO>()
            .ForMember(dest => dest.League, opt => opt.MapFrom(src => src.League))
            .ForMember(dest => dest.Season, opt => opt.MapFrom(src => src.Season));

        CreateMap<ModelPrediction, ModelPredictionDTO>()
            .ForMember(dest => dest.Match, opt => opt.MapFrom(src => src.Match));

        CreateMap<ModelRoundMetric, ModelRoundMetricDTO>();
    }
}