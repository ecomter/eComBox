using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Storage;

namespace eComBox.Services
{
    /// <summary>
    /// Calls the eComBox server. The AI provider credential never reaches the client.
    /// </summary>
    public class AIService : IAIService
    {
        private const string PredictEndpoint = "https://api.cohelper.tech/api/ai/predict";
        private const string DeviceIdSettingKey = "AIServerDeviceId";
        private readonly HttpClient _httpClient;

        public AIService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(35) };
        }

        public async Task<DatePredictionResult> PredictDateFromTaskNameAsync(string taskName)
        {
            var result = new DatePredictionResult { IsSuccessful = false };
            if (string.IsNullOrWhiteSpace(taskName))
            {
                result.ErrorMessage = "Empty task name";
                return result;
            }

            var appToken = ConfigurationService.AppServerToken;
            if (string.IsNullOrWhiteSpace(appToken))
            {
                result.ErrorMessage = "AI server token is not configured.";
                return result;
            }

            try
            {
                var payload = JsonSerializer.Serialize(new
                {
                    taskName = taskName.Trim(),
                    language = GetPreferredLanguage()
                });

                using (var request = new HttpRequestMessage(HttpMethod.Post, PredictEndpoint))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appToken);
                    request.Headers.Add("X-Device-Id", GetOrCreateDeviceId());
                    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                    using (var response = await _httpClient.SendAsync(request))
                    {
                        var responseText = await response.Content.ReadAsStringAsync();
                        if (!response.IsSuccessStatusCode)
                        {
                            result.ErrorMessage = GetServerError(responseText, response.StatusCode.ToString());
                            return result;
                        }

                        using (var document = JsonDocument.Parse(responseText))
                        {
                            if (!document.RootElement.TryGetProperty("prediction", out JsonElement prediction) ||
                                !prediction.TryGetProperty("date", out JsonElement dateElement) ||
                                !DateTime.TryParse(dateElement.GetString(), out DateTime date))
                            {
                                result.ErrorMessage = "AI server returned an invalid prediction.";
                                return result;
                            }

                            double confidence = prediction.TryGetProperty("confidence", out JsonElement confidenceElement)
                                ? confidenceElement.GetDouble()
                                : 0.0;
                            string reason = prediction.TryGetProperty("reason", out JsonElement reasonElement)
                                ? reasonElement.GetString()
                                : "AI prediction";

                            result.IsSuccessful = true;
                            result.AddPrediction(date.Date, Math.Max(0.0, Math.Min(1.0, confidence)),
                                string.IsNullOrWhiteSpace(reason) ? "AI prediction" : reason, true);
                            return result;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"AI server request failed: {exception}");
                result.ErrorMessage = exception.Message;
                return result;
            }
        }

        private static string GetOrCreateDeviceId()
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue(DeviceIdSettingKey, out object value) &&
                value is string existing && !string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            string deviceId = Guid.NewGuid().ToString("N");
            settings.Values[DeviceIdSettingKey] = deviceId;
            return deviceId;
        }

        private static string GetServerError(string responseText, string fallback)
        {
            try
            {
                using (var document = JsonDocument.Parse(responseText))
                {
                    if (document.RootElement.TryGetProperty("error", out JsonElement error) &&
                        error.TryGetProperty("message", out JsonElement message))
                    {
                        return message.GetString() ?? fallback;
                    }
                }
            }
            catch (JsonException) { }

            return fallback;
        }

        private static string GetPreferredLanguage()
        {
            if (!string.IsNullOrWhiteSpace(ApplicationLanguages.PrimaryLanguageOverride))
            {
                return ApplicationLanguages.PrimaryLanguageOverride;
            }

            return ApplicationLanguages.Languages.FirstOrDefault()
                ?? System.Globalization.CultureInfo.CurrentUICulture.Name;
        }
    }
}
