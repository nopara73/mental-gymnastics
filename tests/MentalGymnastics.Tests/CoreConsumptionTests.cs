using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class CoreConsumptionTests
{
    [Fact]
    public void CoreLibraryExposesMentalGymnasticsProgramIdentity()
    {
        var identity = ProgramIdentity.MentalGymnastics;

        Assert.Equal("Mental Gymnastics", identity.Name);
    }
}
