using RedditSharp.Things;
using System;
using System.IO;
using System.Xml;

namespace RedditScraper
{
    public class Settings
    {
		public Settings() => GetSettings();
        public string Destination { get; set; }
        public string ImgurApiKey { get; set; }
        public string[] Subreddits { get; set; }
		public FromTime FromTime { get; set; }
		public int Amount { get; set; } = 1;
		public bool ShouldCollect { get; set; }
		public string CollectionPath { get => $@"{Destination}\Today\"; }


		public void GetSettings()
        {
			if (File.Exists("settings.xml"))
			{
				var document = new XmlDocument();
				document.Load("settings.xml");

				XmlNodeList subredditNodes = document.GetElementsByTagName("subreddit");
				Subreddits = new string[subredditNodes.Count];
				for (var i = 0; i < subredditNodes.Count; i++)
				{
					Subreddits[i] = subredditNodes.Item(i).InnerText;
				}
				Destination = document.SelectSingleNode("//settings/destination")?.InnerText ?? Environment.ExpandEnvironmentVariables(@"%userprofile%\Downloads\RedditScraper");
				ImgurApiKey = document.SelectSingleNode("//settings/imgurApiKey")?.InnerText ?? "";
				int.TryParse(document.SelectSingleNode("//settings/amount")?.InnerText, out int amount);
				int.TryParse(document.SelectSingleNode("//settings/time")?.InnerText, out int fromTime);
				bool.TryParse(document.SelectSingleNode("//settings/shouldCollect")?.InnerText, out bool shouldCollect);
				Amount = Math.Max(Amount, amount);
				FromTime = (FromTime)fromTime;
				ShouldCollect = shouldCollect;
			}
			else
			{
				var settings = new XmlWriterSettings()
				{
					Indent = true,
					IndentChars = "    "
				};

				using (var writer = XmlWriter.Create("settings.xml", settings))
				{
					writer.WriteStartDocument();
					writer.WriteStartElement("settings");

						writer.WriteComment("Full file path where posts are downloaded. E.g., C:\\user\\admin\\documents. Default to downloads folder");
						writer.WriteStartElement("destination");
						writer.WriteFullEndElement();

						writer.WriteComment("Amount to download per subreddit.");
						writer.WriteStartElement("amount");
						writer.WriteFullEndElement();

						writer.WriteComment("Time period to download from. All = 0, Year = 1, Month = 2, Week = 3, Day = 4, Hour = 5");
						writer.WriteStartElement("time");
						writer.WriteFullEndElement();

						writer.WriteStartElement("subreddits");

							writer.WriteComment("Name of subreddit. Copy this for each subreddit");
							writer.WriteStartElement("subreddit");
							writer.WriteFullEndElement();

						writer.WriteEndElement();

						writer.WriteComment("Create a collection of these posts in one folder");
						writer.WriteStartElement("shouldCollect");
						writer.WriteFullEndElement();

						writer.WriteComment("API key from imgur... can be blank");
						writer.WriteStartElement("imgurApiKey");
						writer.WriteFullEndElement();

					writer.WriteEndDocument();

					writer.Flush();
				}
			}
		}
    }
}
