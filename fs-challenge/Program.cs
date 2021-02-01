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
            if (args.Length == 0)
            {
                Console.WriteLine("Error: missing path to database");
                return;
            }
            string connectionString = string.Format("URI=file:{0};", args[0]);

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
            const string sql = "SELECT test_uid FROM Tests";
            rcomm.CommandText = sql;
            SqliteDataReader reader = rcomm.ExecuteReader();

            StringBuilder csvResult = new StringBuilder();
            csvResult.AppendLine("Average Roughness\tRMS Roughness\tMinimum Height\tMinimum Height Location\tMaximum Height\tMaximum Height Location\tHeight Range\tNo. of Included Measurements\tNo. of Excluded Measurements");

            while (reader.Read())
            {
                int testID = reader.GetInt32(0);
                List<Measurement> testMeasurements = QueryMeasurements(testID, conn);

                if (testMeasurements == null)
                {
                    csvResult.AppendLine(string.Format("Invalid Test (ID: {0})", testID));
                    continue;
                }

                csvResult.AppendLine(SummarizeMeasurements(testMeasurements, filterSize));
            }

            File.WriteAllText("test_summaries.csv", csvResult.ToString());

            reader.Close();
            rcomm.Dispose();
            conn.Dispose();
        }

        static List<Measurement> QueryMeasurements(int testID, SqliteConnection conn)
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

        static bool IsValidTest(int testID, SqliteConnection conn)
        {
            string validTestSQL = string.Format("SELECT Count(measurement_uid) FROM Measurements WHERE test_uid = {0}", testID);
            SqliteCommand isValidComm = conn.CreateCommand();
            isValidComm.CommandText = validTestSQL;
            isValidComm.CommandType = System.Data.CommandType.Text;

            int measurementCount = 0;
            measurementCount = Convert.ToInt32(isValidComm.ExecuteScalar());

            return (measurementCount == 1000);
        }

        static string SummarizeMeasurements(List<Measurement> measurements, int filterSize)
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
            foreach (Measurement m in measurements)
            {
                R_q += (m.height - mu) * (m.height - mu);
            }
            R_q = Math.Sqrt(R_q / 1000);

            double R_a = 0;
            int numFiltered = 0;
            foreach (Measurement m in measurements)
            {
                if (Math.Abs(m.height - mu) > (filterSize * R_q))
                {
                    numFiltered++;
                    continue;
                }

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

            return string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}",
                R_a, R_q, minHeight, minHeightLocation, maxHeight, maxHeightLocation, maxHeight - minHeight, 1000 - numFiltered, numFiltered);
        }
    }
}
