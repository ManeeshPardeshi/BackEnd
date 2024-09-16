using Newtonsoft.Json;
using System;

namespace BackEnd.Entities
{
    public class Feed
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "userId")]
        public string UserId { get; set; }

        [JsonProperty(PropertyName = "feedUrl")]
        public string FeedUrl { get; set; }

        [JsonProperty(PropertyName = "description")]
        public string Description { get; set; }

        [JsonProperty(PropertyName = "uploadDate")]
        public DateTime UploadDate { get; set; }
    }
}
