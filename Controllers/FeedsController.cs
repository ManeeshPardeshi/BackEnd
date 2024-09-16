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
        private readonly string _feedContainer = "media";  // Correct Blob container name

        public FeedsController(CosmosDbContext dbContext, BlobServiceClient blobServiceClient, ServiceBusClient serviceBusClient)
        {
            _dbContext = dbContext;
            _blobServiceClient = blobServiceClient;
            _serviceBusSender = serviceBusClient.CreateSender("new-feed-notifications");
        }

        /// <summary>
        /// Upload a new feed with file
        /// </summary>
        [HttpPost("uploadFeed")]
        public async Task<IActionResult> UploadFeed([FromForm] FeedUploadModel model)
        {
            if (model.File == null || model.File.Length == 0)
            {
                return BadRequest("File is required.");
            }

            try
            {

                // Generate a unique feed ID
                var feedId = Guid.NewGuid().ToString();

                // Get Blob container reference
                var containerClient = _blobServiceClient.GetBlobContainerClient(_feedContainer);

                // Append the feedId to the Blob file name to ensure uniqueness
                var blobClient = containerClient.GetBlobClient($"{feedId}_{model.File.FileName}");

                using (var stream = model.File.OpenReadStream())
                {
                    await blobClient.UploadAsync(stream);
                }

                // Save feed details to Cosmos DB
                var feed = new Feed
                {
                    Id = feedId,
                    UserId = model.UserId,
                    Description = model.Description,
                    FeedUrl = blobClient.Uri.ToString(),
                    ContentType = model.ContentType,
                    FileSize = model.File.Length,
                    UploadDate = DateTime.UtcNow
                };

                await _dbContext.FeedsContainer.CreateItemAsync(feed);

                // Send a notification to Azure Service Bus
                var message = new ServiceBusMessage($"New feed uploaded by User: {model.UserId}");
                await _serviceBusSender.SendMessageAsync(message);

                return Ok(new { Message = "Feed uploaded successfully.", FeedUrl = feed.FeedUrl });
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
        public async Task<IActionResult> GetUserFeeds(string userId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var queryOptions = new QueryRequestOptions { MaxItemCount = pageSize };
                var query = _dbContext.FeedsContainer
                    .GetItemLinqQueryable<Feed>(requestOptions: queryOptions)
                    .Where(feed => feed.UserId == userId)
                    .OrderByDescending(feed => feed.UploadDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize);

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

        /// <summary>
        /// Get Feed Details by Feed ID
        /// </summary>
        [HttpGet("getFeed/{feedId}")]
        public async Task<IActionResult> GetFeedById(string feedId)
        {
            try
            {
                var feed = await _dbContext.FeedsContainer.ReadItemAsync<Feed>(feedId, new PartitionKey(feedId));
                return Ok(feed);
            }
            catch (CosmosException ex)
            {
                return StatusCode(404, $"Feed not found: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving feed: {ex.Message}");
            }
        }
    }
}
