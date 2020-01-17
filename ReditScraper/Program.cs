using RedditSharp;
using RedditSharp.Things;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace RedditScraper
{
	class Program
	{
        private static string _directory;
		private static string _filePath;
		private static string _optSubreddit;
		private static Subreddit _subreddit;

        private static void Main(string[] args)
		{
			_optSubreddit = args.Length > 0 ? args[0] : "wallpaper";
			DownloadRedditPost();
			ChangeWallpaper();
        }

		private static void DownloadRedditPost()
		{
			_subreddit = new Reddit().GetSubreddit($"/r/{_optSubreddit}");

			_directory = $@"C:\reddit\wallpaper\";
			Directory.CreateDirectory(_directory);

			DownloadImage(GetRedditPost());
		}

        private static string GetRedditPost() => _subreddit.GetTop(FromTime.Day).Take(1).Select(x => x.Url.ToString()).First();

		private static void DownloadImage(string url)
		{
			FixImageUrl(ref url);
			string fileName = FixFileName(url);

			if (string.IsNullOrWhiteSpace(url)) return;

			_filePath = _directory + fileName;
			using (var wc = new WebClient())
			{
				wc.DownloadFile(url, _filePath);
			}
		}

		private static string FixFileName(string url)
		{
			string fileName;

			string fileExtension = url.Split('.').Last();
			if (fileExtension.Length > 3)
			{ 
				fileExtension = fileExtension.Substring(0, Math.Min(fileExtension.Length, 3));
			}
			var beginDate = new DateTime(2020, 1, 1);
			var nowDate = DateTime.Now;

			var daysSince = (nowDate - beginDate).Days;

			fileName = 1000 - daysSince + "." + fileExtension;
			return fileName;
		}

		private static void FixImageUrl(ref string imageUrl)
		{
			switch (imageUrl)
			{
				case string url when url.Contains("gfycat.com"):
					GetGifyCatUrl(ref imageUrl);
					break;
			}
		}

		private static void GetGifyCatUrl(ref string url)
		{
			var request = (HttpWebRequest)WebRequest.Create($@"https://api.gfycat.com/v1/gfycats/" + url.Split('/').Last());
			request.ContentType = "application/json; charset=utf-8";
			request.Timeout = 10000;
			try
			{
				using (WebResponse response  = (HttpWebResponse)request.GetResponse())
				using (Stream responseStream = response.GetResponseStream())
				{
					var reader = new StreamReader(responseStream, Encoding.UTF8);
					var results = reader.ReadToEnd();

					// very hacky
					int pos1 = results.IndexOf("\"mp4Url\":") + 9;
					int pos2 = results.IndexOf(",\"gifUrl\":");

					url = results.Substring(pos1, pos2 - pos1);
					url = url.Replace("\"", "").Replace("\\", "");
				}
			} catch (Exception) { }
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern int SystemParametersInfo
		(uint uiAction, uint uiParam, string pvParam, uint fWinIni);
		static void ChangeWallpaper() => SystemParametersInfo(20, 1, _filePath, 0x1);
	}
}