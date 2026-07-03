using System;

namespace AgentWire.Core.Entities
{
    public class AIPacket
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string TraceId { get; set; } = string.Empty;
        public string AgentId { get; set; } = string.Empty;
        public string ModelProvider { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public string UserPrompt { get; set; } = string.Empty;
        public string LLMResponse { get; set; } = string.Empty;
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public decimal Cost { get; set; }
        public double LatencyMs { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
