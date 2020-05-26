using RedditSharp;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace RedditScraper
{
    class Program
	{
		private static readonly string _imgurApiKey = File.Exists(AppDomain.CurrentDomain.BaseDirectory + "imgur-api-key.txt") ? File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "imgur-api-key.txt") : "";

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
					.GetTop(RedditSharp.Things.FromTime.Day)
					.ToList()
					.Take(1)
					.Select(x => (url: x.Url.ToString(), title: x.Title)).ToList();

				Directory.CreateDirectory(directory);
				redditPosts.ForEach(post => DownloadImage(post, directory));
				DeleteBadFile(directory);
			}
		}

		private static void DeleteBadFile(string directory)
		{
			var file = new DirectoryInfo(directory).GetFiles().OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
			if (file.Length == 0 || file.Length == 503 || string.IsNullOrWhiteSpace(file.Extension) || file.Extension.Contains("com"))
			{
				file.Delete();
			}
		}

		private static void DownloadImage((string url, string title) post, string directory)
		{
			var (url, fileName) = post;
			
			FixImageUrl(ref url, false);
			FixFileName(ref fileName, ref url);

			var lastFile = new DirectoryInfo(directory).GetFiles().OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
			if (lastFile.Name.Contains(fileName.Substring(4)) || string.IsNullOrWhiteSpace(url))
			{
				return;
			}

			string path = directory + fileName;

			using (var wc = new WebClient())
			{
				wc.DownloadFile(new Uri(url), path);
			}
		}

		private static void FixFileName(ref string fileName, ref string url)
		{
			string fileExtension = url.Split('.').Last();
			if (fileExtension.Contains("com") && url.Contains("imgur.com"))
            {
				FixImageUrl(ref url, true);
				fileExtension = url.Split('.').Last();
			}
			else if (fileExtension.Length > 3)
			{ 
				fileExtension = fileExtension.Substring(0, 3);
			}

			fileName = WebUtility.HtmlEncode(fileName);
			fileName = Regex.Replace(fileName, @"&#[0-9]{5,};", "");
			fileName = WebUtility.HtmlDecode(fileName);
			fileName = fileName.Replace("&amp;", "&").Replace("&lt;3", "♡").Replace("&gt;","");
			fileName = Regex.Replace(fileName, @"\s{2,}", " ").Trim();
			fileName = fileName.Substring(0, Math.Min(225, fileName.Length));

			var daysSince = 1000 - Math.Floor((DateTime.Now - new DateTime(2020, 1, 1)).TotalDays);

			fileName = $"{daysSince}{" " + fileName.Replace(".", "").Trim()}.{fileExtension}";

			foreach(char c in Path.GetInvalidFileNameChars())
			{
				fileName = fileName.Replace(c.ToString(), "");
			}
		}

		private static void FixImageUrl(ref string url, bool imgur)
		{
            (string url, string parent, string child) parameters;
            switch (url)
            {
                case string post when post.Contains("redgifs.com"):
                    parameters = (@"https://api.redgifs.com/v1/gfycats/", "gfyItem", "mp4Url");
                    break;
                case string post when post.Contains(".gifv"):
                    url = url.Replace(".gifv", ".gif");
                    return;
                case string post when post.Contains("gfycat.com"):
                    parameters = (@"https://api.gfycat.com/v1/gfycats/", "gfyItem", "mp4Url");
                    break;
				case string post when post.Contains("imgur.com") && imgur:
					parameters = (@"https://api.imgur.com/3/image/", "data", "link");
					break;
				default:
                    return;
            }
            try
			{
				var request = WebRequest.Create(parameters.url + url.Split('/').Last());
				if (imgur)
				{
					request.Headers.Add("Authorization", _imgurApiKey);
				}

				using (WebResponse response = request.GetResponse())
				using (Stream responseStream = response.GetResponseStream())
				{
					var reader = new StreamReader(responseStream, System.Text.Encoding.UTF8);
					var results = reader.ReadToEnd();
					url = Newtonsoft.Json.Linq.JObject.Parse(results)[parameters.parent][parameters.child].ToString();
				}
			}
			catch { }
		}
	}
}