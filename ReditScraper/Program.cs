using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace RedditScraper
{
	class Program
	{
		#region Field Variables
		private delegate List<(string, string)> RedditPostCaller();
		private static List<string> _subreddits;
		private static Subreddit _subreddit;
        private static string _directory;
		private static FromTime _time;
		private static bool _magOrCy;
		private static int _amount;
		private static int _color;
        #endregion

        private static void Main(string[] args)
		{
			Intro();

			GetInput(args);

            DownloadRedditPosts();
        }

		private static void Intro()
		{
			Console.Title = "Reddit Scraper";
			Console.ForegroundColor = FlipColors();
			Show(new[]
			{
@"  _____          _     _ _ _      _____                                
 |  __ \        | |   | (_) |    / ____|                               
 | |__) |___  __| | __| |_| |_  | (___   ___ _ __ __ _ _ __   ___ _ __ 
 |  _  // _ \/ _` |/ _` | | __|  \___ \ / __| '__/ _` | '_ \ / _ \ '__|
 | | \ \  __/ (_| | (_| | | |_   ____) | (__| | | (_| | |_) |  __/ |   
 |_|  \_\___|\__,_|\__,_|_|\__| |_____/ \___|_|  \__,_| .__/ \___|_|   
                                                      | |              
   by: victor d. johnson                              |_|              
   this code is under MIT licence",
				string.Empty
			});
		}

		private static void GetInput(string[] args)
		{
            for (int i = 0; i < args.Length; i++)
            {
                if (!(i + 1 < args.Length)) break;

                if (args[i].Equals("-sr"))
                {
                    _subreddits = args[i + 1].Split(',').ToList();
                }

                if (args[i].Equals("-t"))
                {
                    if (!Enum.TryParse(args[i+1], out _time)) _time = FromTime.Week;
                }
			}
		}

		private static void DownloadRedditPosts()
		{
			foreach (var sr in _subreddits)
			{
				var srInfo = sr.Split('-');
				string subreddit = srInfo.First();
				_amount = int.Parse(srInfo.Last());

				_subreddit = new Reddit().GetSubreddit($"/r/{subreddit}") ?? throw new WebException();

				var caller = new RedditPostCaller(GetRedditPosts);
				IAsyncResult result = caller.BeginInvoke(null, null);

				Console.ForegroundColor = FlipColors();

				Console.Write($"Looking on {_subreddit} for {_amount} posts...");
				while (!result.IsCompleted)
				{
					Spinner.Turn();
					Thread.Sleep(200);
				}
				var redditPosts = caller.EndInvoke(result);
				Console.WriteLine("\n");
				Console.ForegroundColor = FlipColors();

				_directory = $@"C:\reddit\reddit\{subreddit}\";
				Directory.CreateDirectory(_directory);

				Show(new[] { $"Downloading files from {_subreddit}" });
				Console.ForegroundColor = FlipColors();

				foreach (var post in redditPosts)
				{
					DownloadImage(post);
				}
				Console.WriteLine();
				DeleteBadFiles();
			}
		}

		private static List<(string, string)> GetRedditPosts()
        {
			return _subreddit
				.GetTop(_time)
				.Take(_amount)
				.Select(x =>  
					(url: x.Url.ToString(), 
					title: x.Title))
				.ToList();
        }

		private static void DownloadImage((string url, string title) post)
		{
			var (url, fileName) = post;
			
			FixImageUrl(ref url);
			FixFileName(ref fileName, url);

			if (string.IsNullOrWhiteSpace(url)) return;

			string path = _directory + fileName;
			DownloadFile(new Uri(url), path, fileName);
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

			fileName = (10000000000 - DateTimeOffset.Now.ToUnixTimeSeconds())
				+ " " + fileName.Replace(".", "").Trim()
				+ "." + fileExtension;

			foreach(char c in Path.GetInvalidFileNameChars())
			{
				fileName = fileName.Replace(c.ToString(), "");
			}
		}

		private static void FixImageUrl(ref string imageUrl)
		{
			switch (imageUrl)
			{
				case string url when url.Contains("gfycat.com"):
					Console.WriteLine("Looking for video file url...");
					ReturnPrompt();
					GetGifyCatUrl(ref imageUrl);
					break;
				case string url when url.Contains(".gifv"):
					imageUrl = imageUrl.Replace(".gifv", ".gif");
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

		private static void DownloadFile(Uri uri, string destination, string fileName)
		{
			string abbrFileName = fileName =
				fileName.Length >= 45
					? fileName.Substring(0, 42) + "..."
					: fileName;

			using (var wc = new WebClient())
			{
				var now = DateTime.Now.ToLongTimeString();
				wc.QueryString.Add("fileName", abbrFileName);
				wc.QueryString.Add("time", now.Substring(0, now.Length - 3));
				wc.DownloadFileCompleted += DownloadFileCompleted;
				wc.DownloadProgressChanged += UpdateDownloadProgress;

				var syncObject = new object();
				lock (syncObject)
				{
					wc.DownloadFileAsync(uri, destination, syncObject);
					Monitor.Wait(syncObject);
				}
			}
		}

        private static void DeleteBadFiles()
		{
			int deletedFiles = 0;

            var filesToDelete = new List<string>();
			var files = Directory.GetFiles(_directory);
			foreach (var file in files)
			{
				var fileInfo = new FileInfo(file);
				var length = fileInfo.Length;
				var exten = fileInfo.Name.Split('.').LastOrDefault();

				if (length == 0 || length == 503 || exten.Length > 6 ||string.IsNullOrWhiteSpace(exten) ||exten.Contains("com"))
                {
                    filesToDelete.Add(file);
                }
            }

            if (!filesToDelete.Any()) return;

            filesToDelete.ForEach(x =>
            {
                new FileInfo(x).Delete();
                deletedFiles++;
            });


            Show(new[]
			{
                string.Empty,
				$"{deletedFiles} files have been deleted...",
				$"{files.Length - deletedFiles} remain from {_subreddit}...",
			});
		}

        // Alternate colors for the console then return the color to white
		private static void Show(string[] texts)
		{
			foreach (var text in texts)
			{
				Console.WriteLine(text);
			}
			Console.WriteLine();
		}

        // Toggle between three colors.
		private static ConsoleColor FlipColors()
		{
			_color++;
			switch (_color % 3)
			{
				case 1: return ConsoleColor.Cyan;
				case 2: return ConsoleColor.Magenta;
				default: return ConsoleColor.White;
			}
		}

		private static void ReturnPrompt() => Console.SetCursorPosition(0, Console.CursorTop - 1);

		public class Spinner
        {
            static int counter = 0;

            public static void Turn()
            {
                counter++;
                switch (counter % 4)
                {
                    case 0: Console.Write("/"); break;
                    case 1: Console.Write("-"); break;
                    case 2: Console.Write("\\"); break;
                    case 3: Console.Write("|"); break;
                }
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
            }
        }
        private static void UpdateDownloadProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            int progress = e.ProgressPercentage;
            string progressBar = MakeProgressBar(progress);

			string time = ((WebClient)sender).QueryString["time"];
            string fileName = ((WebClient)sender).QueryString["fileName"];
            Console.Write($"\r({time}) {fileName} {new string(' ', (45 - fileName.Length))} {progressBar}");

		}

        // Take a number that's the percent downloaded and make a loading bar.
        private static string MakeProgressBar(int progress)
        {
            string loadedProgress = new string('#', (int)Math.Round(progress / 2.0));
            string unloadedProgress = new string('-', 50).Substring(0, 50 - loadedProgress.Length);
            string progressPercent = "000%".Substring(0, 3 - progress.ToString().Length) + progress;

            string progressBar = $"{progressPercent}% <{loadedProgress}{unloadedProgress}>";
            return progressBar;
        }

        // Release the 'async' lock on the thread for the file download.
        private static void DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                Console.WriteLine();
            }
            else
            {
                string fileName = ((WebClient)sender).QueryString["fileName"];
                Console.WriteLine($"{fileName} ERROR: {e.Error.Message}");
            }
            Console.ForegroundColor = FlipColors();
            lock (e.UserState)
            {
                Monitor.Pulse(e.UserState);
            }
        }
    }
}