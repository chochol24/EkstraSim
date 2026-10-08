using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public class ModelSnapshotDriftTests
{
    private static ModelSnapshot Snapshot(params (string Key, double Value)[] parameters)
    {
        return new ModelSnapshot
        {
            ModelName = "test",
            Parameters = parameters.ToDictionary(p => p.Key, p => p.Value)
        };
    }

    private static ModelSnapshot Scaled(ModelSnapshot source, double factor)
    {
        return new ModelSnapshot
        {
            ModelName = source.ModelName,
            Parameters = source.Parameters.ToDictionary(p => p.Key, p => p.Value * factor)
        };
    }

    private static ModelSnapshot Shifted(ModelSnapshot source, double offset)
    {
        return new ModelSnapshot
        {
            ModelName = source.ModelName,
            Parameters = source.Parameters.ToDictionary(p => p.Key, p => p.Value + offset)
        };
    }

    private static ModelSnapshot Duplicated(ModelSnapshot source)
    {
        var parameters = new Dictionary<string, double>();

        foreach (var (key, value) in source.Parameters)
        {
            parameters[key] = value;
            parameters[$"{key}_copy"] = value;
        }

        return new ModelSnapshot
        {
            ModelName = source.ModelName,
            Parameters = parameters
        };
    }

    [Fact]
    public void IdenticalSnapshotsHaveNoDrift()
    {
        var snapshot = Snapshot(("a", 1.0), ("b", 2.0), ("c", 3.0));

        Assert.Equal(0, ModelSnapshot.NormalisedDistance(snapshot, snapshot), precision: 12);
    }

    [Fact]
    public void MissingSnapshotHasNoDrift()
    {
        var snapshot = Snapshot(("a", 1.0));

        Assert.Equal(0, ModelSnapshot.NormalisedDistance(null, snapshot));
        Assert.Equal(0, ModelSnapshot.NormalisedDistance(snapshot, null));
        Assert.Equal(0, ModelSnapshot.NormalisedDistance(null, null));
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(10.0)]
    [InlineData(1300.0)]
    public void DriftIsInvariantToParameterScale(double factor)
    {
        var before = Snapshot(("a", 1.0), ("b", 2.0), ("c", 3.0));
        var after = Snapshot(("a", 1.1), ("b", 1.9), ("c", 3.3));

        var baseline = ModelSnapshot.NormalisedDistance(before, after);
        var scaled = ModelSnapshot.NormalisedDistance(Scaled(before, factor), Scaled(after, factor));

        Assert.Equal(baseline, scaled, precision: 10);
    }

    [Theory]
    [InlineData(100.0)]
    [InlineData(1300.0)]
    [InlineData(-50.0)]
    public void DriftIsInvariantToConstantOffset(double offset)
    {
        var before = Snapshot(("a", 1.0), ("b", 2.0), ("c", 3.0));
        var after = Snapshot(("a", 1.1), ("b", 1.9), ("c", 3.3));

        var baseline = ModelSnapshot.NormalisedDistance(before, after);
        var shifted = ModelSnapshot.NormalisedDistance(Shifted(before, offset), Shifted(after, offset));

        Assert.Equal(baseline, shifted, precision: 10);
    }

    [Fact]
    public void DriftIsInvariantToParameterCount()
    {
        var before = Snapshot(("a", 1.0), ("b", 2.0), ("c", 3.0));
        var after = Snapshot(("a", 1.1), ("b", 1.9), ("c", 3.3));

        var baseline = ModelSnapshot.NormalisedDistance(before, after);
        var duplicated = ModelSnapshot.NormalisedDistance(Duplicated(before), Duplicated(after));

        Assert.Equal(baseline, duplicated, precision: 10);
    }

    [Fact]
    public void RatingScaleAndStrengthScaleDriftComparably()
    {
        var eloBefore = Snapshot(("r1", 1300.0), ("r2", 1450.0), ("r3", 1150.0), ("r4", 1250.0));
        var eloAfter = Snapshot(("r1", 1315.0), ("r2", 1435.0), ("r3", 1160.0), ("r4", 1240.0));

        var dcBefore = Snapshot(("a1", 1.00), ("a2", 1.30), ("a3", 0.70), ("a4", 0.90));
        var dcAfter = Snapshot(("a1", 1.03), ("a2", 1.27), ("a3", 0.72), ("a4", 0.88));

        var eloDrift = ModelSnapshot.NormalisedDistance(eloBefore, eloAfter);
        var dcDrift = ModelSnapshot.NormalisedDistance(dcBefore, dcAfter);

        Assert.True(
            eloDrift > 0 && dcDrift > 0,
            $"oba dryfy musza byc dodatnie, byly elo={eloDrift} dc={dcDrift}");

        var ratio = eloDrift / dcDrift;

        Assert.InRange(ratio, 0.5, 2.0);
    }

    [Fact]
    public void OldAbsoluteDistanceStillFavoursTheLargerScale()
    {
        var eloBefore = Snapshot(("r1", 1300.0), ("r2", 1450.0));
        var eloAfter = Snapshot(("r1", 1315.0), ("r2", 1435.0));

        var dcBefore = Snapshot(("a1", 1.00), ("a2", 1.30));
        var dcAfter = Snapshot(("a1", 1.03), ("a2", 1.27));

        Assert.True(
            ModelSnapshot.Distance(eloBefore, eloAfter) > 100 * ModelSnapshot.Distance(dcBefore, dcAfter),
            "absolutna norma L2 ma pozostac nieporownywalna miedzy skalami - to jest powod istnienia NormalisedDistance");
    }

    [Fact]
    public void ZeroSpreadDoesNotProduceNaNOrInfinity()
    {
        var before = Snapshot(("a", 1300.0), ("b", 1300.0), ("c", 1300.0));
        var after = Snapshot(("a", 1305.0), ("b", 1295.0), ("c", 1300.0));

        var drift = ModelSnapshot.NormalisedDistance(before, after);

        Assert.False(double.IsNaN(drift));
        Assert.False(double.IsInfinity(drift));
        Assert.True(drift > 0);
    }

    [Fact]
    public void AllZeroParametersDoNotProduceNaN()
    {
        var before = Snapshot(("a", 0.0), ("b", 0.0));
        var after = Snapshot(("a", 0.0), ("b", 0.0));

        var drift = ModelSnapshot.NormalisedDistance(before, after);

        Assert.False(double.IsNaN(drift));
        Assert.Equal(0, drift, precision: 12);
    }

    [Fact]
    public void OnlyKeysPresentInBothSnapshotsAreCompared()
    {
        var before = Snapshot(("a", 1.0), ("b", 2.0), ("gone", 99.0));
        var after = Snapshot(("a", 1.0), ("b", 2.0), ("fresh", -99.0));

        Assert.Equal(0, ModelSnapshot.NormalisedDistance(before, after), precision: 12);
    }

    private static ModelSnapshot EloLike(double ratingShift)
    {
        var parameters = new Dictionary<string, double>
        {
            ["home_goals_intercept"] = 0.34,
            ["home_goals_slope"] = 0.62,
            ["away_goals_intercept"] = 0.21,
            ["away_goals_slope"] = 0.58
        };

        for (var team = 1; team <= 18; team++)
        {
            var spread = (team - 9) * 18.0;
            parameters[$"rating_{team}"] = 1300 + spread + (team % 2 == 0 ? ratingShift : -ratingShift);
        }

        return new ModelSnapshot { ModelName = "Elo", Parameters = parameters };
    }

    private static ModelSnapshot DixonColesLike(double strengthShift)
    {
        var parameters = new Dictionary<string, double>
        {
            ["home_advantage"] = 1.32,
            ["rho"] = -0.031
        };

        for (var team = 1; team <= 18; team++)
        {
            var spread = (team - 9) * 0.033;
            parameters[$"attack_{team}"] = 1.0 + spread + (team % 2 == 0 ? strengthShift : -strengthShift);
            parameters[$"defence_{team}"] = 1.0 - spread + (team % 2 == 0 ? strengthShift : -strengthShift);
        }

        return new ModelSnapshot { ModelName = "DixonColes", Parameters = parameters };
    }

    [Fact]
    public void RealisticEloAndDixonColesSnapshotsDriftComparably()
    {
        var eloDrift = ModelSnapshot.NormalisedDistance(EloLike(0), EloLike(4.0));
        var dcDrift = ModelSnapshot.NormalisedDistance(DixonColesLike(0), DixonColesLike(0.0073));

        var ratio = eloDrift / dcDrift;

        Assert.InRange(ratio, 0.5, 2.0);
    }

    [Fact]
    public void ScalarParametersDoNotDominateWhenPerTeamFamiliesExist()
    {
        var before = EloLike(0);
        var after = EloLike(4.0);

        var withWildScalar = new ModelSnapshot
        {
            ModelName = "Elo",
            Parameters = after.Parameters.ToDictionary(
                p => p.Key,
                p => p.Key == "home_goals_intercept" ? p.Value * 50 : p.Value)
        };

        Assert.Equal(
            ModelSnapshot.NormalisedDistance(before, after),
            ModelSnapshot.NormalisedDistance(before, withWildScalar),
            precision: 12);
    }

    [Fact]
    public void KeysWithEmbeddedTeamIndicesFormOneFamily()
    {
        var before = new ModelSnapshot
        {
            ModelName = "Poisson",
            Parameters = new Dictionary<string, double>
            {
                ["team_1_home_scored"] = 1.4,
                ["team_2_home_scored"] = 1.8,
                ["team_3_home_scored"] = 1.1,
                ["league_home_scored"] = 1.45
            }
        };

        var after = new ModelSnapshot
        {
            ModelName = "Poisson",
            Parameters = new Dictionary<string, double>
            {
                ["team_1_home_scored"] = 1.45,
                ["team_2_home_scored"] = 1.75,
                ["team_3_home_scored"] = 1.15,
                ["league_home_scored"] = 99.0
            }
        };

        var drift = ModelSnapshot.NormalisedDistance(before, after);

        Assert.True(drift > 0);
        Assert.True(drift < 1, $"skok pojedynczej skalarnej sredniej ligowej nie moze zdominowac dryfu, bylo {drift}");
    }

    [Fact]
    public void LargerParameterMovementGivesLargerDrift()
    {
        var before = Snapshot(("a", 1.0), ("b", 2.0), ("c", 3.0));
        var small = Snapshot(("a", 1.01), ("b", 2.0), ("c", 3.0));
        var large = Snapshot(("a", 1.30), ("b", 2.0), ("c", 3.0));

        Assert.True(
            ModelSnapshot.NormalisedDistance(before, small) < ModelSnapshot.NormalisedDistance(before, large));
    }
}
