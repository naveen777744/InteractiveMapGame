using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InteractiveMapGame.Data;
using InteractiveMapGame.Models;

namespace InteractiveMapGame.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MapController : ControllerBase
    {
        private readonly MapGameDbContext _context;

        public MapController(MapGameDbContext context)
        {
            _context = context;
        }

        // GET: api/Map/objects
        [HttpGet("objects")]
        public async Task<ActionResult<IEnumerable<MapObject>>> GetMapObjects()
        {
            return await _context.MapObjects
                .Where(o => o.IsDiscoverable)
                .OrderBy(o => o.Name)
                .ToListAsync();
        }

        // GET: api/Map/objects/{id}
        [HttpGet("objects/{id}")]
        public async Task<ActionResult<MapObject>> GetMapObject(int id)
        {
            var mapObject = await _context.MapObjects.FindAsync(id);

            if (mapObject == null)
            {
                return NotFound();
            }

            return mapObject;
        }

        // GET: api/Map/objects/nearby
        [HttpGet("objects/nearby")]
        public async Task<ActionResult<IEnumerable<MapObject>>> GetNearbyObjects(
            double x, double y, double z = 0, double radius = 100)
        {
            var nearbyObjects = await _context.MapObjects
                .Where(o => o.IsDiscoverable && 
                           Math.Sqrt(Math.Pow(o.X - x, 2) + Math.Pow(o.Y - y, 2) + Math.Pow(o.Z - z, 2)) <= radius)
                .OrderBy(o => Math.Sqrt(Math.Pow(o.X - x, 2) + Math.Pow(o.Y - y, 2) + Math.Pow(o.Z - z, 2)))
                .ToListAsync();

            return nearbyObjects;
        }

        // GET: api/Map/objects/type/{type}
        [HttpGet("objects/type/{type}")]
        public async Task<ActionResult<IEnumerable<MapObject>>> GetObjectsByType(string type)
        {
            var objects = await _context.MapObjects
                .Where(o => o.Type.ToLower() == type.ToLower() && o.IsDiscoverable)
                .ToListAsync();

            return objects;
        }

        // GET: api/Map/objects/unlocked
        [HttpGet("objects/unlocked")]
        public async Task<ActionResult<IEnumerable<MapObject>>> GetUnlockedObjects()
        {
            var unlockedObjects = await _context.MapObjects
                .Where(o => o.IsUnlocked && o.IsDiscoverable)
                .ToListAsync();

            return unlockedObjects;
        }

        // POST: api/Map/objects
        [HttpPost("objects")]
        public async Task<ActionResult<MapObject>> CreateMapObject(MapObject mapObject)
        {
            mapObject.CreatedAt = DateTime.UtcNow;
            mapObject.UpdatedAt = DateTime.UtcNow;
            
            _context.MapObjects.Add(mapObject);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetMapObject", new { id = mapObject.Id }, mapObject);
        }

        // PUT: api/Map/objects/{id}
        [HttpPut("objects/{id}")]
        public async Task<IActionResult> UpdateMapObject(int id, MapObject mapObject)
        {
            if (id != mapObject.Id)
            {
                return BadRequest();
            }

            mapObject.UpdatedAt = DateTime.UtcNow;
            _context.Entry(mapObject).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MapObjectExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/Map/objects/{id}
        [HttpDelete("objects/{id}")]
        public async Task<IActionResult> DeleteMapObject(int id)
        {
            var mapObject = await _context.MapObjects.FindAsync(id);
            if (mapObject == null)
            {
                return NotFound();
            }

            _context.MapObjects.Remove(mapObject);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/Map/objects/{id}/interact
        [HttpPost("objects/{id}/interact")]
        public async Task<ActionResult> InteractWithObject(int id, [FromBody] InteractionRequest request)
        {
            var mapObject = await _context.MapObjects.FindAsync(id);
            if (mapObject == null)
            {
                return NotFound();
            }

            // Log the interaction
            var interaction = new InteractionLog
            {
                PlayerId = request.PlayerId,
                MapObjectId = id,
                InteractionType = request.InteractionType,
                InteractionData = request.InteractionData,
                Duration = request.Duration,
                WasSuccessful = true,
                Timestamp = DateTime.UtcNow
            };

            _context.InteractionLogs.Add(interaction);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Interaction logged successfully" });
        }

        // POST: api/Map/seed
        [HttpPost("seed")]
        public async Task<ActionResult> SeedDatabase()
        {
            // Check if data already exists
            if (await _context.MapObjects.AnyAsync())
            {
                return Ok(new { message = "Database already has data. Skipping seed." });
            }

            // Create sample map objects
            var mapObjects = new List<MapObject>
            {
                // Aircraft
                new MapObject
                {
                    Name = "SR-71 Blackbird",
                    Description = "The fastest aircraft ever built, capable of Mach 3+ speeds",
                    Type = "Aircraft",
                    Category = "Reconnaissance",
                    Era = "1960s",
                    Manufacturer = "Lockheed",
                    FirstFlight = new DateTime(1964, 12, 22),
                    Status = "Retired",
                    X = 100, Y = 150, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = true,
                    ExperienceValue = 100,
                    DifficultyLevel = 3,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new MapObject
                {
                    Name = "F-22 Raptor",
                    Description = "Fifth-generation stealth fighter aircraft",
                    Type = "Aircraft",
                    Category = "Fighter",
                    Era = "2000s",
                    Manufacturer = "Lockheed Martin",
                    FirstFlight = new DateTime(1997, 9, 7),
                    Status = "Active",
                    X = 300, Y = 250, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = false,
                    ExperienceValue = 150,
                    DifficultyLevel = 4,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new MapObject
                {
                    Name = "Boeing 747",
                    Description = "The original jumbo jet that revolutionized air travel",
                    Type = "Aircraft",
                    Category = "Commercial",
                    Era = "1970s",
                    Manufacturer = "Boeing",
                    FirstFlight = new DateTime(1969, 2, 9),
                    Status = "Active",
                    X = 400, Y = 100, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = false,
                    ExperienceValue = 80,
                    DifficultyLevel = 2,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new MapObject
                {
                    Name = "Concorde",
                    Description = "Supersonic passenger airliner",
                    Type = "Aircraft",
                    Category = "Commercial",
                    Era = "1970s",
                    Manufacturer = "Aérospatiale/BAC",
                    FirstFlight = new DateTime(1969, 3, 2),
                    Status = "Retired",
                    X = 200, Y = 300, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = false,
                    ExperienceValue = 120,
                    DifficultyLevel = 3,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                // Spacecraft
                new MapObject
                {
                    Name = "Apollo 11 Command Module",
                    Description = "The spacecraft that took humans to the moon",
                    Type = "Spacecraft",
                    Category = "Manned",
                    Era = "1960s",
                    Manufacturer = "North American Aviation",
                    FirstFlight = new DateTime(1969, 7, 16),
                    Status = "Historic",
                    X = 200, Y = 100, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = true,
                    ExperienceValue = 200,
                    DifficultyLevel = 5,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new MapObject
                {
                    Name = "Space Shuttle Discovery",
                    Description = "Most flown space shuttle with 39 missions",
                    Type = "Spacecraft",
                    Category = "Manned",
                    Era = "1980s",
                    Manufacturer = "Rockwell International",
                    FirstFlight = new DateTime(1984, 8, 30),
                    Status = "Retired",
                    X = 250, Y = 300, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = false,
                    ExperienceValue = 180,
                    DifficultyLevel = 4,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new MapObject
                {
                    Name = "Dragon Capsule",
                    Description = "Modern commercial spacecraft for cargo and crew",
                    Type = "Spacecraft",
                    Category = "Manned",
                    Era = "2010s",
                    Manufacturer = "SpaceX",
                    FirstFlight = new DateTime(2010, 12, 8),
                    Status = "Active",
                    X = 350, Y = 200, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = false,
                    ExperienceValue = 160,
                    DifficultyLevel = 3,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                // Satellites
                new MapObject
                {
                    Name = "Hubble Space Telescope",
                    Description = "Revolutionary space observatory",
                    Type = "Satellite",
                    Category = "Scientific",
                    Era = "1990s",
                    Manufacturer = "Lockheed Martin",
                    FirstFlight = new DateTime(1990, 4, 24),
                    Status = "Active",
                    X = 150, Y = 200, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = false,
                    ExperienceValue = 140,
                    DifficultyLevel = 3,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new MapObject
                {
                    Name = "Sputnik 1",
                    Description = "The first artificial satellite",
                    Type = "Satellite",
                    Category = "Scientific",
                    Era = "1950s",
                    Manufacturer = "Soviet Union",
                    FirstFlight = new DateTime(1957, 10, 4),
                    Status = "Historic",
                    X = 50, Y = 50, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = true,
                    ExperienceValue = 100,
                    DifficultyLevel = 2,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new MapObject
                {
                    Name = "GPS Satellite",
                    Description = "Global Positioning System satellite",
                    Type = "Satellite",
                    Category = "Navigation",
                    Era = "1980s",
                    Manufacturer = "Rockwell International",
                    FirstFlight = new DateTime(1978, 2, 22),
                    Status = "Active",
                    X = 500, Y = 400, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = false,
                    ExperienceValue = 90,
                    DifficultyLevel = 2,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                // Rockets
                new MapObject
                {
                    Name = "Saturn V",
                    Description = "The rocket that launched Apollo missions to the moon",
                    Type = "Rocket",
                    Category = "Launch Vehicle",
                    Era = "1960s",
                    Manufacturer = "Boeing/North American",
                    FirstFlight = new DateTime(1967, 11, 9),
                    Status = "Retired",
                    X = 300, Y = 150, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = false,
                    ExperienceValue = 250,
                    DifficultyLevel = 5,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new MapObject
                {
                    Name = "Falcon Heavy",
                    Description = "Most powerful operational rocket",
                    Type = "Rocket",
                    Category = "Launch Vehicle",
                    Era = "2010s",
                    Manufacturer = "SpaceX",
                    FirstFlight = new DateTime(2018, 2, 6),
                    Status = "Active",
                    X = 450, Y = 350, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = false,
                    ExperienceValue = 200,
                    DifficultyLevel = 4,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                // Helicopters
                new MapObject
                {
                    Name = "Bell UH-1 Iroquois",
                    Description = "Iconic Vietnam War helicopter",
                    Type = "Helicopter",
                    Category = "Military",
                    Era = "1960s",
                    Manufacturer = "Bell Helicopter",
                    FirstFlight = new DateTime(1956, 10, 20),
                    Status = "Active",
                    X = 150, Y = 400, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = false,
                    ExperienceValue = 70,
                    DifficultyLevel = 2,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new MapObject
                {
                    Name = "Apache AH-64",
                    Description = "Advanced attack helicopter",
                    Type = "Helicopter",
                    Category = "Military",
                    Era = "1980s",
                    Manufacturer = "Boeing",
                    FirstFlight = new DateTime(1975, 9, 30),
                    Status = "Active",
                    X = 250, Y = 450, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = false,
                    ExperienceValue = 110,
                    DifficultyLevel = 3,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                // Museums and Landmarks
                new MapObject
                {
                    Name = "National Air and Space Museum",
                    Description = "Premier aerospace museum",
                    Type = "Museum",
                    Category = "Institution",
                    Era = "Modern",
                    Manufacturer = "Smithsonian",
                    Status = "Active",
                    X = 100, Y = 100, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = true,
                    ExperienceValue = 50,
                    DifficultyLevel = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new MapObject
                {
                    Name = "Kennedy Space Center",
                    Description = "NASA launch facility",
                    Type = "Museum",
                    Category = "Institution",
                    Era = "Modern",
                    Manufacturer = "NASA",
                    Status = "Active",
                    X = 400, Y = 100, Z = 0,
                    IsInteractive = true,
                    IsDiscoverable = true,
                    IsUnlocked = false,
                    ExperienceValue = 80,
                    DifficultyLevel = 2,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            _context.MapObjects.AddRange(mapObjects);

            // Create sample interaction logs
            var interactionLogs = new List<InteractionLog>
            {
                new InteractionLog
                {
                    PlayerId = "player_demo_001",
                    MapObjectId = 1,
                    InteractionType = "click",
                    InteractionData = "{\"x\":100,\"y\":150}",
                    Duration = 500,
                    WasSuccessful = true,
                    UsedLLM = true,
                    LLMPrompt = "Generate a description for SR-71 Blackbird",
                    LLMResponse = "The SR-71 Blackbird is a legendary reconnaissance aircraft...",
                    LLMTokens = 45,
                    Timestamp = DateTime.UtcNow
                },
                new InteractionLog
                {
                    PlayerId = "player_demo_001",
                    MapObjectId = 5,
                    InteractionType = "hover",
                    InteractionData = "{\"duration\":2000}",
                    Duration = 2000,
                    WasSuccessful = true,
                    UsedLLM = false,
                    Timestamp = DateTime.UtcNow
                },
                new InteractionLog
                {
                    PlayerId = "player_demo_001",
                    MapObjectId = 9,
                    InteractionType = "click",
                    InteractionData = "{\"x\":50,\"y\":50}",
                    Duration = 300,
                    WasSuccessful = true,
                    UsedLLM = true,
                    LLMPrompt = "Tell me about Sputnik 1",
                    LLMResponse = "Sputnik 1 was the first artificial satellite...",
                    LLMTokens = 38,
                    Timestamp = DateTime.UtcNow
                }
            };

            _context.InteractionLogs.AddRange(interactionLogs);

            await _context.SaveChangesAsync();

            var totalObjects = await _context.MapObjects.CountAsync();
            var totalLogs = await _context.InteractionLogs.CountAsync();

            return Ok(new { 
                message = "Database seeded successfully!",
                totalObjects,
                totalLogs
            });
        }

        private bool MapObjectExists(int id)
        {
            return _context.MapObjects.Any(e => e.Id == id);
        }
    }

    public record InteractionRequest(
        string PlayerId,
        string InteractionType,
        string? InteractionData = null,
        int Duration = 0
    );
}
