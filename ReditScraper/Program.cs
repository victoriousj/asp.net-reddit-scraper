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
		#region Files
		private static Settings _settings = new Settings();
		#endregion
        private static void Main(string[] args)
		{
            DownloadRedditPosts();
        }

		private static void DownloadRedditPosts()
		{
            foreach (var subreddit in _settings.Subreddits)
            {
                var directory = $@"{_settings.Destination}\{subreddit}\";
                var subredditPage = new Reddit().GetSubreddit($"/r/{subreddit}") ?? throw new WebException();
                var posts = subredditPage.GetTop(_settings.FromTime).Select(x => (url: x.Url.ToString(), title: x.Title)).Take(_settings.Amount).ToList();

                Directory.CreateDirectory(directory);
                posts.ForEach(post => DownloadImage(post, directory));
                DeleteBadFile(directory);
            }
        }

		private static void DeleteBadFile(string directory)
		{
			var fileTypes = new []{ ".JPG", ".JPE", ".BMP", ".GIF", ".PNG", ".MP4" };

            var files = new DirectoryInfo(directory).GetFiles().OrderByDescending(f => f.LastWriteTime).Take(_settings.Amount);
			foreach (var file in files)
            {
				if (file != null && file.Length == 0 || file.Length == 503 || file.Length == 9236 || !fileTypes.Contains(file.Extension.ToUpper()))
				{
					file.Delete();
				}
            }
		}

		private static void DownloadImage((string url, string title) post, string directory)
		{
			var (url, fileName) = post;
			
			FixFileAndURL(ref fileName, ref url);

			var lastFile = new DirectoryInfo(directory).GetFiles().OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
			if (lastFile != null && lastFile.Name.Contains(fileName.Substring(4)) || string.IsNullOrWhiteSpace(url)) return;

			string path = directory + fileName;
			using (var wc = new WebClient())
			{
				try { wc.DownloadFile(new Uri(url), path); } catch { }
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
			fileExtension = url.Split('.').Last();

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
	}
}