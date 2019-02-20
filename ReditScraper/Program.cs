using NReco.VideoConverter;
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
        #region Field Variables
        private static string _inputSubreddit;
		private static Subreddit _subreddit;
        private static string _directory;
        private static int _fileIndex;
		private static FromTime _time;
		private static bool _magOrCy;
		private static int _amount;
		private static int _color;
        #endregion

        public static void Main(string[] args)
		{
            Intro();
            GetUserInput(args);
            DownloadRedditPosts();
            DeleteBadFiles();
			ConvertVideos();

			Show(new[] {
				"All steps complete...",
				$"Go to \"{_directory}\" to see your files. Enjoy!"
			});
			Console.Beep();
			Console.ReadLine();
		}

		public static void Intro()
		{
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

			if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
			{
				Show(new[] { "Internet connection not availible, dawg", string.Empty });
				Show(new[] { "Try again, later."});
				Console.ReadLine();
				return;
			}
		}

		private static void GetUserInput(string[] args)
		{
            // Check for command line arguments passed
            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (!(i + 1 < args.Length)) break;

                    // Subreddit
                    if (args[i].Equals("-sr"))
                    {
                        _inputSubreddit = args[i + 1];
                    }

                    // Amount
                    if (args[i].Equals("-a"))
                    {
                        if (!int.TryParse(args[i + 1], out _amount)) _amount = 25;
                    }

                    // Time frame
                    if (args[i].Equals("-t"))
                    {
                        if (!Enum.TryParse(args[i+1], out _time)) _time = FromTime.All;
                    }
                }
            }

            // The Subreddit we are looking at
            if (string.IsNullOrWhiteSpace(_inputSubreddit))
            {
			    Show(new[] { "Which subreddit would you like to scrape? [funny]" });
			    _inputSubreddit = GetInput("subreddit: ", "funny");
            }

            // The amount to download
            if (_amount == default)
            {
			    Show(new[] { "How many posts would you like to try to download? [25]" });
			    if (!int.TryParse(GetInput("amount: ", "25"), out _amount)) _amount = 25;
            }

            // The time line we are looking at
            if (!args.Any(x => x.Equals("-t")))
            {
			    Show(new[]
			    {
				    "Time Period? [All Time]",
				    "0 = All Time",
				    "1 = Past Year",
				    "2 = Past Month",
				    "3 = Past Week",
				    "4 = Past Day",
				    "5 = Past Hour",
			    });
			    if (Enum.TryParse(GetInput("from: ", "0"), out _time))
			    {
				    Console.SetCursorPosition(6, Console.CursorTop - 2);
			    }
			    Console.WriteLine(_time);
			    Console.WriteLine();
            }
		}

		private static void DownloadRedditPosts()
		{
			try
			{
				_subreddit = new Reddit().GetSubreddit($"/r/{_inputSubreddit}");
            }
            catch (WebException)
			{
				Show(new[] { "404: subreddit not found", string.Empty });
			}

            // If the subreddit wasn't found, try again
			if (_subreddit == null)
			{
				Show(new[]
				{
					$"/r/{_inputSubreddit} doesn't appear to be a subreddit...",
					"Try again...",
					string.Empty
				});
                _inputSubreddit = string.Empty;
                _amount = 0;
				GetUserInput(new string[0]);
				DownloadRedditPosts();
			}

            var caller = new RedditPostCaller(GetRedditPosts);
            IAsyncResult result = caller.BeginInvoke(null, null);

            Console.ForegroundColor = _magOrCy
                ? ConsoleColor.Magenta
                : ConsoleColor.Cyan;
            _magOrCy = !_magOrCy;

            Console.Write($"Looking on {_subreddit} for {_amount} posts...");
            Console.ForegroundColor = ConsoleColor.White;
            while (!result.IsCompleted)
            {
                Spinner.Turn();
                Thread.Sleep(100);
            }
            var redditPosts = caller.EndInvoke(result);
            Console.WriteLine("\n");

            Show(new[] { $"Found {redditPosts.Count()} posts on {_subreddit}", string.Empty });

            // Create and show a directory that matches the subreddit we are searching
			_directory = $@"C:\reddit\{_inputSubreddit}\";
			Show(new[] { $"Creating directory at \"{_directory}\"", string.Empty });
			Directory.CreateDirectory(_directory);
			System.Diagnostics.Process.Start(_directory);

			Show(new[] { $"Downloading files from {_subreddit}", string.Empty });
			foreach(var foundPost in redditPosts)
			{
				DownloadImage(foundPost);
			}

			var files = Directory.GetFiles(_directory);
			Show(new[] 
			{
				string.Empty,
				$"Downloaded {files.Length} files from  {_subreddit}...",
				string.Empty
			});
		}

        private delegate IEnumerable<string> RedditPostCaller();
        private static IEnumerable<string> GetRedditPosts()
        {
            return _subreddit
                .GetTop(_time)
                .Take(_amount)
                .Select(x => x.Url.ToString())
                .ToList();
        }

		private static void DownloadImage(string imageURL)
		{
			FixImageUrl(ref imageURL);
			string fileName = $"{++_fileIndex}-{imageURL.Split('/').Last()}";
			string path = _directory + fileName;

			DownloadFile(new Uri(imageURL), path, fileName);
		}

        // Needed to overcome url changes which happen automatically when browsing
        // but not when web crawling
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
				wc.DownloadProgressChanged += UpdateDownloadProgress;

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

        private static void DeleteBadFiles()
		{
			int badExtensionFiles = 0;
			int removedFiles = 0;
			int deletedFiles = 0;
			int brokenFiles = 0;

            var filesToDelete = new List<string>();
			var files = Directory.GetFiles(_directory);
			foreach (var file in files)
			{
				var fileInfo = new FileInfo(file);
                if (fileInfo == null) return;
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
                    filesToDelete.Add(file);
                }
            }

            if (!filesToDelete.Any()) return;

            Show(new[]
            {
                "Looking through the files downloaded, there were:",
                $"{brokenFiles} were improperly downloaded...",
                $"{removedFiles} are no longer availible...",
                $"{badExtensionFiles} have an unknown file extension...",
				string.Empty,
            });

            if (!Confirm(new[] { "Remove corrupted files? [Y/n]" })) return;

            filesToDelete.ForEach(x =>
            {
                new FileInfo(x).Delete();
                deletedFiles++;
            });


            Show(new[]
			{
                string.Empty,
				$"{deletedFiles} files have been deleted overall...",
				$"{files.Length - deletedFiles} remain from {_subreddit}...",
				string.Empty,
			});
		}

		private static void ConvertVideos()
		{
			var files = Directory.GetFiles(_directory).Where(x => x.Contains("webm"));
			if (files.Any())
			{
				if (!Confirm(new[]
				{
					"It appears there were some .webm files downloaded. These can be hard",
					"to play. Would you like to convert these videos? Short videos will be",
					"converted to .gifs and longer ones will be .mp4 [Y/n]",
				})) return;

				int gifCount = 0;
				int mp4Count = 0;
				int convertedVideoCount = 1;

				var ffMpeg = new FFMpegConverter();
                ffMpeg.ConvertProgress += UpdateConversionProgress;

                // Sort files by name, using int values and not string ones, so '10' comes before '2'
				files = files.OrderBy(x => int.Parse(Path.GetFileName(x.Split('-')[0])));

				Show(new[] { "Converting videos... This may take a couple of minutes...", string.Empty });
				foreach (var file in files)
				{
					var fileInfo = new FileInfo(file);
					string outputFormat = fileInfo.Length < 2560000 ? Format.gif : Format.mp4;
					string newFileName = file.Replace(Format.webm, outputFormat);

                    string fileName = Path.GetFileName(file);
                    fileName = fileName.Length + 67 >= Console.WindowWidth ? $"{fileName.Substring(0, Math.Max(0, Console.WindowWidth - 75))}..." : fileName;

                    Console.Write($"({convertedVideoCount} of {files.Count()}) {fileName} {new string(' ', 57)}");
                    try
                    {
					    ffMpeg.ConvertMedia(file, newFileName, outputFormat);

                        Console.ForegroundColor = FlipColors();
                        Console.WriteLine();

                        // Increment one of these depending on the format we are converting the file to. Cool feature of C# 7.0+
                        (outputFormat == Format.gif ? ref gifCount : ref mp4Count) += 1;
                        fileInfo.Delete();
					    convertedVideoCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.Write(ex.Message);
                    }
				}
				Show(new[]
				{
					string.Empty,
					"All videos have been converted...",
					$"{gifCount} gif's have been created...",
					$"{mp4Count}.mp4's have been created...",
					string.Empty
				});

                // Delete the ffmpeg binary file that was extracted as it's not needed anymore.
				new FileInfo("ffmpeg.exe").Delete();
				Console.WriteLine();
			}
		}

        #region Console Helpers
        // Ask user for input. If the answer wasn't given, return the default value.
        private static string GetInput(string prompt, string defaultAnswer)
		{
			Console.Write(prompt);
			var input = Console.ReadLine();
			if (string.IsNullOrWhiteSpace(input))
			{
				Console.SetCursorPosition(prompt.Length, Console.CursorTop - 1);
				Console.Write(defaultAnswer);
				Console.WriteLine();
				Console.WriteLine();
				return defaultAnswer;
			}
			Console.WriteLine();
			return input;
		}

        // Alternate colors for the console then return the color to white
		private static void Show(string[] texts)
		{
			Console.ForegroundColor = _magOrCy 
				? ConsoleColor.Magenta 
				: ConsoleColor.Cyan;
			_magOrCy = !_magOrCy;

			foreach (var text in texts)
			{
				Console.WriteLine(text);
			}
			Console.ForegroundColor = ConsoleColor.White;
		}

        // Prompt user for a boolean value. Not letting anything other than y/n/enter be entered.
		public static bool Confirm(string[] prompt)
		{
			ConsoleKey res;
			do
			{
				Show(prompt);
				res = Console.ReadKey(false).Key;
				if (res != ConsoleKey.Enter)
				{
					Console.WriteLine();
				}
			} while (res != ConsoleKey.Y && res != ConsoleKey.N && res != ConsoleKey.Enter);

			Console.WriteLine(res == ConsoleKey.Y || res == ConsoleKey.Enter ? "yes" : "no");
			Console.WriteLine();
			return (res == ConsoleKey.Y || res == ConsoleKey.Enter);
		}

        // Toggle between three colors.
		public static ConsoleColor FlipColors()
		{
			_color++;
			switch (_color % 3)
			{
				case 1: return ConsoleColor.Cyan;
				case 2: return ConsoleColor.Magenta;
				default: return ConsoleColor.White;
			}
		}

        public static class Spinner
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
        #endregion

        #region Event Handlers
        // Write to console when a download progress event is triggered. Show filename and progress bar.
        private static void UpdateDownloadProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            int progress = e.ProgressPercentage;
            string progressBar = MakeProgressBar(progress);

            string fileName = ((WebClient)sender).QueryString["fileName"];
            Console.Write($"\r{fileName} - {progressBar}");
        }

        // Display how far we are in the file conversion process.
        private static void UpdateConversionProgress(object sender, ConvertProgressEventArgs e)
        {
            int progress = (int)Math.Round((double)e.Processed.Ticks / e.TotalDuration.Ticks * 100);
            string progressBar = MakeProgressBar(progress);

            Console.SetCursorPosition(Console.CursorLeft - Math.Min(Console.CursorLeft, progressBar.Length), Console.CursorTop);
            Console.Write(progressBar);
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
        #endregion
    }
}