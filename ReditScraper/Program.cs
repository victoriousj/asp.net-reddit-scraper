using RedditSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace RedditScraper
{
    class Program
	{
		#region Files
		private static readonly Settings _settings = new Settings();
		private static List<(string directory, string fileName)> files = new List<(string, string)>();
		#endregion
        private static void Main(string[] args)
		{
            DownloadRedditPosts();
        }

		private static void DownloadRedditPosts()
		{
			if (_settings.ShouldCollect)
			{
				if (Directory.Exists(_settings.CollectionPath)) Directory.Delete(_settings.CollectionPath, true);
				Directory.CreateDirectory(_settings.CollectionPath);
			}
			foreach (var subreddit in _settings.Subreddits)
			{
				var directory = $@"{_settings.Destination}\{subreddit}\";
				var subredditPage = new Reddit().GetSubreddit($"/r/{subreddit}") ?? throw new WebException();
				var posts = subredditPage
					.GetTop(_settings.FromTime)
					.Select(x => ($"{x.Url}", x.Title))
					.Take(_settings.Amount)
					.ToList();

                Directory.CreateDirectory(directory);
				posts.ForEach(post => DownloadImage(post, directory));
                DeleteBadFile(directory);
            }
			if (_settings.ShouldCollect)
            {
				CopyFiles();
            }
        }

		private static void DeleteBadFile(string directory)
		{
			var fileTypes = new []{ ".JPG", ".JPE", ".BMP", ".GIF", ".PNG", ".MP4" };
			var removedPostFileSize = new[] { 0, 503, 9236 };

            var files = new DirectoryInfo(directory)
				.GetFiles()
				.OrderByDescending(f => f.LastWriteTime)
				.Take(_settings.Amount);

			foreach (var file in files)
            {
				if (file != null && removedPostFileSize.Any(x => x == file.Length) || !fileTypes.Contains(file.Extension.ToUpper()))
				{
					file.Delete();
				}
            }
		}

		private static void DownloadImage((string url, string title) post, string directory)
		{
			var (url, fileName) = post;
			
			FixFileAndURL(ref fileName, ref url);

			// Do not download the same file day after day.
			var lastFile = new DirectoryInfo(directory).GetFiles().OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
			if (lastFile != null && lastFile.Name.Contains(fileName.Substring(4)) || string.IsNullOrWhiteSpace(url)) return;

			string path = directory + fileName;
			using (var wc = new WebClient())
			{
				try 
				{ 
					wc.DownloadFile(new Uri(url), path); 
					if (_settings.ShouldCollect)
					{
						files.Add((directory, fileName));
					}
				} catch { }
			}
		}

		private static void FixFileAndURL(ref string fileName, ref string url)
		{
			var daysSince = 1000 - Math.Floor((DateTime.Now - new DateTime(2020, 1, 1)).TotalDays);
			string fileExtension = url.Split('.').Last();
			FixImageUrl
			(
				url : ref url, 
				imgur: fileExtension.Contains("com") && url.Contains("imgur.com"), 
				transfer : fileExtension.Contains("com") && url.Contains("gfycat.com")
			);
			fileExtension = url.Split('.').Last().Substring(0,3);
			if (fileExtension.Length > 3)
            {
				fileExtension = fileExtension.Substring(0, 3);
            }

			fileName = WebUtility.HtmlEncode(fileName);
			fileName = Regex.Replace(fileName, @"&#[0-9]{5,};", "").Replace(@"\s+", " ");
			fileName = WebUtility.HtmlDecode(fileName);
			fileName = Regex.Replace(fileName, "&amp;", "&").Replace("&lt;3", "♡").Replace("&gt;","");
			fileName = fileName.Substring(0, Math.Min(225, fileName.Length));
			fileName = string.IsNullOrWhiteSpace(fileName) ? "" : $" {fileName.Replace(".", "").Trim()}";
			fileName = string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));

			fileName = $"{daysSince}{fileName}.{fileExtension}";
		}

		private static void FixImageUrl(ref string url, bool imgur, bool transfer)
		{
			(string url, string parent, string child) parameters;
			switch (url)
			{
				case string post when post.Contains("redgifs.com") || transfer:
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
				if (imgur) request.Headers.Add("Authorization", _settings.ImgurApiKey);

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

		private static void CopyFiles()
        {
			int index = 0;
			foreach(var (directory, fileName) in files)
            {
				var subredditName = directory.Split('\\').Reverse().Skip(1).FirstOrDefault();
				var indexStr = _settings.Amount > 1 ? (++index).ToString() + "-" : "";

				var destinationName = $"{indexStr}{subredditName}-{fileName.Substring(4)}";
				var destination = Path.Combine(_settings.CollectionPath, destinationName);
				
				var source = Path.Combine(directory, fileName);
				
				if (File.Exists(source))
                {
					File.Copy(source, destination);
                }
			}
        }
	}
}