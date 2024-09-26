using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using BackEnd.Entities;
using BackEnd.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace BackEnd.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FeedsController : ControllerBase
    {
        private readonly CosmosDbContext _dbContext;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ServiceBusSender _serviceBusSender;
        private readonly string _feedContainer = "media";  // Blob container for storing feeds

        public FeedsController(CosmosDbContext dbContext, BlobServiceClient blobServiceClient, ServiceBusClient serviceBusClient)
        {
            _dbContext = dbContext;
            _blobServiceClient = blobServiceClient;
            _serviceBusSender = serviceBusClient.CreateSender("new-feed-notifications");
        }

        /// <summary>
        /// Upload a new feed with media.
        /// </summary>
        [HttpPost("uploadFeed")]
        public async Task<IActionResult> UploadFeed([FromForm] FeedUploadModel model)
        {
            try
            {
                // Ensure required fields are present
                if (model.File == null || string.IsNullOrEmpty(model.UserId) || string.IsNullOrEmpty(model.FileName))
                {
                    return BadRequest("Missing required fields.");
                }

                // Get Blob container reference
                var containerClient = _blobServiceClient.GetBlobContainerClient(_feedContainer);

                // Generate a unique Blob name using a unique value
                var blobName = $"{Guid.NewGuid()}-{model.File.FileName}";
                var blobClient = containerClient.GetBlobClient(blobName);

                // Upload the file to Blob Storage
                using (var stream = model.File.OpenReadStream())
                {
                    await blobClient.UploadAsync(stream);
                }

                // Get the Blob URL
                var blobUrl = blobClient.Uri.ToString();

                // Save the feed data to CosmosDB
                var feed = new Feed
                {
                    Id = Guid.NewGuid().ToString(),  // Generate unique ID for the feed
                    UserId = model.UserId,
                    Description = model.Description,
                    FeedUrl = blobUrl,  // Set the Blob URL
                    UploadDate = DateTime.UtcNow
                };

                await _dbContext.FeedsContainer.CreateItemAsync(feed, new PartitionKey(feed.UserId));

                return Ok(new { Message = "Feed uploaded successfully.", FeedId = feed.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error uploading feed: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieve Feeds By User ID (with Pagination)
        /// </summary>
        [HttpGet("getUserFeeds")]
        public async Task<IActionResult> GetUserFeeds(string userId = null, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var queryOptions = new QueryRequestOptions { MaxItemCount = pageSize };

                var query = _dbContext.FeedsContainer
                    .GetItemLinqQueryable<Feed>(requestOptions: queryOptions)
                    .OrderByDescending(feed => feed.UploadDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize);

                // If userId is provided, filter feeds by UserId
                if (!string.IsNullOrEmpty(userId))
                {
                    query = query.Where(feed => feed.UserId == userId);
                }

                var iterator = query.ToFeedIterator();
                var feeds = new List<Feed>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    feeds.AddRange(response);
                }

                return Ok(feeds);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving feeds: {ex.Message}");
            }
        }
    }
}
