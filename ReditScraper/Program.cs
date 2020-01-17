using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace RedditScraper
{
	class Program
	{
		#region Field Variables
		private delegate string RedditPostCaller();
		private static Subreddit _subreddit;
        private static string _directory;
        #endregion

        private static void Main(string[] args)
		{
            DownloadRedditPost();
			ChangeWallpaper();
        }


		private static void DownloadRedditPost()
		{
				_subreddit = new Reddit().GetSubreddit("/r/wallpaper");

            var caller = new RedditPostCaller(GetRedditPost);
            IAsyncResult result = caller.BeginInvoke(null, null);


            var post = caller.EndInvoke(result);
			_directory = $@"C:\reddit\wallpaper\";
			Directory.CreateDirectory(_directory);

			DownloadImage(post);

		}

        private static string GetRedditPost() => _subreddit.GetTop(FromTime.Day).Take(1).Select(x => x.Url.ToString()).First();

		private static void DownloadImage(string url)
		{
			
			FixImageUrl(ref url);
			string fileName = FixFileName(url);

			if (string.IsNullOrWhiteSpace(url)) return;

			string path = _directory + fileName;
			DownloadFile(new Uri(url), path);
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

		// Needed to overcome url changes which happen automatically when browsing
		// but not when web crawling
		private static void FixImageUrl(ref string imageUrl)
		{
			switch (imageUrl)
			{
				case string url when url.Contains("gfycat.com"):
					GetGifyCatUrl(ref imageUrl);
					break;
				case string url when url.Contains(".gifv"):
					imageUrl = imageUrl.Replace(".gifv", ".gif");
					break;
			}
		}

		// Use the gfycat API to find the URL of the associated file
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

		private static void DownloadFile(Uri uri, string destination)
		{
			using (var wc = new WebClient())
			{
				var now = DateTime.Now.ToLongTimeString();

				// Locking the thread and making a sync download into
				// a psuedo async one so we have access to the download events
				wc.DownloadFile(uri, destination);
			}
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern int SystemParametersInfo
		(
			uint uiAction,
			uint uiParam,
			string pvParam,
			uint fWinIni
		);

		private static uint SPI_SETDESKWALLPAPER = 20;

		private static uint SPIF_UPDATEINIFILE = 0x1;

		static void ChangeWallpaper()
		{
			string[] files = Directory.EnumerateFiles(@"C:\reddit\wallpaper\").ToArray();
			SystemParametersInfo(SPI_SETDESKWALLPAPER, 1, files[0].ToString(), SPIF_UPDATEINIFILE);
		}
	}
}