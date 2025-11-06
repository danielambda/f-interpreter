using FCompiler.Utils;

namespace FCompiler.Semantic;

public abstract record SymbolInfo(Span span);
public record VariableInfo(Span span) : SymbolInfo(span);
public record FunctionInfo(List<string> parameters, Span span) : SymbolInfo(span);

public record Scope(string Name, Scope.ScopeType Type, Scope? Parent = null) {
    private readonly Dictionary<string, SymbolInfo> _symbols = [];

    public enum ScopeType {
        Global,
        Function,
        Loop,
        Prog
    }

    public bool TryAddSymbol(string name, SymbolInfo info) {
        if (_symbols.ContainsKey(name))
            return false;

        _symbols[name] = info;
        return true;
    }

    public SymbolInfo? Lookup(string name) {
        if (_symbols.TryGetValue(name, out var info))
            return info;

        return Parent?.Lookup(name);
    }

    public bool IsInLoopContext() {
        var current = this;
        while (current is not null) {
            if (current.Type == ScopeType.Loop)
                return true;
            current = current.Parent;
        }
        return false;
    }

    public bool IsInFunctionContext() {
        var current = this;
        while (current is not null) {
            if (current.Type == ScopeType.Function)
                return true;
            current = current.Parent;
        }
        return false;
    }
}

