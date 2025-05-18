using Azure;
using Azure.AI.TextAnalytics;
using Azure.AI.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using eComBox.Views;
using System.Diagnostics;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Net.Http.Headers;
using CommunityToolkit.WinUI;


namespace eComBox.Services
{
    public class AzureDatePredictionService
    {
        private readonly TextAnalyticsClient _client;
        private List<UserDateSelection> _userDateHistory = new List<UserDateSelection>();
        private List<HolidayInfo> _holidays = new List<HolidayInfo>();
        private readonly OpenAIClient _openAIClient;
        private readonly string _deploymentName;
        private readonly HttpClient _httpClient = new HttpClient();
        private string _azureMLEndpoint;
        private string _azureMLApiKey;
        private string _userRegion = "CN"; // 默认为中国，可以从设置中读取

        public AzureDatePredictionService(string openAIEndpoint, string openAIKey, string deploymentName
                                  )
        {
           
            // Update the initialization of OpenAIClient to use the correct constructor
            _openAIClient = new OpenAIClient(new Uri(openAIEndpoint), new AzureKeyCredential(openAIKey));
          
            _deploymentName = deploymentName;
            //_azureMLEndpoint = azureMLEndpoint;
            //_azureMLApiKey = azureMLApiKey;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _azureMLApiKey);

            // 初始化节假日数据
            InitializeHolidays();

            // 加载用户历史数据
            _ = LoadUserHistoryAsync();
        }

        private void InitializeHolidays()
        {
            
            int currentYear = DateTime.Now.Year;
            int nextYear = currentYear + 1;

            // 添加固定日期的节假日
           

            _holidays.Add(new HolidayInfo
            {
                Name = "劳动节",
                Date = new DateTime(nextYear, 5, 1),
                Region = "CN",
                Category = "NationalHoliday"
            });
            _holidays.Add(new HolidayInfo
            {
                Name = "端午节",
                Date = new DateTime(currentYear, 5, 31),
                Region = "CN",
                Category = "NationalHoliday"
            });
            _holidays.Add(new HolidayInfo
            {
                Name = "父亲节",
                Date = new DateTime(currentYear, 6, 15),
                Region = "CN",
                Category = "NationalHoliday"
            });
            _holidays.Add(new HolidayInfo
            {
                Name = "建党节",
                Date = new DateTime(currentYear, 7, 1),
                Region = "CN",
                Category = "NationalHoliday"
            });
            _holidays.Add(new HolidayInfo
            {
                Name = "建军节",
                Date = new DateTime(currentYear, 8, 1),
                Region = "CN",
                Category = "NationalHoliday"
            });
            _holidays.Add(new HolidayInfo
            {
                Name = "教师节",
                Date = new DateTime(currentYear, 9, 10),
                Region = "CN",
                Category = "NationalHoliday"
            });
            _holidays.Add(new HolidayInfo
            {
                Name = "国庆节",
                Date = new DateTime(currentYear, 10, 1),
                Region = "CN",
                Category = "NationalHoliday"
            });

            _holidays.Add(new HolidayInfo
            {
                Name = "元旦",
                Date = new DateTime(nextYear, 1, 1),
                Region = "CN",
                Category = "NationalHoliday"
            });
            _holidays.Add(new HolidayInfo
            {
                Name = "Independence Day",
                Date = new DateTime(currentYear, 7, 4),
                Region = "US",
                Category = "NationalHoliday"
            });
            _holidays.Add(new HolidayInfo
            {
                Name = "Juneteenth",
                Date = new DateTime(currentYear, 6, 19),
                Region = "US",
                Category = "NationalHoliday"
            });
            _holidays.Add(new HolidayInfo
            {
                Name = "Labor Day",
                Date = new DateTime(currentYear, 9, 1),
                Region = "US",
                Category = "NationalHoliday"
            });
            _holidays.Add(new HolidayInfo
            {
                Name = "Columbus Day",
                Date = new DateTime(currentYear, 10, 13),
                Region = "US",
                Category = "NationalHoliday"
            });

            _holidays.Add(new HolidayInfo
            {
                Name = "Thanksgiving Day",
                Date = new DateTime(currentYear, 11, 27),
                Region = "US",
                Category = "NationalHoliday"
            });

            _holidays.Add(new HolidayInfo
            {
                Name = "Christmas Day",
                Date = new DateTime(currentYear, 12, 25),
                Region = "US",
                Category = "NationalHoliday"
            });
            _holidays.Add(new HolidayInfo
            {
                Name = "Christmas Day",
                Date = new DateTime(currentYear, 12, 25),
                Region = "UK",
                Category = "NationalHoliday"
            });
            _holidays.Add(new HolidayInfo
            {
                Name = "Summer Bank Holiday",
                Date = new DateTime(currentYear, 8, 25),
                Region = "UK",
                Category = "NationalHoliday"
            });

        }

       

        private async Task LoadUserHistoryAsync()
        {
            try
            {
                // 从本地存储加载用户历史选择数据
                // 实际实现时从 ApplicationData 或其他存储获取
                _userDateHistory = await DataStorage.LoadDateHistoryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载用户历史数据失败: {ex.Message}");
            }
        }

        

        // 分析任务名称，预测可能的日期
        public async Task<DatePredictionResult> PredictDateFromTaskNameAsync(string taskName)
        {
            if (string.IsNullOrEmpty(taskName))
            {
                return new DatePredictionResult { IsSuccessful = false };
            }

            try
            {
                // 创建结果对象
                var result = new DatePredictionResult { IsSuccessful = true };


                // 1. 基于用户历史的预测
                AddHistoryBasedPredictions(taskName, result);

                // 2. 节假日预测
                AddHolidayPredictions(taskName, result);

                // 3. 使用Azure AI进行分析
                await AddAzureAIPredictions(taskName, result);

                // 4. 使用Azure ML进行预测
                //await AddAzureMLPredictions(taskName, result);

                // 5. 整合预测结果，合并相似日期并调整置信度
                MergeSimilarPredictions(result);

                return result;
            }
            catch (Exception ex)
            {
                return new DatePredictionResult
                {
                    IsSuccessful = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        private void MergeSimilarPredictions(DatePredictionResult result)
        {
            // 按日期分组
            var groupedByDate = result.Suggestions
                .GroupBy(s => s.SuggestedDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    MaxConfidence = g.Max(s => s.Confidence),
                    AvgConfidence = g.Average(s => s.Confidence),
                    Reasons = g.Select(s => s.Reason).Distinct().ToList()
                })
                .OrderByDescending(g => g.MaxConfidence)
                .ToList();

            // 清除旧建议
            result.Suggestions.Clear();

            // 添加新的合并后的建议
            foreach (var group in groupedByDate)
            {
                string combinedReason = string.Join("；", group.Reasons);
                if (combinedReason.Length > 100)
                {
                    combinedReason = combinedReason.Substring(0, 97) + "...";
                }

                result.AddPrediction(
                    group.Date,
                    Math.Min(group.MaxConfidence + 0.1, 1.0), // 提升置信度但不超过1
                    combinedReason
                );
            }
        }
        public async Task ProvideUserFeedbackAsync(string taskName, DateTime selectedDate, bool wasUseful)
        {
            try
            {
                // 记录用户选择
                var selection = new UserDateSelection
                {
                    TaskName = taskName,
                    SelectedDate = selectedDate,
                    SelectionTime = DateTime.Now
                };

                // 添加到历史
                _userDateHistory.Add(selection);

                // 裁剪历史(保留最近300条)
                if (_userDateHistory.Count > 300)
                {
                    _userDateHistory = _userDateHistory
                        .OrderByDescending(h => h.SelectionTime)
                        .Take(300)
                        .ToList();
                }

                // 保存到存储
                await DataStorage.SaveDateHistoryAsync(_userDateHistory);

                // 如果集成了在线学习反馈，则发送到 Azure ML
                if (wasUseful && !string.IsNullOrEmpty(_azureMLEndpoint) && !string.IsNullOrEmpty(_azureMLApiKey))
                {
                    // 构建反馈数据
                    var feedback = new
                    {
                        taskName = taskName,
                        selectedDate = selectedDate.ToString("yyyy-MM-dd"),
                        userRegion = _userRegion,
                        wasUseful = wasUseful,
                        timestamp = DateTime.Now.ToString("o")
                    };

                    // 将反馈发送到反馈API终结点
                    var feedbackEndpoint = _azureMLEndpoint.Replace("/score", "/feedback");
                    await _httpClient.PostAsJsonAsync(feedbackEndpoint, feedback);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存用户反馈失败: {ex.Message}");
            }
        }
        private void AddHistoryBasedPredictions(string taskName, DatePredictionResult result)
        {
            if (_userDateHistory.Count == 0) return;

            // 寻找类似名称的历史事件
            var similarEvents = _userDateHistory
                .Where(h => h.TaskName.Contains(taskName) || taskName.Contains(h.TaskName))
                .OrderByDescending(h => h.SelectionTime)
                .Take(3)
                .ToList();

            foreach (var ev in similarEvents)
            {
                // 如果历史日期在今天之前，则预测下一个周期
                DateTime predictedDate;
                if (ev.SelectedDate < DateTime.Now.Date)
                {
                    // 估算周期
                    int daysPassed = (DateTime.Now.Date - ev.SelectedDate).Days;
                    int cycle = EstimateCycle(daysPassed);
                    predictedDate = DateTime.Now.Date.AddDays(cycle - (daysPassed % cycle));
                }
                else
                {
                    predictedDate = ev.SelectedDate;
                }

                result.AddPrediction(
                    predictedDate,
                    0.8,
                    $"基于您之前设置的{ ev.TaskName}推荐"
                );
            }

            // 分析用户的日期选择模式
        }

        private int EstimateCycle(int days)
        {
            // 简单的周期估算
            if (days >= 350) return 365; // 年度
            if (days >= 85) return 90;   // 季度
            if (days >= 25) return 30;   // 月度
            if (days >= 5) return 7;     // 周度
            return 1;                    // 日度
        }



      

        private void AddHolidayPredictions(string taskName, DatePredictionResult result)
        {
            // 筛选用户地区的即将到来的节假日
            var upcomingHolidays = _holidays
                .Where(h => h.Region == _userRegion && h.Date >= DateTime.Now.Date)
                .OrderBy(h => h.Date)
                .Take(5);

            foreach (var holiday in upcomingHolidays)
            {
                // 检查任务名称是否与节日相关
                if (taskName.Contains(holiday.Name) ||
                    (taskName.Contains("节") && holiday.Category.Contains("Holiday")))
                {
                    result.AddPrediction(
                        holiday.Date,
                        0.9,
                        "DatePage_AI_upcoming ".GetLocalized()+ holiday.Name
                    );
                }
                else
                {
                    // 添加为普通建议
                    result.AddPrediction(
                        holiday.Date,
                        0.5,
                        "DatePage_AI_upcoming".GetLocalized()+ holiday.Name
                    );
                }
            }
        }
    
        private async Task AddAzureAIPredictions(string taskName, DatePredictionResult result)
        {
            try
            {
                Debug.WriteLine($"开始Azure AI预测: 任务名称={taskName}");

                // 构建系统指令和提示
                string systemMessage = "你是一个日期预测专家，根据任务描述分析可能的日期。" +
                                     "今天是 " + DateTime.Now.ToString("yyyy-MM-dd") + "。" +
                                     "分析任务是否与特定日期有关，如会议、截止日期、节日等。";

                string userPrompt = $"分析以下任务名称，预测最可能的日期（yyyy-MM-dd格式）和信心程度（0-1之间）（信心值不用展示在分析原因中，分析原因言简意赅，你的原因语言和接下来我给你的任务名相同（略微偏向英语），历史事件请给出时间）：\"{taskName}\"。" +
                                  "回复使用JSON格式：{\"date\": \"yyyy-MM-dd\", \"confidence\": 0.x, \"reason\": \"分析原因\"}";

                // 创建对话消息列表
                var chatCompletionsOptions = new ChatCompletionsOptions()
                {
                    Messages =
                    {
                        new ChatMessage(ChatRole.System, systemMessage),
                        new ChatMessage(ChatRole.User, userPrompt)
                    },
                    Temperature = 0.1f, // 低温度以获得更确定的回复
                    MaxTokens = 800
                };

                Debug.WriteLine("发送请求到Azure OpenAI...");

                // 发送请求到 Azure OpenAI
                var response = await _openAIClient.GetChatCompletionsAsync(
                    _deploymentName,
                    chatCompletionsOptions);

                var responseContent = response.Value.Choices[0].Message.Content;
                Debug.WriteLine($"收到原始OpenAI响应: {responseContent}");

                // 清理响应内容，移除Markdown代码块标记
                string cleanedContent = CleanJsonResponseContent(responseContent);
                Debug.WriteLine($"清理后的响应内容: {cleanedContent}");

                // 解析 JSON 响应
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(cleanedContent))
                    {
                        var root = doc.RootElement;

                        if (root.TryGetProperty("date", out var dateElement) &&
                            root.TryGetProperty("confidence", out var confidenceElement) &&
                            root.TryGetProperty("reason", out var reasonElement))
                        {
                            if (DateTime.TryParse(dateElement.GetString(), out DateTime predictedDate))
                            {
                                double confidence = confidenceElement.GetDouble();
                                string reason = reasonElement.GetString();

                                Debug.WriteLine($"成功解析AI预测: 日期={predictedDate}, 置信度={confidence}, 原因={reason}");

                                // 为Azure AI预测增加一个小的置信度加成，确保高相关度的AI预测优先显示
                                double adjustedConfidence = Math.Min(confidence + 0.05, 1.0);

                                result.AddPrediction(
                                    predictedDate,
                                    adjustedConfidence,
                                    reason,
                                    true  // 标记为Azure AI预测
                                );
                            }
                            else
                            {
                                Debug.WriteLine($"预测日期无效: {dateElement.GetString()}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"响应中缺少必要字段");
                            Debug.WriteLine($"完整响应内容: {cleanedContent}");
                        }
                    }
                }
                catch (JsonException ex)
                {
                    // 解析失败时记录日志
                    Debug.WriteLine($"无法解析OpenAI响应: {cleanedContent}");
                    Debug.WriteLine($"JSON解析错误: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                // 错误处理
                Debug.WriteLine($"Azure OpenAI 预测失败: {ex.Message}");
                Debug.WriteLine($"错误详情: {ex}");
            }
        }
        private string CleanJsonResponseContent(string content)
        {
            // 移除可能的Markdown代码块标记
            string cleaned = content;

            if (cleaned.StartsWith("```json")){

                cleaned = cleaned.Substring("```json".Length);

            }

            if (cleaned.EndsWith("```"))
            {

                cleaned = cleaned.Substring(0, cleaned.Length - "```".Length);


            }



            cleaned = cleaned.Trim();

            return cleaned;
        }

        private async Task AddAzureMLPredictions(string taskName, DatePredictionResult result)
        {
            try
            {
                if (_userDateHistory.Count < 5)
                {
                    return; // 历史数据不足，无法使用 ML 模型
                }

                // 准备模型输入数据
                var mlInput = new
                {
                    taskName = taskName,
                    userHistory = _userDateHistory.Select(h => new
                    {
                        taskName = h.TaskName,
                        selectedDate = h.SelectedDate.ToString("yyyy-MM-dd"),
                        dayOfWeek = (int)h.SelectedDate.DayOfWeek,
                        dayOfMonth = h.SelectedDate.Day,
                        month = h.SelectedDate.Month
                    }).ToArray(),
                    currentDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    userRegion = _userRegion
                };

                // 调用 Azure ML 评分终结点
                var response = await _httpClient.PostAsJsonAsync(_azureMLEndpoint, mlInput);

                if (response.IsSuccessStatusCode)
                {
                    var mlPrediction = await response.Content.ReadFromJsonAsync<MLPredictionResponse>();

                    if (mlPrediction != null && mlPrediction.Predictions != null)
                    {
                        foreach (var prediction in mlPrediction.Predictions)
                        {
                            if (DateTime.TryParse(prediction.PredictedDate, out DateTime predictedDate) &&
                                predictedDate >= DateTime.Now)
                            {
                                result.AddPrediction(
                                    predictedDate,
                                    prediction.Confidence,
                                    $"机器学习模型预测：{prediction.Reason}"
                                );
                            }
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"Azure ML API 请求失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Azure ML 预测失败: {ex.Message}");
            }
        }

        private class MLPredictionResponse
        {
            public List<MLPrediction> Predictions { get; set; }
        }

        private class MLPrediction
        {
            public string PredictedDate { get; set; }
            public double Confidence { get; set; }
            public string Reason { get; set; }
        }
        // 尝试从文本中提取天数
        private bool TryExtractDays(string text, out int days)
        {
            days = 0;

            // 简单的数字提取
            var match = System.Text.RegularExpressions.Regex.Match(text, @"\d+");
            if (match.Success && int.TryParse(match.Value, out days))
            {
                return true;
            }

            return false;
        }

        // 更新用户区域
        public void SetUserRegion(string regionCode)
        {
            _userRegion = regionCode;
        }
    }

    // 用户日期选择历史记录
    public class UserDateSelection
    {
        public string TaskName { get; set; }
        public DateTime SelectedDate { get; set; }
        public DateTime SelectionTime { get; set; }
    }

    // 节假日信息
    public class HolidayInfo
    {
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public string Region { get; set; } // 国家/地区代码
        public string Category { get; set; } // NationalHoliday, TraditionalHoliday, etc.
    }

    // 预测结果模型
    public class DatePredictionResult
    {
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; }
        public List<DateSuggestion> Suggestions { get; private set; } = new List<DateSuggestion>();

        public void AddPrediction(DateTime date, double confidence, string reason, bool isAIPredict = false)
        {
            // 避免添加重复的日期
            if (!Suggestions.Any(s => s.SuggestedDate.Date == date.Date))
            {
                Suggestions.Add(new DateSuggestion
                {
                    SuggestedDate = date,
                    Confidence = confidence,
                    Reason = reason,
                    IsAIPredict = isAIPredict
                });
            }
        }

        // 获取排序后的建议列表，高相关度AI预测优先
        public List<DateSuggestion> GetSortedSuggestions()
        {
            // 首先按是否AI预测排序，然后按信心度排序
            return Suggestions
                .OrderByDescending(s => s.IsAIPredict && s.Confidence > 0.7) // 高相关度的AI预测优先
                .ThenByDescending(s => s.Confidence)
                .Take(5)
                .ToList();
        }
    }

    // 日期建议模型
    public class DateSuggestion
    {
        public DateTime SuggestedDate { get; set; }
        public double Confidence { get; set; } // 0-1之间
        public string Reason { get; set; }
        public bool IsAIPredict { get; set; } = false; // 新增属性，标识是否来自Azure AI
    }

}
