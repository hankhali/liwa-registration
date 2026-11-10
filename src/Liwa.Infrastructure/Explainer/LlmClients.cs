using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Liwa.Application.Admin;
using Liwa.Application.Explainer;

namespace Liwa.Infrastructure.Explainer;

/// <summary>Anthropic Messages API (default model claude-sonnet-5-5). Base URL is configurable.</summary>
internal sealed class AnthropicClient(HttpClient http, AiProviderConfig config) : ILlmClient
{
    public async Task<LlmReply> CompleteAsync(string system, string user, int maxTokens, CancellationToken ct)
    {
        var url = new Uri(new Uri((config.BaseUrl ?? "https://api.anthropic.com").TrimEnd('/') + "/"), "v1/messages");
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                model = config.Model,
                max_tokens = maxTokens,
                system,
                messages = new[] { new { role = "user", content = user } },
            }),
        };
        req.Headers.Add("x-api-key", config.ApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var json = JsonNode.Parse(await res.Content.ReadAsStringAsync(ct))!;
        var text = string.Concat((json["content"]?.AsArray() ?? []).Where(c => (string?)c?["type"] == "text").Select(c => (string?)c?["text"]));
        return new LlmReply(text.Trim(), (int?)json["usage"]?["input_tokens"] ?? 0, (int?)json["usage"]?["output_tokens"] ?? 0);
    }
}

/// <summary>Any OpenAI-compatible chat-completions endpoint (brief §4: provider/model/base URL configurable).</summary>
internal sealed class OpenAiCompatibleClient(HttpClient http, AiProviderConfig config) : ILlmClient
{
    public async Task<LlmReply> CompleteAsync(string system, string user, int maxTokens, CancellationToken ct)
    {
        var url = new Uri(new Uri(config.BaseUrl!.TrimEnd('/') + "/"), "chat/completions");
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                model = config.Model,
                max_tokens = maxTokens,
                messages = new[] { new { role = "system", content = system }, new { role = "user", content = user } },
            }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var json = JsonNode.Parse(await res.Content.ReadAsStringAsync(ct))!;
        var text = (string?)json["choices"]?[0]?["message"]?["content"] ?? "";
        return new LlmReply(text.Trim(), (int?)json["usage"]?["prompt_tokens"] ?? 0, (int?)json["usage"]?["completion_tokens"] ?? 0);
    }
}

/// <summary>
/// Demo provider: builds a plain explanation from the payload itself, with no network call. Only offered when the API
/// runs in Development; any other environment refuses it at start-up (brief §4).
/// </summary>
internal sealed class MockLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<LlmReply> CompleteAsync(string system, string user, int maxTokens, CancellationToken ct)
    {
        if (system.Contains("Classify", StringComparison.Ordinal)) return Task.FromResult(new LlmReply("{\"intent\":\"none\",\"course\":null}", 10, 5));
        var marker = user.IndexOf("PAYLOAD:", StringComparison.Ordinal);
        var payload = JsonSerializer.Deserialize<ExplanationPayload>(user[(marker + 8)..], Json)!;
        var ar = payload.Language == "ar";
        var sb = new StringBuilder();
        if (payload.WhatIf is { } w)
        {
            sb.Append(ar ? $"لقد أعاد المخطِّط حساب خطتك. " : "The planner re-ran your plan. ");
            sb.Append(w.SemesterDelta switch
            {
                > 0 => ar ? $"هذا التغيير يؤخر تخرجك {w.SemesterDelta} فصل." : $"This change delays your graduation by {w.SemesterDelta} semester(s).",
                < 0 => ar ? "هذا التغيير يقدّم موعد تخرجك." : "This change brings your graduation earlier.",
                _ => ar ? "موعد تخرجك لا يتغير." : "Your graduation date stays the same.",
            });
        }
        else if (payload.Focus is { } f)
            sb.Append(ar ? $"لا يمكنك أخذ {f.Code} هذا الفصل بعد. اطّلع على السبب أدناه، ويمكن لمرشدك مساعدتك." : $"You can't take {f.Code} this semester yet. The reason is shown below, and your advisor can help.");
        else
        {
            sb.Append(ar ? $"تتضمن خطتك {payload.Planned.Count} مقررات بمجموع {payload.TotalCredits} ساعة معتمدة. " : $"Your plan has {payload.Planned.Count} courses, {payload.TotalCredits} credits in total. ");
            if (payload.Planned.Count > 0 && payload.Planned[0] is { } first)
                sb.Append(ar ? $"بدأنا بـ {first.Code} لأن مقررات أخرى تعتمد عليه. " : $"We started with {first.Code} because other courses depend on it. ");
            sb.Append(ar ? "كل المحاضرات المختارة لا تتعارض في الوقت. " : "None of the classes clash. ");
            sb.Append(ar ? "إذا كانت لديك أسئلة فاسأل مرشدك." : "If anything is unclear, ask your advisor.");
        }

        var text = sb.ToString();
        return Task.FromResult(new LlmReply(text, user.Length / 4, text.Length / 4));
    }
}
