using System;
using System.Collections.Generic;
using CodeClassifier.StringTokenizer;

namespace CodeClassifier
{
	public class TokenNode
	{
		public TokenKind Kind { get; private set; }
		public List<TokenNode> NextTokens { get; private set; }
		public HashSet<string> Examples { get; private set; }
		public double Score { get; private set; }
		public int Level { get; set; }

		public TokenNode(TokenKind kind, int level, double score, string firstExample)
		{
			Kind = kind;
			Level = level;
			Score = score;
			NextTokens = new List<TokenNode>();
			Examples = new HashSet<string> { firstExample };
		}
	}
}
