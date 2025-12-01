using FCompiler.Interpreter;

namespace FCompiler.Printing;

public static class LispPrinter {
    public static string? FormatValue(Value value) => value switch {
        Value.Atom a    => "'" + a.Name,
        Value.Integer i => i.Value.ToString(),
        Value.Real r    => r.Value.ToString(),
        Value.Bool b    => b.Value.ToString().ToLower(),
        Value.Null      => "null",
        Value.List list => $"'({string.Join(separator: ' ', list.Values.Map(InQuoteFormatValue))})",
        Value.Function func => null,
        Value.BuiltinFunction builtin => null,
        _ => $"Unknown: {value.GetType().Name}",
    };

    private static string? InQuoteFormatValue(Value value) => value switch {
        Value.List list => $"({string.Join(separator: ' ', list.Values.Map(InQuoteFormatValue))})",
        Value.Atom a    => a.Name,
        _ => FormatValue(value),
    };
}
