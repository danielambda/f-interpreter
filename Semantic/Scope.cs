namespace FCompiler.Semantic;

public record Scope(string name, Scope.Type type, Scope? parent = null) {
    public enum Type {
        Global,
        Function,
        While,
        Prog
    }

    private readonly HashSet<string> _symbols = [];

    public bool TryAddSymbol(string name) =>
        _symbols.Add(name);

    public bool Contains(string name) =>
        _symbols.Contains(name)
            ? true
            : parent?.Contains(name) ?? false;

    public bool IsIn(Type scopeType) {
        var current = this;
        while (current is { type: var t }) {
            if (t == scopeType)
                return true;
            current = current.parent;
        }
        return false;
    }
}

