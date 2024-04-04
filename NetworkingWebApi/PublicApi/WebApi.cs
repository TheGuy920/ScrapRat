using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace CrashbotWebApi.PublicApi
{
    [ApiController]
    [Route("[controller]")]
    public class StreamController : ControllerBase
    {
        private readonly ConcurrentDictionary<ulong, string> LiveStreams = [];

        public StreamController()
        {

        }

        // Handling different ID formats
        [HttpGet]
        [HttpGet("{id}")]
        public IActionResult GetId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                // Try to get ID from query string if not in route
                id = HttpContext.Request.Query["id"];
                id ??= HttpContext.Request.Query["steam"];
                id ??= HttpContext.Request.Query["steamid"];
                id ??= HttpContext.Request.Query["steam_id"];
            }

            if (ulong.TryParse(id, out ulong steamId))
            {

                return Ok($"Received ID: {steamId}");
            }
            else
            {
                return BadRequest("Invalid ID format.");
            }
        }
    }
}
