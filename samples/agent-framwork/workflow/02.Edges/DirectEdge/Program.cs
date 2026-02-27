using System;
using Microsoft.Agents.AI.Workflows;

namespace DirectEdge;

public static class Program
{
	public static async Task Main(string[] args)
	{
		var normalizeExecutor = new NormalizeTextExecutor();
		var decorateExecutor = new DecorateTextExecutor();

		WorkflowBuilder workflowBuilder = new(normalizeExecutor);
		workflowBuilder
			.AddEdge(normalizeExecutor, decorateExecutor)
			.WithOutputFrom(decorateExecutor);

		Workflow workflow = workflowBuilder.Build();

		const string input = "hello, microsoft agent framework";
		Console.WriteLine($"Input: {input}");

		await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input);
		await foreach (WorkflowEvent evt in run.WatchStreamAsync())
		{
			if (evt is ExecutorCompletedEvent completedEvent)
			{
				Console.WriteLine($"Executor '{completedEvent.ExecutorId}' output: {completedEvent.Data}");
			}
		}
	}
}

internal class NormalizeTextExecutor() : Executor<string, string>("NormalizeTextExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		var normalized = message.Trim().ToUpperInvariant();
		return ValueTask.FromResult(normalized);
	}
}

internal class DecorateTextExecutor() : Executor<string, string>("DecorateTextExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		var output = $"[Direct Edge Result] {message}";
		return ValueTask.FromResult(output);
	}
}

