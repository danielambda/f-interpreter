namespace FCompiler.Lexer;

public record struct Span(int lineNumber, int begin, int end);

public abstract record Token(Span span);
public record Identifier(string value, Span span)   : Token(span);
public record Integer(long value, Span span)        : Token(span);
public record Real(double value, Span span)         : Token(span);
public record Bool(bool value, Span span)           : Token(span);
public record Keyword(Keyword.Type type, Span span) : Token(span) {
  public enum Type : byte {
    Null,
    Quote,
    Setq,
    Func,
    Lambda,
    Cond,
    Prog,
    While,
    Return,
    Break,
  }
}
public record Punctuation(Punctuation.Type type, Span span) : Token(span) {
  public enum Type : byte {
    LParen,
    RParen,
    QuoteOp,
  }
}
