using FCompiler.Lexer;
using FCompiler.Parser;
using FCompiler.Utils;

namespace FCompiler.Interpreter;

public abstract record Value {
    public record Integer(long Value) : Value;
    public record Real(double Value) : Value;
    public record Bool(bool Value) : Value;
    public record Null : Value {
        public static Null Instance = new();
    }
    public record List(List<Value> Values) : Value;
    public record Function(List<string> Parameters, Semantic.Sem.Expr Body, Environment Closure) : Value;
    public record BuiltinFunction(string Name, Func<List<Value>, Value> Implementation) : Value;
}