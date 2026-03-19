// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI.DurableTask.Planning;

namespace Microsoft.Agents.AI.DurableTask.State;

/// <summary>
/// Represents the data of a durable agent, including its conversation history.
/// </summary>
internal sealed class DurableAgentStateData
{
    /// <summary>
    /// Gets the ordered list of state entries representing the complete conversation history.
    /// This includes both user messages and agent responses in chronological order.
    /// </summary>
    [JsonPropertyName("conversationHistory")]
    public IList<DurableAgentStateEntry> ConversationHistory { get; init; } = [];

    /// <summary>
    /// Gets or sets the expiration time (UTC) for this agent entity.
    /// If the entity is idle beyond this time, it will be automatically deleted.
    /// </summary>
    [JsonPropertyName("expirationTimeUtc")]
    public DateTime? ExpirationTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the plan tasks tracked by this agent session, keyed by task ID.
    /// This field persists task IDs independently of conversation history, ensuring
    /// they survive LLM context compaction. Null or empty means no active plan tasks.
    /// </summary>
    /// <remarks>
    /// This is an additive field — existing entities without it deserialize as null,
    /// which is treated as "no plan tasks" (backward compatible).
    /// </remarks>
    [JsonPropertyName("planTasks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, PlanTaskRecord>? PlanTasks { get; set; }

    /// <summary>
    /// Gets any additional data found during deserialization that does not map to known properties.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; }
}
