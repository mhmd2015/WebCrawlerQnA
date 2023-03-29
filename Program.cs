namespace WebCrawlerQnA
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Directory.SetCurrentDirectory(@"d:\data");
            // Define root domain to crawl
            string domain = "openai.com";
            string fullUrl = "https://openai.com/";
            
            //await WebCrawler.CrawlAsync(fullUrl);
            TextProcessor.ProcessTextFiles(domain);         
            

            // Load and process DataFrame
            TextTokenizer.TokenizeTextFile(domain);

            // Split text into chunks
            //var shortenedTexts = TextTokenizer.SplitTextIntoChunks(dataFrame);

            // Create DataFrame with shortened texts
            //var shortenedDataFrame = TextTokenizer.CreateShortenedDataFrame(shortenedTexts);

            // Visualize the distribution of the number of tokens per row using a histogram
            // In C#, you may need to use a library like MathNet.Numerics or OxyPlot to create histograms

        }
    }
}