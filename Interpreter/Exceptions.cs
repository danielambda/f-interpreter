namespace FCompiler.Interpreter;

public class ReturnException : Exception {
    public Value Value { get; }
    
    public ReturnException(Value value) {
        Value = value;
    }
}

public class BreakException : Exception {
}