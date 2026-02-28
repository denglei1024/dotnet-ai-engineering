using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI.Workflows;

namespace QuickStart;

public static class Program
{
	public static async Task Main(string[] args)
	{
		string? apiKey = GetEnvironmentVar("AI_API_KEY");
		string? endpoint = GetEnvironmentVar("AI_ENDPOINT");
		string modelId = GetEnvironmentVar("AI_MODEL") ?? "moonshot-v1-8k";

		if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(endpoint))
		{
			Console.WriteLine("请先配置环境变量：");
			Console.WriteLine("- AI_API_KEY: 月之暗面 API Key");
			Console.WriteLine("- AI_ENDPOINT: OpenAI 兼容端点，例如 https://api.moonshot.cn/v1");
			Console.WriteLine("可选：AI_MODEL，默认 moonshot-v1-8k");
			Console.WriteLine("\n配置方法（选一种）：");
			Console.WriteLine("1. 命令行（临时）：$env:AI_API_KEY=\"your-key\"; $env:AI_ENDPOINT=\"...\"; dotnet run");
			Console.WriteLine("2. 系统环境变量（永久）：控制面板 > 系统 > 高级系统设置 > 环境变量");
			Console.WriteLine("3. 快捷脚本（当前会话）：在本目录新建 setup-env.ps1");
			return;
		}

		using var httpClient = new HttpClient();
		var moonshotClient = new MoonshotClient(httpClient, endpoint, apiKey, modelId);
		var qaAgent = new QnAAgent(moonshotClient);

		WorkflowBuilder workflowBuilder = new(qaAgent);
		workflowBuilder.WithOutputFrom(qaAgent);
		var workflow = workflowBuilder.Build();

		Console.WriteLine("Microsoft Agent Framework + 月之暗面 示例");
		Console.WriteLine($"Model: {modelId}");
		Console.WriteLine("输入你的问题（输入 exit 退出）：\n");

		var conversationHistory = new List<ChatMessageDTO>();

		while (true)
		{
			Console.Write("你 > ");
			string? input = Console.ReadLine();

			if (string.IsNullOrWhiteSpace(input))
			{
				continue;
			}

			if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
			{
				break;
			}

			conversationHistory.Add(new ChatMessageDTO { role = "user", content = input });
			moonshotClient.SetConversationHistory(conversationHistory);

			await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input);
			Console.Write("\nAgent > ");
			string agentResponse = "";
			
			await foreach (WorkflowEvent evt in run.WatchStreamAsync())
			{
				if (evt is ExecutorCompletedEvent completedEvent)
				{
					if (completedEvent.Data is string text && text.StartsWith("调用模型失败", StringComparison.OrdinalIgnoreCase))
					{
						Console.Write(text);
					}

					agentResponse = completedEvent.Data as string ?? "";
					Console.WriteLine();
				}
			}

			if (!string.IsNullOrWhiteSpace(agentResponse))
			{
				conversationHistory.Add(new ChatMessageDTO { role = "assistant", content = agentResponse });
			}
		}
	}

	private static string? GetEnvironmentVar(string name)
	{
		string? value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
		if (!string.IsNullOrWhiteSpace(value)) return value;

		value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
		if (!string.IsNullOrWhiteSpace(value)) return value;

		value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
		return !string.IsNullOrWhiteSpace(value) ? value : null;
	}
}

internal sealed class QnAAgent(IChatClient chatClient) : Executor<string, string>("MoonshotQnAAgent")
{
	public override async ValueTask<string> HandleAsync(
		string message,
		IWorkflowContext context,
		CancellationToken cancellationToken = default)
	{
		return await chatClient.GetReplyAsync(message, cancellationToken);
	}
}

internal interface IChatClient
{
	Task<string> GetReplyAsync(string userInput, CancellationToken cancellationToken = default);
}

internal sealed class MoonshotClient(HttpClient httpClient, string endpoint, string apiKey, string modelId) : IChatClient
{
	private readonly HttpClient _httpClient = httpClient;
	private readonly string _url = BuildCompletionUrl(endpoint);
	private readonly string _apiKey = apiKey;
	private readonly string _modelId = modelId;
	private List<ChatMessageDTO> _conversationHistory = [];

	public void SetConversationHistory(List<ChatMessageDTO> history)
	{
		_conversationHistory = history ?? [];
	}

	public async Task<string> GetReplyAsync(string userInput, CancellationToken cancellationToken = default)
	{
		var messages = new List<object>
		{
			new { role = "system", content = "你是一个简洁、专业的中文 AI 助手。" }
		};

		foreach (var msg in _conversationHistory)
		{
			messages.Add(new { role = msg.role, content = msg.content });
		}

		var payload = new
		{
			model = _modelId,
			temperature = 0.7,
			stream = true,
			messages = messages
		};

		var request = new HttpRequestMessage(HttpMethod.Post, _url)
		{
			Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

		var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorJson = await response.Content.ReadAsStringAsync(cancellationToken);
			Console.Write($"调用模型失败: {(int)response.StatusCode} - {errorJson}");

			return $"调用模型失败: {(int)response.StatusCode} - {errorJson}";
		}

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var reader = new StreamReader(stream);
		var fullText = new StringBuilder();

		while (true)
		{
			string? line = await reader.ReadLineAsync();
			if (line is null)
			{
				break;
			}

			if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string data = line[5..].Trim();
			if (data.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
			{
				break;
			}

			if (!TryExtractDeltaText(data, out string? delta) || string.IsNullOrEmpty(delta))
			{
				continue;
			}

			Console.Write(delta);
			fullText.Append(delta);
		}

		if (fullText.Length == 0)
		{
			return "未获取到模型输出。";
		}

		return fullText.ToString();
	}

	private static bool TryExtractDeltaText(string jsonData, out string? delta)
	{
		delta = null;

		try
		{
			using var doc = JsonDocument.Parse(jsonData);
			var root = doc.RootElement;
			if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
			{
				return false;
			}

			var firstChoice = choices[0];
			if (firstChoice.TryGetProperty("delta", out var deltaObj) &&
				deltaObj.TryGetProperty("content", out var content))
			{
				delta = content.GetString();
				return true;
			}

			if (firstChoice.TryGetProperty("message", out var messageObj) &&
				messageObj.TryGetProperty("content", out var messageContent))
			{
				delta = messageContent.GetString();
				return true;
			}

			return false;
		}
		catch
		{
			return false;
		}
	}

	private static string BuildCompletionUrl(string endpoint)
	{
		string trimmed = endpoint.Trim().TrimEnd('/');
		if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
		{
			return trimmed;
		}

		return $"{trimmed}/chat/completions";
	}
}

internal sealed class ChatMessageDTO
{
	public string role { get; set; } = string.Empty;
	public string content { get; set; } = string.Empty;
}
