namespace NRKPodcastFetcher;
// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Availability
    {
        public string status { get; set; }
        public bool hasLabel { get; set; }
    }

    public class Badge
    {
        public string label { get; set; }
        public string type { get; set; }
    }

    public class Category
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class Embedded
    {
        public List<Episode> episodes { get; set; }
    }

    public class Episode
    {
        public Links _links { get; set; }
        public string id { get; set; }
        public string episodeId { get; set; }
        public Titles titles { get; set; }

        /// <summary>
        /// The title of the whole podcast
        /// </summary>
        public string originalTitle { get; set; }
        /// <summary>
        /// Episode image
        /// </summary>
        public List<Image> image { get; set; }
        /// <summary>
        /// Whole podcast image
        /// </summary>
        public List<SquareImage> squareImage { get; set; }
        public string duration { get; set; }
        public DateTime date { get; set; }
        public string clipId { get; set; }
        public int durationInSeconds { get; set; }
        public UsageRights usageRights { get; set; }
        public Availability availability { get; set; }
        public List<Badge> badges { get; set; }
        public Category category { get; set; }
        /// <summary>
        /// Needs to be fetched another endpoint
        /// </summary>
        public string ContentUrl { get; set; }
    }

    public class Favourite
    {
        public string href { get; set; }
        public bool templated { get; set; }
    }

    public class From
    {
        public DateTime date { get; set; }
        public string displayValue { get; set; }
    }

    public class GeoBlock
    {
        public bool isGeoBlocked { get; set; }
        public string displayValue { get; set; }
    }

    public class Image
    {
        public string url { get; set; }
        public int width { get; set; }
    }

    public class Links
    {
        public Self self { get; set; }
        public List<Progress> progresses { get; set; }
        public Playback playback { get; set; }
        public Series series { get; set; }
        public Season season { get; set; }
        public Favourite favourite { get; set; }
        public Share share { get; set; }
        public Progress progress { get; set; }
        public Recommendations recommendations { get; set; }
    }

    public class Playback
    {
        public string href { get; set; }
    }

    public class Progress
    {
        public string href { get; set; }
        public bool templated { get; set; }
    }

    public class Progress2
    {
        public string href { get; set; }
        public bool templated { get; set; }
    }

    public class Recommendations
    {
        public string href { get; set; }
        public bool templated { get; set; }
    }

    public class PodcastRoot
    {
        public Links _links { get; set; }
        public string seriesType { get; set; }
        public Embedded _embedded { get; set; }
    }

    public class Season
    {
        public string name { get; set; }
        public string title { get; set; }
        public string href { get; set; }
        public string seriesType { get; set; }
    }

    public class Self
    {
        public string href { get; set; }
    }

    public class Series
    {
        public string name { get; set; }
        public string href { get; set; }
        public string title { get; set; }
    }

    public class Share
    {
        public string href { get; set; }
    }

    public class SquareImage
    {
        public string url { get; set; }
        public int width { get; set; }
    }

    public class Titles
    {
        public string title { get; set; }
        public string subtitle { get; set; }
    }

    public class To
    {
        public DateTime date { get; set; }
        public string displayValue { get; set; }
    }

    public class UsageRights
    {
        public From from { get; set; }
        public To to { get; set; }
        public GeoBlock geoBlock { get; set; }
    }

