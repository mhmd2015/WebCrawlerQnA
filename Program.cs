using Microsoft.Data.Analysis;
using Microsoft.Extensions.Configuration;
using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels.ResponseModels;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using XPlot.Plotly;

namespace WebCrawlerQnA
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile(AppDomain.CurrentDomain.BaseDirectory + "\\appsettings.json", optional: true, reloadOnChange: true)
               .Build();

            var apiKey = config.GetSection("apiKey").Get<string>();

            var openAiService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = apiKey
            });            


            var buildCommand = new Command("build", "build the QnA")
            {
                new Argument<string>("domain", "root domain to crawl"),
                new Option<string>(new string[]{"-p","--path" }, ()=>@"d:\data\", "data path")
            };


            var askCommand = new Command("ask", "ask questions")
            {
                new Argument<string>("domain", "root domain to ask"),
                new Option<string>(new []{"-q","--question" },"question"),
                new Option<string>(new string[]{"-p","--path" }, ()=>@"d:\data\", "data path")
            };


            buildCommand.Handler = CommandHandler.Create<string, string>(async (string domain, string path) =>
            {
                Directory.SetCurrentDirectory(path);
                await WebCrawler.CrawlAsync(domain);
                TextProcessor.ProcessTextFiles(domain);
                var df = TextTokenizer.TokenizeTextFile(domain);
                await TextEmbedding.CreateEmbeddings(openAiService, df, domain);
            });

            askCommand.Handler = CommandHandler.Create<string,string,string>(async (string domain, string path, string question) =>
            {
                Directory.SetCurrentDirectory(path);
                await TextEmbedding.AnswerQuestion(openAiService, domain, question);
            });

            var rootCommand = new RootCommand("A simple Q&A CLI application.")
            {
                buildCommand,
                askCommand
            };

            var builder = new CommandLineBuilder(rootCommand);
            builder.UseDefaults();
            builder.UseHelp();
            builder.UseVersionOption();
            var parser = builder.Build();

            await parser.InvokeAsync(args);
        }
    }
}