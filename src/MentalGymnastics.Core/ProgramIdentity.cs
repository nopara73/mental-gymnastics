namespace MentalGymnastics.Core;

public sealed record ProgramIdentity(string Name)
{
    public static ProgramIdentity MentalGymnastics { get; } = new("Mental Gymnastics");
}
