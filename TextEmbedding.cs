using Microsoft.Data.Analysis;
using Newtonsoft.Json.Linq;
using OpenAI.GPT3.Interfaces;
using OpenAI.GPT3.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using System.Collections;
using static System.Net.Mime.MediaTypeNames;
using OpenAI.GPT3.ObjectModels.ResponseModels;
using OpenAI.GPT3.ObjectModels.RequestModels;

namespace WebCrawlerQnA
{
    public class TextEmbedding
    {
        private static double[] DistancesFromEmbeddings(IEnumerable<double> qEmbeddings, IEnumerable<IEnumerable<double>> embeddings, string distanceMetric = "cosine")
        {

            Vector<double> questionVector = Vector<double>.Build.DenseOfArray(qEmbeddings.ToArray());

            int numEmbeddings = embeddings.Count();
            double[] distances = new double[numEmbeddings];

            int index = 0;
            foreach (var embedding in embeddings)
            {
                double[] currentEmbedding = embedding.ToArray();
                Vector<double> currentVector = Vector<double>.Build.DenseOfArray(currentEmbedding);

                if (distanceMetric == "cosine")
                {
                    double cosineDistance = 1 - (questionVector * currentVector) / (questionVector.L2Norm() * currentVector.L2Norm());
                    distances[index++] = cosineDistance;
                }
                else
                {
                    throw new ArgumentException("Unsupported distance metric");
                }
            }

            return distances;
        }


        public static async Task CreateEmbeddings(IOpenAIService openAiService, DataFrame df, string domain)
        {
            var Embeddings = new List<List<double>>();
            for (long rowIndex = 0; rowIndex < df.Rows.Count; rowIndex++)
            {
                var row = df.Rows[rowIndex];
                var text = row[2].ToString();
                var response = await openAiService.Embeddings.CreateEmbedding(new OpenAI.GPT3.ObjectModels.RequestModels.EmbeddingCreateRequest()
                {
                    Input = text,
                    Model = "text-embedding-ada-002"
                });
                Embeddings.Add(response.Data[0].Embedding);
            }

            // Add the token counts to the DataFrame
            var embeddingsColumn = new StringDataFrameColumn("embeddings", Embeddings.Select(e => $"[{string.Join(",", e)}]"));
            df.Columns.Add(embeddingsColumn);
            DataFrame.SaveCsv(df, $"processed/{domain}/embeddings.csv");
        }

        public static DataFrame GetEmbeddings(string domain)
        {
            string csvFilePath = "processed/embeddings.csv";
            DataFrame df = DataFrame.LoadCsv(csvFilePath);
            
            return df;
        }

        public static async Task<string> CreateContext(IOpenAIService openAiService, string question, DataFrame df, int maxLen = 1800, string size = "ada")
        {
            var response = await openAiService.Embeddings.CreateEmbedding(new OpenAI.GPT3.ObjectModels.RequestModels.EmbeddingCreateRequest()
            {
                Input = question,
                Model = "text-embedding-ada-002"
            });

            var qEmbeddings = response.Data[0].Embedding;

            List<List<double>> embeddings = new List<List<double>>();

            foreach (var row in df.Rows)
            {
                List<double> embedding = row[3].ToString().Trim('[', ']').Split(", ").Select(double.Parse).ToList(); 
                embeddings.Add(embedding);
            }

            double[] distances = DistancesFromEmbeddings(qEmbeddings, embeddings);

            var distancesColumn = new PrimitiveDataFrameColumn<double>("distances", distances);
            df.Columns.Add(distancesColumn);

            DataFrame sortedDf = df.OrderBy("distances");

            List<string> returns = new List<string>();
            int curLen = 0;

            foreach (DataFrameRow row in sortedDf.Rows)
            {
                curLen += int.Parse(row[df.Columns.IndexOf("n_tokens")].ToString()) + 4;

                if (curLen > maxLen)
                {
                    break;
                }

                returns.Add(row[df.Columns.IndexOf("text")].ToString());
            }

            return string.Join("\n\n###\n\n", returns);
        }

        public static async Task<string> AnswerQuestion(
            IOpenAIService openAiService,        
        string domain,
        string question,
        string model = "text-davinci-003",
        int maxLen = 1800,
        string size = "ada",
        bool debug = false,
        int maxTokens = 150,
        string stopSequence = null)
        {           

            string context = await CreateContext(openAiService,question, GetEmbeddings(domain), maxLen, size);

            if (debug)
            {
                Console.WriteLine("Context:\n" + context);
                Console.WriteLine("\n\n");
            }

            try
            {
                var prompt = $"Answer the question based on the context below, and if the question can't be answered based on the context, say \"I don't know\"\n\nContext: {context}\n\n---\n\nQuestion: {question}\nAnswer:";
                var completionRequest = new CompletionCreateRequest()
                {
                    Prompt = prompt,
                    Temperature = 0,
                    MaxTokens = maxTokens,
                    TopP = 1,
                    FrequencyPenalty = 0,
                    PresencePenalty = 0,                    
                    StopAsList = stopSequence != null ? new[] { stopSequence } : null,
                    Model = model
                };

                var completionResponse = await openAiService.Completions.CreateCompletion(completionRequest);
                return completionResponse.Choices[0].Text.Trim();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return "";
            }
        }

    }
}
