using System.Text;
using System.Text.Json;

namespace NRKPodcastFetcher
{
    class Program
    {
        private static readonly string PODLISTFILENAME = "PodcastsToUpdate.txt";
        private static readonly Dictionary<string, string> RequestHeaders = new Dictionary<string, string>
        {
            { "Accept", "*/*" },
            { "Accept-Language", "en-US,en;q=0.5" }
        };

        static async Task Main(string[] args)
        {
            // The default list of podcasts
            List<string> podnames =
            [
                "monsens_univers",
                // "berrum_beyer_snakker_om_greier",
                // "trygdekontoret",
                // "abels_taarn",
                // "hele_historien",
                // "loerdagsraadet",
                // "debatten",
                // "radio_moerch",
                // "baade_erlend_og_steinar_",
                // "radiodokumentaren",
            ];

            Console.WriteLine("Trying to read settings file");
            try
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string fullPathToPodnames = Path.Combine(baseDirectory, PODLISTFILENAME);

                if (File.Exists(fullPathToPodnames))
                {
                    Console.WriteLine($"Found {PODLISTFILENAME}. Using it to lookup podcasts.");
                    var lines = await File.ReadAllLinesAsync(fullPathToPodnames);
                    podnames = lines.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                }
                else
                {
                    Console.WriteLine($"Did not find any settings file in '{fullPathToPodnames}'");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to get dirname. '{e.Message}'");
            }

            // Process each podcast and generate an RSS file
            foreach (var pod in podnames)
            {
                Console.WriteLine($"Getting rss feed for '{pod}'");
                string baseUrl = $"https://psapi.nrk.no/radio/catalog/podcast/{pod}/episodes?pageSize=50&sort=desc&page=";

                string rssFeed = await CreateFeed(baseUrl);
                if (string.IsNullOrEmpty(rssFeed))
                {
                    Console.WriteLine($"RSS feed for '{pod}' was empty. Skipping...");
                    continue;
                }

                string outputFileName = $"{pod}.rss";
                await File.WriteAllTextAsync(outputFileName, rssFeed);

                Console.WriteLine($"Done with '{pod}'. Sleeping before starting next...");
                Thread.Sleep(2000); // 2 seconds
            }

            Console.WriteLine("All done!");
        }

        private static async Task<string> CreateFeed(string baseUrl)
        {
            // 1. Get a list of all episodes across pages
            var allEpisodes = await GetAllEpisodeItems(baseUrl, requestsPerSecond: 2);

            if (allEpisodes == null || allEpisodes.Count == 0)
            {
                return string.Empty;
            }

            // 2. Create an RSS header using the first episode for show metadata
            string header = CreateHeader(allEpisodes[0]._embedded.episodes[0]);

            // 3. Create all <item> elements
            string items = await CreateEpisodeItems([.. allEpisodes.SelectMany(x => x._embedded.episodes)]);

            // 4. Create footer
            string footer = CreateFooter();

            return header + items + footer;
        }

        /// <summary>
        /// Iterates over paged results until fewer than 50 episodes are returned.
        /// </summary>
        private static async Task<List<PodcastRoot>> GetAllEpisodeItems(string baseUrl, int requestsPerSecond)
        {
            bool moreToGet = true;
            int pageIndex = 1;
            // var allEpisodes = new List<JsonElement>();
            var allEpisodeModels = new List<PodcastRoot>();
            using (HttpClient client = new HttpClient())
            {
                // Set request headers
                foreach (var kvp in RequestHeaders)
                {
                    client.DefaultRequestHeaders.Remove(kvp.Key);
                    client.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
                }

                while (moreToGet)
                {
                    string url = baseUrl + pageIndex;
                    try
                    {
                        Console.WriteLine($"Fetching page {pageIndex}...");
                        var response = await client.GetAsync(url);
                        response.EnsureSuccessStatusCode();

                        string jsonStr = await response.Content.ReadAsStringAsync();

                        if (string.IsNullOrEmpty(jsonStr))
                        {
                            Console.WriteLine($"Empty response for page {pageIndex}. Skipping...");
                            break;
                        }
                        PodcastRoot? deserializedPodcast = JsonSerializer.Deserialize<PodcastRoot>(jsonStr);
                        if (deserializedPodcast == null)
                        {
                            Console.WriteLine($"Failed to deserialize JSON for page {pageIndex}. Skipping...");
                            break;
                        }
                        allEpisodeModels.Add(deserializedPodcast);
                        // using var doc = JsonDocument.Parse(jsonStr);
                        // var episodes = doc.RootElement
                        //                   .GetProperty("_embedded")
                        //                   .GetProperty("episodes");

                        // // Add to list
                        // foreach (var ep in episodes.EnumerateArray())
                        // {
                        //     allEpisodes.Add(ep);
                        // }

                        int numberOfEpisodes = deserializedPodcast._embedded.episodes.Count;
                        Console.WriteLine($"Page {pageIndex} returned {numberOfEpisodes} episodes.");

                        if (numberOfEpisodes < 50)
                        {
                            moreToGet = false;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error fetching page {pageIndex}: {ex.Message}");
                        break;
                    }

                    pageIndex++;

                    // Throttle requests
                    Thread.Sleep(1000 / requestsPerSecond);
                }
            }
            return allEpisodeModels;
        }

        private static async Task<string> CreateEpisodeItems(List<Episode> episodes)
        {
            Console.WriteLine("Getting playback urls...");
            var playbackUrls = await GetAllPlaybackUrls(episodes);
            return GenerateEpisodeRss(episodes, playbackUrls);
        }

        /// <summary>
        /// For each episode, calls the playback/manifest endpoint to get the actual MP3 URL.
        /// </summary>
        private static async Task<List<string>> GetAllPlaybackUrls(List<Episode> episodeList, int requestsPerSec = 2)
        {
            var playbackUrls = new List<string>();

            using (var client = new HttpClient())
            {
                foreach (var kvp in RequestHeaders)
                {
                    client.DefaultRequestHeaders.Remove(kvp.Key);
                    client.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
                }

                foreach (var ep in episodeList)
                {
                    string url = $"https://psapi.nrk.no/playback/manifest/podcast/{ep.episodeId}";
                    try
                    {
                        var response = await client.GetAsync(url);
                        response.EnsureSuccessStatusCode();

                        string jsonStr = await response.Content.ReadAsStringAsync();
                        ManifestRoot? deserializedManifest = JsonSerializer.Deserialize<ManifestRoot>(jsonStr);
                        if (deserializedManifest == null)
                        {
                            Console.WriteLine($"Failed to deserialize JSON for ID {ep.episodeId}. Skipping...");
                            playbackUrls.Add(string.Empty); // keep list aligned
                            continue;
                        }
                        playbackUrls.Add(deserializedManifest.playable.assets[0].url);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to get playback for ID {ep.episodeId}: {ex.Message}");
                    }

                    // Throttle
                    Thread.Sleep(1000 / requestsPerSec);
                }
            }

            return playbackUrls;
        }

        /// <summary>
        /// Converts the JSON from the playback call to the actual audio file URL
        /// </summary>
        // private static string GetPlaybackUrl(string jsonStr)
        // {
        //     using var doc = JsonDocument.Parse(jsonStr);
        //     return doc.RootElement
        //               .GetProperty("playable")
        //               .GetProperty("assets")[0]
        //               .GetProperty("url")
        //               .GetString();
        // }

        /// <summary>
        /// Converts pairs of (episode json, playback url) to EpisodeItem objects.
        /// </summary>
        // private static List<EpisodeItem> JsonToEpisodeItem(List<Episode> episodes, List<string> playbackList)
        // {
        //     var results = new List<EpisodeItem>();
        //     for (int i = 0; i < episodes.Count; i++)
        //     {
        //         var ep = episodes[i];
        //         string playbackUrl = playbackList.ElementAtOrDefault(i) ?? string.Empty;
        //         results.Add(new EpisodeItem(ep, playbackUrl));
        //     }
        //     return results;
        // }

        private static string GenerateEpisodeRss(List<Episode> episodes, List<string> playbackUrls)
        {
            StringBuilder rssStringBuilder = new();
            foreach (var zip in Enumerable.Zip(episodes, playbackUrls, (ep, contentUrl) => new { ep, contentUrl }))
            {
                rssStringBuilder.AppendLine($"""
        <item>
            <title>{zip.ep.titles.title.EscapeXmlCharacters()}</title>
            <description>{zip.ep.titles.subtitle.EscapeXmlCharacters()}</description>
            <pubDate>{zip.ep.date}</pubDate>
            <enclosure url="{zip.contentUrl.EscapeXmlCharacters()}" type="audio/mpeg" />
            <itunes:duration>{zip.ep.duration}</itunes:duration>
            {(string.IsNullOrEmpty(zip.ep.image.MaxBy(i => i.width)?.url) ? "" : $@"<itunes:image href=""{zip.ep.image.MaxBy(i => i.width)?.url}""/>")}
            <guid isPermaLink="false">{zip.contentUrl.Split("/").Last().Split("_").First()}</guid>
        </item>
        """);
            }

            return rssStringBuilder.ToString();
        }

        private static string CreateHeader(Episode firstEpisode)
        {

            string title = firstEpisode.originalTitle.EscapeXmlCharacters();
            // string title = GetShowTitle(firstEpisode);
            string showLink = firstEpisode._links.share.href.EscapeXmlCharacters();
            string? image = firstEpisode?.squareImage != null ? firstEpisode.squareImage.MaxBy(img => img?.width)?.url?.EscapeXmlCharacters() : "";
            // This is effectively the same as the Python create_header function
            return $"""
<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0"
     xmlns:itunes="http://www.itunes.com/dtds/podcast-1.0.dtd"
     xmlns:android="http://schemas.android.com/apk/res/android">
<channel>
    <title>CUSTOM: {title}</title>
    <link>{showLink}</link>
    <!--<description></description>-->
    <language>no</language>
    <copyright>NRK © 2022</copyright>
    <category>Comedy</category>
    <image>
        <title>{title}</title>
        <url>{image}</url>
        <link>{showLink}</link>
        <width>144</width>
        <height>144</height>
    </image>
""";
        }

        private static string CreateFooter()
        {
            return @"
</channel>
</rss>";
        }

        private static string GetShowTitle(JsonElement episode)
        {
            // Python: episode_json.get("originalTitle").strip().replace("&", "&amp;")
            // The NRK API: the top-level might have originalTitle
            // or is it in "titles"? 
            // We'll replicate your code's usage:
            if (episode.TryGetProperty("originalTitle", out JsonElement titleEl))
            {
                string t = titleEl.GetString() ?? "";
                t = t.Trim().Replace("&", "&amp;");
                return t;
            }
            return "";
        }

        private static string GetShowLink(JsonElement episode)
        {
            // In Python: get_episode_link(episode) => 
            //    ep.get("_links").get("share").get("href")
            // Then chop off last part
            string link = GetEpisodeLink(episode);
            if (string.IsNullOrEmpty(link)) return "";
            // same approach as Python:
            var parts = link.Split('/');
            if (parts.Length <= 1) return link;
            return string.Join("/", parts.Take(parts.Length - 1));
        }

        private static string GetEpisodeLink(JsonElement episode)
        {
            try
            {
                var share = episode.GetProperty("_links").GetProperty("share");
                if (share.TryGetProperty("href", out JsonElement hrefEl))
                {
                    return hrefEl.GetString() ?? "";
                }
            }
            catch { }
            return "";
        }

        private static string GetShowImage(JsonElement episode)
        {
            try
            {
                var arr = episode.GetProperty("squareImage");
                var last = arr[arr.GetArrayLength() - 1];
                return last.GetProperty("url").GetString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static string GetEpisodeId(JsonElement episode)
        {
            // Python: episode.get("episodeId")
            if (episode.TryGetProperty("episodeId", out JsonElement idEl))
            {
                return idEl.GetString() ?? "";
            }
            return "";
        }
    }


    public static class Extensions
    {
        public static string EscapeXmlCharacters(this string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input
                .Replace("&", "&amp;")
                .Replace("<", " &lt;")
                .Replace(">", "&gt;")
                .Replace("'", "&apos;")
                .Replace("\"", "&quot;");
        }
    }

    /// <summary>
    /// C# equivalent of the EpisodeItem class
    /// </summary>
    public class EpisodeItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string DatetimeString { get; set; }
        public string EpisodeImageUrl { get; set; }
        public string ContentUrl { get; set; }
        public int ContentLength { get; set; }
        public string Guid { get; set; }
        public int Bitrate { get; set; }
        public string DurationStr { get; set; }

        public EpisodeItem(JsonElement ep, string playbackUrl)
        {
            // Python: 
            // 1) get("titles").get("title") => ep["titles"]["title"]
            // 2) get("titles").get("subtitle") => ep["titles"]["subtitle"]
            // 3) date => ep["date"]
            // 4) squareImage => ep["squareImage"][-1]["url"]
            // 5) content_url => playback_url
            // 6) guid => snippet from content_url
            // 7) bitrate => 192_000 if "192" else 128_000
            // 8) duration_str => ep["duration"]

            Title = SafeReplace(SafeGetTitle(ep));
            Description = SafeReplace(SafeGetSubtitle(ep));
            DatetimeString = SafeGetDateString(ep);
            EpisodeImageUrl = SafeGetImageUrl(ep);
            ContentUrl = playbackUrl;
            // content length could be computed or looked up, skipping here
            ContentLength = 0;
            Guid = GetGuidFromUrl(ContentUrl);
            Bitrate = ContentUrl != null && ContentUrl.Contains("192") ? 192000 : 128000;
            DurationStr = SafeGetDuration(ep);
        }

        private string SafeGetTitle(JsonElement ep)
        {
            try
            {
                return ep.GetProperty("titles").GetProperty("title").GetString() ?? "";
            }
            catch { return ""; }
        }

        private string SafeGetSubtitle(JsonElement ep)
        {
            try
            {
                return ep.GetProperty("titles").GetProperty("subtitle").GetString() ?? "";
            }
            catch { return ""; }
        }

        private string SafeGetDuration(JsonElement ep)
        {
            // ep["duration"] in Python
            try
            {
                if (ep.TryGetProperty("duration", out JsonElement dur))
                {
                    return dur.GetString() ?? "";
                }
            }
            catch { }
            return "";
        }

        private string SafeGetDateString(JsonElement ep)
        {
            // Python: itemjson.get("date")
            try
            {
                if (ep.TryGetProperty("date", out JsonElement dateEl))
                {
                    string dateVal = dateEl.GetString() ?? "";
                    return dateVal;
                }
            }
            catch { }
            return "";
        }

        private string SafeGetImageUrl(JsonElement ep)
        {
            try
            {
                var arr = ep.GetProperty("squareImage");
                var last = arr[arr.GetArrayLength() - 1];
                return last.GetProperty("url").GetString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private string GetGuidFromUrl(string contentUrl)
        {
            if (string.IsNullOrEmpty(contentUrl)) return "";
            // In Python: content_url.split("/")[-1].split("_0_")[0].split(".mp3")[0]
            try
            {
                string[] parts = contentUrl.Split('/');
                string lastSegment = parts[parts.Length - 1];
                string[] underscoreSplit = lastSegment.Split(new string[] { "_0_" }, StringSplitOptions.None);
                string leftPart = underscoreSplit[0];
                // strip ".mp3" from leftPart
                return leftPart.Replace(".mp3", "");
            }
            catch
            {
                return "";
            }
        }

        private string SafeReplace(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input
                .Replace("&", "&amp;")
                .Replace("<", " &lt;")
                .Replace(">", "&gt;")
                .Replace("'", "&apos;")
                .Replace("\"", "&quot;");
        }
    }
}
