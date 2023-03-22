using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Data;


namespace WebCrawlerQnA
{
    public class TextProcessor
    {
        // Step 5
        public static string RemoveNewlines(string input)
        {
            string result = Regex.Replace(input, @"\n", " ");
            result = Regex.Replace(result, @"\\n", " ");
            result = Regex.Replace(result, "  ", " ");
            result = Regex.Replace(result, "  ", " ");
            return result;
        }

        // Step 6
        public static void ProcessTextFiles(string domain)
        {
            string textDirectoryPath = $"text/{domain}";
            List<Tuple<string, string>> texts = new List<Tuple<string, string>>();

            foreach (var file in Directory.GetFiles(textDirectoryPath))
            {
                using (StreamReader reader = new StreamReader(file, System.Text.Encoding.UTF8))
                {
                    string text = reader.ReadToEnd();
                    string fileName = Path.GetFileName(file);
                    string formattedName = fileName.Substring(11, fileName.Length - 15).Replace('-', ' ').Replace('_', ' ').Replace("#update", "");
                    texts.Add(new Tuple<string, string>(formattedName, text));
                }
            }

            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("fname", typeof(string));
            dataTable.Columns.Add("text", typeof(string));

            foreach (var item in texts)
            {
                DataRow row = dataTable.NewRow();
                row["fname"] = item.Item1;
                row["text"] = $"{item.Item1}. {RemoveNewlines(item.Item2)}";
                dataTable.Rows.Add(row);
            }

            dataTable.WriteCsv("processed/scraped.csv");
        }
    }

    public static class DataTableExtensions
    {
        public static void WriteCsv(this DataTable table, string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                IEnumerable<string> columnNames = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName);
                writer.WriteLine(string.Join(",", columnNames));

                foreach (DataRow row in table.Rows)
                {
                    IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString());
                    writer.WriteLine(string.Join(",", fields));
                }
            }
        }
    }
}
