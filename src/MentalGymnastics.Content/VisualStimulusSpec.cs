namespace MentalGymnastics.Content;

public enum VisualStimulusShape
{
    Dot,
    Circle,
    Square,
    Bar,
    Arrow,
    Ring,
    Triangle,
    Diamond,
}

public enum VisualStimulusColor
{
    Red,
    Blue,
    Green,
    Black,
    White,
    Amber,
    Violet,
    Gray,
}

public enum VisualStimulusFill
{
    Solid,
    Outline,
    Striped,
    Crossed,
}

public enum VisualStimulusSize
{
    Small,
    Medium,
    Large,
}

public enum VisualStimulusDirection
{
    None,
    North,
    South,
    East,
    West,
}

public enum VisualStimulusMark
{
    None,
    Dot,
    Notch,
    Bar,
    Slash,
    Tick,
    Crossbar,
    Slot,
    Corner,
}

public enum VisualStimulusPosition
{
    Center,
    Left,
    Right,
    Top,
    Bottom,
}

public enum VisualStimulusOrientation
{
    None,
    Horizontal,
    Vertical,
    DiagonalLeft,
    DiagonalRight,
}

public enum VisualStimulusBorder
{
    None,
    Light,
    Regular,
    Heavy,
}

public enum VisualStimulusFeature
{
    Shape,
    Color,
    Fill,
    Size,
    Direction,
    Mark,
    MarkPosition,
    MarkCount,
    MarkOrientation,
    Border,
}

public enum VisualStimulusResponseAction
{
    Tap,
    Withhold,
}

public sealed record VisualStimulusSpec(
    VisualStimulusShape Shape,
    VisualStimulusColor Color,
    VisualStimulusFill Fill = VisualStimulusFill.Solid,
    VisualStimulusSize Size = VisualStimulusSize.Medium,
    VisualStimulusDirection Direction = VisualStimulusDirection.None,
    VisualStimulusMark Mark = VisualStimulusMark.None,
    VisualStimulusPosition MarkPosition = VisualStimulusPosition.Center,
    int MarkCount = 0,
    VisualStimulusOrientation MarkOrientation = VisualStimulusOrientation.None,
    VisualStimulusBorder Border = VisualStimulusBorder.Regular);

public sealed record VisualStimulusPairSpec(
    VisualStimulusSpec First,
    VisualStimulusSpec Second,
    VisualStimulusFeature RelevantFeature)
{
    public bool RelevantFeatureMatches => VisualStimulusComparison.RelevantFeatureMatches(
        First,
        Second,
        RelevantFeature);

    public string RelevantFeatureName => VisualStimulusFeatureNames.NameFor(RelevantFeature);
}

public sealed record VisualStimulusExceptionSpec(
    int Ordinal,
    VisualStimulusSpec Stimulus,
    VisualStimulusResponseAction ExpectedAction,
    string Reason);

public static class VisualStimulusFeatureNames
{
    public static string NameFor(VisualStimulusFeature feature)
    {
        return feature switch
        {
            VisualStimulusFeature.Shape => "shape",
            VisualStimulusFeature.Color => "color",
            VisualStimulusFeature.Fill => "fill pattern",
            VisualStimulusFeature.Size => "size",
            VisualStimulusFeature.Direction => "direction",
            VisualStimulusFeature.Mark => "inner mark",
            VisualStimulusFeature.MarkPosition => "mark position",
            VisualStimulusFeature.MarkCount => "mark count",
            VisualStimulusFeature.MarkOrientation => "mark orientation",
            VisualStimulusFeature.Border => "border weight",
            _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, "Unknown visual stimulus feature."),
        };
    }
}

public static class VisualStimulusComparison
{
    public static bool RelevantFeatureMatches(
        VisualStimulusSpec first,
        VisualStimulusSpec second,
        VisualStimulusFeature feature)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return feature switch
        {
            VisualStimulusFeature.Shape => first.Shape == second.Shape,
            VisualStimulusFeature.Color => first.Color == second.Color,
            VisualStimulusFeature.Fill => first.Fill == second.Fill,
            VisualStimulusFeature.Size => first.Size == second.Size,
            VisualStimulusFeature.Direction => first.Direction == second.Direction,
            VisualStimulusFeature.Mark => first.Mark == second.Mark,
            VisualStimulusFeature.MarkPosition => first.MarkPosition == second.MarkPosition,
            VisualStimulusFeature.MarkCount => first.MarkCount == second.MarkCount,
            VisualStimulusFeature.MarkOrientation => first.MarkOrientation == second.MarkOrientation,
            VisualStimulusFeature.Border => first.Border == second.Border,
            _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, "Unknown visual stimulus feature."),
        };
    }
}

public static class VisualStimulusCodec
{
    public const string FormatVersion = "visual-stimulus-v1";
    public const string PairFormatVersion = "visual-stimulus-pair-v1";
    public const string ExceptionFormatVersion = "visual-stimulus-exception-v1";

    private const int SpecPropertyCount = 10;

    public static string Encode(VisualStimulusSpec stimulus)
    {
        ArgumentNullException.ThrowIfNull(stimulus);
        Validate(stimulus, nameof(stimulus));

        return string.Join(
            ';',
            FormatVersion,
            $"shape={stimulus.Shape}",
            $"color={stimulus.Color}",
            $"fill={stimulus.Fill}",
            $"size={stimulus.Size}",
            $"direction={stimulus.Direction}",
            $"mark={stimulus.Mark}",
            $"mark-position={stimulus.MarkPosition}",
            $"mark-count={Invariant(stimulus.MarkCount)}",
            $"mark-orientation={stimulus.MarkOrientation}",
            $"border={stimulus.Border}");
    }

    public static VisualStimulusSpec Decode(string encoded)
    {
        var segments = Segments(encoded, FormatVersion, 1 + SpecPropertyCount);
        var stimulus = DecodeSpec(segments, 1, prefix: null);
        Validate(stimulus, nameof(encoded));
        return stimulus;
    }

    public static bool TryDecode(string encoded, out VisualStimulusSpec? stimulus)
    {
        try
        {
            stimulus = Decode(encoded);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            stimulus = null;
            return false;
        }
    }

    public static string EncodePair(VisualStimulusPairSpec pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        ArgumentNullException.ThrowIfNull(pair.First);
        ArgumentNullException.ThrowIfNull(pair.Second);
        ValidateEnum(pair.RelevantFeature, nameof(pair.RelevantFeature));
        Validate(pair.First, nameof(pair.First));
        Validate(pair.Second, nameof(pair.Second));

        return string.Join(
            ';',
            new[] { PairFormatVersion, $"feature={pair.RelevantFeature}" }
                .Concat(SpecSegments(pair.First, "first"))
                .Concat(SpecSegments(pair.Second, "second")));
    }

    public static VisualStimulusPairSpec DecodePair(string encoded)
    {
        var segments = Segments(encoded, PairFormatVersion, 2 + (SpecPropertyCount * 2));
        var feature = ReadEnum<VisualStimulusFeature>(segments[1], "feature");
        var first = DecodeSpec(segments, 2, "first");
        var second = DecodeSpec(segments, 2 + SpecPropertyCount, "second");
        Validate(first, nameof(encoded));
        Validate(second, nameof(encoded));
        return new VisualStimulusPairSpec(first, second, feature);
    }

    public static bool TryDecodePair(string encoded, out VisualStimulusPairSpec? pair)
    {
        try
        {
            pair = DecodePair(encoded);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            pair = null;
            return false;
        }
    }

    public static string EncodeException(VisualStimulusExceptionSpec exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(exception.Stimulus);
        Validate(exception.Stimulus, nameof(exception));
        ValidateEnum(exception.ExpectedAction, nameof(exception.ExpectedAction));
        if (exception.Ordinal <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exception.Ordinal),
                exception.Ordinal,
                "Visual stimulus exception ordinal must be positive.");
        }

        if (string.IsNullOrWhiteSpace(exception.Reason) ||
            exception.Reason.Contains(';') ||
            exception.Reason.Contains('|'))
        {
            throw new ArgumentException(
                "Visual stimulus exception reason must be non-empty and cannot contain collection delimiters.",
                nameof(exception));
        }

        var ordinal = Invariant(exception.Ordinal);
        return $"exception {ordinal}: exception-{ordinal} -> " +
            $"{exception.ExpectedAction.ToString().ToLowerInvariant()} instead; {ExceptionFormatVersion};" +
            $"stimulus {Encode(exception.Stimulus)};reason {exception.Reason.Trim()}";
    }

    public static VisualStimulusExceptionSpec DecodeException(string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded) ||
            !encoded.StartsWith("exception ", StringComparison.Ordinal))
        {
            throw new FormatException("Visual stimulus exception material must begin with a numbered exception.");
        }

        var insteadMarker = $" instead; {ExceptionFormatVersion};stimulus ";
        const string reasonMarker = ";reason ";
        var colon = encoded.IndexOf(": ", StringComparison.Ordinal);
        var arrow = encoded.IndexOf(" -> ", colon + 2, StringComparison.Ordinal);
        var instead = encoded.IndexOf(insteadMarker, arrow + 4, StringComparison.Ordinal);
        var reasonStart = encoded.LastIndexOf(reasonMarker, StringComparison.Ordinal);
        if (colon < 0 || arrow <= colon || instead <= arrow || reasonStart <= instead)
        {
            throw new FormatException("Visual stimulus exception material does not use canonical separators.");
        }

        var ordinalValue = encoded["exception ".Length..colon];
        if (!int.TryParse(
                ordinalValue,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var ordinal) ||
            ordinal <= 0)
        {
            throw new FormatException("Visual stimulus exception ordinal must be a positive integer.");
        }

        var stableToken = encoded[(colon + 2)..arrow];
        if (!string.Equals(stableToken, $"exception-{Invariant(ordinal)}", StringComparison.Ordinal))
        {
            throw new FormatException("Visual stimulus exception token must match its ordinal.");
        }

        var actionValue = encoded[(arrow + 4)..instead];
        if (!Enum.TryParse<VisualStimulusResponseAction>(
                actionValue,
                ignoreCase: true,
                out var action) ||
            !Enum.IsDefined(action))
        {
            throw new FormatException("Visual stimulus exception action must be tap or withhold.");
        }

        var stimulusStart = instead + insteadMarker.Length;
        var stimulus = Decode(encoded[stimulusStart..reasonStart]);
        var reason = encoded[(reasonStart + reasonMarker.Length)..];
        var exception = new VisualStimulusExceptionSpec(ordinal, stimulus, action, reason);
        if (!string.Equals(EncodeException(exception), encoded, StringComparison.Ordinal))
        {
            throw new FormatException("Visual stimulus exception material is not canonical.");
        }

        return exception;
    }

    public static bool TryDecodeException(string encoded, out VisualStimulusExceptionSpec? exception)
    {
        try
        {
            exception = DecodeException(encoded);
            return true;
        }
        catch (Exception caught) when (caught is FormatException or ArgumentException)
        {
            exception = null;
            return false;
        }
    }

    private static string[] Segments(string encoded, string expectedVersion, int expectedCount)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            throw new FormatException("Visual stimulus material must be non-empty.");
        }

        var segments = encoded.Split(';', StringSplitOptions.None);
        if (segments.Length != expectedCount ||
            !string.Equals(segments[0], expectedVersion, StringComparison.Ordinal))
        {
            throw new FormatException($"Visual stimulus material must use canonical {expectedVersion} format.");
        }

        return segments;
    }

    private static IEnumerable<string> SpecSegments(VisualStimulusSpec stimulus, string prefix)
    {
        return
        [
            $"{prefix}.shape={stimulus.Shape}",
            $"{prefix}.color={stimulus.Color}",
            $"{prefix}.fill={stimulus.Fill}",
            $"{prefix}.size={stimulus.Size}",
            $"{prefix}.direction={stimulus.Direction}",
            $"{prefix}.mark={stimulus.Mark}",
            $"{prefix}.mark-position={stimulus.MarkPosition}",
            $"{prefix}.mark-count={Invariant(stimulus.MarkCount)}",
            $"{prefix}.mark-orientation={stimulus.MarkOrientation}",
            $"{prefix}.border={stimulus.Border}",
        ];
    }

    private static VisualStimulusSpec DecodeSpec(
        IReadOnlyList<string> segments,
        int offset,
        string? prefix)
    {
        var keyPrefix = prefix is null ? string.Empty : prefix + ".";
        return new VisualStimulusSpec(
            ReadEnum<VisualStimulusShape>(segments[offset], keyPrefix + "shape"),
            ReadEnum<VisualStimulusColor>(segments[offset + 1], keyPrefix + "color"),
            ReadEnum<VisualStimulusFill>(segments[offset + 2], keyPrefix + "fill"),
            ReadEnum<VisualStimulusSize>(segments[offset + 3], keyPrefix + "size"),
            ReadEnum<VisualStimulusDirection>(segments[offset + 4], keyPrefix + "direction"),
            ReadEnum<VisualStimulusMark>(segments[offset + 5], keyPrefix + "mark"),
            ReadEnum<VisualStimulusPosition>(segments[offset + 6], keyPrefix + "mark-position"),
            ReadInt(segments[offset + 7], keyPrefix + "mark-count"),
            ReadEnum<VisualStimulusOrientation>(segments[offset + 8], keyPrefix + "mark-orientation"),
            ReadEnum<VisualStimulusBorder>(segments[offset + 9], keyPrefix + "border"));
    }

    private static TEnum ReadEnum<TEnum>(string segment, string key)
        where TEnum : struct, Enum
    {
        var value = ReadValue(segment, key);
        if (!Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new FormatException($"Visual stimulus property {key} has unsupported value {value}.");
        }

        return parsed;
    }

    private static int ReadInt(string segment, string key)
    {
        var value = ReadValue(segment, key);
        return int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Visual stimulus property {key} must be an integer.");
    }

    private static string Invariant(int value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ReadValue(string segment, string key)
    {
        var expectedPrefix = key + "=";
        if (!segment.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
            segment.Length == expectedPrefix.Length)
        {
            throw new FormatException($"Visual stimulus material is missing canonical property {key}.");
        }

        return segment[expectedPrefix.Length..];
    }

    private static void Validate(VisualStimulusSpec stimulus, string parameterName)
    {
        ValidateEnum(stimulus.Shape, parameterName);
        ValidateEnum(stimulus.Color, parameterName);
        ValidateEnum(stimulus.Fill, parameterName);
        ValidateEnum(stimulus.Size, parameterName);
        ValidateEnum(stimulus.Direction, parameterName);
        ValidateEnum(stimulus.Mark, parameterName);
        ValidateEnum(stimulus.MarkPosition, parameterName);
        ValidateEnum(stimulus.MarkOrientation, parameterName);
        ValidateEnum(stimulus.Border, parameterName);

        if (stimulus.MarkCount is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                stimulus.MarkCount,
                "Visual stimulus mark count must be between zero and four.");
        }

        if ((stimulus.Mark == VisualStimulusMark.None && stimulus.MarkCount != 0) ||
            (stimulus.Mark != VisualStimulusMark.None && stimulus.MarkCount == 0))
        {
            throw new ArgumentException(
                "Visual stimulus mark and mark count must either both be absent or both be present.",
                parameterName);
        }

        if (stimulus.Mark == VisualStimulusMark.None &&
            (stimulus.MarkPosition != VisualStimulusPosition.Center ||
                stimulus.MarkOrientation != VisualStimulusOrientation.None))
        {
            throw new ArgumentException(
                "Visual stimuli without a mark cannot assign mark position or orientation.",
                parameterName);
        }

        if ((stimulus.Shape == VisualStimulusShape.Arrow &&
                stimulus.Direction == VisualStimulusDirection.None) ||
            (stimulus.Shape != VisualStimulusShape.Arrow &&
                stimulus.Direction != VisualStimulusDirection.None))
        {
            throw new ArgumentException(
                "Only arrow stimuli must assign a non-empty direction.",
                parameterName);
        }
    }

    private static void ValidateEnum<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown visual stimulus value.");
        }
    }
}
