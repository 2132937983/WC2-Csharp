namespace WC2.Tests;

/// <summary>
/// AI profiles (data/json/ai/profiles.json): classic keeps the original's numbers (the classic goldens
/// prove the decisions); improved profiles build on a base and switch the defence terms on. The
/// behaviour itself is tested by the scripts ai_defends_marseille_* (France holds Marseille against an
/// Italian stack with the standard profile) and ai_standard_autoplay.
/// </summary>
[Collection("assets")]
public class AiProfileTests
{
    [Fact]
    public void ClassicIsTheOriginalAndImprovedProfilesInherit()
    {
        Support.MountRepositoryAssets();
        Terrain.LoadJson();
        Assert.True(AiProfiles.LoadJson());
        var classic = AiProfiles.profiles["classic"];
        var original = new AiProfile();
        foreach (var f in typeof(AiProfile).GetFields().Where(f => f.Name != "id" && f.Name != "description"))
        {
            object a = f.GetValue(classic), b = f.GetValue(original);
            if (a is int[] x) Assert.Equal((int[])b, x);
            else Assert.Equal(b, a);
        }
        Assert.False(classic.DefendsAreas);

        var standard = AiProfiles.profiles["standard"];
        Assert.True(standard.DefendsAreas);
        Assert.True(standard.defendCapital > 0 && standard.capitalLastUnitPenalty > 0);
        Assert.Equal(classic.moveEnemyBonus, standard.moveEnemyBonus);           // inherited from classic
        Assert.True(standard.cardReservePercent < classic.cardReservePercent);    // buys cards more readily

        var hard = AiProfiles.profiles["hard"];
        Assert.Equal(standard.defendPort, hard.defendPort);                       // inherited from standard
        Assert.True(hard.attackPlayerBonus > standard.attackPlayerBonus);
    }

    [Fact]
    public void TheCommandLineOverridesTheDefault()
    {
        Support.MountRepositoryAssets();
        AiProfiles.LoadJson();
        try
        {
            AiProfiles.Override = null;
            Assert.Equal("standard", AiProfiles.Select().id);
            AiProfiles.Override = "classic";
            Assert.Equal("classic", AiProfiles.Select().id);
            AiProfiles.Override = "no-such-profile";
            Assert.Equal("classic", AiProfiles.Select().id);   // unknown names fall back to classic
        }
        finally { AiProfiles.Override = null; }
    }
}
