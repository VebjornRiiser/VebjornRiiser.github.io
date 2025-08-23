using System.Text.Json;

namespace NRKPodcastFetcher
{
    /// <summary>
    /// C# equivalent of the EpisodeItem class
    /// </summary>
    public class EpisodeItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string DatetimeString { get; set; }
        public string EpisodeImageUrl { get; set; }
        public string? ContentUrl { get; set; }
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
