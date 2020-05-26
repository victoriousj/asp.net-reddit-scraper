using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace RedditScraper
{
	class Program
	{
        private static void Main(string[] args)
		{
            DownloadRedditPosts();
		}

		private static void DownloadRedditPosts()
		{
			var subreddits = File.ReadLines(AppDomain.CurrentDomain.BaseDirectory + "subreddits.txt");
			foreach (var subreddit in subreddits)
			{
				var directory = $@"D:\Documents\Back-up\documents\abc\p\{subreddit}\";
				var subredditPage = new Reddit().GetSubreddit($"/r/{subreddit}") ?? throw new WebException();
				var redditPosts = subredditPage
					.GetTop(FromTime.Day)
					.ToList()
					.Take(1)
					.Select(x => (url: x.Url.ToString(), title: x.Title)).ToList();

				Directory.CreateDirectory(directory);
				redditPosts.ForEach(post => DownloadImage(post, directory));
				DeleteBadFiles(directory);
			}
		}

		private static void DownloadImage((string url, string title) post, string directory)
		{
			var (url, fileName) = post;
			
			FixImageUrl(ref url);
			FixFileName(ref fileName, url);

			if (string.IsNullOrWhiteSpace(url)) return;
			
			string path = directory + fileName;

			using (var wc = new WebClient())
			{
				wc.DownloadFile(new Uri(url), path);
			}
		}

		private static void FixFileName(ref string fileName, string url)
		{
			string fileExtension = url.Split('.').Last();
			if (fileExtension.Length > 3)
			{ 
				fileExtension = fileExtension.Substring(0, Math.Min(fileExtension.Length, 3));
			}
			fileName = WebUtility.HtmlEncode(fileName);
			fileName = Regex.Replace(fileName, @"&#[0-9]{5,};", "");
			fileName = WebUtility.HtmlDecode(fileName);
			fileName = fileName.Replace("&amp;", "&").Replace("&lt;3", "♡").Replace("&gt;","");
			fileName = Regex.Replace(fileName, @"\s{2,}", " ").Trim();
			fileName = fileName.Substring(0, Math.Min(225, fileName.Length));

			var daysSince = 1000 - Math.Floor((DateTime.Now - new DateTime(2020, 1, 1)).TotalDays);
			fileName = $"{daysSince} {fileName.Replace(".", "").Trim()}.{fileExtension}";

			foreach(char c in Path.GetInvalidFileNameChars())
			{
				fileName = fileName.Replace(c.ToString(), "");
			}
		}

		private static void FixImageUrl(ref string imageUrl)
		{
			switch (imageUrl)
			{
				case string url when url.Contains("redgifs.com"):
					GetRedGifUrl(ref imageUrl);
					break;
				case string url when url.Contains(".gifv"):
					imageUrl = imageUrl.Replace(".gifv", ".gif");
					break;
			}
		}

		private static void GetRedGifUrl(ref string url)
		{
			try
			{
				using (WebResponse response = WebRequest.Create($@"https://api.redgifs.com/v1/gfycats/" + url.Split('/').Last()).GetResponse())
				using (Stream responseStream = response.GetResponseStream())
				{
					var reader = new StreamReader(responseStream, Encoding.UTF8);
					var results = reader.ReadToEnd();
					url = Newtonsoft.Json.Linq.JObject.Parse(results)["gfyItem"]["mp4Url"].ToString();
				}
			}
			catch { }
		}

		private static void DeleteBadFiles(string directory)
		{
            var filesToDelete = new List<string>();
			var files = Directory.GetFiles(directory);
			foreach (var file in files)
			{
				var fileInfo = new FileInfo(file);
				var length = fileInfo.Length;
				var exten = fileInfo.Name.Split('.').LastOrDefault();

				if (length == 0 || length == 503 || exten.Length > 6 ||string.IsNullOrWhiteSpace(exten) || exten.Contains("com"))
                {
                    filesToDelete.Add(file);
                }
            }

            filesToDelete.ForEach(x => new FileInfo(x).Delete());
		}
    }
}