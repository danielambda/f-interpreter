using FCompiler.Utils;

namespace FCompiler.Interpreter;

public class Environment {
    private readonly Dictionary<string, Value> _symbols;
    private readonly Environment? _parent;

    public Environment() : this([], null) {}
    public Environment(Environment parent) : this([], parent) {}

    private Environment(Dictionary<string, Value> symbols, Environment? parent) {
        _symbols = symbols;
        _parent = parent;
    }

    public void Define(string name, Value value) {
        _symbols[name] = value;
    }

    public void Set(string name, Value value, Span? span = null) {
        if (_symbols.ContainsKey(name)) {
            _symbols[name] = value;
        } else if (_parent is not null) {
            _parent.Set(name, value, span);
        } else {
            throw new Exception($"Undefined variable: {name} at {span?.PrettyPrint()}");
        }
    }

    public Value Get(string name, Span? span = null) =>
        _symbols.TryGetValue(name, out var value)
            ? value
            : _parent?.Get(name, span) ?? throw new Exception($"Undefined variable: {name} at {span?.PrettyPrint()}");

    public bool Contains(string name)
        => _symbols.ContainsKey(name)
        || (_parent?.Contains(name) ?? false);
}
