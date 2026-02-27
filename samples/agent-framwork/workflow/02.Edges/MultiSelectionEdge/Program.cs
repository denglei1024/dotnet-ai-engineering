using Microsoft.Agents.AI.Workflows;

namespace MultiSelectionEdge;

public static class Program
{
	public static async Task Main(string[] args)
	{
		// 初始化执行器
		var classifyExecutor = new MultiIntentClassifyExecutor();
		var billingExecutor = new BillingExecutor();
		var technicalExecutor = new TechnicalExecutor();
		var accountExecutor = new AccountExecutor();
		var fallbackExecutor = new FallbackExecutor();

		// 构建工作流：一个输入可同时命中多个分支（多重选择 Edge）
		WorkflowBuilder workflowBuilder = new(classifyExecutor);
		var targets = new ExecutorBinding[]
		{
			billingExecutor,
			technicalExecutor,
			accountExecutor,
			fallbackExecutor,
		};

		workflowBuilder.AddFanOutEdge<string>(
			classifyExecutor,
			targets,
			(classification, targetCount) =>
			{
				var routeText = classification ?? string.Empty;
                // 根据分类结果，确定应该路由到哪些分支。这里允许一个输入同时命中多个分支。
                // 数字索引对应 targets 数组中的执行器位置，最后一个索引（targetCount - 1）保留给默认分支（fallbackExecutor）。
				var indexes = new List<int>();

				if (routeText.Contains("BILLING", StringComparison.OrdinalIgnoreCase))
				{
                    // 命中账单分支
					indexes.Add(0);
				}

				if (routeText.Contains("TECH", StringComparison.OrdinalIgnoreCase))
				{
                    // 命中技术分支
					indexes.Add(1);
				}

				if (routeText.Contains("ACCOUNT", StringComparison.OrdinalIgnoreCase))
				{
                    // 命中账号分支
					indexes.Add(2);
				}

				if (indexes.Count == 0)
				{
                    // 没有命中任何特定分支，路由到默认分支
					indexes.Add(targetCount - 1);
				}

				return indexes;
			});

		workflowBuilder.WithOutputFrom(billingExecutor, technicalExecutor, accountExecutor, fallbackExecutor);
		Workflow workflow = workflowBuilder.Build();

		string input = args.Length > 0
			? string.Join(' ', args)
			: "billing invoice wrong and login error";

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

internal class MultiIntentClassifyExecutor() : Executor<string, string>("MultiIntentClassifyExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		var normalized = message.Trim();
		var tags = new List<string>();

		if (normalized.Contains("billing", StringComparison.OrdinalIgnoreCase)
			|| normalized.Contains("invoice", StringComparison.OrdinalIgnoreCase)
			|| normalized.Contains("账单", StringComparison.OrdinalIgnoreCase))
		{
			tags.Add("BILLING");
		}

		if (normalized.Contains("error", StringComparison.OrdinalIgnoreCase)
			|| normalized.Contains("bug", StringComparison.OrdinalIgnoreCase)
			|| normalized.Contains("技术", StringComparison.OrdinalIgnoreCase)
			|| normalized.Contains("login", StringComparison.OrdinalIgnoreCase))
		{
			tags.Add("TECH");
		}

		if (normalized.Contains("account", StringComparison.OrdinalIgnoreCase)
			|| normalized.Contains("profile", StringComparison.OrdinalIgnoreCase)
			|| normalized.Contains("账号", StringComparison.OrdinalIgnoreCase))
		{
			tags.Add("ACCOUNT");
		}

		var result = tags.Count > 0
			? string.Join('|', tags)
			: "OTHER";

		return ValueTask.FromResult(result);
	}
}

internal class BillingExecutor() : Executor<string, string>("BillingExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		return ValueTask.FromResult($"[Billing Branch] 处理账单相关请求 => {message}");
	}
}

internal class TechnicalExecutor() : Executor<string, string>("TechnicalExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		return ValueTask.FromResult($"[Technical Branch] 处理技术支持请求 => {message}");
	}
}

internal class AccountExecutor() : Executor<string, string>("AccountExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		return ValueTask.FromResult($"[Account Branch] 处理账号相关请求 => {message}");
	}
}

internal class FallbackExecutor() : Executor<string, string>("FallbackExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		return ValueTask.FromResult($"[Fallback Branch] 未命中分类，进入默认处理 => {message}");
	}
}
