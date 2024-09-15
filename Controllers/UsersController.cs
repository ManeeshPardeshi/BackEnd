using Microsoft.AspNetCore.Mvc;
using BackEnd.Entities;
using Microsoft.Azure.Cosmos;
using System;
using System.Resources;
using System.Threading.Tasks;
using User = BackEnd.Entities.User;
using System.Collections;
using System.Reflection;

namespace BackEnd.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UsersController : ControllerBase
    {
        private readonly CosmosDbContext _dbContext;
        private static readonly Random _random = new Random();

        // Base URL for the profile pictures served via CDN
        private static readonly string _cdnBaseUrl = "https://tenx-ghg3hcg0bphxd3b9.z02.azurefd.net/profilepic/";

        public UsersController(CosmosDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Change the route to "create" for clarity
        [HttpGet("create")]
        public async Task<IActionResult> CreateUser()
        {
            try
            {
                // Generate a random username and profile picture URL
                var newUser = new User
                {
                    Username = GenerateRandomName(),
                    ProfilePicUrl = GetRandomProfilePic()
                };

                // Ensure PartitionKey is provided and valid (could be UserId or Id)
                await _dbContext.UsersContainer.CreateItemAsync(newUser);

                // Return the user details including the profile picture
                return Ok(new
                {
                    userId = newUser.Id,
                    username = newUser.Username,
                    profilePic = newUser.ProfilePicUrl
                });
            }
            catch (CosmosException ex)
            {
                return StatusCode(500, $"Cosmos DB Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        // Generates a random name by combining a random adjective and noun from the resx file
        private string GenerateRandomName()
        {
            string adjective = GetRandomResource("Adj_");
            string noun = GetRandomResource("Noun_");

            // Make sure at least one word is French
            bool isFrenchInAdjective = _random.Next(2) == 0;
            string finalAdjective = isFrenchInAdjective ? GetFrenchPart(adjective) : GetEnglishPart(adjective);
            string finalNoun = isFrenchInAdjective ? GetEnglishPart(noun) : GetFrenchPart(noun);

            return $"{finalAdjective}_{finalNoun}";
        }

        // Returns a random profile picture URL from the range pp1 to pp25
        private string GetRandomProfilePic()
        {
            int randomNumber = _random.Next(1, 26); // Generate a random number between 1 and 25
            return $"{_cdnBaseUrl}pp{randomNumber}.jpg";
        }

        // Get a random resource entry (adjective or noun) from the resx file
        private string GetRandomResource(string resourceType)
        {
            ResourceManager resourceManager = new ResourceManager("BackEnd.Resources.AdjectivesNouns", Assembly.GetExecutingAssembly());
            var resourceSet = resourceManager.GetResourceSet(System.Globalization.CultureInfo.CurrentUICulture, true, true);

            if (resourceSet == null)
            {
                throw new Exception("ResourceSet is null. Resource file might not be found.");
            }

            var matchingEntries = new List<DictionaryEntry>();
            foreach (DictionaryEntry entry in resourceSet)
            {
                if (entry.Key.ToString().StartsWith(resourceType))
                {
                    matchingEntries.Add(entry);
                }
            }

            if (matchingEntries.Count == 0)
            {
                throw new Exception($"No matching {resourceType} resources found.");
            }

            // Select a random entry
            DictionaryEntry selectedEntry = matchingEntries[_random.Next(matchingEntries.Count)];

            // Safeguard against unboxing null values (CS8605 fix)
            if (selectedEntry.Value != null && selectedEntry.Key != null)
            {
                return $"{selectedEntry.Key}-{selectedEntry.Value}";
            }

            throw new Exception("Invalid resource entry detected.");
        }

        // Extract the French part from the name (e.g., "Adj_Aventureux-Adventurous")
        private string GetFrenchPart(string entry)
        {
            var parts = entry?.Split('-');
            return parts?[0].Split('_')[1];
        }

        // Extract the English part from the value (e.g., "Adj_Aventureux-Adventurous")
        private string GetEnglishPart(string entry)
        {
            var parts = entry?.Split('-');
            return parts?[1];
        }
    }
}
