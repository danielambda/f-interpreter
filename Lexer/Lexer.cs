namespace FCompiler.Lexer;

public class Lexer {
  private readonly string _input;
  private int _position;

  public Lexer(string input) {
    _input = input;
    _position = 0;
  }

  private char Current => _position < _input.Length ? _input[_position] : '\0';

  private char Peek => _position + 1 < _input.Length ? _input[_position + 1] : '\0';

  private void Advance() => _position++;

  private void SkipWhitespaceAndComments() {
    while (true) {
      while (char.IsWhiteSpace(Current))
        Advance();

      if (Current == ';') {
        Advance();
        while (Current != '\n' && Current != '\0')
          Advance();
        continue;
      }

      break;
    }
  }

  public Token? NextToken() {
    SkipWhitespaceAndComments();

    if (Current == '\0')
      return null;

    if (Current == '(') {
      Advance();
      return new Token(TokenType.LParen);
    }

    if (Current == ')') {
      Advance();
      return new Token(TokenType.RParen);
    }

    if (Current == '\'') {
      Advance();
      return new Token(TokenType.Quote);
    }

    if (char.IsLetter(Current)) {
      var id = "";
      while (char.IsLetter(Current) || char.IsDigit(Current)) {
        id += Current;
        Advance();
      }

      return id switch {
        "true"   => new Token(TokenType.Boolean, true),
        "false"  => new Token(TokenType.Boolean, false),
        "null"   => new Token(TokenType.Null, null),
        "quote"  => new Token(TokenType.Quote),
        "setq"   => new Token(TokenType.Setq),
        "func"   => new Token(TokenType.Func),
        "lambda" => new Token(TokenType.Lambda),
        "cond"   => new Token(TokenType.Cond),
        "prog"   => new Token(TokenType.Prog),
        "while"  => new Token(TokenType.While),
        "return" => new Token(TokenType.Return),
        "break"  => new Token(TokenType.Break),
        _        => new Token(TokenType.Identifier, id)
      };
    }

    if (char.IsDigit(Current) ||
        ((Current == '+' || Current == '-') && char.IsDigit(Peek))) {
      var num = "";
      if (Current == '+' || Current == '-') {
        num += Current;
        Advance();
      }

      if (!char.IsDigit(Current))
        throw new LexerException("Expected digit after sign");

      while (char.IsDigit(Current)) {
        num += Current;
        Advance();
      }

      var hasDot = false;
      if (Current == '.') {
        hasDot = true;
        num += Current;
        Advance();

        if (!char.IsDigit(Current))
          throw new LexerException("Expected digit after '.'");

        while (char.IsDigit(Current)) {
          num += Current;
          Advance();
        }
      }

      return hasDot switch {
        true => double.TryParse(num, out double realValue)
          ? new Token(TokenType.Real, realValue)
          : throw new LexerException($"Invalid real number: {num}"),
        false => int.TryParse(num, out int intValue)
          ? new Token(TokenType.Integer, intValue)
          : throw new LexerException($"Invalid integer number: {num}")
      };
    }

    throw new LexerException($"Unexpected character: {Current}");
  }
}

public class LexerException : Exception {
  public LexerException(string message) : base(message) { }
}
