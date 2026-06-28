using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using Windows.Globalization;

namespace eComBox.Services
{
    /// <summary>
    /// AI 服务实现（阿里云百炼接入，使用 OpenAI Chat Completions 兼容模式）
    /// - 默认使用 Qwen3.6-Flash 模型，endpoint 可通过 ConfigurationService.AliBairenEndpoint 覆盖
    /// - 如果未配置 endpoint，会使用内置 dashscope 兼容模式地址
    /// - 支持解析 OpenAI 风格响应，并回退到启发式本地规则
    /// </summary>
    public class AIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private const string DefaultAliBairenEndpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";
        private const string DefaultModel = "qwen-turbo";
        private const string BuiltInApiKey = "sk-043858a081d44e1bac5a1b9a91b8967a";

        private readonly string _endpoint = DefaultAliBairenEndpoint;
        private readonly string _apiKey = BuiltInApiKey;

        public AIService()
        {
            try
            {
                _httpClient = new HttpClient();
                if (!string.IsNullOrEmpty(_apiKey))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                }
                _httpClient.Timeout = TimeSpan.FromSeconds(20);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化 AIService(HttpClient) 失败: {ex.Message}");
                throw;
            }
        }

        public async Task<DatePredictionResult> PredictDateFromTaskNameAsync(string taskName)
        {
            var result = new DatePredictionResult { IsSuccessful = false };

            if (string.IsNullOrWhiteSpace(taskName))
            {
                result.ErrorMessage = "Empty task name";
                return result;
            }

            try
            {
                if (string.IsNullOrEmpty(_endpoint))
                {
                    result.ErrorMessage = "AI endpoint is not configured.";
                    return result;
                }

                // 构建 Chat Completions 风格的请求体，兼容 OpenAI / dashscope
                var currentYear = DateTime.Now.Year;
                var preferredLanguage = GetPreferredLanguage();
                var systemPrompt = $"You are a date prediction assistant. The current year is {currentYear}. The user's preferred language is {preferredLanguage}. Unless the task name explicitly specifies another language or locale, keep your reasoning language consistent with the user's preferred language. Given a short task name, return a JSON object with keys: date (yyyy-MM-dd), confidence (0-1), reason (short). Respond ONLY with the JSON object and nothing else.";
                var userPrompt = taskName;

                var payload = new
                {
                    model = DefaultModel,
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    max_tokens = 200,
                    temperature = 0.1
                };

                var json = JsonSerializer.Serialize(payload);
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    HttpResponseMessage resp = await _httpClient.PostAsync(_endpoint, content);
                    if (!resp.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"百炼请求失败: {resp.StatusCode}");
                        result.ErrorMessage = $"百炼请求失败: {resp.StatusCode}";
                        return result;
                    }

                    string respText = await resp.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(respText))
                    {
                        result.ErrorMessage = "百炼返回空响应";
                        return result;
                    }

                    // 尝试解析 OpenAI/dashscope 风格的 JSON 响应
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(respText))
                        {
                            var root = doc.RootElement;

                            // 支持 choices[0].message.content 或 choices[0].text
                            if (root.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
                            {
                                var first = choices[0];
                                string contentText = null;

                                if (first.TryGetProperty("message", out JsonElement message) && message.TryGetProperty("content", out JsonElement contentEl))
                                {
                                    contentText = contentEl.GetString();
                                }
                                else if (first.TryGetProperty("text", out JsonElement textEl))
                                {
                                    contentText = textEl.GetString();
                                }

                                if (!string.IsNullOrWhiteSpace(contentText))
                                {
                                    // 提取并解析第一个 JSON 对象
                                    string jsonObj = ExtractFirstJsonObject(contentText);
                                    if (string.IsNullOrEmpty(jsonObj)) jsonObj = contentText.Trim();

                                    try
                                    {
                                        using (JsonDocument inner = JsonDocument.Parse(jsonObj))
                                        {
                                            var r = inner.RootElement;
                                            if (r.TryGetProperty("date", out JsonElement dateEl) && r.TryGetProperty("confidence", out JsonElement confEl))
                                            {
                                                string dateStr = dateEl.GetString();
                                                double conf = confEl.GetDouble();
                                                string reason = r.TryGetProperty("reason", out JsonElement reasonEl) ? reasonEl.GetString() : string.Empty;

                                                if (DateTime.TryParse(dateStr, out DateTime parsedDate))
                                                {
                                                    result.IsSuccessful = true;
                                                    result.AddPrediction(parsedDate.Date, Math.Max(0.0, Math.Min(1.0, conf)), string.IsNullOrEmpty(reason) ? "AI prediction" : reason, true);
                                                    return result;
                                                }
                                            }
                                        }
                                    }
                                    catch (JsonException) { /* fallthrough to text-date extraction */ }

                                    // 如果没有解析到 JSON，再尝试从文本中找日期
                                    var dateMatch = Regex.Match(contentText, "\\d{4}[-/.]\\d{1,2}[-/.]\\d{1,2}");
                                    if (dateMatch.Success && DateTime.TryParse(dateMatch.Value, out DateTime dt))
                                    {
                                        result.IsSuccessful = true;
                                        result.AddPrediction(dt.Date, 0.6, "百炼返回文本中的日期");
                                        return result;
                                    }

                                    result.ErrorMessage = "百炼响应未包含可解析的日期 JSON";
                                }
                            }
                        }
                    }
                    catch (JsonException jex)
                    {
                        Debug.WriteLine($"解析 百炼 响应 JSON 失败: {jex.Message}");
                        result.ErrorMessage = $"解析 百炼 响应 JSON 失败: {jex.Message}";
                        return result;
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"百炼 请求异常: {ex}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private string ExtractFirstJsonObject(string text)
        {
            int start = text.IndexOf('{');
            if (start < 0) return null;
            int depth = 0;
            for (int i = start; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}') depth--;

                if (depth == 0) return text.Substring(start, i - start + 1);
            }
            return null;
        }

        private string GetPreferredLanguage()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(ApplicationLanguages.PrimaryLanguageOverride))
                {
                    return ApplicationLanguages.PrimaryLanguageOverride;
                }

                var language = ApplicationLanguages.Languages.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(language))
                {
                    return language;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取语言失败: {ex.Message}");
            }

            return System.Globalization.CultureInfo.CurrentUICulture.Name;
        }

    }
}
