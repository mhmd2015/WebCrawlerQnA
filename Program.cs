namespace WebCrawlerQnA
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Define root domain to crawl
            string domain = "openai.com";
            string fullUrl = "https://openai.com/";

            var crawler = new WebCrawler();
            List<string> hyperlinks = await crawler.GetHyperlinksAsync("https://openai.com/");


            Console.WriteLine("Hello, World!");
        }
    }
}