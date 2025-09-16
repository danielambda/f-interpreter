using LanguageExt;
using System.Text;

namespace FCompiler.Lexer;

public record struct LexerError(string message);

public struct Lexer {
  public static IEnumerable<Either<LexerError, Token>> Lex(string input) {
    var lexer = new Lexer(input);
    var mToken = lexer.NextToken();
    while(mToken is { } token) {
      yield return token;
      mToken = lexer.NextToken();
    }
  }

  private readonly string _input;
  private int _position;

  private Lexer(string input) {
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

  private Either<LexerError, Token>? NextToken() {
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
      return new Token(TokenType.QuoteOp);
    }

    if (char.IsLetter(Current)) {
      var idBuilder = new StringBuilder();
      while (char.IsLetter(Current) || char.IsDigit(Current)) {
          idBuilder.Append(Current);
          Advance();
      }
      string id = idBuilder.ToString();

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
      
      var numBuilder = new StringBuilder();
      
      if (Current == '+' || Current == '-') {
          numBuilder.Append(Current);
          Advance();
      }

      while (char.IsDigit(Current)) {
          numBuilder.Append(Current);
          Advance();
      }

      var hasDot = false;
      if (Current == '.') {
          hasDot = true;
          numBuilder.Append(Current);
          Advance();

          if (!char.IsDigit(Current))
              return new LexerError("Expected digit after '.'");

          while (char.IsDigit(Current)) {
              numBuilder.Append(Current);
              Advance();
          }
      }

      string numStr = numBuilder.ToString();
      return hasDot switch {
          true => double.TryParse(numStr, out double realValue)
              ? new Token(TokenType.Real, realValue)
              : new LexerError($"Invalid real number: {numStr}"),
          false => int.TryParse(numStr, out int intValue)
              ? new Token(TokenType.Integer, intValue)
              : new LexerError($"Invalid integer number: {numStr}")
      };
  }

    return new LexerError($"Unexpected character: {Current}");
  }
}
