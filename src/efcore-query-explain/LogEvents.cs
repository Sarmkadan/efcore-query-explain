using Microsoft.Extensions.Logging;

namespace EfCore.QueryExplain;

/// <summary>
/// <see cref="EventId"/>s used when logging <see cref="QueryFinding"/>s, one per <see cref="FindingKind"/>.
/// </summary>
internal static class LogEvents
{
    /// <summary>Logged for <see cref="FindingKind.MissingIndex"/>.</summary>
    public static readonly EventId MissingIndex = new(1001, nameof(MissingIndex));

    /// <summary>Logged for <see cref="FindingKind.NPlusOne"/>.</summary>
    public static readonly EventId NPlusOne = new(1002, nameof(NPlusOne));

    /// <summary>Logged for <see cref="FindingKind.UnusedIndex"/>.</summary>
    public static readonly EventId UnusedIndex = new(1003, nameof(UnusedIndex));

    /// <summary>Logged for <see cref="FindingKind.Other"/>.</summary>
    public static readonly EventId Other = new(1000, nameof(Other));

    /// <summary>Reserved for failures while analyzing a captured plan.</summary>
    public static readonly EventId AnalysisFailed = new(1900, nameof(AnalysisFailed));

    /// <summary>Reserved for EXPLAIN calls that exceed <see cref="QueryExplainOptions.ExplainTimeout"/>.</summary>
    public static readonly EventId ExplainTimeout = new(1901, nameof(ExplainTimeout));
}
