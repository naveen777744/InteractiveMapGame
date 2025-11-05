using System.ComponentModel.DataAnnotations;

namespace InteractiveMapGame.Models
{
    public class InteractionLog
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(64)]
        public string PlayerId { get; set; } = string.Empty;
        
        public int MapObjectId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string InteractionType { get; set; } = string.Empty; // Click, Hover, Video, Explore, etc.
        
        [StringLength(1000)]
        public string? InteractionData { get; set; } // JSON data about the interaction
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        // Performance metrics
        public int Duration { get; set; } = 0; // in milliseconds
        public bool WasSuccessful { get; set; } = true;
        
        // LLM interaction tracking
        public bool UsedLLM { get; set; } = false;
        public string? LLMPrompt { get; set; }
        public string? LLMResponse { get; set; }
        public int? LLMTokens { get; set; }
    }
}
