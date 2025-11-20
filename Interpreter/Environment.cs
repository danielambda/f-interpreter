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

    public void Set(string name, Value value) {
        if (_symbols.ContainsKey(name)) {
            _symbols[name] = value;
        } else if (_parent is not null) {
            _parent.Set(name, value);
        } else {
            throw new Exception($"Undefined variable: {name}");
        }
    }

    public Value? Get(string name) =>
        _symbols.TryGetValue(name, out var value)
            ? value
            : _parent?.Get(name) ?? throw new Exception($"Undefined variable: {name}");

    public bool Contains(string name)
        => _symbols.ContainsKey(name)
        || (_parent?.Contains(name) ?? false);
}
