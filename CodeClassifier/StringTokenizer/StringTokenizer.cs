/********************************************************8
 *	Author: Andrew Deren
 *	Date: July, 2004
 *	http://www.adersoftware.com
 *
 *	StringTokenizer class. You can use this class in any way you want
 * as long as this header remains in this file.
 *
 **********************************************************/

using System;
using System.Collections.Generic;
using System.IO;

namespace CodeClassifier.StringTokenizer
{
	/// <summary>
	/// StringTokenizer tokenized string (or stream) into tokens.
	/// </summary>
	public class StringTokenizer
	{
		const char EOF = (char)0;

		int _line;
		int _column;
		int _pos;	// position within data

		readonly string _data;

		bool _ignoreWhiteSpace;
		Dictionary<char, TokenKind> _symbolChars;

		int _saveLine;
		int _saveCol;
		int _savePos;

		public StringTokenizer(TextReader reader)
		{
			if (reader == null)
				throw new ArgumentNullException("reader");

			_data = reader.ReadToEnd();

			Reset();
		}

		public StringTokenizer(string data)
		{
			if (data == null)
				throw new ArgumentNullException("data");

			_data = data;

			Reset();
		}

		/// <summary>
		/// gets or sets which characters are part of TokenKind.Symbol
		/// </summary>
		public Dictionary<char, TokenKind> SymbolChars
		{
			get { return _symbolChars; }
			set { _symbolChars = value; }
		}

		/// <summary>
		/// if set to true, white space characters will be ignored,
		/// but EOL and whitespace inside of string will still be tokenized
		/// </summary>
		public bool IgnoreWhiteSpace
		{
			get { return _ignoreWhiteSpace; }
			set { _ignoreWhiteSpace = value; }
		}

		private void Reset()
		{
			_ignoreWhiteSpace = false;
			_symbolChars = new Dictionary<char, TokenKind>
			               {
				                {'=' , TokenKind.EqualsSign},
								{'+' , TokenKind.PlusSign},
								{'-' , TokenKind.Hyphen},
								{'/' , TokenKind.Slash},
								{',' , TokenKind.Comma},
								{'.' , TokenKind.FullStop},
								{'*' , TokenKind.Asterisk},
								{'~' , TokenKind.Tilde},
								{'!' , TokenKind.ExplanationMark},
								{'@' , TokenKind.AtSign},
								{'#' , TokenKind.Hash},
								{'$' , TokenKind.Dollar},
								{'%' , TokenKind.Percent},
								{'^' , TokenKind.CircumflexAccent},
								{'&' , TokenKind.Ampersand},
								{'(' , TokenKind.LeftParenthesis},
								{')' , TokenKind.RightParenthesis},
								{'{' , TokenKind.LeftCurlyBracket},
								{'}' , TokenKind.RightCurlyBracket},
								{'[' , TokenKind.LeftBracket},
								{']' , TokenKind.RightBracket},
								{':' , TokenKind.Colon},
								{';' , TokenKind.SemiColon},
								{'<' , TokenKind.LessThanSign},
								{'>' , TokenKind.GreaterThanSign},
								{'?' , TokenKind.QuestionMark},
								{'|' , TokenKind.VerticalLine},
								{'\\', TokenKind.Backslash},
								{'`' , TokenKind.GraveAccent},
								{'\'', TokenKind.SingleQuote},
								{'_' , TokenKind.Underscore}
			               };

			_line = 1;
			_column = 1;
			_pos = 0;
		}

		protected char La(int count)
		{
			return _pos + count >= _data.Length ? EOF : _data[_pos + count];
		}

		protected char Consume()
		{
			char ret = _data[_pos];
			_pos++;
			_column++;

			return ret;
		}

		protected Token CreateToken(TokenKind kind, string value)
		{
			return new Token(kind, value, _line, _column);
		}

		protected Token CreateToken(TokenKind kind)
		{
			string tokenData = _data.Substring(_savePos, _pos - _savePos);
			return new Token(kind, tokenData, _saveLine, _saveCol);
		}

		public Token Next()
		{
		ReadToken:

			char ch = La(0);
			switch (ch)
			{
				case EOF:
					return CreateToken(TokenKind.Eof, string.Empty);

				case ' ':
				case '\t':
					{
						if (_ignoreWhiteSpace)
						{
							Consume();
							goto ReadToken;
						}
						return ReadWhitespace();
					}
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
					return ReadNumber();

				case '\r':
					{
						StartRead();
						Consume();
						if (La(0) == '\n')
							Consume();	// on DOS/Windows we have \r\n for new line

						_line++;
						_column = 1;

						return CreateToken(TokenKind.Eol);
					}
				case '\n':
					{
						StartRead();
						Consume();
						_line++;
						_column = 1;

						return CreateToken(TokenKind.Eol);
					}

				case '"':
					{
						return ReadDoubleQuotedString();
					}

				case '\'':
					{
						return ReadSingleQuotedString();
					}


				default:
					{
						if (Char.IsLetter(ch))
							return ReadWord();
						if (SymbolChars.ContainsKey(ch))
						{
							StartRead();
							Consume();
							return CreateToken(SymbolChars[ch]);
						}
						StartRead();
						Consume();
						return CreateToken(TokenKind.Unknown);
					}
			}
		}

		/// <summary>
		/// save read point positions so that CreateToken can use those
		/// </summary>
		private void StartRead()
		{
			_saveLine = _line;
			_saveCol = _column;
			_savePos = _pos;
		}

		/// <summary>
		/// reads all whitespace characters (does not include newline)
		/// </summary>
		/// <returns></returns>
		protected Token ReadWhitespace()
		{
			StartRead();

			Consume(); // consume the looked-ahead whitespace char

			while (true)
			{
				char ch = La(0);
				if (ch == '\t' || ch == ' ')
					Consume();
				else
					break;
			}

			return CreateToken(TokenKind.WhiteSpace);

		}

		/// <summary>
		/// reads number. Number is: DIGIT+ ("." DIGIT*)?
		/// </summary>
		/// <returns></returns>
		protected Token ReadNumber()
		{
			StartRead();

			bool hadDot = false;

			Consume(); // read first digit

			while (true)
			{
				char ch = La(0);
				if (Char.IsDigit(ch))
					Consume();
				else if (ch == '.' && !hadDot)
				{
					hadDot = true;
					Consume();
				}
				else
					break;
			}

			return CreateToken(TokenKind.Number);
		}

		/// <summary>
		/// reads word. Word contains any alpha character or _
		/// </summary>
		protected Token ReadWord()
		{
			StartRead();

			Consume(); // consume first character of the word

			while (true)
			{
				char ch = La(0);
				if (Char.IsLetter(ch) || ch == '_')
					Consume();
				else
					break;
			}

			return CreateToken(TokenKind.Word);
		}

		/// <summary>
		/// reads all characters until next " is found.
		/// If "" (2 quotes) are found, then they are consumed as
		/// part of the string
		/// </summary>
		/// <returns></returns>
		protected Token ReadDoubleQuotedString()
		{
			StartRead();

			Consume(); // read "

			while (true)
			{
				char ch = La(0);
				if (ch == EOF)
					break;
				if (ch == '\r')	// handle CR in strings
				{
					Consume();
					if (La(0) == '\n')	// for DOS & windows
						Consume();

					_line++;
					_column = 1;
				}
				else if (ch == '\n')	// new line in quoted string
				{
					Consume();

					_line++;
					_column = 1;
				}
				else if (ch == '"')
				{
					Consume();
					if (La(0) != '"')
						break;	// done reading, and this quotes does not have escape character
					Consume(); // consume second ", because first was just an escape
				}
				else
					Consume();
			}

			return CreateToken(TokenKind.DoubleQuotedString);
		}

		/// <summary>
		/// reads all characters until next " is found.
		/// If "" (2 quotes) are found, then they are consumed as
		/// part of the string
		/// </summary>
		/// <returns></returns>
		protected Token ReadSingleQuotedString()
		{
			StartRead();

			Consume(); // read "

			while (true)
			{
				char ch = La(0);
				if (ch == EOF)
					break;
				if (ch == '\r')	// handle CR in strings
				{
					Consume();
					if (La(0) == '\n')	// for DOS & windows
						Consume();

					_line++;
					_column = 1;
				}
				else if (ch == '\n')	// new line in quoted string
				{
					Consume();

					_line++;
					_column = 1;
				}
				else if (ch == '\'')
				{
					Consume();
					if (La(0) != '\'')
						break;	// done reading, and this quotes does not have escape character
					Consume(); // consume second ", because first was just an escape
				}
				else
					Consume();
			}

			return CreateToken(TokenKind.SingleQuotedString);
		}
	}
}
