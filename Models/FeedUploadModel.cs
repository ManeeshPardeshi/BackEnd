namespace BackEnd.Models
{
    public class FeedUploadModel
    {
        public string UserId { get; set; }
        public string Description { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; } // File size to ensure it’s under 0.5 GB

        public IFormFile File { get; set; }
    }
}
