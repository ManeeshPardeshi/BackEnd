using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using BackEnd.Entities;
using BackEnd.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly string _feedContainer = "feedscontainer";  // Blob container for storing feeds

        public FeedsController(CosmosDbContext dbContext, BlobServiceClient blobServiceClient, ServiceBusClient serviceBusClient)
        {
            _dbContext = dbContext;
            _blobServiceClient = blobServiceClient;
            _serviceBusSender = serviceBusClient.CreateSender("new-feed-notifications");
        }

        /// <summary>
        /// Get Feeds By User ID (with Pagination)
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
        /// Upload a New Feed (File Upload to Blob + Metadata to Cosmos DB)
        /// </summary>
        [HttpPost("uploadFeed")]
        public async Task<IActionResult> UploadFeed([FromForm] FeedUploadModel model)
        {
            try
            {
                // Upload feed to Blob Storage
                var blobContainerClient = _blobServiceClient.GetBlobContainerClient(_feedContainer);
                var blobClient = blobContainerClient.GetBlobClient(model.File.FileName);

                using (var stream = model.File.OpenReadStream())
                {
                    await blobClient.UploadAsync(stream, true);
                }

                // Create feed metadata object
                var feed = new Feed
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = model.UserId,
                    FeedUrl = blobClient.Uri.ToString(),
                    UploadDate = DateTime.UtcNow,
                    Description = model.Description
                };

                // Store feed metadata in Cosmos DB
                await _dbContext.FeedsContainer.CreateItemAsync(feed);

                // Notify users via Azure Service Bus
                var notificationMessage = new ServiceBusMessage($"New feed uploaded by {model.UserId}");
                await _serviceBusSender.SendMessageAsync(notificationMessage);

                return Ok(feed);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error uploading feed: {ex.Message}");
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
