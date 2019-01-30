using NReco.VideoConverter;
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
		private static string _inputSubreddit;
		private static Subreddit _subreddit;
		private static string _directory;
		private static int _fileIndex;
		private static FromTime _time;
		private static bool _magOrCy;
		private static int _amount;


		public static void Main()
		{
			if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return;

			Console.Title = "Reddit Scraper";
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
				string.Empty,
				@"Enter the name of a subreddit (without spaces) and specify an amount of",
				@"of photos and a time frame. This program will download an image from each",
				@"post found on the subreddit and place the image in a directory on your ",
				@"computer. This will then try to clean up broken files and convert web ",
				@"movies into video files that can be played locally.",
				string.Empty
			});


			GetUserInput();
			DownloadRedditPosts();
			DeleteBadFiles();
			ConvertVideos();

			Show(new[] {
				"All steps complete…",
				$"Go to \"{_directory}\" to see your files. Enjoy!"
			});
			Console.Beep();
			Console.ReadLine();
		}

		private static void GetUserInput()
		{
			Show(new[] { "Which subreddit would you like to scrape? (default \"funny\")" });
			_inputSubreddit = GetInput("subreddit", "funny");

			Show(new[] { "How many posts would you like to try to download? (default 25)" });
			if (!int.TryParse(GetInput("amount", "25"), out _amount)) _amount = 25;

			Show(new[]
			{
				"Time Period? (default \"All Time\")",
				"0 = All Time",
				"1 = Past Year",
				"2 = Past Month",
				"3 = Past Week",
				"4 = Past Day",
				"5 = Past Hour",
			});
			if (!Enum.TryParse(GetInput("from", "0"), out _time)) _time = FromTime.All;
		}

		private static void DownloadRedditPosts()
		{
			_directory = $@"C:\reddit\{_inputSubreddit}\";
			Show(new[] { $"Creating directory at \"{_directory}\"", string.Empty });

			Show(new[] { $"Looking on {_subreddit} for {_amount} posts…", string.Empty });
			_subreddit = new Reddit().GetSubreddit($"/r/{_inputSubreddit}");

			if (_subreddit == null)
			{
				Show(new[]
				{
					$"/r/{_inputSubreddit} doesn't appear to be a subreddit…",
					"Try again…",
					string.Empty
				});
				GetUserInput();
			}

			var foundPosts = _subreddit
				.GetTop(_time)
				.Take(_amount)
				.Select(x => x.Url.ToString());

			Show(new[] { $"Found {foundPosts.Count()} posts on {_subreddit}", string.Empty });

			Directory.CreateDirectory(_directory);
			System.Diagnostics.Process.Start(_directory);

			foreach(var foundPost in foundPosts)
			{
				DownloadImage(foundPost);
			}

			var files = Directory.GetFiles(_directory);
			Show(new[] { "\n", $"Downloaded {files.Length} files from  {_subreddit}…", string.Empty });
		}

		private static void DownloadImage(string imageURL)
		{
			FixImageUrl(ref imageURL);
			string fileName = $"{++_fileIndex}-{imageURL.Split('/').Last()}";
			string path = _directory + fileName;

			DownloadFile(new Uri(imageURL), path, fileName);
		}

		private static void FixImageUrl(ref string imageUrl)
		{
			switch (imageUrl)
			{
				case string url when url.Contains("gfycat.com"):
					imageUrl =  imageUrl.Replace("gfycat.com", "giant.gfycat.com") + ".webm";
					break;
				case string url when url.Contains(".gifv"):
					imageUrl = imageUrl.Replace(".gifv", ".gif");
					break;
			}
		}

		public static void DownloadFile(Uri uri, string destination, string fileName)
		{
			using (var wc = new WebClient())
			{
				wc.QueryString.Add("fileName", fileName);
				wc.DownloadFileCompleted += DownloadFileCompleted;
				wc.DownloadProgressChanged += DownloadProgressChanged;

				// Locking the thread and making a sync download into
				// a psuedo async one so we have access to the download events
				var syncObject = new object();
				lock (syncObject)
				{
					wc.DownloadFileAsync(uri, destination, syncObject);
					Monitor.Wait(syncObject);
				}
			}
		}

		private static void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			string loadedProgress = new string('#', (int) Math.Round(e.ProgressPercentage / 2.0));
			string unloadedProgress = new string('-', 50).Substring(0, 50 - loadedProgress.Length);

			string progressBar = $"<{loadedProgress}{unloadedProgress}>";

			string progressPercent = "000".Substring(0, 3 - e.ProgressPercentage.ToString().Length) + e.ProgressPercentage;

			string fileName = ((WebClient)sender).QueryString["fileName"];
			Console.WriteLine($"{fileName} - {progressPercent}% {progressBar}");
		}

		private static void DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
		{
			lock (e.UserState) { Monitor.Pulse(e.UserState);}
		}

		private static void DeleteBadFiles()
		{
			Show(new[]
			{
				"Attempt to clean up potionally corrupted files? (default yes)",
				"1 = Yes",
				"2 = No"
			});

			if (!int.TryParse(GetInput("clean", "1"), out int answer)) answer = 1;
			if (answer != 1) return;

			int badExtensionFiles = 0;
			int removedFiles = 0;
			int deletedFiles = 0;
			int brokenFiles = 0;

			var files = Directory.GetFiles(_directory);
			foreach (var file in files)
			{
				var fileInfo = new FileInfo(file);
				if (fileInfo != null)
				{
					if (fileInfo.Length == 0) brokenFiles++;
					if (fileInfo.Length == 503) removedFiles++;
					if (string.IsNullOrWhiteSpace(fileInfo.Extension) || fileInfo.Extension.Length > 6)
					{
						badExtensionFiles++;
					}

					if 
					(
						fileInfo.Length == 0 || 
						fileInfo.Length == 503 || 
						fileInfo.Extension.Length > 6 ||
						string.IsNullOrWhiteSpace(fileInfo.Extension)
					)
					{
						fileInfo.Delete();
						deletedFiles++;
					}
				}
			}
			Show(new[]
			{
				$"{brokenFiles} were improperly downloaded…",
				$"{removedFiles} are no longer availible…",
				$"{badExtensionFiles} have an unknown file extension…",
				string.Empty,
				$"{deletedFiles} files have been deleted overall…",
				$"{files.Length - deletedFiles} remain from {_subreddit}…",
				string.Empty,
			});
		}
		private static void ConvertVideos()
		{
			var files = Directory.GetFiles(_directory).Where(x => x.Contains("webm"));
			if (files.Any())
			{
				Show(new[]
				{
					"It appears there were some .webm files downloaded. These can be hard",
					"to play. Would you like to convert these videos? Short videos will be",
					"converted to .gifs and longer ones will be .mp4 (default Yes)",
					"1 = Yes",
					"2 = No"
				});

				if (!int.TryParse(GetInput("convert", "1"), out int answer)) answer = 1;
				if (answer != 1) return;

				Show(new[] { "Converting videos… This may take a couple of minutes…" });
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
					string outputFormat = fileInfo.Length < 2560000 ? Format.gif : Format.mp4;

					var newFileName = file.Replace(Format.webm, outputFormat);
					ffMpeg.ConvertMedia(file, newFileName, outputFormat);

					((outputFormat == Format.gif) ? ref gifCount : ref mp4Count) += 1;

					fileInfo.Delete();
					convertedVideoCount++;
				}
				Show(new[] 
				{
					"All videos have been converted…",
					$"{gifCount} gif's have been created…",
					$"{mp4Count}.mp4's have been created…"
				});

				new FileInfo("ffmpeg.exe").Delete();
				Console.WriteLine();
			}
		}
		private static string GetInput(string prompt, string defaultAnswer)
		{
			Console.Write(prompt + ": ");
			var input = Console.ReadLine();
			if (string.IsNullOrWhiteSpace(input))
			{
				Console.Write(defaultAnswer);
				Console.WriteLine();
				Console.WriteLine();
				return defaultAnswer;
			}
			Console.WriteLine();
			return input;
		}

		private static void Show(string[] texts)
		{
			if (_magOrCy)
			{
				_magOrCy = false;
				Console.ForegroundColor = ConsoleColor.Magenta;
			}
			else
			{
				_magOrCy = true;
				Console.ForegroundColor = ConsoleColor.Cyan;
			}
			foreach(var text in texts)
			{
				Console.WriteLine(text);
			}
			Console.ForegroundColor = ConsoleColor.White;
		}
	}
}