namespace FCompiler.Interpreter;

public class Environment {
    private readonly Dictionary<string, Value> _symbols = new();
    private readonly Environment? _parent;

    public Environment(Environment? parent = null) {
        _parent = parent;
    }

    public void Define(string name, Value value) {
        _symbols[name] = value;
    }

    public void Set(string name, Value value) {
        if (_symbols.ContainsKey(name)) {
            _symbols[name] = value;
        } else if (_parent != null) {
            _parent.Set(name, value);
        } else {
            throw new Exception($"Undefined variable: {name}");
        }
    }

    public Value Get(string name) {
        if (_symbols.TryGetValue(name, out var value)) {
            return value;
        }
        return _parent?.Get(name) ?? throw new Exception($"Undefined variable: {name}");
    }

    public bool Contains(string name) {
        return _symbols.ContainsKey(name) || (_parent?.Contains(name) ?? false);
    }
}