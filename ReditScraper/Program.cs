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
		private static string directory;
		private static WebClient wc;
		private static List<string> downloadedFiles;


		public static void Main()
		{
			Console.WriteLine("Subreddit?");
			inputSubreddit = Console.ReadLine();
			inputSubreddit = !string.IsNullOrWhiteSpace(inputSubreddit) ? inputSubreddit : "funny";

			if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
			{
				Console.WriteLine("Internet not availible");
				return;
			}
			var reddit = new Reddit();
			var subreddit = reddit.GetSubreddit($"/r/{inputSubreddit}");
			if (subreddit == null)
			{
				Console.WriteLine("Subreddit not found");
				Console.ReadLine();
				return;
			}

			directory = $@"C:\{inputSubreddit}\";

			Console.WriteLine("Amount: (default 100)");
			var inputAmount = Console.ReadLine();
			int amount = !string.IsNullOrEmpty(inputAmount) ? int.Parse(inputAmount) : 100;

			Console.WriteLine($"Looking on {subreddit} for {amount} posts...");
			var foundPosts = subreddit.GetTop(FromTime.All).Take(amount);
			var posts = foundPosts.Select(x => x.Url.ToString());
			var postTuple = foundPosts.Select(x => new { title = x.Title, url = x.Url.ToString() });

			Console.WriteLine($"Found {posts.Count()} posts on {subreddit}");

			downloadedFiles = new List<string>();
			foreach (var post in posts)
			{
				DownloadImages(post);
			}

			var createdFiles = Directory.GetFiles($@"C:\{inputSubreddit}\");
			Console.WriteLine($"Downloaded {createdFiles.Count()} files from {subreddit}");

			Console.WriteLine($"{createdFiles.Count()} remain. Enjoy!");
			Console.ReadLine();
		}

		private static void DownloadImages(string imageURL)
		{
			FixImageUrl(ref imageURL);
			string fileName = imageURL.Split('/').Last();
			foreach (var c in Path.GetInvalidFileNameChars())
			{
				fileName = fileName.Replace(c, '.');
			}
			Console.WriteLine($"Downloading {fileName}");

			try
			{
				wc = new WebClient();
				using (var cts = new CancellationTokenSource())
				{
					cts.CancelAfter(600000);
					Directory.CreateDirectory(directory);
					wc.QueryString.Add("fileName", fileName);
					wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
					wc.DownloadFileCompleted += Wc_DownloadFileCompleted;
					wc.DownloadFileAsync(new Uri(imageURL), directory + fileName, cts);
				}
			}
			catch { Console.WriteLine($"ERROR: {fileName} is not availible"); }
		}

		private static void FixImageUrl(ref string imageUrl)
		{
			switch (imageUrl)
			{
				case string url when url.Contains("gfycat.com"):
					imageUrl = imageUrl.Replace("gfycat.com", "zippy.gfycat.com") + ".mp4";
					break;
				case string url when url.Contains(".gifv"):
					imageUrl = imageUrl.Replace(".gifv", ".mp4");
					break;
			}
		}

		private static void DeleteBrokenFiles(string[] createdFiles)
		{
			foreach (var file in createdFiles)
			{
				var fileInfo = new FileInfo(file);
				if (fileInfo.Length == 0 || string.IsNullOrWhiteSpace(fileInfo.Extension)) fileInfo.Delete();
			}
		}

		private static void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			var fileName = ((WebClient)(sender)).QueryString["fileName"];
			Console.WriteLine($"{fileName} - {e.ProgressPercentage}%");
		}

		private static void Wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
		{
			var fileName = ((WebClient)(sender)).QueryString["fileName"];

			if (string.IsNullOrWhiteSpace(fileName)) return;

			Console.WriteLine($"{fileName} succesfully downloaded");
			wc.Dispose();
			try
			{
				var fileInfo = new FileInfo(directory + fileName);
				if (fileInfo.Length == 0 || string.IsNullOrWhiteSpace(fileInfo.Extension))
				{
					Console.WriteLine($"{fileName} appears broken... removing it.");
					fileInfo.Delete();
				}
				else
				{
					downloadedFiles.Add(fileName);
				}
			}
			catch (IOException)
			{
				Console.WriteLine($"{fileName} is still in use. Can not assess it.");
			}
		}
	}
}