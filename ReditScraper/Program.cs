using RedditSharp;
using RedditSharp.Things;
using System;
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
		private static int fileIndex;
		private static FromTime time;
		private static int amount;


		public static void Main()
		{
			if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return;

			Console.Title = "Reddit Scraper";
			GetUserInput();
			DownloadRedditPosts();
			DeleteBadFiles();

			Show("All steps complete...");
			Show($"Go to \"{directory}\" to see your files. Enjoy!");
			Console.Beep();
			Console.ReadLine();
		}

		private static void GetUserInput()
		{
			Show("Which subreddit would you like to scrape? (default \"funny\")");
			inputSubreddit = GetInput("subreddit");
			if (string.IsNullOrWhiteSpace(inputSubreddit))
			{
				inputSubreddit = "funny";
			}

			Console.WriteLine();
			Show("How many posts would you like to try to download? (default 25)");
			if (!int.TryParse(GetInput("amount"), out amount)) amount = 25;

			Show("Time Period? (default \"All Time\")");
			Show("0 = All Time");
			Show("1 = Past Year");
			Show("2 = Past Month");
			Show("3 = Past Week");
			Show("4 = Past Day");
			Show("5 = Past Hour");
			if (!Enum.TryParse(GetInput("from"), out FromTime time)) time = FromTime.All;
			Console.WriteLine();
		}

		private static void DownloadRedditPosts()
		{
			directory = $@"C:\reddit\{inputSubreddit}\";
			Show($"Creating directory at \"{directory}\"");
			Console.WriteLine();
			Directory.CreateDirectory(directory);

			subreddit = new Reddit().GetSubreddit($"/r/{inputSubreddit}");
			Show($"Looking on {subreddit} for {amount} posts...");
			Console.WriteLine();

			var foundPosts = subreddit.GetTop(time).Take(amount);
			Show($"Found {foundPosts.Count()} posts on {subreddit}");
			Console.WriteLine();

			System.Diagnostics.Process.Start(directory);
			foundPosts.ToList().ForEach(x => DownloadImage(x.Url.ToString()));

			var files = Directory.GetFiles(directory);
			Console.WriteLine();
			Show($"Downloaded {files.Length} files from  {subreddit}...");
			Console.WriteLine();
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
					imageUrl = imageUrl.Replace("gfycat.com", "giant.gfycat.com") + ".webm";
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
			return $"{++fileIndex}-{fileName}";
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

			Console.WriteLine($"{fileName} - {progressPercent}% {progressBar}");
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
			Show("Attempt to clean up potionally corrupted files? (default yes)");
			Show("1 = Yes");
			Show("2 = No");

			if (!int.TryParse(GetInput("clean"), out int answer)) answer = 1;
			if (answer != 1) return;

			var files = Directory.GetFiles(directory);
			int deletedFiles = 0;
			int brokenFiles = 0;
			int removedFiles = 0;
			int badExtensionFiles = 0;
			foreach (var file in files)
			{
				var fileInfo = new FileInfo(file);
				if (fileInfo != null)
				{
					if (fileInfo.Length == 0) brokenFiles++;
					if (fileInfo.Length == 503) removedFiles++;
					if (string.IsNullOrWhiteSpace(fileInfo.Extension) || fileInfo.Extension.Length > 6) badExtensionFiles++;

					if (fileInfo.Length == 0 || fileInfo.Length == 503 || string.IsNullOrWhiteSpace(fileInfo.Extension) || fileInfo.Extension.Length > 6)
					{
						fileInfo.Delete();
						deletedFiles++;
					}
				}
			}
			Show($"{brokenFiles} were improperly downloaded...");
			Show($"{removedFiles} are no longer availible...");
			Show($"{badExtensionFiles} have an unknown file extension...");
			Console.WriteLine();
			Show($"{deletedFiles} files have been deleted overall...");
			Show($"{files.Length - deletedFiles} remain from {subreddit}...");
			Console.WriteLine();
		}
		private static void ConvertVideos()
		{
			var files = Directory.GetFiles(directory).Where(x => x.Contains("webm"));
			if (files.Any())
			{
				Show("It appears there were some .webm files downloaded. These can be hard");
				Show("to play. Would you like to convert these videos? Short videos will be");
				Show("converted to .gifs and longer ones will be .mp4 (default No)");
				Show("1 = Yes");
				Show("2 = No");

				if (!int.TryParse(GetInput("convert"), out int answer)) answer = 2;
				if (answer != 1) return;

				Show("Converting videos... This may take a couple of minutes...");
				int convertedVideoCount = 1;
				int gifCount = 0;
				int mp4Count = 0;
				var ffMpeg = new FFMpegConverter();
				foreach (var file in files)
				{
					Console.ForegroundColor = ConsoleColor.Magenta;
					Console.Write($"({convertedVideoCount} of {files.Count()}) Converting: ");
					Console.ForegroundColor = ConsoleColor.White;
					Console.Write(Path.GetFileName(file));
					Console.WriteLine();

					var fileInfo = new FileInfo(file);
					if (fileInfo.Length < 21000)
					{
						var newFileName = file.Replace(Format.webm, Format.gif);
						ffMpeg.ConvertMedia(file, newFileName, Format.gif);
						gifCount++;
					} 
					else
					{
						var newFileName = file.Replace(Format.webm, Format.mp4);
						ffMpeg.ConvertMedia(file, newFileName, Format.mp4);
						mp4Count++;
					}
					fileInfo.Delete();
					convertedVideoCount++;
				}
				Show("All videos have been converted...");
				Console.Write(gifCount);
				Console.ForegroundColor = ConsoleColor.Magenta;
				Console.WriteLine(" gif's have been created...");
				Console.ForegroundColor = ConsoleColor.White;
				Console.Write(mp4Count);
				Console.ForegroundColor = ConsoleColor.Magenta;
				Console.WriteLine(" .mp4's have been created...");
				Console.ForegroundColor = ConsoleColor.White;
				Console.WriteLine();

				var ffmpegFile = new FileInfo("ffmpeg.exe");
				ffmpegFile.Delete();
			}
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