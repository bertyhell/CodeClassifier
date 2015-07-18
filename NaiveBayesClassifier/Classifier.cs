using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;

namespace ProbabilityFunctions
{
	public class Classifier
	{
		private DataSet dataSet = new DataSet();

		public DataSet DataSet
		{
			get { return dataSet; }
			set { dataSet = value; }
		}

		public void TrainClassifier(DataTable table)
		{
			dataSet.Tables.Add(table);

			//table
			DataTable GaussianDistribution = dataSet.Tables.Add("Gaussian");
			GaussianDistribution.Columns.Add(table.Columns[0].ColumnName);

			//columns
			int percentage = 0;
			for (int i = 1; i < table.Columns.Count; i++)
			{
				int newPercentage = i * 100 / table.Columns.Count;
				if (newPercentage != percentage)
				{
					Console.WriteLine("handling column: " + table.Columns[i].ColumnName + "\t" + i + " / " + table.Columns.Count);
					percentage = newPercentage;
				}
				GaussianDistribution.Columns.Add(table.Columns[i].ColumnName + "Mean");
				GaussianDistribution.Columns.Add(table.Columns[i].ColumnName + "Variance");
			}

			//calc data
			var results = (from myRow in table.AsEnumerable()
						   group myRow by myRow.Field<string>(table.Columns[0].ColumnName) into g
						   select new { Name = g.Key, Count = g.Count() }).ToList();

			for (int j = 0; j < results.Count; j++)
			{
				Console.WriteLine("handling: " + results[j].Name + "\t" + (j * 100 / results.Count));
				DataRow row = GaussianDistribution.Rows.Add();
				row[0] = results[j].Name;

				int a = 1;
				percentage = 0;
				for (int i = 1; i < table.Columns.Count; i++)
				{
					int newPercentage = i * 100 / table.Columns.Count;
					if (newPercentage != percentage)
					{
						percentage = newPercentage;
					}
					row[a] = Helper.Mean(SelectRows(table, i, string.Format("{0} = '{1}'", table.Columns[0].ColumnName, results[j].Name)));
					row[++a] = Helper.Variance(SelectRows(table, i, string.Format("{0} = '{1}'", table.Columns[0].ColumnName, results[j].Name)));
					a++;
				}
			}
		}

		public string Classify(double[] obj, out Dictionary<string, double> scores)
		{
			scores = new Dictionary<string, double>();

			var results = (from myRow in dataSet.Tables[0].AsEnumerable()
						   group myRow by myRow.Field<string>(dataSet.Tables[0].Columns[0].ColumnName) into g
						   select new { Name = g.Key, Count = g.Count() }).ToList();

			for (int i = 0; i < results.Count; i++)
			{
				List<double> subScoreList = new List<double>();
				int a = 1, b = 1;
				for (int k = 1; k < dataSet.Tables["Gaussian"].Columns.Count; k = k + 2)
				{
					double mean = Convert.ToDouble(dataSet.Tables["Gaussian"].Rows[i][a]);
					double variance = Convert.ToDouble(dataSet.Tables["Gaussian"].Rows[i][++a]);
					double result = Helper.NormalDist(obj[b - 1], mean, Helper.SquareRoot(variance));
					if (!Double.IsNaN(result) && !Double.IsInfinity(result) && result != 0)
					{
						// if number add to scores
						subScoreList.Add(result);
					}
					a++; b++;
				}

				double finalScore = subScoreList[0];
				for (int k = 1; k < subScoreList.Count; k++)
				{
					finalScore = finalScore * subScoreList[k];
					if (finalScore == 0)
					{
						break; // No point in going further
					}
				}

				scores.Add(results[i].Name, finalScore * 0.5);
			}

			double maxOne = scores.Max(c => c.Value);
			var name = (from c in scores
						where c.Value == maxOne
						select c.Key).First();

			return name;
		}

		#region Helper Function

		public IEnumerable<double> SelectRows(DataTable table, int column, string filter)
		{
			List<double> doubleList = new List<double>();

			DataRow[] rows;
			//try
			//{
			rows = table.Select(filter);
			//}
			//catch (Exception ex)
			//{
			//	Console.WriteLine("filter: |" + filter + "|");
			//	rows = new DataRow[0];
			//}
			foreach (DataRow row in rows)
			{
				doubleList.Add(Convert.ToDouble(row[column]));
			}

			return doubleList;
		}

		public void Clear()
		{
			dataSet = new DataSet();
		}

		#endregion
	}
}
