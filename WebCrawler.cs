﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace WebCrawlerQnA
{
    public class WebCrawler
    {
        // Define a regex pattern to match a URL
        private static readonly Regex HttpUrlPattern = new(@"^http[s]*://.+", RegexOptions.Compiled);

        // Function to get the hyperlinks from a URL
        public async Task<List<string>> GetHyperlinksAsync(string url)
        {
            try
            {
                using var httpClient = new HttpClient();
                using var httpResponse = await httpClient.GetAsync(url);

                if (httpResponse.Content?.Headers?.ContentType?.MediaType?.StartsWith("text/html") == false)
                {
                    return new List<string>();
                }

                using var contentStream = await httpResponse!.Content!.ReadAsStreamAsync();
                using var reader = new StreamReader(contentStream);
                var html = await reader.ReadToEndAsync();               

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var hyperlinks = new List<string>();

                foreach (var linkNode in doc.DocumentNode.SelectNodes("//a[@href]"))
                {
                    var href = linkNode.GetAttributeValue("href", string.Empty);
                    if (HttpUrlPattern.IsMatch(href))
                    {
                        hyperlinks.Add(href);
                    }
                }

                return hyperlinks;
            }
            catch (Exception e) 
            {
                Console.WriteLine(e);
                return new List<string>();
            }
        }

        // Function to get the hyperlinks from a URL that are within the same domain
        public async Task<List<string>> GetDomainHyperlinks(string localDomain, string url)
        {
            var cleanLinks = new HashSet<string>();
            var rawLinks = await GetHyperlinksAsync(url);

            foreach (var link in rawLinks.Distinct())
            {
                string cleanLink = null;

                // If the link is a URL, check if it is within the same domain
                if (HttpUrlPattern.IsMatch(link))
                {
                    // Parse the URL and check if the domain is the same
                    var urlObj = new Uri(link);
                    if (urlObj.Host == localDomain)
                    {
                        cleanLink = link;
                    }
                }
                // If the link is not a URL, check if it is a relative link
                else
                {
                    if (link.StartsWith("/"))
                    {                        
                        cleanLink = $"https://{localDomain}/{link.Substring(1)}";
                    }
                    else if (link.StartsWith("#") || link.StartsWith("mailto:"))
                    {
                        continue;
                    }                    
                }

                if (cleanLink != null)
                {
                    if (cleanLink.EndsWith("/"))
                    {
                        cleanLink = cleanLink.Remove(cleanLink.Length - 1);
                    }
                    cleanLinks.Add(cleanLink);
                }
            }

            // Return the list of hyperlinks that are within the same domain
            return cleanLinks.ToList();
        }

        public async Task CrawlAsync(string url)
        {
            // Parse the URL and get the domain
            var localDomain = new Uri(url).Host;

            // Create a queue to store the URLs to crawl
            var queue = new Queue<string>();
            queue.Enqueue(url);

            // Create a set to store the URLs that have already been seen (no duplicates)
            var seen = new HashSet<string> { url };

            // Create a directory to store the text files
            var textDirectoryPath = $"text/{localDomain}";
            Directory.CreateDirectory(textDirectoryPath);

            // Create a directory to store the csv files
            Directory.CreateDirectory("processed");

            // While the queue is not empty, continue crawling
            while (queue.Count > 0)
            {
                // Get the next URL from the queue
                url = queue.Dequeue();
                Console.WriteLine(url); // for debugging and to see the progress

                // Save text from the url to a <url>.txt file
                var fileName = $"{textDirectoryPath}/{url.Substring(8).Replace("/", "_")}.txt";
                using (var writer = new StreamWriter(fileName, false, System.Text.Encoding.UTF8))
                {
                    // Get the text from the URL using HtmlAgilityPack
                    var web = new HtmlWeb();
                    var doc = await web.LoadFromWebAsync(url);

                    // Get the text but remove the tags
                    var text = doc.DocumentNode.InnerText;

                    // If the crawler gets to a page that requires JavaScript, it will stop the crawl
                    if (text.Contains("You need to enable JavaScript to run this app."))
                    {
                        Console.WriteLine($"Unable to parse page {url} due to JavaScript being required");
                    }

                    // Otherwise, write the text to the file in the text directory
                    await writer.WriteAsync(text);
                }

                // Get the hyperlinks from the URL and add them to the queue
                var hyperlinks = await GetDomainHyperlinks(localDomain, url);
                foreach (var link in hyperlinks)
                {
                    if (!seen.Contains(link))
                    {
                        queue.Enqueue(link);
                        seen.Add(link);
                    }
                }
            }
        }
    }
}