using Microsoft.Agents.AI.Workflows;

namespace SwitchCaseEdge;

public static class Program
{
	public static async Task Main(string[] args)
	{
		// 初始化执行器
		var routeKeyExecutor = new RouteKeyExecutor();
		var billingExecutor = new BillingPathExecutor();
		var technicalExecutor = new TechnicalPathExecutor();
		var fallbackExecutor = new FallbackPathExecutor();

		// 构建工作流并添加 Switch-Case Edge
		WorkflowBuilder workflowBuilder = new(routeKeyExecutor);
		workflowBuilder.AddSwitch(routeKeyExecutor, switchBuilder =>
		{
			switchBuilder
				.AddCase<string>(
					result => result?.StartsWith("BILLING:", StringComparison.OrdinalIgnoreCase) == true,
					billingExecutor)
				.AddCase<string>(
					result => result?.StartsWith("TECH:", StringComparison.OrdinalIgnoreCase) == true,
					technicalExecutor)
				.WithDefault(fallbackExecutor);
		});

		workflowBuilder.WithOutputFrom(billingExecutor, technicalExecutor, fallbackExecutor);
		Workflow workflow = workflowBuilder.Build();

		string input = args.Length > 0
			? string.Join(' ', args)
			: "my billing invoice is incorrect";

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

internal class RouteKeyExecutor() : Executor<string, string>("RouteKeyExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		var normalized = message.Trim();
		if (normalized.Contains("billing", StringComparison.OrdinalIgnoreCase)
			|| normalized.Contains("invoice", StringComparison.OrdinalIgnoreCase)
			|| normalized.Contains("账单", StringComparison.OrdinalIgnoreCase))
		{
			return ValueTask.FromResult($"BILLING:{normalized}");
		}

		if (normalized.Contains("error", StringComparison.OrdinalIgnoreCase)
			|| normalized.Contains("bug", StringComparison.OrdinalIgnoreCase)
			|| normalized.Contains("login", StringComparison.OrdinalIgnoreCase)
			|| normalized.Contains("技术", StringComparison.OrdinalIgnoreCase))
		{
			return ValueTask.FromResult($"TECH:{normalized}");
		}

		return ValueTask.FromResult($"OTHER:{normalized}");
	}
}

internal class BillingPathExecutor() : Executor<string, string>("BillingPathExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		return ValueTask.FromResult($"[Billing Case] 已路由到账单处理分支 => {message}");
	}
}

internal class TechnicalPathExecutor() : Executor<string, string>("TechnicalPathExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		return ValueTask.FromResult($"[Technical Case] 已路由到技术支持分支 => {message}");
	}
}

internal class FallbackPathExecutor() : Executor<string, string>("FallbackPathExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		return ValueTask.FromResult($"[Default Case] 未匹配到明确分类，进入默认分支 => {message}");
	}
}
