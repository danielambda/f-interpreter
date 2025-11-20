namespace FCompiler.Interpreter;

public class ReturnException(Value value) : Exception {
    public Value Value { get; } = value;
}

public class BreakException : Exception;
