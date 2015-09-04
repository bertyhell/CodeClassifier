using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Xml.Schema;
using CodeClassifier.StringTokenizer;

namespace CodeClassifier
{
    public class CodeClassifier
    {
        private static CodeClassifier _instance;

        private const double SCORE_MULTIPLIER_PER_LEVEL = 2;
        private const double SCORE_MULTIPLIER_FOR_EXACT_MATCH = 5;

        private const double MIN_TOKEN_FREQ_PER_FILE = 2;

        private const double FREQ_SCORE_MULTIPLIER = 20;
        
        private static List<MatchTree> _matchTrees;
        private static HashSet<string> _uniqueTokenSet;
        private static Dictionary<string, Dictionary<string, double>> _tokenFreqPerLanguage;

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

            _tokenFreqPerLanguage = new Dictionary<string, Dictionary<string, double>>();

            string[] folders = Directory.GetDirectories(path);
            foreach (string languageFolder in folders)
            {
                string[] files = Directory.GetFiles(languageFolder);
                TokenNode rootNode = null;
                double totalPossibleScore = 0;
                string languageName = Path.GetFileNameWithoutExtension(languageFolder) ?? "undefined";

                Dictionary<string, double> tokenFreq = new Dictionary<string, double>();

                string allFilesContent = "";
                foreach (string filePath in files)
                {
                    allFilesContent += File.ReadAllText(filePath);
                }
                List<Token> tokens = GetAllTokens(allFilesContent);

                // Verhelst algo
                // Calculate the total possible score to normalize the score results
                rootNode = BuildMatchTree(tokens, out totalPossibleScore);

                // Frequency algorithm
                foreach (KeyValuePair<string, double> keyValuePair in BuildFrequencyTable(tokens))
                {
                    // Sumize all frequencies for files of the same language
                    if (!tokenFreq.ContainsKey(keyValuePair.Key))
                    {
                        tokenFreq[keyValuePair.Key] = 0;
                    }
                    tokenFreq[keyValuePair.Key] += keyValuePair.Value;
                }


                _matchTrees.Add(new MatchTree(rootNode, languageName, totalPossibleScore));

                _tokenFreqPerLanguage.Add(languageName, tokenFreq);
            }
        }

        private static Dictionary<string, double> BuildFrequencyTable(List<Token> tokens)
        {
            Dictionary<string, double> tokenFrequencies = new Dictionary<string, double>();
            // Count token frequencies
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

            // Limit number of results
            Dictionary<string, double> filteredTokenFrequencies = new Dictionary<string, double>();
            foreach (string tokenString in tokenFrequencies.Keys)
            {
                if (tokenFrequencies[tokenString] >= MIN_TOKEN_FREQ_PER_FILE)
                {
                    _uniqueTokenSet.Add(tokenString);
                    filteredTokenFrequencies.Add(tokenString, tokenFrequencies[tokenString]);
                }
            }

            // Normalize frequencies [0-1]
            double maxTokenFreq = filteredTokenFrequencies.Values.Max();
            List<string> keys = filteredTokenFrequencies.Keys.ToList();
            foreach (string key in keys)
            {
                filteredTokenFrequencies[key] /= maxTokenFreq;
            }
            return filteredTokenFrequencies;
        }

        private static TokenNode BuildMatchTree(IList<Token> tokens, out double totalScorePossible)
        {
            // Recursivly build the tree
            TokenNode root = new TokenNode(TokenKind.Unknown, 0, 1, null);

            double totalScore = 0;
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
            double centainty;
            return Classify(snippet, out centainty);
        }

        public static string Classify(string snippet, out double certainty)
        {
            // ReSharper disable once RedundantAssignment
            Dictionary<string, double> scores;
            double cert;
            string bestLanguage = Classify(snippet, out cert, out scores);
            certainty = cert;
            return bestLanguage;
        }

        public static string Classify(string snippet, out double certainty, out Dictionary<string, double> scores)
        {
            if (_instance == null)
            {
                _instance = new CodeClassifier();
            }

            Dictionary<string, double> scoresTp;
            string bestLanguageTp = ClassifyByTokenProbability(snippet, out scoresTp);
            Console.WriteLine("\n\nToken frequencies: ");
            OutputLanguageScores(scoresTp, bestLanguageTp);
            double certTp = CalculateCertainty(scoresTp);

            Dictionary<string, double> scoresMt;
            string bestLanguageMt = ClassifyByMatchTrees(snippet, out scoresMt);
            Console.WriteLine("\n\nMatch trees: ");
            OutputLanguageScores(scoresMt, bestLanguageMt);
            double certMt = CalculateCertainty(scoresMt);

            scores = new Dictionary<string, double>();
            foreach (string language in scoresTp.Keys.ToList())
            {
                scores[language] = scoresTp[language]*certTp + scoresMt[language]*certMt;
            }


            certainty = CalculateCertainty(scores);
            return CalculateWinningLanguage(scores);

            //if (certTp > certMt)
            //{
            //    scores = scoresTp;
            //    certainty = certTp;
            //    return bestLanguageTp;
            //}
            //else
            //{
            //    scores = scoresMt;
            //    certainty = certMt;
            //    return bestLanguageMt;
            //}
        }

        private static double CalculateCertainty(Dictionary<string, double> scores)
        {
            double maxScore = 0;
            double runnerUpScore = 0;
            foreach (KeyValuePair<string, double> keyValuePair in scores)
            {
                if (keyValuePair.Value > maxScore)
                {
                    runnerUpScore = maxScore;
                    maxScore = keyValuePair.Value;
                }
            }
            return (maxScore - runnerUpScore) / maxScore;
        }

        private static string CalculateWinningLanguage(Dictionary<string, double> scores)
        {
            double maxScore = 0;
            string bestMatchLanguage = "";
            foreach (KeyValuePair<string, double> keyValuePair in scores)
            {
                if (keyValuePair.Value > maxScore)
                {
                    bestMatchLanguage = keyValuePair.Key;
                    maxScore = keyValuePair.Value;
                }
            }
            return bestMatchLanguage;
        }

        private static void OutputLanguageScores(IDictionary<string, double> scores, string bestLanguage)
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
            Dictionary<string, double> snippletTokenFreqs = BuildFrequencyTable(GetAllTokens(snippet));
            scores = new Dictionary<string, double>();
            
            double maxScore = 0;
            string bestMatchLanguage = "";
            foreach (string language in _tokenFreqPerLanguage.Keys)
            {
                double languageScore = 0;
                // Score frequency tables by differance to snipplet freq table
                foreach (KeyValuePair<string, double> tokenFreqPair in _tokenFreqPerLanguage[language])
                {
                    double snippletFreq = snippletTokenFreqs.ContainsKey(tokenFreqPair.Key) ? snippletTokenFreqs[tokenFreqPair.Key] : 0;
                    double trainingFreq = tokenFreqPair.Value;

                    if (trainingFreq == 0 && snippletFreq == 0)
                    {
                        languageScore += 1;
                    } else if (trainingFreq != 0 && snippletFreq != 0)
                    {
                        languageScore += (1 - Math.Abs(trainingFreq - snippletFreq))*FREQ_SCORE_MULTIPLIER;
                    }
                }
                scores.Add(language, languageScore);

                if (languageScore > maxScore)
                {
                    maxScore = languageScore;
                    bestMatchLanguage = language;
                }
            }
            // Normalize scores [0-1]
            foreach (string language in scores.Keys.ToList())
            {
                scores[language] /= maxScore;
            }

            return bestMatchLanguage;
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
