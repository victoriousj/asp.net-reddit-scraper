using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace RedditScraper
{
	class Program
	{
		private static string inputSubreddit;
		private static Subreddit subreddit;
		private static string directory;
		private static FromTime time;
		private static int amount;

		public static void Main()
		{
			if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return;

			Console.Title = "Reddit Scraper";

			GetUserInput();
			DownloadRedditPosts();
			AttemptFileCleanup();
			Console.WriteLine();
			Show($"Go to \"{directory}\" to see your files");
			Console.ReadLine();
		}

		private static void GetUserInput()
		{
			Show("Which subreddit would you like to scrape? (default \"funny\")");
			inputSubreddit = GetInput("subreddit");
			inputSubreddit = !string.IsNullOrWhiteSpace(inputSubreddit) ? inputSubreddit : "funny";

			Show("How many posts would you like to try to download? (default 100)");
			var inputAmount = GetInput("amount");
			amount = !string.IsNullOrEmpty(inputAmount) ? int.Parse(inputAmount) : 100;

			Show("Time Period? (default \"All Time\")");
			Show("0 = All Time");
			Show("1 = Past Year");
			Show("2 = Past Month");
			Show("3 = Past Week");
			Show("4 = Past Day");
			Show("5 = Past Hour");
			if (!Enum.TryParse(GetInput("From"), out FromTime time)) time = FromTime.All;
		}

		private static void DownloadRedditPosts()
		{
			directory = $@"C:\reddit\{inputSubreddit}\";
			Show($"Creating directory at \"{directory}\"");
			Directory.CreateDirectory(directory);

			subreddit = new Reddit().GetSubreddit($"/r/{inputSubreddit}");
			Show($"Looking on {subreddit} for {amount} posts...");

			var foundPosts = subreddit.GetTop(time).Take(amount);
			Show($"Found {foundPosts.Count()} posts on {subreddit}");

			foundPosts.ToList().ForEach(x => DownloadImage(x.Url.ToString()));

			var files = Directory.GetFiles(directory);
			Show($"Downloaded {files.Length} files from  {subreddit}...");
		}

		private static void AttemptFileCleanup()
		{
			Show($"Attempting to clean up bad files...");
			DeleteBadFiles();

			var files = Directory.GetFiles(directory);
			Show($"{files.Length} remain from {subreddit}... Enjoy!");
			Console.Beep();
		}

		private static void DownloadImage(string imageURL)
		{
			FixImageUrl(ref imageURL);
			string fileName = CreateFileName(imageURL);
			string path = directory + fileName;

			DownloadFile(new Uri(imageURL), path, fileName);
		}

		private static void FixImageUrl(ref string imageUrl)
		{
			switch (imageUrl)
			{
				case string url when url.Contains("gfycat.com"):
					imageUrl = imageUrl.Replace("gfycat.com", "zippy.gfycat.com") + ".mp4";
					break;
				case string url when url.Contains(".gifv"):
					imageUrl = imageUrl.Replace(".gifv", ".gif");
					break;
			}
		}

		private static string CreateFileName(string imageURL)
		{
			string fileName = imageURL.Split('/').Last();
			foreach (var c in Path.GetInvalidFileNameChars())
			{
				fileName = fileName.Replace(c, '.');
			}
			if (fileName.IndexOf(".") != fileName.LastIndexOf(".") && fileName.Count(x => x == '.') > 1)
			{
				fileName = fileName.Substring(0, fileName.LastIndexOf("."));
			}
			return fileName;
		}

		public static void DownloadFile(Uri uri, string destination, string fileName)
		{
			using (var wc = new WebClient())
			{
				wc.QueryString.Add("fileName", fileName);
				wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
				wc.DownloadFileCompleted += Wc_DownloadFileCompleted;

				var syncObject = new object();
				lock (syncObject)
				{
					wc.DownloadFileAsync(uri, destination, syncObject);
					Monitor.Wait(syncObject);
				}
			}
		}

		private static void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			var fileName = ((WebClient)(sender)).QueryString["fileName"];
		
			string progress = new string('#', (int) Math.Round(e.ProgressPercentage / 2.0));
			string progressBar = progress + "--------------------------------------------------".Substring(0, 50 - progress.Length);
			string progressPercent = "000".Substring(0, 3 - e.ProgressPercentage.ToString().Length) + e.ProgressPercentage;

			Console.WriteLine($"{fileName} - {progressPercent}% {progressBar}", ConsoleColor.Yellow);
		}

		private static void Wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
		{
			lock (e.UserState)
			{
				Monitor.Pulse(e.UserState);
			}
		}

		private static void DeleteBadFiles()
		{
			var files = Directory.GetFiles(directory);
			var deletedFiles = 0;
			foreach (var file in files)
			{
				var fileInfo = new FileInfo(file);
				if (fileInfo != null && fileInfo.Length == 0 || fileInfo.Length == 503 ||  string.IsNullOrWhiteSpace(fileInfo.Extension) || fileInfo.Extension.Length > 4)
				{
					fileInfo.Delete();
					deletedFiles++;
				}
			}
			Show($"Deleted {deletedFiles} files...");
		}

		private static string GetInput(string prompt)
		{
			Console.Write(prompt + ": ");
			var input = Console.ReadLine();
			Console.WriteLine();
			return input;
		}

		private static void Show(string text)
		{
			Console.ForegroundColor = ConsoleColor.Magenta;
			Console.WriteLine(text);
			Console.ForegroundColor = ConsoleColor.White;
		}
	}
}