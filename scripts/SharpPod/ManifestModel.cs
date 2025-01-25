// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Asset
    {
        public string url { get; set; }
        public string format { get; set; }
        public string mimeType { get; set; }
        public bool encrypted { get; set; }
        public string encryptionScheme { get; set; }
    }

    public class Availability
    {
        public string information { get; set; }
        public bool isGeoBlocked { get; set; }
        public OnDemand onDemand { get; set; }
        public object live { get; set; }
        public bool externalEmbeddingAllowed { get; set; }
    }

    public class Config
    {
        public string beacon { get; set; }
    }

    public class Data
    {
        public string title { get; set; }
        public string device { get; set; }
        public string playerId { get; set; }
        public string deliveryType { get; set; }
        public string playerInfo { get; set; }
        public string cdnName { get; set; }
    }

    public class Ga
    {
        public string dimension1 { get; set; }
        public string dimension2 { get; set; }
        public string dimension3 { get; set; }
        public string dimension4 { get; set; }
        public string dimension5 { get; set; }
        public string dimension10 { get; set; }
        public string dimension21 { get; set; }
        public string dimension22 { get; set; }
        public string dimension23 { get; set; }
        public string dimension25 { get; set; }
        public string dimension26 { get; set; }
        public string dimension29 { get; set; }
        public string dimension36 { get; set; }
    }

    public class Links
    {
        public Self self { get; set; }
        public Metadata metadata { get; set; }
    }

    public class Luna
    {
        public Config config { get; set; }
        public Data data { get; set; }
    }

    public class Metadata
    {
        public string href { get; set; }
        public string name { get; set; }
    }

    public class OnDemand
    {
        public DateTime from { get; set; }
        public DateTime to { get; set; }
        public bool hasRightsNow { get; set; }
    }

    public class Playable
    {
        public object endSequenceStartTime { get; set; }
        public string duration { get; set; }
        public List<Asset> assets { get; set; }
        public object liveBuffer { get; set; }
        public List<object> subtitles { get; set; }
        public List<object> thumbnails { get; set; }
    }

    public class QualityOfExperience
    {
        public string clientName { get; set; }
        public string cdnName { get; set; }
        public string streamingFormat { get; set; }
        public string segmentLength { get; set; }
        public string assetType { get; set; }
        public string correlationId { get; set; }
    }

    public class ManifestRoot
    {
        public Links _links { get; set; }
        public string id { get; set; }
        public string playability { get; set; }
        public string streamingMode { get; set; }
        public Availability availability { get; set; }
        public Statistics statistics { get; set; }
        public Playable playable { get; set; }
        public object nonPlayable { get; set; }
        public object displayAspectRatio { get; set; }
        public string sourceMedium { get; set; }
    }

    public class Self
    {
        public string href { get; set; }
    }

    public class Snowplow
    {
        public string source { get; set; }
    }

    public class Statistics
    {
        public object scores { get; set; }
        public Ga ga { get; set; }
        public object conviva { get; set; }
        public Luna luna { get; set; }
        public QualityOfExperience qualityOfExperience { get; set; }
        public Snowplow snowplow { get; set; }
    }

