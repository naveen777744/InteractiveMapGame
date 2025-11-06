using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InteractiveMapGame.Data;
using InteractiveMapGame.Models;

namespace InteractiveMapGame.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LLMController : ControllerBase
    {
        private readonly MapGameDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public LLMController(MapGameDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        // POST: api/LLM/generate-content
        [HttpPost("generate-content")]
        public async Task<ActionResult<LLMResponse>> GenerateContent([FromBody] LLMRequest request)
        {
            // Get the map object for context
            var mapObject = await _context.MapObjects.FindAsync(request.MapObjectId);
            if (mapObject == null)
            {
                return NotFound("Map object not found");
            }

            // For description requests, check if we already have a generated description
            if (request.ContentType.ToLower() == "description" && !string.IsNullOrWhiteSpace(mapObject.GeneratedDescription))
            {
                // Log the interaction (retrieval from database)
                var interaction = new InteractionLog
                {
                    PlayerId = request.PlayerId,
                    MapObjectId = request.MapObjectId,
                    InteractionType = "Description_Retrieval",
                    InteractionData = JsonSerializer.Serialize(new { ContentType = request.ContentType, Source = "Database" }),
                    WasSuccessful = true,
                    UsedLLM = false,
                    LLMPrompt = null,
                    LLMResponse = mapObject.GeneratedDescription,
                    LLMTokens = null,
                    Timestamp = DateTime.UtcNow
                };

                _context.InteractionLogs.Add(interaction);
                await _context.SaveChangesAsync();

                return Ok(new LLMResponse(mapObject.GeneratedDescription, request.ContentType));
            }

            // If no cached description or not a description request, generate new content
            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return StatusCode(500, "OpenAI API key is not configured.");
            }

            var systemPrompt = CreateSystemPrompt(mapObject, request.ContentType);
            var userPrompt = CreateUserPrompt(mapObject, request.ContentType, request.SpecificRequest);

            // Build messages array with conversation history if available
            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            // Add conversation history if provided (for conversation type)
            if (request.ContentType.ToLower() == "conversation" && request.ConversationHistory != null && request.ConversationHistory.Count > 0)
            {
                // Add conversation history messages (excluding the current user message which is in specificRequest)
                foreach (var historyMsg in request.ConversationHistory)
                {
                    // Only add valid roles (user or assistant)
                    if (historyMsg.Role == "user" || historyMsg.Role == "assistant")
                    {
                        messages.Add(new { role = historyMsg.Role, content = historyMsg.Content });
                    }
                }
            }

            // Add the current user message
            messages.Add(new { role = "user", content = userPrompt });

            var payload = new
            {
                model = "gpt-3.5-turbo",
                messages = messages.ToArray(),
                temperature = 0.7,
                max_tokens = 500
            };

            var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var httpContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            
            try
            {
                using var resp = await http.PostAsync("https://api.openai.com/v1/chat/completions", httpContent);
                if (!resp.IsSuccessStatusCode)
                {
                    var errText = await resp.Content.ReadAsStringAsync();
                    var errorMessage = string.IsNullOrWhiteSpace(errText) 
                        ? $"OpenAI API request failed with status {resp.StatusCode}: {resp.ReasonPhrase}"
                        : errText;
                    
                    // Try to parse JSON error response for better error message
                    try
                    {
                        using var errorDoc = JsonDocument.Parse(errText);
                        var errorRoot = errorDoc.RootElement;
                        if (errorRoot.TryGetProperty("error", out var errorObj))
                        {
                            if (errorObj.TryGetProperty("message", out var message))
                            {
                                errorMessage = message.GetString() ?? errorMessage;
                            }
                            else if (errorObj.TryGetProperty("type", out var type))
                            {
                                errorMessage = $"{type.GetString()}: {errorMessage}";
                            }
                        }
                    }
                    catch
                    {
                        // If JSON parsing fails, use the raw error text
                    }
                    
                    return StatusCode((int)resp.StatusCode, new { error = errorMessage, statusCode = (int)resp.StatusCode });
                }

                string content;
                int? tokenCount = null;
                try
                {
                    using var stream = await resp.Content.ReadAsStreamAsync();
                    using var doc = await JsonDocument.ParseAsync(stream);
                    var root = doc.RootElement;
                    
                    // Validate response structure
                    if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                    {
                        var errorMsg = "Invalid response from OpenAI API: missing or empty choices array";
                        return StatusCode(500, new { error = errorMsg, statusCode = 500 });
                    }
                    
                    var firstChoice = choices[0];
                    if (!firstChoice.TryGetProperty("message", out var message))
                    {
                        var errorMsg = "Invalid response from OpenAI API: missing message property";
                        return StatusCode(500, new { error = errorMsg, statusCode = 500 });
                    }
                    
                    if (!message.TryGetProperty("content", out var contentElement))
                    {
                        var errorMsg = "Invalid response from OpenAI API: missing content property";
                        return StatusCode(500, new { error = errorMsg, statusCode = 500 });
                    }
                    
                    content = contentElement.GetString() ?? string.Empty;
                    
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        var errorMsg = "OpenAI API returned empty content";
                        return StatusCode(500, new { error = errorMsg, statusCode = 500 });
                    }
                    
                    // Extract token count before disposing the document
                    if (root.TryGetProperty("usage", out var usage))
                    {
                        if (usage.TryGetProperty("total_tokens", out var tokens))
                        {
                            tokenCount = tokens.GetInt32();
                        }
                    }
                }
                catch (JsonException jsonEx)
                {
                    var errorMsg = $"Failed to parse OpenAI API response as JSON: {jsonEx.Message}";
                    return StatusCode(500, new { error = errorMsg, statusCode = 500 });
                }
                catch (Exception parseEx)
                {
                    var errorMsg = $"Unexpected error parsing OpenAI API response: {parseEx.Message}";
                    return StatusCode(500, new { error = errorMsg, statusCode = 500 });
                }

                // If this is a description request, save it to the database for future use
                if (request.ContentType.ToLower() == "description")
                {
                    mapObject.GeneratedDescription = content;
                    mapObject.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                // Log the LLM interaction
                // Truncate LLMResponse to fit database column (2000 chars max)
                var truncatedResponse = content.Length > 2000 ? content.Substring(0, 1997) + "..." : content;
                var truncatedPrompt = userPrompt?.Length > 2000 ? userPrompt.Substring(0, 1997) + "..." : userPrompt;
                
                var interaction = new InteractionLog
                {
                    PlayerId = request.PlayerId,
                    MapObjectId = request.MapObjectId,
                    InteractionType = "LLM_Generation",
                    InteractionData = JsonSerializer.Serialize(new { ContentType = request.ContentType, SpecificRequest = request.SpecificRequest }),
                    WasSuccessful = true,
                    UsedLLM = true,
                    LLMPrompt = truncatedPrompt,
                    LLMResponse = truncatedResponse,
                    LLMTokens = tokenCount,
                    Timestamp = DateTime.UtcNow
                };

                _context.InteractionLogs.Add(interaction);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    // Log the error but don't fail the request - the content was already generated successfully
                    // The user should still get their description even if logging fails
                    Console.Error.WriteLine($"Failed to save interaction log: {ex.Message}");
                }

                return Ok(new LLMResponse(content, request.ContentType));
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error calling OpenAI API: {ex.Message}";
                return StatusCode(500, new { error = errorMsg, statusCode = 500, exception = ex.GetType().Name });
            }
        }

        // POST: api/LLM/populate-all-descriptions
        [HttpPost("populate-all-descriptions")]
        public async Task<ActionResult> PopulateAllDescriptions()
        {
            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return StatusCode(500, "OpenAI API key is not configured.");
            }

            // Get all map objects that don't have generated descriptions
            var objectsToProcess = await _context.MapObjects
                .Where(m => string.IsNullOrWhiteSpace(m.GeneratedDescription))
                .ToListAsync();

            if (objectsToProcess.Count == 0)
            {
                return Ok(new { message = "All MapObjects already have generated descriptions!", count = 0 });
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            int processed = 0;
            int successful = 0;
            int failed = 0;

            foreach (var mapObject in objectsToProcess)
            {
                processed++;
                try
                {
                    var systemPrompt = CreateSystemPrompt(mapObject, "description");
                    var userPrompt = CreateUserPrompt(mapObject, "description", null);

                    var payload = new
                    {
                        model = "gpt-3.5-turbo",
                        messages = new object[]
                        {
                            new { role = "system", content = systemPrompt },
                            new { role = "user", content = userPrompt }
                        },
                        temperature = 0.7,
                        max_tokens = 500
                    };

                    var httpContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    
                    using var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", httpContent);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        failed++;
                        continue;
                    }

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var doc = await JsonDocument.ParseAsync(stream);
                    var root = doc.RootElement;
                    var content = root
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? string.Empty;

                    // Save to database
                    mapObject.GeneratedDescription = content;
                    mapObject.UpdatedAt = DateTime.UtcNow;
                    
                    await _context.SaveChangesAsync();
                    successful++;
                }
                catch (Exception)
                {
                    failed++;
                }

                // Small delay to be respectful to the API
                await Task.Delay(1000);
            }

            return Ok(new { 
                message = "Description generation complete!", 
                processed = processed,
                successful = successful, 
                failed = failed 
            });
        }

        // POST: api/LLM/populate-object
        [HttpPost("populate-object")]
        public async Task<ActionResult> PopulateMapObject([FromBody] PopulateRequest request)
        {
            var mapObject = await _context.MapObjects.FindAsync(request.MapObjectId);
            if (mapObject == null)
            {
                return NotFound("Map object not found");
            }

            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return StatusCode(500, "OpenAI API key is not configured.");
            }

            try
            {
                // Generate description
                var descriptionResponse = await GenerateContentInternal(apiKey, mapObject, "description", null);
                if (descriptionResponse != null)
                {
                    mapObject.GeneratedDescription = descriptionResponse;
                }

                // Generate story
                var storyResponse = await GenerateContentInternal(apiKey, mapObject, "story", null);
                if (storyResponse != null)
                {
                    mapObject.GeneratedStory = storyResponse;
                }

                // Generate facts
                var factsResponse = await GenerateContentInternal(apiKey, mapObject, "facts", null);
                if (factsResponse != null)
                {
                    mapObject.GeneratedFacts = factsResponse;
                }

                mapObject.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Object populated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error populating object: {ex.Message}");
            }
        }

        private async Task<string?> GenerateContentInternal(string apiKey, MapObject mapObject, string contentType, string? specificRequest)
        {
            var systemPrompt = CreateSystemPrompt(mapObject, contentType);
            var userPrompt = CreateUserPrompt(mapObject, contentType, specificRequest);

            var payload = new
            {
                model = "gpt-3.5-turbo",
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.7,
                max_tokens = 300
            };

            var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var httpContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            
            using var resp = await http.PostAsync("https://api.openai.com/v1/chat/completions", httpContent);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            return root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }

        private string CreateSystemPrompt(MapObject mapObject, string contentType)
        {
            if (contentType.ToLower() == "conversation")
            {
                return "You are an expert conversational AI map guide. Use the GeneratedDescription field from the map object as your primary source of facts to answer the user's question, and be concise.";
            }

            var basePrompt = $"You are an expert aerospace historian and museum guide. You are helping visitors learn about {mapObject.Name}, a {mapObject.Type}";
            
            if (!string.IsNullOrEmpty(mapObject.Category))
            {
                basePrompt += $" in the {mapObject.Category} category";
            }
            
            if (!string.IsNullOrEmpty(mapObject.Era))
            {
                basePrompt += $" from the {mapObject.Era}";
            }

            return contentType.ToLower() switch
            {
                "description" => basePrompt + ". Provide a detailed, engaging description that would captivate museum visitors.",
                "story" => basePrompt + ". Tell an interesting story or historical narrative about this object that would engage visitors.",
                "facts" => basePrompt + ". Share fascinating facts and technical details that would educate visitors.",
                _ => basePrompt + ". Provide helpful information about this object."
            };
        }

        private string CreateUserPrompt(MapObject mapObject, string contentType, string? specificRequest)
        {
            var baseInfo = $"Object: {mapObject.Name}\nType: {mapObject.Type}";
            
            if (!string.IsNullOrEmpty(mapObject.Category))
                baseInfo += $"\nCategory: {mapObject.Category}";
            if (!string.IsNullOrEmpty(mapObject.Era))
                baseInfo += $"\nEra: {mapObject.Era}";
            if (!string.IsNullOrEmpty(mapObject.Manufacturer))
                baseInfo += $"\nManufacturer: {mapObject.Manufacturer}";
            if (!string.IsNullOrEmpty(mapObject.Description))
                baseInfo += $"\nCurrent Description: {mapObject.Description}";

            // If specificRequest is provided, use the new format with GeneratedDescription as context
            if (!string.IsNullOrEmpty(specificRequest))
            {
                if (!string.IsNullOrEmpty(mapObject.GeneratedDescription))
                {
                    return $"CONTEXT: {mapObject.GeneratedDescription}\n\nUSER QUESTION: {specificRequest}";
                }
                else
                {
                    return $"{baseInfo}\n\nSpecific request: {specificRequest}";
                }
            }

            // For conversation type, include the GeneratedDescription as the primary source
            if (contentType.ToLower() == "conversation" && !string.IsNullOrEmpty(mapObject.GeneratedDescription))
            {
                baseInfo += $"\n\nPrimary Source (GeneratedDescription): {mapObject.GeneratedDescription}";
            }

            return contentType.ToLower() switch
            {
                "description" => $"{baseInfo}\n\nGenerate an engaging description for museum visitors.",
                "story" => $"{baseInfo}\n\nTell an interesting story about this object.",
                "facts" => $"{baseInfo}\n\nShare fascinating facts about this object.",
                "conversation" => $"{baseInfo}\n\nAnswer the user's question based on the provided information.",
                _ => $"{baseInfo}\n\nProvide information about this object."
            };
        }
    }

    public record LLMRequest(
        string PlayerId,
        int MapObjectId,
        string ContentType, // "description", "story", "facts", "conversation"
        string? SpecificRequest = null,
        List<ConversationMessage>? ConversationHistory = null
    );

    public record ConversationMessage(
        string Role, // "user" or "assistant"
        string Content
    );

    public record LLMResponse(
        string Content,
        string ContentType
    );

    public record PopulateRequest(
        int MapObjectId
    );
}
