using Microsoft.AspNetCore.Mvc;
using ApcUpsLogParser.Common.DTOs;
using ApcUpsLogParser.Common.Services;
using System.Text.Json;

namespace ApcUpsLogParser.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VoltageController : ControllerBase
    {
        [HttpPost("data")]
        public async Task<IActionResult> GetVoltageData()
        {
            try
            {
                VoltageDataRequest? request;
                
                // Read the raw request body and manually deserialize
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                Console.WriteLine($"Raw request body: {body}");
                Console.WriteLine($"Content-Type: {Request.ContentType}");
                Console.WriteLine($"Content-Length: {Request.ContentLength}");
                
                if (string.IsNullOrEmpty(body))
                {
                    Console.WriteLine("Empty request body, using default request");
                    request = new VoltageDataRequest { Today = true };
                }
                else
                {
                    try
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        };
                        request = JsonSerializer.Deserialize<VoltageDataRequest>(body, options);
                        Console.WriteLine($"Successfully deserialized request: IsLive={request?.IsLive}, Days={request?.Days}, Today={request?.Today}, Compare={request?.Compare}, Smooth={request?.Smooth}");
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"JSON deserialization failed: {jsonEx.Message}");
                        Console.WriteLine($"Attempting fallback deserialization...");
                        
                        // Try without camel case policy
                        var fallbackOptions = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        request = JsonSerializer.Deserialize<VoltageDataRequest>(body, fallbackOptions);
                    }
                }
                
                // Handle null request
                if (request == null)
                {
                    Console.WriteLine("Request is null after deserialization, creating default request");
                    request = new VoltageDataRequest { Today = true };
                }
                
                Console.WriteLine($"Final request: IsLive={request.IsLive}, Days={request.Days}, Today={request.Today}, Compare={request.Compare}, Smooth={request.Smooth}");
                
                var response = VoltageAnalysisService.GetVoltageData(request);
                
                // Ensure LastRefreshTime is set
                if (response.LastRefreshTime == default)
                {
                    response.LastRefreshTime = DateTime.Now;
                }
                
                Console.WriteLine($"Returning response with {response.CurrentEntries?.Count ?? 0} current entries");
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetVoltageData: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { error = ex.Message, details = ex.ToString() });
            }
        }

        [HttpGet("health")]
        public ActionResult<object> Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
    }
}