using System;
using Microsoft.Agents.AI.Workflows;

namespace Streaming;
public static class Program
{
    public static async Task Main(string[] args)
    {
        // 创建执行器
        UppercaseExecutor uppercaseExecutor = new();
        ReverseExecutor reverseExecutor = new();

        // 构建工作流
        WorkflowBuilder wfb = new(uppercaseExecutor);
        // 将 uppercaseExecutor 的输出连接到 reverseExecutor 的输入
        wfb.AddEdge(uppercaseExecutor, reverseExecutor).WithOutputFrom(reverseExecutor);
        var wf = wfb.Build();

        // 执行工作流
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(wf, input: "Hello, World!");
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is ExecutorCompletedEvent completedEvent)
            {
                Console.WriteLine($"Executor '{completedEvent.ExecutorId}' completed with output: {completedEvent.Data}");
            }
        }
    }
}

/**
 * 下面示例中定义了两个执行器
    * UppercaseExecutor：将输入字符串转换为大写。
    * ReverseExecutor：将输入字符串反转。
    * 这些执行器继承自Executor基类，并实现了HandleAsync方法来处理输入消息并返回结果。
    * 你可以在工作流中使用这些执行器来处理字符串数据。
 */
internal class UppercaseExecutor() : Executor<string, string>("UppercaseExecutor")
{
    public override ValueTask<string> HandleAsync(string message, IWorkflowContext context, 
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(message.ToUpperInvariant());
    }
}

internal class ReverseExecutor() : Executor<string, string>("ReverseExecutor")
{
    public override ValueTask<string> HandleAsync(string message, IWorkflowContext context, 
        CancellationToken cancellationToken = default)
    {
        var charArray = message.ToCharArray();
        Array.Reverse(charArray);
        return ValueTask.FromResult(new string(charArray));
    }
}