using Microsoft.Agents.AI.Workflows;

namespace FanInEdge;

public static class Program
{
	public static async Task Main(string[] args)
	{
		// 入口执行器：对输入做简单标准化
		var inputExecutor = new InputExecutor();

		// 三个上游执行器：模拟不同维度的处理
		var billingAnalysisExecutor = new BillingAnalysisExecutor();
		var technicalAnalysisExecutor = new TechnicalAnalysisExecutor();
		var accountAnalysisExecutor = new AccountAnalysisExecutor();

		// 目标执行器：扇入汇总节点（等待所有上游结果）
		var summaryExecutor = new SummaryExecutor();

		WorkflowBuilder workflowBuilder = new(inputExecutor);

		// input -> 三个上游（可并行）
		workflowBuilder.AddEdge(inputExecutor, billingAnalysisExecutor);
		workflowBuilder.AddEdge(inputExecutor, technicalAnalysisExecutor);
		workflowBuilder.AddEdge(inputExecutor, accountAnalysisExecutor);

		// 三个上游 -> 一个目标（Fan-In Barrier）
		workflowBuilder.AddFanInBarrierEdge(
			new ExecutorBinding[] { billingAnalysisExecutor, technicalAnalysisExecutor, accountAnalysisExecutor },
			summaryExecutor);

		workflowBuilder.WithOutputFrom(summaryExecutor);
		Workflow workflow = workflowBuilder.Build();

		string input = args.Length > 0
			? string.Join(' ', args)
			: "billing invoice issue, login failed, and account profile update needed";

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

internal class InputExecutor() : Executor<string, string>("InputExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		var normalized = message.Trim();
		return ValueTask.FromResult(normalized);
	}
}

internal class BillingAnalysisExecutor() : Executor<string, string>("BillingAnalysisExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		return ValueTask.FromResult($"[Billing] 检测到账单关键词，建议核对 invoice 与扣费记录。原文: {message}");
	}
}

internal class TechnicalAnalysisExecutor() : Executor<string, string>("TechnicalAnalysisExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		return ValueTask.FromResult($"[Technical] 检测到技术问题关键词，建议查看错误日志与重试链路。原文: {message}");
	}
}

internal class AccountAnalysisExecutor() : Executor<string, string>("AccountAnalysisExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		return ValueTask.FromResult($"[Account] 检测到账号诉求，建议验证身份并检查账户配置。原文: {message}");
	}
}

internal class SummaryExecutor() : Executor<string, string>("SummaryExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		return ValueTask.FromResult($"[Fan-In Summary] 汇聚节点收到合并触发消息 => {message}");
	}
}
