using System;
using System.Collections.Generic;
using System.Linq;

namespace ProbabilityFunctions
{
    public static class Helper
    {
        public static double Variance(this IEnumerable<double> source)
        {
	        IEnumerable<double> sourceEnumerable = source as double[] ?? source.ToArray();
	        if (sourceEnumerable.Any())
	        {
		        double avg = sourceEnumerable.Average();
		        double d = sourceEnumerable.Aggregate(0.0, (total, next) => Math.Pow(next - avg, 2));
		        return d/(sourceEnumerable.Count() - 1);
	        }
	        return 0;
        }

	    public static double Mean(this IEnumerable<double> source)
        {
		    IEnumerable<double> sourceEnumarable = source as double[] ?? source.ToArray();
		    if (!sourceEnumarable.Any())
		    {
			    return 0.0;
		    }

		    double length = sourceEnumarable.Count();
            double sum = sourceEnumarable.Sum();
            return sum / length;
        }

        public static double NormalDist(double x, double mean, double standard_dev)
        {
            double fact = standard_dev * Math.Sqrt(2.0 * Math.PI);
            double expo = (x - mean) * (x - mean) / (2.0 * standard_dev * standard_dev);
            return Math.Exp(-expo) / fact;
        }

        public static double NORMDIST(double x, double mean, double standard_dev, bool cumulative)
        {
            const double parts = 50000.0; //large enough to make the trapzoids small enough

            double lowBound = 0.0;
            if (cumulative) //do integration: trapezoidal rule used here
            {
                double width = (x - lowBound) / (parts - 1.0);
                double integral = 0.0;
                for (int i = 1; i < parts - 1; i++)
                {
                    integral += 0.5 * width * (NormalDist(lowBound + width * i, mean, standard_dev) +
                        (NormalDist(lowBound + width * (i + 1), mean, standard_dev)));
                }
                return integral;
            }
            else //return function value
            {
                return NormalDist(x, mean, standard_dev);
            }
        }

        public static double SquareRoot(double source)
        {
            return Math.Sqrt(source);
        }
    }
}
