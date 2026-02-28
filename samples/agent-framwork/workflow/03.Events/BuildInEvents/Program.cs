using Microsoft.Agents.AI.Workflows;

namespace BuildInEvents;

public static class Program
{
	public static async Task Main(string[] args)
	{
		var normalizeExecutor = new NormalizeTextExecutor();
		var wordCountExecutor = new WordCountExecutor();

		WorkflowBuilder workflowBuilder = new(normalizeExecutor);
		workflowBuilder
			.AddEdge(normalizeExecutor, wordCountExecutor)
			.WithOutputFrom(wordCountExecutor);

		Workflow workflow = workflowBuilder.Build();

		const string input = "  Hello, Built-in Workflow Events!  ";
		Console.WriteLine($"Input: {input}");
		Console.WriteLine("--- Built-in events ---");

		await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input);

		var eventIndex = 0;
		var workflowStartedSeen = false;

		await foreach (WorkflowEvent evt in run.WatchStreamAsync())
		{
			eventIndex++;

			switch (evt)
			{
                // 1. 步骤开始
                case SuperStepStartedEvent superStepStartedEvent:
                    Console.WriteLine($"#{eventIndex} [SuperStepStartedEvent] SuperStep={superStepStartedEvent.StepNumber}, StepInfo = {superStepStartedEvent.StartInfo}, Data={superStepStartedEvent.Data}");
                    break;

                // 2. 执行器被调用
                case ExecutorInvokedEvent invokedEvent:
                    Console.WriteLine($"#{eventIndex} [ExecutorInvokedEvent] Executor={invokedEvent.ExecutorId}, Data={invokedEvent.Data}");
                    break;
                
                // 3. 执行器完成
				case ExecutorCompletedEvent completedEvent:
					Console.WriteLine($"#{eventIndex} [ExecutorCompletedEvent] Executor={completedEvent.ExecutorId}, Data={completedEvent.Data}");
					break;

                // 4. 执行器产出
                case WorkflowOutputEvent outputEvent:
                    Console.WriteLine($"#{eventIndex} [WorkflowOutputEvent] Output={outputEvent.Data}");
                    break;        
                
                // 5. 超级步骤完成
                case SuperStepCompletedEvent superStepCompletedEvent:
                    Console.WriteLine($"#{eventIndex} [SuperStepCompletedEvent] SuperStep={superStepCompletedEvent.StepNumber}, StepInfo = {superStepCompletedEvent.CompletionInfo}, Data={superStepCompletedEvent.Data}");
                    break;

                // 请求相关事件
                case RequestInfoEvent requestInfoEvent:
                    Console.WriteLine($"#{eventIndex} [RequestInfoEvent] RequestId={requestInfoEvent.Request}, Info={requestInfoEvent.Data}");
                    break;
                case ExecutorFailedEvent failedEvent:
                    Console.WriteLine($"#{eventIndex} [ExecutorFailedEvent] Executor={failedEvent.ExecutorId}, Error={failedEvent.Data}");
                    break;
                // case AgentResponseEvent agentResponseEvent:
                //     Console.WriteLine($"[AgentResponseEvent] Executor={agentResponseEvent.ExecutorId}, Response={agentResponseEvent.Data}");
                //     break;
                // case AgentResponseUpdateEvent responseUpdateEvent:
                //     Console.WriteLine($"[AgentResponseUpdateEvent] Executor={responseUpdateEvent.ExecutorId}, Update={responseUpdateEvent.Data}");
                //     break;
                
                // 工作流相关事件
                // 触发时机：工作流运行上下文初始化完成后触发，通常会早于第一个 SuperStepStartedEvent。
                case WorkflowStartedEvent startedEvent:
                    workflowStartedSeen = true;
                    Console.WriteLine($"#{eventIndex} [WorkflowStartedEvent] Workflow={startedEvent.Data}");
                    break;

                case WorkflowErrorEvent errorEvent:
                    Console.WriteLine($"#{eventIndex} [WorkflowErrorEvent] Error={errorEvent.Data}");
                    break;
                case WorkflowWarningEvent warningEvent:
                    Console.WriteLine($"#{eventIndex} [WorkflowWarningEvent] Warning={warningEvent.Data}");
                    break;
				default:
					Console.WriteLine($"#{eventIndex} [{evt.GetType().Name}] {evt}");
					break;
			}
		}

		if (!workflowStartedSeen)
		{
			Console.WriteLine("[说明] 本次运行未收到 WorkflowStartedEvent；在当前执行模式下该事件可能不会被发出。");
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

internal class WordCountExecutor() : Executor<string, string>("WordCountExecutor")
{
	public override ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		var wordCount = message.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
		var output = $"{message} | WordCount={wordCount}";
		return ValueTask.FromResult(output);
	}
}
