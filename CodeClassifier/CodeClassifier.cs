using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeClassifier.StringTokenizer;
using ProbabilityFunctions;

namespace CodeClassifier
{
	public class CodeClassifier
	{
		private static CodeClassifier _instance;

		private const double SCORE_MULTIPLIER_PER_LEVEL = 2;
		private const double SCORE_MULTIPLIER_FOR_EXACT_MATCH = 5;

		private const double MIN_TOKEN_FREQ_PER_FILE = 2;


		private static List<MatchTree> _matchTrees;
		private static Classifier _bayesClassifier;
		private static HashSet<string> _uniqueTokenSet;
		private static List<string> _uniqueTokenList;

		private CodeClassifier()
		{
			string trainingSetPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			if (trainingSetPath == null)
			{
				throw new DirectoryNotFoundException("Could not find the training-set folder.");
			}

			// Train Verhelst MatchTree classifier
			string path = Path.Combine(trainingSetPath, "training-set");

			// unique tokens list
			_uniqueTokenSet = new HashSet<string>();
			_matchTrees = new List<MatchTree>();

			Dictionary<string, List<Dictionary<string, double>>> tokenFreqPerLanguage = new Dictionary<string, List<Dictionary<string, double>>>();

			string[] folders = Directory.GetDirectories(path);
			foreach (string languageFolder in folders)
			{
				//Console.WriteLine("handling language: " + languageFolder);
				string[] files = Directory.GetFiles(languageFolder);
				TokenNode rootNode = null;
				double totalPossibleScore = 0;
				string languageName = Path.GetFileNameWithoutExtension(languageFolder) ?? "undefined";

				List<Dictionary<string, double>> tokenFreqPerFile = new List<Dictionary<string, double>>();

				foreach (string filePath in files)
				{
					//Console.WriteLine("\thandling file: " + Path.GetFileNameWithoutExtension(filePath));
					string fileContent = File.ReadAllText(filePath);
					List<Token> tokens = GetAllTokens(fileContent);


					// Verhelst algo
					// Calculate the total possible score to normalize the score results
					if (rootNode == null)
					{
						rootNode = BuildMatchTree(tokens, 0, out totalPossibleScore, null);
					}
					else
					{
						rootNode = BuildMatchTree(tokens, totalPossibleScore, out totalPossibleScore, rootNode);
					}

					//// Train Bayes Hellinger Classifier
					tokenFreqPerFile.Add(BuildFrequencyTable(tokens));
				}


				_matchTrees.Add(new MatchTree(rootNode, languageName, totalPossibleScore));

				tokenFreqPerLanguage.Add(languageName, tokenFreqPerFile);
			}

			DataTable bayesMatchTable = new DataTable();
			bayesMatchTable.Columns.Add("blablablabla"); // Must be different from any token in any snipplet :( => will fail some of the time :p
			foreach (string uniqueToken in _uniqueTokenSet)
			{
				bayesMatchTable.Columns.Add(uniqueToken, typeof(double));
			}


			Console.WriteLine("finished calculating freq tables----------------------------------");

			//TODO Remove tokens that are longer than 20 chars

			// Optimize freq tables => only use most used tokens


			//training data.
			_uniqueTokenList = _uniqueTokenSet.ToList();
			foreach (string language in tokenFreqPerLanguage.Keys)
			{

				foreach (Dictionary<string, double> tokenFreq in tokenFreqPerLanguage[language])
				{
					object[] rowValues = new object[_uniqueTokenList.Count + 1];
					rowValues[0] = language;
					Console.WriteLine(language);
					for (int i = 0; i < _uniqueTokenList.Count; i++)
					{
						if (tokenFreq.ContainsKey(_uniqueTokenList[i]))
						{
							rowValues[i + 1] = tokenFreq[_uniqueTokenList[i]];
						}
						else
						{
							rowValues[i + 1] = 0;
						}
					}
					bayesMatchTable.Rows.Add(rowValues);
				}
			}

			_bayesClassifier = new Classifier();

			Console.WriteLine("training classifier----------------------------------");

			_bayesClassifier.TrainClassifier(bayesMatchTable);

			Console.WriteLine("finished training classifier----------------------------------");
		}

		private static Dictionary<string, double> BuildFrequencyTable(List<Token> tokens)
		{
			Dictionary<string, double> tokenFrequencies = new Dictionary<string, double>();
			foreach (Token token in tokens)
			{
				// Variablenames do not contribute much to the freq table signature of a language
				if (token.Kind != TokenKind.DoubleQuotedString && token.Kind != TokenKind.SingleQuotedString && token.Value.Length < 10)
				{
					if (tokenFrequencies.ContainsKey(token.Value))
					{
						tokenFrequencies[token.Value]++;
					}
					else
					{
						tokenFrequencies.Add(token.Value, 1);
					}
				}
			}

			Dictionary<string, double> filteredTokenFrequencies = new Dictionary<string, double>();
			foreach (string tokenString in tokenFrequencies.Keys)
			{
				if (tokenFrequencies[tokenString] >= MIN_TOKEN_FREQ_PER_FILE)
				{
					_uniqueTokenSet.Add(tokenString);
					filteredTokenFrequencies.Add(tokenString, tokenFrequencies[tokenString]);
				}
			}

			List<string> keys = filteredTokenFrequencies.Keys.ToList();
			foreach (string key in keys)
			{
				filteredTokenFrequencies[key] = filteredTokenFrequencies[key] / tokens.Count;
			}
			return filteredTokenFrequencies;
		}

		private static TokenNode BuildMatchTree(List<Token> tokens, double exisitngTotalPossibleScore, out double totalScorePossible, TokenNode root)
		{
			// Recursivly build the tree
			if (root == null)
			{
				// If rootnode was not passed in => create a new one
				root = new TokenNode(TokenKind.Unknown, 0, 1, null);
			}
			double totalScore = exisitngTotalPossibleScore;
			for (int index = 0; index < tokens.Count - 1; index++)
			{
				totalScore += AddTokens(root, tokens, index);
			}

			totalScorePossible = totalScore;
			return root;
		}

		private static double AddTokens(TokenNode tokenNode, IList<Token> tokens, int index)
		{
			double totalScore = 0;
			while (index < tokens.Count && tokenNode.Level < 10)
			{
				Token codeToken = tokens[index];
				TokenNode nextTreeToken = tokenNode.NextTokens.FirstOrDefault(nt => nt.Kind == codeToken.Kind);
				if (nextTreeToken == null)
				{
					// Token doesn't exist on this tree level yet
					var newToken = new TokenNode(codeToken.Kind, tokenNode.Level + 1, tokenNode.Score * SCORE_MULTIPLIER_PER_LEVEL, codeToken.Value);
					totalScore += tokenNode.Score * SCORE_MULTIPLIER_PER_LEVEL;
					tokenNode.NextTokens.Add(newToken);
					tokenNode = newToken;
				}
				else
				{
					// Token already exists on this level
					nextTreeToken.Examples.Add(codeToken.Value);
					tokenNode = nextTreeToken;
				}
				index++;
			}
			return totalScore;
		}

		private static List<Token> GetAllTokens(string code)
		{
			StringTokenizer.StringTokenizer stringTokenizer = new StringTokenizer.StringTokenizer(code);

			List<Token> tokens = new List<Token>();
			Token token;
			do
			{
				token = stringTokenizer.Next();
				tokens.Add(token);
			} while (token.Kind != TokenKind.Eof);
			return tokens;
		}

		public static string Classify(string snippet)
		{
			// ReSharper disable once RedundantAssignment
			Dictionary<string, double> scores;
			return Classify(snippet, out scores);
		}

		public static string Classify(string snippet, out Dictionary<string, double> scores)
		{
			if (_instance == null)
			{
				_instance = new CodeClassifier();
			}

			string bestLanguageTp = ClassifyByTokenProbability(snippet, out scores);
			OutputLanguageScores(scores, bestLanguageTp);

			string bestLanguageMt = ClassifyByMatchTrees(snippet, out scores);
			OutputLanguageScores(scores, bestLanguageMt);

			return bestLanguageMt;
		}

		private static void OutputLanguageScores(Dictionary<string, double> scores, string bestLanguage)
		{
			string languagesAndScores = "";

			KeyValuePair<string, double> maxLanguage = scores.Aggregate((l, r) => l.Value > r.Value ? l : r);
			KeyValuePair<string, double> minLanguage = scores.Aggregate((l, r) => l.Value < r.Value ? l : r);
			scores.Remove(maxLanguage.Key);
			KeyValuePair<string, double> secondLanguage = scores.Aggregate((l, r) => l.Value > r.Value ? l : r);
			scores.Add(maxLanguage.Key, maxLanguage.Value);

			double scorePercentageDiff = Math.Round((maxLanguage.Value - secondLanguage.Value) / (maxLanguage.Value - minLanguage.Value) * 100, 2);

			foreach (KeyValuePair<string, double> keyValuePair in scores)
			{
				languagesAndScores += keyValuePair.Key + "\t" + keyValuePair.Value + (keyValuePair.Key == bestLanguage ? "***" : "") + "\n";
			}
			Console.WriteLine(languagesAndScores + "\nDifference between first and runner-up: " + scorePercentageDiff + "%.");
		}

		private static string ClassifyByTokenProbability(string snippet, out Dictionary<string, double> scores)
		{
			Dictionary<string, double> tokenFrequencies = BuildFrequencyTable(GetAllTokens(snippet));

			double[] rowValues = new double[_uniqueTokenList.Count];
			for (int i = 0; i < _uniqueTokenList.Count; i++)
			{
				if (tokenFrequencies.ContainsKey(_uniqueTokenList[i]))
				{
					rowValues[i] = tokenFrequencies[_uniqueTokenList[i]];
				}
				else
				{
					rowValues[i] = 0;
				}
			}
			return _bayesClassifier.Classify(rowValues, out scores);
		}

		private static string ClassifyByMatchTrees(string snippet, out Dictionary<string, double> scores)
		{
			scores = new Dictionary<string, double>();

			List<Token> tokens = GetAllTokens(snippet);
			double maxScore = 0;
			string bestMatchLanguage = null;

			foreach (MatchTree matchTree in _matchTrees)
			{
				double score = 0;
				for (int index = 0; index < tokens.Count; index++)
				{
					score += ScoreTokens(matchTree.MatchTreeRoot, tokens, index);
				}
				score = score / tokens.Count() / matchTree.TotalPossibleScore;

				Console.WriteLine(matchTree.Language + "\t" + score);
				scores.Add(matchTree.Language, score);
				if (score > maxScore)
				{
					maxScore = score;
					bestMatchLanguage = matchTree.Language;
				}
			}
			return bestMatchLanguage;
		}

		private static double ScoreTokens(TokenNode tokenNode, IList<Token> tokens, int index)
		{
			Token codeToken = tokens[index];
			TokenNode nextToken = tokenNode.NextTokens.FirstOrDefault(nt => nt.Kind == codeToken.Kind);
			if (nextToken != null)
			{
				// Token exists in match tree => points !!!
				double score = nextToken.Examples.Contains(codeToken.Value) ?
									SCORE_MULTIPLIER_FOR_EXACT_MATCH :
									SCORE_MULTIPLIER_PER_LEVEL;

				if (index < tokens.Count() - 1)
				{
					return score * ScoreTokens(nextToken, tokens, index + 1);
				}
				return score;
			}
			// Token did not exist => no points
			return 1;
		}
	}
}
