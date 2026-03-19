// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.AI.DurableTask.Planning;

internal static partial class PlanLogs
{
    [LoggerMessage(
        EventId = 200,
        Level = LogLevel.Information,
        Message = "[{PlanId}] Plan started: '{Goal}' with {StepCount} steps")]
    public static partial void LogPlanStarted(
        this ILogger logger,
        string planId,
        string goal,
        int stepCount);

    [LoggerMessage(
        EventId = 201,
        Level = LogLevel.Warning,
        Message = "[{PlanId}] Plan validation failed with {ErrorCount} errors")]
    public static partial void LogPlanValidationFailed(
        this ILogger logger,
        string planId,
        int errorCount);

    [LoggerMessage(
        EventId = 202,
        Level = LogLevel.Information,
        Message = "[{PlanId}] Step '{StepId}' started (action: {ActionType})")]
    public static partial void LogPlanStepStarted(
        this ILogger logger,
        string planId,
        string stepId,
        string actionType);

    [LoggerMessage(
        EventId = 203,
        Level = LogLevel.Information,
        Message = "[{PlanId}] Step '{StepId}' completed in {DurationMs}ms")]
    public static partial void LogPlanStepCompleted(
        this ILogger logger,
        string planId,
        string stepId,
        long durationMs);

    [LoggerMessage(
        EventId = 204,
        Level = LogLevel.Error,
        Message = "[{PlanId}] Step '{StepId}' failed: {Error}")]
    public static partial void LogPlanStepFailed(
        this ILogger logger,
        string planId,
        string stepId,
        string error);

    [LoggerMessage(
        EventId = 205,
        Level = LogLevel.Warning,
        Message = "[{PlanId}] Replanning triggered after step '{FailedStepId}' failed (attempt {AttemptNumber})")]
    public static partial void LogPlanReplanningTriggered(
        this ILogger logger,
        string planId,
        string failedStepId,
        int attemptNumber);

    [LoggerMessage(
        EventId = 206,
        Level = LogLevel.Information,
        Message = "[{PlanId}] Step '{StepId}' waiting for input '{InputName}'")]
    public static partial void LogPlanWaitingForInput(
        this ILogger logger,
        string planId,
        string stepId,
        string inputName);

    [LoggerMessage(
        EventId = 207,
        Level = LogLevel.Information,
        Message = "[{PlanId}] Step '{StepId}' received input '{InputName}'")]
    public static partial void LogPlanInputReceived(
        this ILogger logger,
        string planId,
        string stepId,
        string inputName);

    [LoggerMessage(
        EventId = 208,
        Level = LogLevel.Information,
        Message = "[{PlanId}] Plan completed: {TotalSteps} steps in {DurationMs}ms")]
    public static partial void LogPlanCompleted(
        this ILogger logger,
        string planId,
        int totalSteps,
        long durationMs);

    [LoggerMessage(
        EventId = 209,
        Level = LogLevel.Error,
        Message = "[{PlanId}] Plan failed: {Error}")]
    public static partial void LogPlanFailed(
        this ILogger logger,
        string planId,
        string error);
}
