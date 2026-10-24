using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace EfCore.QueryExplain.Providers;

/// <summary>
/// Parses the output of SQL Server <c>SET SHOWPLAN_XML ON</c> into <see cref="PlanNode"/> trees.
/// </summary>
public static class SqlServerPlanParser
{
    private static readonly XNamespace ShowPlanNamespace = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";

    /// <summary>
    /// Parses a ShowPlan XML document.
    /// </summary>
    /// <param name="showplanXml">The raw ShowPlan XML returned by SQL Server.</param>
    /// <returns>
    /// One root node per top-level <c>RelOp</c> under each <c>QueryPlan</c>, or an empty list when the XML
    /// is invalid or does not use the ShowPlan namespace.
    /// </returns>
    public static IReadOnlyList<PlanNode> Parse(string showplanXml)
    {
        try
        {
            var document = XDocument.Parse(showplanXml);

            return document.Descendants(ShowPlanNamespace + "QueryPlan")
                .SelectMany(queryPlan => queryPlan.Elements(ShowPlanNamespace + "RelOp"))
                .Select(MapNode)
                .ToList();
        }
        catch (XmlException)
        {
            return [];
        }
    }

    private static PlanNode MapNode(XElement relOp)
    {
        var obj = OwnDescendants(relOp).FirstOrDefault(e => e.Name == ShowPlanNamespace + "Object");

        return new PlanNode(
            Operation: (string?)relOp.Attribute("PhysicalOp") ?? string.Empty,
            Table: StripBrackets((string?)obj?.Attribute("Table")),
            Index: StripBrackets((string?)obj?.Attribute("Index")),
            EstimatedRows: ParseDouble(relOp.Attribute("EstimateRows")) is { } rows ? (long)Math.Round(rows) : null,
            EstimatedCost: ParseDouble(relOp.Attribute("EstimatedTotalSubtreeCost")),
            Children: NestedRelOps(relOp).Select(MapNode).ToList());
    }

    /// <summary>
    /// Returns the RelOp elements directly beneath <paramref name="relOp"/>, skipping over intermediate
    /// operators (e.g. <c>NestedLoops</c>) and not descending into the RelOps found.
    /// </summary>
    private static IEnumerable<XElement> NestedRelOps(XElement relOp)
    {
        foreach (var child in relOp.Elements())
        {
            if (child.Name == ShowPlanNamespace + "RelOp")
            {
                yield return child;
            }
            else
            {
                foreach (var nested in NestedRelOps(child))
                {
                    yield return nested;
                }
            }
        }
    }

    /// <summary>
    /// Returns the descendants of <paramref name="relOp"/> that belong to it, not to any nested RelOp.
    /// </summary>
    private static IEnumerable<XElement> OwnDescendants(XElement relOp)
    {
        foreach (var child in relOp.Elements())
        {
            if (child.Name == ShowPlanNamespace + "RelOp")
            {
                continue;
            }

            yield return child;

            foreach (var descendant in OwnDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static double? ParseDouble(XAttribute? attribute) =>
        attribute is not null &&
        double.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static string? StripBrackets(string? identifier) =>
        identifier?.Replace("[", string.Empty).Replace("]", string.Empty);
}
