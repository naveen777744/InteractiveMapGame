using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InteractiveMapGame.Data;

namespace InteractiveMapGame.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminDashboardController : ControllerBase
    {
        private readonly MapGameDbContext _context;

        public AdminDashboardController(MapGameDbContext context)
        {
            _context = context;
        }

        // GET: api/AdminDashboard/stats
        [HttpGet("stats")]
        public async Task<ActionResult> GetStats()
        {
            var totalObjects = await _context.MapObjects.CountAsync();
            var totalInteractions = await _context.InteractionLogs.CountAsync();
            var totalAdmins = await _context.Admins.CountAsync();

            return Ok(new
            {
                totalObjects,
                totalInteractions,
                totalAdmins
            });
        }

        // GET: api/AdminDashboard/analytics
        [HttpGet("analytics")]
        public ActionResult GetAnalytics()
        {
            // Placeholder for analytics endpoint
            return Ok(new { message = "Analytics endpoint - to be implemented" });
        }
    }
}

