// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// Provides ambient access to the current agent session's plan task records.
/// Set by the entity infrastructure before running the agent and cleared afterward.
/// </summary>
public static class PlanTasksContext
{
    [ThreadStatic]
    private static Dictionary<string, PlanTaskRecord>? s_current;

    /// <summary>
    /// Gets or sets the plan tasks dictionary for the current execution context.
    /// </summary>
    public static Dictionary<string, PlanTaskRecord>? Current
    {
        get => s_current;
        internal set => s_current = value;
    }
}
