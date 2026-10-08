using EkstraSim.Shared;

namespace EkstraSim.Tests;

public class TeamNameAliasesTests
{
    [Theory]
    [InlineData("Legia Warsaw", "Legia Warszawa")]
    [InlineData("Wisla Plock", "Wisła Płock")]
    [InlineData("Cracovia Kraków", "Cracovia")]
    public void MapsFileNameToNameUsedInDatabase(string fromFile, string expected)
    {
        Assert.Equal(expected, TeamNameAliases.Canonicalise(fromFile));
    }

    [Theory]
    [InlineData("  legia WARSAW  ")]
    [InlineData("LEGIA WARSAW")]
    [InlineData("legia warsaw")]
    public void AliasMatchingIgnoresCaseAndSurroundingWhitespace(string fromFile)
    {
        Assert.Equal("Legia Warszawa", TeamNameAliases.Canonicalise(fromFile));
    }

    [Theory]
    [InlineData("Legia Warszawa")]
    [InlineData("Cracovia")]
    [InlineData("Wisła Płock")]
    [InlineData("Wisła Kraków")]
    [InlineData("Lech Poznań")]
    [InlineData("Jagiellonia Białystok")]
    [InlineData("Widzew Łódź")]
    [InlineData("Raków Częstochowa")]
    [InlineData("Śląsk Wrocław")]
    public void LeavesNamesAlreadyMatchingTheDatabaseUnchanged(string canonical)
    {
        Assert.Equal(canonical, TeamNameAliases.Canonicalise(canonical));
    }

    [Theory]
    [InlineData("Wieczysta Kraków")]
    [InlineData("Arka Gdynia")]
    [InlineData("Bruk-Bet Termalica Nieciecza")]
    public void LeavesUnknownNamesUnchanged(string name)
    {
        Assert.Equal(name, TeamNameAliases.Canonicalise(name));
    }

    [Fact]
    public void TrimsNamesThatHaveNoAlias()
    {
        Assert.Equal("Wieczysta Kraków", TeamNameAliases.Canonicalise("  Wieczysta Kraków  "));
    }

    [Theory]
    [InlineData("Legia Warsaw")]
    [InlineData("Cracovia Kraków")]
    [InlineData("Wisla Plock")]
    [InlineData("Wieczysta Kraków")]
    public void CanonicalisingTwiceGivesTheSameResult(string name)
    {
        var once = TeamNameAliases.Canonicalise(name);

        Assert.Equal(once, TeamNameAliases.Canonicalise(once));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankNameBecomesEmpty(string name)
    {
        Assert.Equal(string.Empty, TeamNameAliases.Canonicalise(name));
    }

    [Fact]
    public void BothCracoviaSpellingsLandOnTheDatabaseName()
    {
        Assert.Equal("Cracovia", TeamNameAliases.Canonicalise("Cracovia"));
        Assert.Equal("Cracovia", TeamNameAliases.Canonicalise("Cracovia Kraków"));
    }

    [Fact]
    public void WislaKrakowIsNotConfusedWithWislaPlock()
    {
        Assert.Equal("Wisła Kraków", TeamNameAliases.Canonicalise("Wisła Kraków"));
        Assert.Equal("Wisła Płock", TeamNameAliases.Canonicalise("Wisla Plock"));
    }

    [Fact]
    public void NormaliseProducesTheKeyFormUsedForLookups()
    {
        Assert.Equal("legia warszawa", TeamNameAliases.Normalise("  Legia Warszawa "));
        Assert.Equal(
            TeamNameAliases.Normalise(TeamNameAliases.Canonicalise("Legia Warsaw")),
            TeamNameAliases.Normalise("Legia Warszawa"));
    }
}
