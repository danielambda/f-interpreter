namespace FCompiler.Utils;

public record struct Span(int lineNumber, int begin, int end) {
    public string PrettyPrint() =>
        $"Line {lineNumber}, Symbol {begin}";
};
