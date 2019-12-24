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
		private static string _inputSubreddit;
		private static Subreddit _subreddit;
        private static string _directory;
		private static int _fileIndex;
		private static FromTime _time;
		private static bool _magOrCy;
		private static int _amount;
		private static int _color;
        #endregion

        private static void Main(string[] args)
		{
            Intro();

            GetUserInput(args);

            DownloadRedditPosts();

            DeleteBadFiles();

			Show(new[] {
				"All steps complete...",
				$"Go to \"{_directory}\" to see your files. Enjoy!",
			});

            PromptForReset();
        }

		private static void Intro()
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
				@"computer.",
				string.Empty
			});

			if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
			{
				Show(new[] { "Internet connection not availible, dawg", "Try again, later." });
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
                        if (!Enum.TryParse(args[i+1], out _time)) _time = FromTime.Week;
                    }
                }
            }

            // The Subreddit we are looking at
            if (string.IsNullOrWhiteSpace(_inputSubreddit))
            {
			    Show(new[] { "Which subreddit would you like to scrape? [EarthPorn]" });
			    _inputSubreddit = GetInput("subreddit: ", "EarthPorn");
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
				    "Time Period? [Week]",
				    "0 = All Time",
				    "1 = Past Year",
				    "2 = Past Month",
				    "3 = Past Week",
				    "4 = Past Day",
				    "5 = Past Hour",
			    });
			    if (Enum.TryParse(GetInput("from: ", "3"), out _time))
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
				Show(new[] { "404: subreddit not found" });
			}

            // If the subreddit wasn't found, try again
			if (_subreddit == null)
			{
				Show(new[]
				{
					$"/r/{_inputSubreddit} doesn't appear to be a subreddit...",
					"Try again...",
				});
                _inputSubreddit = string.Empty;
                _amount = 0;
				GetUserInput(new string[0]);
				DownloadRedditPosts();
				return;
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

            Show(new[] { $"Found {redditPosts.Count()} posts on {_subreddit}" });

            // Create and show a directory that matches the subreddit we are searching
			_directory = $@"C:\reddit\{_inputSubreddit}\";
			Show(new[] { $"Creating directory at \"{_directory}\"" });
			Directory.CreateDirectory(_directory);
			System.Diagnostics.Process.Start(_directory);

			Show(new[] { $"Downloading files from {_subreddit}" });

			foreach(var post in redditPosts)
			{
				DownloadImage(post);
			}

			Console.Beep();
			Show(new[] {"",$"Downloaded {_fileIndex} files from  {_subreddit}..."});
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
			//Limit file names to 225, replace HTML escape characters, remove excess white space, and remove emojis
			fileName = WebUtility.HtmlEncode(fileName);
			fileName = Regex.Replace(fileName, @"&#[0-9]{5,};", "");
			fileName = WebUtility.HtmlDecode(fileName);
			fileName = fileName.Replace("&amp;", "&");
			fileName = Regex.Replace(fileName, @"\s{2,}", " ").Trim();
			fileName = fileName.Substring(0, Math.Min(225, fileName.Length));

			// e.g., "2020-01-15 10-File Name.jpg
			fileName = DateTime.Now.ToString("yyyy-MM-dd")
				+ " " + ++_fileIndex + "-" + fileName.Replace(".", "").Trim()
				+ "." + fileExtension;

			foreach(char c in Path.GetInvalidFileNameChars())
			{
				fileName = fileName.Replace(c.ToString(), "");
			}
		}

		// Needed to overcome url changes which happen automatically when browsing
		// but not when web crawling
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
				var length = fileInfo.Length;
				var exten = fileInfo.Extension;

                if (fileInfo == null) return;
				if (length == 0) brokenFiles++;
				if (length == 503) removedFiles++;
				if (string.IsNullOrWhiteSpace(exten) || exten.Length > 6 || exten.Contains("com"))
				{
					badExtensionFiles++;
				}

				if (length == 0 || length == 503 || exten.Length > 6 ||string.IsNullOrWhiteSpace(exten) ||exten.Contains("com"))
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
			});
		}

        private static void PromptForReset()
        {
            if (!Confirm(new[] { "Download More? [Y/n]" })) return;

			_inputSubreddit = default;
			_fileIndex = default;
            _amount = default;

            Main(new string[] { });
        }

        #region Console Helpers
        // Ask user for input. If the answer wasn't given, return the default value.
        private static string GetInput(string prompt, string defaultAnswer)
		{
			ReturnPrompt();
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
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.White;
		}

        // Prompt user for a boolean value. Not letting anything other than y/n/enter be entered.
		private static bool Confirm(string[] prompt)
		{
			ConsoleKey res;
			do
			{
				Show(prompt);
				ReturnPrompt();
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
        #endregion

        #region Event Handlers
        // Write to console when a download progress event is triggered. Show filename and progress bar.
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
        #endregion
    }
}