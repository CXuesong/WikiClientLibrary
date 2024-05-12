namespace WikiClientLibrary.Cargo;

public class CargoQueryParameters
{

    /// <summary>The query offset.</summary>
    public int Offset { get; set; }

    /// <summary>A limit on the number of results returned.</summary>
    public int Limit { get; set; } = 50;

    /// <summary>The Cargo database table or tables on which to search.</summary>
    public IEnumerable<string> Tables { get; set; } = Array.Empty<string>();

    /// <summary>The table field(s) to retrieve.</summary>
    public IEnumerable<string> Fields { get; set; } = Array.Empty<string>();

    /// <summary>The conditions for the query, corresponding to an SQL WHERE clause.</summary>
    public string? Where { get; set; }

    /// <summary>Conditions for joining multiple tables (LEFT OUTER JOIN), corresponding to an SQL JOIN ON clause.</summary>
    /// <value>a sequence containing search conditions (<c>ON table1a.field1a = table1b.field1b, ... </c>) for the JOIN clause.
    /// Conditions will be concatenated with comma (<c>,</c>).</value>
    public IEnumerable<string>? JoinOn { get; set; }

    /// <summary>Field(s) on which to group results, corresponding to an SQL GROUP BY clause.</summary>
    public string? GroupBy { get; set; }

    /// <summary>Conditions for grouped values, corresponding to an SQL HAVING clause.</summary>
    public string? Having { get; set; }

    /// <summary>The order of results, corresponding to an SQL ORDER BY clause.</summary>
    public IEnumerable<string>? OrderBy { get; set; }

}