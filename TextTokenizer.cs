using Microsoft.Data.Analysis;
using Microsoft.ML.Data;
using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawlerQnA
{
    public class TextTokenizer
    {
        //Step 6
        public static DataFrame LoadAndProcessDataFrame(string filePath)
        {
            // Load the CSV file
            var df = DataFrame.LoadCsv(filePath);

            // Rename columns
            df.Columns[0].SetName("title");
            df.Columns[1].SetName("text");

            // Initialize tokenizer
            var mlContext = new MLContext();
            var tokenizerPipeline = mlContext.Transforms.Text.TokenizeIntoWords("tokens", "text", separators: new[] { ' ', '\t', '\n', '\r', ',', '.', ';', ':', '(', ')', '[', ']', '{', '}', '\"', '\'', '<', '>', '/', '\\', '|', '@', '&', '*', '%', '+', '-', '=', '!', '?', '`', '~', '#' });
            var tokenizerTransformer = tokenizerPipeline.Fit(df);

            // Tokenize the text and save the number of tokens to a new column
            var tokenizedDataFrame = tokenizerTransformer.Transform(df).ToDataFrame();

            // Count the number of tokens per row
            var tokenCounts = Enumerable.Cast<VBuffer<ReadOnlyMemory<char>>>(tokenizedDataFrame["tokens"]).Select(tokens => ((VBuffer<ReadOnlyMemory<char>>)tokens).Length).ToArray();

            // Add the token counts to the DataFrame
            var tokenCountsColumn = new PrimitiveDataFrameColumn<int>("n_tokens", tokenCounts);
            tokenizedDataFrame.Columns.Add(tokenCountsColumn);

            //tokenizedDataFrame.Columns.Add(new Int32DataFrameColumn("n_tokens", tokenizedDataFrame.Rows.Count, i => ((VBuffer<ReadOnlyMemory<char>>)tokenizedDataFrame.Rows[i]["tokens"]).Length));

            return tokenizedDataFrame;
        }

        // Function to split the text into chunks of a maximum number of tokens
        public static List<string> SplitIntoMany(string text, int maxTokens, ITransformer tokenizerTransformer)
        {
            // Split the text into sentences
            var sentences = text.Split(". ");

            // Get the number of tokens for each sentence
            var tokenCounts = sentences.Select(sentence => tokenizerTransformer.Transform(new DataFrame(new StringDataFrameColumn("text", new[] { " " + sentence }))).ToDataFrame().Columns["tokens"][0]).Select(tokens => ((VBuffer<ReadOnlyMemory<char>>)tokens).Length).ToArray();

            var chunks = new List<string>();
            int tokensSoFar = 0;
            var chunk = new List<string>();

            // Loop through the sentences and tokens joined together in a tuple
            for (int i = 0; i < sentences.Length; i++)
            {
                var sentence = sentences[i];
                var token = tokenCounts[i];

                // If the number of tokens so far plus the number of tokens in the current sentence is greater 
                // than the max number of tokens, then add the chunk to the list of chunks and reset
                // the chunk and tokens so far
                if (tokensSoFar + token > maxTokens)
                {
                    chunks.Add(string.Join(". ", chunk) + ".");
                    chunk = new List<string>();
                    tokensSoFar = 0;
                }

                // If the number of tokens in the current sentence is greater than the max number of 
                // tokens, go to the next sentence
                if (token > maxTokens)
                {
                    continue;
                }

                // Otherwise, add the sentence to the chunk and add the number of tokens to the total
                chunk.Add(sentence);
                tokensSoFar += token + 1;
            }

            // Add the last chunk to the list of chunks
            if (chunk.Count > 0)
            {
                chunks.Add(string.Join(". ", chunk) + ".");
            }

            return chunks;
        }

        public static List<string> SplitTextIntoChunks(DataFrame dataFrame, ITransformer tokenizerTransformer)
        {
            int maxTokens = 500;           
            

            var shortened = new List<string>();

            // Loop through the DataFrame
            for (long rowIndex = 0; rowIndex < dataFrame.Rows.Count; rowIndex++)
            {
                var row = dataFrame.Rows[rowIndex];
                var text = row[1].ToString();

                // If the text is null, go to the next row
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                // If the number of tokens is greater than the max number of tokens, split the text into chunks
                int nTokens = (int)row[2];
                if (nTokens > maxTokens)
                {
                    shortened.AddRange(SplitIntoMany(text, maxTokens, tokenizerTransformer));
                }
                // Otherwise, add the text to the list of shortened texts
                else
                {
                    shortened.Add(text);
                }
            }

            return shortened;
        }

        public static DataFrame CreateShortenedDataFrame(List<string> shortenedTexts, ITransformer tokenizerTransformer)
        {
            // Create a DataFrame with the shortened texts
            var df = new DataFrame(new StringDataFrameColumn("text", shortenedTexts));

            // Tokenize the text and save the number of tokens to a new column
            var tokenizedDataFrame = tokenizerTransformer.Transform(df).ToDataFrame();           
            tokenizedDataFrame.Columns.Add(new Int32DataFrameColumn("n_tokens", tokenizedDataFrame["tokens"].Cast<object>().ToList().Select(x => ((VBuffer<ReadOnlyMemory<char>>)x).Length)));


            return tokenizedDataFrame;
        }
    }
}
//https://towardsdatascience.com/getting-started-with-c-dataframe-and-xplot-ploty-6ea6ce0ce8e3