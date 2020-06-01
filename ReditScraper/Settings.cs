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
		public string CollectionPath { get => $@"{Destination}\!\"; }


		public void GetSettings()
        {
			if (File.Exists("RedditScraper-Settings.xml"))
			{
				var document = new XmlDocument();
				document.Load("RedditScraper-Settings.xml");

				XmlNodeList subredditNodes = document.GetElementsByTagName("subreddit");
				Subreddits = new string[subredditNodes.Count];
				for (var i = 0; i < subredditNodes.Count; i++)
				{
					Subreddits[i] = subredditNodes.Item(i).InnerText;
				}

				string destination = document.SelectSingleNode("//settings/destination")?.InnerText;
				Destination = !string.IsNullOrWhiteSpace(destination) ? destination : Environment.ExpandEnvironmentVariables(@"%userprofile%\Downloads\RedditScraper");
				bool.TryParse(document.SelectSingleNode("//settings/shouldCollect")?.InnerText, out bool shouldCollect);
				int.TryParse(document.SelectSingleNode("//settings/amount")?.InnerText, out int amount);
				int.TryParse(document.SelectSingleNode("//settings/time")?.InnerText, out int fromTime);
				ImgurApiKey = document.SelectSingleNode("//settings/imgurApiKey")?.InnerText;
				Amount = Math.Max(Amount, amount);
				FromTime = (FromTime)fromTime;
				ShouldCollect = shouldCollect;
			}
			else
			{
				var settings = new XmlWriterSettings()
				{
					Indent = true,
					IndentChars = "	"
				};

				using (var writer = XmlWriter.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RedditScraper-Settings.xml"), settings))
				{
					writer.WriteStartDocument();
					writer.WriteComment("This file is used to configure RedditScraper. Edit the elements below");
					writer.WriteComment("in order to get the posts you are looking for.");
					writer.WriteStartElement("settings");

						writer.WriteComment("Full file path where posts are downloaded."); 
						writer.WriteComment("E.g., C:\\Users\\Admin\\Documents. Default to your user downloads folder");
						writer.WriteStartElement("destination");
						writer.WriteFullEndElement();

						writer.WriteComment("Amount to download per subreddit.");
						writer.WriteStartElement("amount");
						writer.WriteString("10");
						writer.WriteFullEndElement();

						writer.WriteComment("Time period to download posts from."); 
						writer.WriteComment("All = 0, Year = 1, Month = 2, Week = 3, Day = 4, Hour = 5");
						writer.WriteStartElement("time");
						writer.WriteString("2");
						writer.WriteFullEndElement();

						writer.WriteStartElement("subreddits");

							writer.WriteComment("Name of subreddit. Copy this for each subreddit");
							writer.WriteStartElement("subreddit");
							writer.WriteString("astrophotography");
							writer.WriteFullEndElement();							
							writer.WriteStartElement("subreddit");
							writer.WriteString("Cinemagraphs");
							writer.WriteFullEndElement();
							writer.WriteStartElement("subreddit");
							writer.WriteString("DesignPorn");
							writer.WriteFullEndElement();
							writer.WriteStartElement("subreddit");
							writer.WriteString("earthporn");
							writer.WriteFullEndElement();
							writer.WriteStartElement("subreddit");
							writer.WriteString("Eyebleach");
							writer.WriteFullEndElement();
							writer.WriteStartElement("subreddit");
							writer.WriteString("ExposurePorn");
							writer.WriteFullEndElement();
							writer.WriteStartElement("subreddit");
							writer.WriteString("itookapicture");
							writer.WriteFullEndElement();							
							writer.WriteStartElement("subreddit");
							writer.WriteString("oldschoolcool");
							writer.WriteFullEndElement();							

						writer.WriteEndElement();

						writer.WriteComment("Create a collection of these posts in one folder");
						writer.WriteStartElement("shouldCollect");
						writer.WriteString("true");
						writer.WriteFullEndElement();

						writer.WriteComment("API key for ensuring posts from imgur work. Can be left blank.");
						writer.WriteStartElement("imgurApiKey");
						writer.WriteFullEndElement();

					writer.WriteEndDocument();

					writer.Flush();
				}
				GetSettings();
			}
		}
    }
}
