namespace CodeClassifier
{
	class MatchTree
	{
		public TokenNode MatchTreeRoot { get; set; }
		public string Language { get; set; }
		public double TotalPossibleScore { get; set; }

		public MatchTree(TokenNode matchTreeRoot, string language, double totalPossibleScore)
		{
			MatchTreeRoot = matchTreeRoot;
			Language = language;
			TotalPossibleScore = totalPossibleScore;
		}
	}
}
