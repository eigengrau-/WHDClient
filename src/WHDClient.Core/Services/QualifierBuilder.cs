using System.Text;

namespace WHDClient.Core.Services;

/// <summary>
/// Builds WHD qualifier strings, e.g. ((statustype.statusTypeName = 'Open') and (location.locationName = 'ATL')).
/// </summary>
public static class QualifierBuilder
{
    public enum Op
    {
        Eq, NotEq, Lt, Gt, LtEq, GtEq, Like, CaseInsensitiveLike
    }

    public static string OpToString(Op op) => op switch
    {
        Op.Eq => "=",
        Op.NotEq => "!=",
        Op.Lt => "<",
        Op.Gt => ">",
        Op.LtEq => "<=",
        Op.GtEq => ">=",
        Op.Like => "like",
        Op.CaseInsensitiveLike => "caseInsensitiveLike",
        _ => "="
    };

    /// <summary>Escapes a string literal for inclusion inside single quotes in a qualifier.</summary>
    public static string Quote(string value) => $"'{value.Replace("'", "\\'")}'";

    public static string Clause(string attribute, Op op, string rawValue, bool valueIsLiteralString = true)
    {
        var v = valueIsLiteralString ? Quote(rawValue) : rawValue;
        return $"({attribute} {OpToString(op)} {v})";
    }

    public static string And(params string[] clauses) => Join("and", clauses);
    public static string Or(params string[] clauses) => Join("or", clauses);
    public static string Not(string clause) => $"(not {clause})";

    private static string Join(string keyword, IEnumerable<string> clauses)
    {
        var list = clauses.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        if (list.Count == 0) return "";
        if (list.Count == 1) return list[0];
        var sb = new StringBuilder("(");
        sb.Append(string.Join($" {keyword} ", list));
        sb.Append(')');
        return sb.ToString();
    }
}
