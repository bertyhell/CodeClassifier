using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Test.Annotations;

namespace Test
{
	public partial class MainWindow : INotifyPropertyChanged
	{
		private string _inputString;
		private string _outputString;

		public MainWindow()
		{
			InitializeComponent();
			DataContext = this;
		}

		public string InputString
		{
			get { return _inputString; }
			set
			{
				if (value == _inputString) return;
				_inputString = value;
				OnPropertyChanged();
			}
		}

		public string OutputString
		{
			get { return _outputString; }
			set
			{
				if (value == _outputString) return;
				_outputString = value;
				OnPropertyChanged();
			}
		}

		private void ButtonClick(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(InputString))
			{
				MessageBox.Show("Please enter a code snippet before continuing.");
				return;
			}

		    double certainty;
            Dictionary<string, double> scores;
			string bestLanguage = CodeClassifier.CodeClassifier.Classify(InputString, out certainty, out scores);
			string languagesAndScores = "";

			KeyValuePair<string, double> maxLanguage = scores.Aggregate((l, r) => l.Value > r.Value ? l : r);
			KeyValuePair<string, double> minLanguage = scores.Aggregate((l, r) => l.Value < r.Value ? l : r);
			scores.Remove(maxLanguage.Key);
			KeyValuePair<string, double> secondLanguage = scores.Aggregate((l, r) => l.Value > r.Value ? l : r);
			scores.Add(maxLanguage.Key, maxLanguage.Value);

			double scorePercentageDiff = Math.Round((maxLanguage.Value - secondLanguage.Value) / (maxLanguage.Value - minLanguage.Value) * 100, 2);

			foreach (KeyValuePair<string, double> keyValuePair in scores)
			{
				languagesAndScores += keyValuePair.Key + "\t" + keyValuePair.Value + (keyValuePair.Key == bestLanguage ? " certainty: " + Math.Round(certainty*100, 0) : "") + "\n";
			}
			OutputString = languagesAndScores + "\nDifference between first and runner-up: " + scorePercentageDiff + "%.";
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChangedEventHandler handler = PropertyChanged;
		    handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
