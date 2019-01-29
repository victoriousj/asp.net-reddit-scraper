using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace RedditScraper
{
	class Program
	{
		private static string inputSubreddit;
		private static string directory;
		private static FromTime time;
		private static WebClient wc;
		private static int amount;

		public static void Main()
		{
			if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return;

			GetUserInput();
			DownloadRedditPostsAsync();
		}

		private static void GetUserInput()
		{
			Console.WriteLine("Subreddit?");
			inputSubreddit = Console.ReadLine();
			inputSubreddit = !string.IsNullOrWhiteSpace(inputSubreddit) ? inputSubreddit : "funny";

			Console.WriteLine("Amount: (default 100)");
			var inputAmount = Console.ReadLine();
			amount = !string.IsNullOrEmpty(inputAmount) ? int.Parse(inputAmount) : 100;

			Console.WriteLine("Time Period?");
			Console.WriteLine("0 = All Time");
			Console.WriteLine("1 = Past Year");
			Console.WriteLine("2 = Past Month");
			Console.WriteLine("3 = Past Week");
			Console.WriteLine("4 = Past Day");
			Console.WriteLine("5 = Past Hour");
			string timeInput = Console.ReadLine();
			if (!Enum.TryParse(timeInput, out FromTime time)) time = FromTime.All;
		}

		private static void DownloadRedditPostsAsync()
		{
			directory = $@"C:\reddit\{inputSubreddit}\";
			Console.WriteLine($"Creating directory at \"{directory}\"");
			Directory.CreateDirectory(directory);

			var subreddit = new Reddit().GetSubreddit($"/r/{inputSubreddit}");
			Console.WriteLine($"Looking on {subreddit} for {amount} posts...");

			var foundPosts = subreddit.GetTop(time).Take(amount);
			Console.WriteLine($"Found {foundPosts.Count()} posts on {subreddit}");

			var posts = foundPosts.Select(x => x.Url.ToString());
			DownloadImagesAsync(posts);

			Console.ReadLine();
		}

		private static async Task DownloadImagesAsync(IEnumerable<string> posts)
		{
			await Task.WhenAll(posts.Select(i => DownloadImages(i)));
		}

		private static async Task DownloadImages(string imageURL)
		{
			FixImageUrl(ref imageURL);
			string fileName = CreateFileName(imageURL);

			try
			{
				wc = new WebClient();
				wc.QueryString.Add("fileName", fileName);
				wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
				wc.DownloadFileCompleted += Wc_DownloadFileCompleted;
				await wc.DownloadFileTaskAsync(new Uri(imageURL), directory + fileName);
			}
			catch { }
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

		private static void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			var fileName = ((WebClient)(sender)).QueryString["fileName"];
		
			string progress = new string('#', (int) Math.Round(e.ProgressPercentage / 5.0));
			string progressBar = progress + "--------------------".Substring(0, 20 - progress.Length);

			Console.WriteLine($"{fileName} - {e.ProgressPercentage}% {progressBar}");
		}

		private static void Wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
		{
			wc.Dispose();
			try
			{
				var fileName = ((WebClient)(sender)).QueryString["fileName"];
				var fileInfo = new FileInfo(directory + fileName);
				if 
				(
					fileInfo != null &&
					fileInfo.Length == 0 || // Broken file
					fileInfo.Length == 503 ||  // Imgur's 'missing' image 
					string.IsNullOrWhiteSpace(fileInfo.Extension) || // Missing extension
					fileInfo.Extension.Length > 4 // URL parameters probably passed into filename
				)
				{
					fileInfo.Delete();
				}
			}
			catch (IOException) { }
			catch (ArgumentException) { }
		}
	}
}