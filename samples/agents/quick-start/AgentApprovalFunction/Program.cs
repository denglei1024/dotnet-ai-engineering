using System.ComponentModel;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

[Description("根据提供的城市名称，返回当前的天气信息")]
static string GetWeather([Description("城市名称")] string city)
{
    // 这里可以调用实际的天气API来获取天气信息
    return $"当前{city}的天气是晴朗，温度25.333摄氏度。";
}

var endpoint = Environment.GetEnvironmentVariable("MODELPROVIDER_ENDPOINT")
    ?? throw new InvalidOperationException("请设置环境变量 MODELPROVIDER_ENDPOINT");

var apiKey = Environment.GetEnvironmentVariable("MODELPROVIDER_API_KEY")
    ?? throw new InvalidOperationException("请设置环境变量 MODELPROVIDER_API_KEY");

var modelId = Environment.GetEnvironmentVariable("MODELPROVIDER_MODEL_ID")
    ?? "kimi-k2-0905-preview";

AIFunction weatherFunction = AIFunctionFactory.Create(GetWeather);

AIAgent agent = new OpenAIClient(
        new System.ClientModel.ApiKeyCredential(apiKey),
        new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
    )
    .GetChatClient(modelId)
    .AsAIAgent(instructions: "你是一个智能助手，请保持友好和专业的态度，帮助用户解答问题。", name: "hello-agent",
        tools: [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(GetWeather))]);

Console.WriteLine(await agent.RunAsync("请告诉我武汉的天气。"));

/**
 *
 * 这是一段多轮对话的，保存上下文的示例
 * 如果在 RunAsync不传递 session，第二次对话，ai不会知道”我的名字“
// 创建一个session，保存对话上下文
AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine(await agent.RunAsync("我的名字是缤果，请记住我的名字。", session));

Console.WriteLine(await agent.RunAsync("你知道我叫什么名字吗？", session));
*/
    