using FCompiler.Interpreter;

namespace FCompiler.Printing;

public static class PrettyPrinter {
    public static string PrintValue(Value value) => value switch {
        Value.Atom a    => $"Atom: {a.Name}",
        Value.Integer i => $"Integer: {i.Value}",
        Value.Real r    => $"Real: {r.Value}",
        Value.Bool b    => $"Bool: {b.Value}",
        Value.Null      => "Null",
        Value.List list => $"List: {string.Join(separator: ' ', list.Values.Select(PrintValue))}",
        Value.Function func => $"Function with params: ({string.Join(' ', func.Parameters)})",
        Value.BuiltinFunction builtin => $"Builtin: {builtin.Name}",
        _ => $"Unknown: {value.GetType().Name}",
    };
}
