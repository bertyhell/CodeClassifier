/********************************************************
 *	Author: Andrew Deren
 *	Date: July, 2004
 *	http://www.adersoftware.com
 *
 *	StringTokenizer class. You can use this class in any way you want
 * as long as this header remains in this file.
 *
 **********************************************************/

namespace CodeClassifier.StringTokenizer
{
	public enum TokenKind
	{
		Unknown,
		Word,
		Number,
		DoubleQuotedString,
		SingleQuotedString,
		WhiteSpace,
		EqualsSign,
		PlusSign,
		Hyphen,
		Slash,
		Comma,
		FullStop,
		Asterisk,
		Tilde,
		ExplanationMark,
		AtSign,
		Hash,
		Dollar,
		Percent,
		CircumflexAccent,
		Ampersand,
		LeftParenthesis,
		RightParenthesis,
		LeftCurlyBracket,
		RightCurlyBracket,
		LeftBracket,
		RightBracket,
		Colon,
		SemiColon,
		LessThanSign,
		GreaterThanSign,
		QuestionMark,
		VerticalLine,
		Backslash,
		GraveAccent,
		SingleQuote,
		Underscore,
		Eol,
		Eof
	}

	public class Token
	{
		readonly int _line;
		readonly int _column;
		readonly string _value;
		readonly TokenKind _kind;

		public Token(TokenKind kind, string value, int line, int column)
		{
			_kind = kind;
			_value = value;
			_line = line;
			_column = column;
		}

		public int Column
		{
			get { return _column; }
		}

		public TokenKind Kind
		{
			get { return _kind; }
		}

		public int Line
		{
			get { return _line; }
		}

		public string Value
		{
			get { return _value; }
		}
	}

}
