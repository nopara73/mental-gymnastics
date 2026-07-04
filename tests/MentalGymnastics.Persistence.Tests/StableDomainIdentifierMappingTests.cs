using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.Persistence.Tests;

public sealed class StableDomainIdentifierMappingTests
{
    [Fact]
    public void StableIdentifiersRoundTripForAllPersistedDomainEnums()
    {
        AssertRoundTrips(StableDomainIdentifiers.Branches, Enum.GetValues<BranchCode>());
        AssertRoundTrips(StableDomainIdentifiers.Levels, Enum.GetValues<GlobalLevelId>());
        AssertRoundTrips(StableDomainIdentifiers.Drills, Enum.GetValues<DrillId>());
        AssertRoundTrips(StableDomainIdentifiers.SessionTypes, Enum.GetValues<SessionType>());
        AssertRoundTrips(StableDomainIdentifiers.GateOutcomes, Enum.GetValues<GateOutcome>());
        AssertRoundTrips(StableDomainIdentifiers.FailureTypes, Enum.GetValues<FailureType>());
        AssertRoundTrips(StableDomainIdentifiers.MaintenanceStates, Enum.GetValues<MaintenanceCurrencyState>());
        AssertRoundTrips(StableDomainIdentifiers.BranchLevelStates, Enum.GetValues<BranchLevelState>());
    }

    [Fact]
    public void StableIdentifiersUseProgramCodesNotDisplayWording()
    {
        Assert.Equal("FH", StableDomainIdentifiers.Branches.ToPersistedId(BranchCode.FH));
        Assert.Equal("L1", StableDomainIdentifiers.Levels.ToPersistedId(GlobalLevelId.L1));
        Assert.Equal("FH1TargetHold", StableDomainIdentifiers.Drills.ToPersistedId(DrillId.FH1TargetHold));
        Assert.Equal("Practice", StableDomainIdentifiers.SessionTypes.ToPersistedId(SessionType.Practice));
        Assert.Equal("PassOnce", StableDomainIdentifiers.GateOutcomes.ToPersistedId(GateOutcome.PassOnce));
        Assert.Equal("TechnicalFailure", StableDomainIdentifiers.FailureTypes.ToPersistedId(FailureType.TechnicalFailure));
        Assert.Equal("Warning", StableDomainIdentifiers.MaintenanceStates.ToPersistedId(MaintenanceCurrencyState.Warning));
        Assert.Equal("TestReady", StableDomainIdentifiers.BranchLevelStates.ToPersistedId(BranchLevelState.TestReady));
    }

    [Fact]
    public void DisplayOnlyWordingIsNotAcceptedAsPersistedIdentifier()
    {
        Assert.False(StableDomainIdentifiers.Branches.TryFromPersistedId("Focus Hold", out _));
        Assert.False(StableDomainIdentifiers.Levels.TryFromPersistedId("Level 1", out _));
        Assert.False(StableDomainIdentifiers.Drills.TryFromPersistedId("FH-1 Target Hold", out _));
        Assert.False(StableDomainIdentifiers.SessionTypes.TryFromPersistedId("Practice session", out _));
        Assert.False(StableDomainIdentifiers.GateOutcomes.TryFromPersistedId("Pass once", out _));
        Assert.False(StableDomainIdentifiers.FailureTypes.TryFromPersistedId("Technical failure", out _));
        Assert.False(StableDomainIdentifiers.MaintenanceStates.TryFromPersistedId("Needs attention", out _));
        Assert.False(StableDomainIdentifiers.BranchLevelStates.TryFromPersistedId("Test-ready", out _));
    }

    [Fact]
    public void StableIdentifiersRemainDeterministicAcrossRepeatedAccesses()
    {
        var firstPersistedId = StableDomainIdentifiers.Drills.ToPersistedId(DrillId.TI2GlobalReviewTask);
        var secondPersistedId = StableDomainIdentifiers.Drills.ToPersistedId(DrillId.TI2GlobalReviewTask);

        Assert.Equal("TI2GlobalReviewTask", firstPersistedId);
        Assert.Equal(firstPersistedId, secondPersistedId);
        Assert.Equal(DrillId.TI2GlobalReviewTask, StableDomainIdentifiers.Drills.FromPersistedId(firstPersistedId));
    }

    [Fact]
    public void UnknownPersistedIdentifiersAreRejected()
    {
        Assert.False(StableDomainIdentifiers.Branches.TryFromPersistedId("ZZ", out _));
        Assert.Throws<ArgumentException>(() => StableDomainIdentifiers.Branches.FromPersistedId("ZZ"));
        Assert.Throws<ArgumentException>(() => StableDomainIdentifiers.Branches.FromPersistedId(""));
    }

    private static void AssertRoundTrips<TDomain>(
        IStableDomainIdentifierMap<TDomain> map,
        IReadOnlyCollection<TDomain> values)
        where TDomain : struct, Enum
    {
        Assert.Equal(values.Count, map.PersistedIds.Count);

        foreach (var value in values)
        {
            var persistedId = map.ToPersistedId(value);

            Assert.False(string.IsNullOrWhiteSpace(persistedId));
            Assert.True(map.TryFromPersistedId(persistedId, out var roundTripped));
            Assert.Equal(value, roundTripped);
            Assert.Equal(value, map.FromPersistedId(persistedId));
        }
    }
}
