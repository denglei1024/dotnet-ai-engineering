using Microsoft.Agents.AI.Workflows;

namespace ConditionalEdge;

public static class Program
{
	public static async Task Main(string[] args)
	{
        // 初始化执行器
		var classifyExecutor = new ClassifyRequestExecutor();
		var urgentExecutor = new UrgentPathExecutor();
		var normalExecutor = new NormalPathExecutor();

        // 构建工作流并添加条件边
		WorkflowBuilder workflowBuilder = new(classifyExecutor);

        // 根据 classifyExecutor 的输出结果，动态路由到 urgentExecutor 或 normalExecutor
		workflowBuilder.AddEdge<string>(
			classifyExecutor,
			urgentExecutor,
			result => result.StartsWith("URGENT:", StringComparison.OrdinalIgnoreCase));

		workflowBuilder.AddEdge<string>(
			classifyExecutor,
			normalExecutor,
			result => !result.StartsWith("URGENT:", StringComparison.OrdinalIgnoreCase));

		workflowBuilder.WithOutputFrom(urgentExecutor, normalExecutor);
		Workflow workflow = workflowBuilder.Build();

		string input = args.Length > 0
			? string.Join(' ', args)
			: "Please help me reset my password urgently";

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

internal class ClassifyRequestExecutor() : Executor<string, string>("ClassifyRequestExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		var normalized = message.Trim();
		var isUrgent = normalized.Contains("urgent", StringComparison.OrdinalIgnoreCase)
			|| normalized.Contains("asap", StringComparison.OrdinalIgnoreCase)
			|| normalized.Contains("紧急", StringComparison.OrdinalIgnoreCase);

		var result = isUrgent ? $"URGENT:{normalized}" : $"NORMAL:{normalized}";
		return ValueTask.FromResult(result);
	}
}

internal class UrgentPathExecutor() : Executor<string, string>("UrgentPathExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		return ValueTask.FromResult($"[Urgent Path] 已升级为高优先级处理 => {message}");
	}
}

internal class NormalPathExecutor() : Executor<string, string>("NormalPathExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		return ValueTask.FromResult($"[Normal Path] 已进入常规处理队列 => {message}");
	}
}
