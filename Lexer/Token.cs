namespace FCompiler.Lexer;

public enum TokenType
{
  LParen,
  RParen,
  Quote,
  Identifier,
  Integer,
  Real,
  Boolean,
  Null,
  Setq,
  Func,
  Lambda,
  Cond,
  Prog,
  While,
  Return,
  Break
}

public record Token(TokenType Type, object? Value = null) {
  public override string ToString() =>
    Value is null
      ? $"{Type}"
      : $"{Type}({Value})";
}
