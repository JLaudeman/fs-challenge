using System;
using Mono.Data.Sqlite;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace fschallenge
{
    struct Measurement
    {
        public double x, y, height; 

        public Measurement(double x, double y, double height)
        {
            this.x = x;
            this.y = y;
            this.height = height;
        }
    }

    class MainClass
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Enter the path to .db3");
            string dbPath = Console.ReadLine();
            string connectionString = string.Format("URI=file:{0};", dbPath);

            int filterSize = 3;
            Console.WriteLine("Type a new filter size, or press enter to use the default");
            while (true)
            {
                string newSize = Console.ReadLine();
                if (newSize.Equals(""))
                {
                    Console.WriteLine("Using default filter size of 3 standard deviations");
                    break;
                }
                else if (Int32.TryParse(newSize, out filterSize))
                {
                    Console.WriteLine(string.Format("Filter set to {0} standard deviations", filterSize));
                    break;
                }
                else
                {
                    Console.WriteLine("Please type a number or press enter");
                    continue;
                }
            }

            SqliteConnection conn = new SqliteConnection(connectionString);
            conn.Open();
            SqliteCommand rcomm = conn.CreateCommand();
            const string sql = "SELECT test_uid, sTime, PlaneID, Operator FROM Tests";
            rcomm.CommandText = sql;
            SqliteDataReader reader = rcomm.ExecuteReader();

            StringBuilder unfilteredResult = new StringBuilder();
            unfilteredResult.AppendLine("Test ID,Time,Plane ID,Operator,Average Roughness,RMS Roughness,Minimum Height,Minimum Height Location," +
            	                        "Maximum Height,Maximum Height Location,Height Range");
            StringBuilder filteredResult = new StringBuilder();
            filteredResult.AppendLine("Test ID,Time,Plane ID,Operator,Average Roughness,RMS Roughness,Minimum Height,Minimum Height Location," +
            	                      "Maximum Height,Maximum Height Location,Height Range,No. of Filtered Measurements,No. of Unfiltered Measurements");

            while (reader.Read())
            {
                long testID = reader.GetInt64(0);
                List<Measurement> testMeasurements = QueryMeasurements(testID, conn);

                if (testMeasurements == null)
                {
                    unfilteredResult.AppendLine(string.Format("Invalid Test (ID: {0})", testID));
                    filteredResult.AppendLine(string.Format("Invalid Test (ID: {0})", testID));
                    continue;
                }

                string operatorString;
                try
                {
                    operatorString = reader.GetString(3);
                }
                catch
                {
                    operatorString = "N/A";
                }

                string testInfo = string.Format("{0},{1},{2},{3},", testID, reader.GetDateTime(1), reader.GetString(2), operatorString);
                unfilteredResult.Append(testInfo);
                filteredResult.Append(testInfo);

                unfilteredResult.AppendLine(UnfilteredTestSummary(testMeasurements));
                filteredResult.AppendLine(FilteredTestSummary(testMeasurements, filterSize));
            }

            File.WriteAllText("unfiltered_test_summaries.csv", unfilteredResult.ToString());
            File.WriteAllText("filtered_test_summaries.csv", filteredResult.ToString());

            reader.Close();
            rcomm.Dispose();
            conn.Dispose();

            Console.ReadLine();
        }

        static List<Measurement> QueryMeasurements(long testID, SqliteConnection conn)
        {
            if (IsValidTest(testID, conn) != true)
            {
                return null;
            }

            List<Measurement> measurements = new List<Measurement>();
            SqliteCommand rcomm = conn.CreateCommand();
            string getMeasurementsSQL = string.Format("SELECT x, y, height FROM Measurements WHERE test_uid = {0}", testID);
            rcomm.CommandText = getMeasurementsSQL;
            SqliteDataReader reader = rcomm.ExecuteReader();

            while (reader.Read())
            {
                double x = reader.GetDouble(0);
                double y = reader.GetDouble(1);
                double height = reader.GetDouble(2);

                measurements.Add(new Measurement(x, y, height));
            }

            return measurements;
        }

        static bool IsValidTest(long testID, SqliteConnection conn)
        {
            string validTestSQL = string.Format("SELECT Count(measurement_uid) FROM Measurements WHERE test_uid = {0}", testID);
            SqliteCommand isValidComm = conn.CreateCommand();
            isValidComm.CommandText = validTestSQL;
            isValidComm.CommandType = System.Data.CommandType.Text;

            int measurementCount = 0;
            measurementCount = Convert.ToInt32(isValidComm.ExecuteScalar());

            return (measurementCount == 1000);
        }

        static string UnfilteredTestSummary(List<Measurement> measurements)
        {
            double mu = 0;
            double minHeight = measurements[0].height;
            double maxHeight = measurements[0].height;
            (double, double) minHeightLocation = (measurements[0].x, measurements[0].y);
            (double, double) maxHeightLocation = (measurements[0].x, measurements[0].y);

            foreach (Measurement m in measurements)
            {
                mu += m.height;
            }
            mu = mu / 1000;

            double R_q = 0;
            double R_a = 0;
            foreach (Measurement m in measurements)
            {
                R_q += Math.Pow(m.height - mu, 2);
                R_a += Math.Abs(m.height - mu);

                if (m.height > maxHeight)
                {
                    maxHeight = m.height;
                    maxHeightLocation = (m.x, m.y);
                }
                else if (m.height < minHeight)
                {
                    minHeight = m.height;
                    minHeightLocation = (m.x, m.y);
                }
            }
            R_q = Math.Sqrt(R_q / 1000);
            R_a = R_a / 1000;

            return string.Format("{0},{1},{2},\"{3}\",{4},\"{5}\",{6}",
                R_a, R_q, minHeight, minHeightLocation, maxHeight, maxHeightLocation, maxHeight - minHeight);
        }

        static string FilteredTestSummary(List<Measurement> measurements, int filterSize)
        {
            double mu = 0;
            double minHeight = measurements[0].height;
            double maxHeight = measurements[0].height;
            (double, double) minHeightLocation = (measurements[0].x, measurements[0].y);
            (double, double) maxHeightLocation = (measurements[0].x, measurements[0].y);

            foreach (Measurement m in measurements)
            {
                mu += m.height;
            }
            mu = mu / 1000;

            double stdDeviation = 0;
            foreach (Measurement m in measurements)
            {
                stdDeviation += (m.height - mu) * (m.height - mu);
            }
            stdDeviation = Math.Sqrt(stdDeviation / 1000);

            double R_a = 0;
            double R_q = 0;
            int numFiltered = 0;
            foreach (Measurement m in measurements)
            {
                if (Math.Abs(m.height - mu) > (filterSize * stdDeviation))
                {
                    numFiltered++;
                    continue;
                }
                R_q += Math.Pow(m.height - mu, 2);
                R_a += Math.Abs(m.height - mu);

                if (m.height > maxHeight)
                {
                    maxHeight = m.height;
                    maxHeightLocation = (m.x, m.y);
                }
                else if (m.height < minHeight)
                {
                    minHeight = m.height;
                    minHeightLocation = (m.x, m.y);
                }
            }
            R_a = R_a / (1000 - numFiltered);
            R_q = Math.Sqrt(R_q / (1000 - numFiltered));

            return string.Format("{0},{1},{2},\"{3}\",{4},\"{5}\",{6},{7},{8}",
                R_a, R_q, minHeight, minHeightLocation, maxHeight, maxHeightLocation, maxHeight - minHeight, numFiltered, 1000 - numFiltered);
        }
    }
}
