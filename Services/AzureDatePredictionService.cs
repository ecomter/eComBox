using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace eComBox.Services
{
    /// <summary>
    /// 精简后的 AzureDatePredictionService：不再直接依赖 Azure/OpenAI SDK。
    /// 改为使用本地的 AIService（已切换为 Qwen HTTP 实现）并整合用户历史与节假日建议。
    /// 保留原有的公共模型以兼容项目其它代码。
    /// </summary>
    public class AzureDatePredictionService
    {
        private List<UserDateSelection> _userDateHistory = new List<UserDateSelection>();
        private List<HolidayInfo> _holidays = new List<HolidayInfo>();
        private string _userRegion = "CN"; // 默认为中国，可以从设置中读取

        // 使用本地 AIService（Qwen 接入）作为后端
        private readonly IAIService _localAiService;

        public AzureDatePredictionService(string openAIEndpoint = null, string openAIKey = null, string deploymentName = null)
        {
            try
            {
                _localAiService = new AIService();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化本地 AIService 失败: {ex.Message}");
                _localAiService = null;
            }

            InitializeHolidays();
            _ = LoadUserHistoryAsync();
        }

        private void InitializeHolidays()
        {
            int currentYear = DateTime.Now.Year;
            int nextYear = currentYear + 1;

            _holidays.Add(new HolidayInfo { Name = "劳动节", Date = new DateTime(nextYear, 5, 1), Region = "CN", Category = "NationalHoliday" });
            _holidays.Add(new HolidayInfo { Name = "端午节", Date = new DateTime(currentYear, 5, 31), Region = "CN", Category = "NationalHoliday" });
            _holidays.Add(new HolidayInfo { Name = "父亲节", Date = new DateTime(currentYear, 6, 15), Region = "CN", Category = "NationalHoliday" });
            _holidays.Add(new HolidayInfo { Name = "建党节", Date = new DateTime(currentYear, 7, 1), Region = "CN", Category = "NationalHoliday" });
            _holidays.Add(new HolidayInfo { Name = "建军节", Date = new DateTime(currentYear, 8, 1), Region = "CN", Category = "NationalHoliday" });
            _holidays.Add(new HolidayInfo { Name = "教师节", Date = new DateTime(currentYear, 9, 10), Region = "CN", Category = "NationalHoliday" });
            _holidays.Add(new HolidayInfo { Name = "国庆节", Date = new DateTime(currentYear, 10, 1), Region = "CN", Category = "NationalHoliday" });
            _holidays.Add(new HolidayInfo { Name = "元旦", Date = new DateTime(nextYear, 1, 1), Region = "CN", Category = "NationalHoliday" });

            _holidays.Add(new HolidayInfo { Name = "Independence Day", Date = new DateTime(currentYear, 7, 4), Region = "US", Category = "NationalHoliday" });
            _holidays.Add(new HolidayInfo { Name = "Juneteenth", Date = new DateTime(currentYear, 6, 19), Region = "US", Category = "NationalHoliday" });
            _holidays.Add(new HolidayInfo { Name = "Labor Day", Date = new DateTime(currentYear, 9, 1), Region = "US", Category = "NationalHoliday" });
            _holidays.Add(new HolidayInfo { Name = "Columbus Day", Date = new DateTime(currentYear, 10, 13), Region = "US", Category = "NationalHoliday" });
            _holidays.Add(new HolidayInfo { Name = "Thanksgiving Day", Date = new DateTime(currentYear, 11, 27), Region = "US", Category = "NationalHoliday" });
            _holidays.Add(new HolidayInfo { Name = "Christmas Day", Date = new DateTime(currentYear, 12, 25), Region = "US", Category = "NationalHoliday" });
            _holidays.Add(new HolidayInfo { Name = "Christmas Day", Date = new DateTime(currentYear, 12, 25), Region = "UK", Category = "NationalHoliday" });
            _holidays.Add(new HolidayInfo { Name = "Summer Bank Holiday", Date = new DateTime(currentYear, 8, 25), Region = "UK", Category = "NationalHoliday" });
        }

        private async Task LoadUserHistoryAsync()
        {
            try
            {
                _userDateHistory = await CountdownStorageService.LoadDateHistoryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载用户历史数据失败: {ex.Message}");
            }
        }

        public async Task<DatePredictionResult> PredictDateFromTaskNameAsync(string taskName)
        {
            if (string.IsNullOrEmpty(taskName))
            {
                return new DatePredictionResult { IsSuccessful = false };
            }

            try
            {
                var result = new DatePredictionResult { IsSuccessful = true };

                // 1. 基于用户历史的预测
                AddHistoryBasedPredictions(taskName, result);

                // 2. 节假日预测
                AddHolidayPredictions(taskName, result);

                // 3. 使用本地 AI 服务（Qwen）进行预测
                if (_localAiService != null)
                {
                    try
                    {
                        var aiRes = await _localAiService.PredictDateFromTaskNameAsync(taskName);
                        if (aiRes != null && aiRes.Suggestions != null)
                        {
                            foreach (var s in aiRes.Suggestions)
                            {
                                // 标记来源为 AI
                                result.AddPrediction(s.SuggestedDate, s.Confidence, s.Reason, true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"本地 AI 调用失败: {ex.Message}");
                    }
                }

                // 合并并提升相似预测
                MergeSimilarPredictions(result);

                return result;
            }
            catch (Exception ex)
            {
                return new DatePredictionResult { IsSuccessful = false, ErrorMessage = ex.Message };
            }
        }

        private void MergeSimilarPredictions(DatePredictionResult result)
        {
            var groupedByDate = result.Suggestions
                .GroupBy(s => s.SuggestedDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    MaxConfidence = g.Max(s => s.Confidence),
                    Reasons = g.Select(s => s.Reason).Distinct().ToList()
                })
                .OrderByDescending(g => g.MaxConfidence)
                .ToList();

            result.Suggestions.Clear();

            foreach (var group in groupedByDate)
            {
                string combinedReason = string.Join("；", group.Reasons);
                if (combinedReason.Length > 100) combinedReason = combinedReason.Substring(0, 97) + "...";

                result.AddPrediction(group.Date, Math.Min(group.MaxConfidence + 0.05, 1.0), combinedReason);
            }
        }

        public async Task ProvideUserFeedbackAsync(string taskName, DateTime selectedDate, bool wasUseful)
        {
            try
            {
                var selection = new UserDateSelection { TaskName = taskName, SelectedDate = selectedDate, SelectionTime = DateTime.Now };
                _userDateHistory.Add(selection);

                if (_userDateHistory.Count > 300)
                {
                    _userDateHistory = _userDateHistory.OrderByDescending(h => h.SelectionTime).Take(300).ToList();
                }

                await CountdownStorageService.SaveDateHistoryAsync(_userDateHistory);

                // 不再发送云端反馈，精简离线实现
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存用户反馈失败: {ex.Message}");
            }
        }

        private void AddHistoryBasedPredictions(string taskName, DatePredictionResult result)
        {
            if (_userDateHistory == null || _userDateHistory.Count == 0) return;

            var similarEvents = _userDateHistory.Where(h => !string.IsNullOrEmpty(h.TaskName) && (h.TaskName.Contains(taskName) || taskName.Contains(h.TaskName)))
                .OrderByDescending(h => h.SelectionTime)
                .Take(3)
                .ToList();

            foreach (var ev in similarEvents)
            {
                DateTime predictedDate = ev.SelectedDate < DateTime.Now.Date ? AdjustToNextCycle(ev.SelectedDate) : ev.SelectedDate;
                result.AddPrediction(predictedDate, 0.8, $"基于您之前设置的 {ev.TaskName} 推荐");
            }
        }

        private DateTime AdjustToNextCycle(DateTime pastDate)
        {
            int daysPassed = (DateTime.Now.Date - pastDate).Days;
            int cycle = EstimateCycle(daysPassed);
            return DateTime.Now.Date.AddDays(cycle - (daysPassed % cycle));
        }

        private int EstimateCycle(int days)
        {
            if (days >= 350) return 365;
            if (days >= 85) return 90;
            if (days >= 25) return 30;
            if (days >= 5) return 7;
            return 1;
        }

        private void AddHolidayPredictions(string taskName, DatePredictionResult result)
        {
            var upcomingHolidays = _holidays.Where(h => h.Region == _userRegion && h.Date >= DateTime.Now.Date).OrderBy(h => h.Date).Take(5);
            foreach (var holiday in upcomingHolidays)
            {
                if (!string.IsNullOrEmpty(taskName) && (taskName.Contains(holiday.Name) || (taskName.Contains("节") && holiday.Category.Contains("Holiday"))))
                {
                    result.AddPrediction(holiday.Date, 0.9, "DatePage_AI_upcoming " + holiday.Name);
                }
                else
                {
                    result.AddPrediction(holiday.Date, 0.5, "DatePage_AI_upcoming" + holiday.Name);
                }
            }
        }

        // 更新用户区域
        public void SetUserRegion(string regionCode)
        {
            _userRegion = regionCode;
        }
    }

    public class UserDateSelection
    {
        public string TaskName { get; set; }
        public DateTime SelectedDate { get; set; }
        public DateTime SelectionTime { get; set; }
    }

    public class HolidayInfo
    {
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public string Region { get; set; }
        public string Category { get; set; }
    }

    public class DatePredictionResult
    {
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; }
        public List<DateSuggestion> Suggestions { get; private set; } = new List<DateSuggestion>();

        public void AddPrediction(DateTime date, double confidence, string reason, bool isAIPredict = false)
        {
            if (!Suggestions.Any(s => s.SuggestedDate.Date == date.Date))
            {
                Suggestions.Add(new DateSuggestion { SuggestedDate = date, Confidence = confidence, Reason = reason, IsAIPredict = isAIPredict });
            }
        }

        public List<DateSuggestion> GetSortedSuggestions()
        {
            return Suggestions.OrderByDescending(s => s.IsAIPredict && s.Confidence > 0.7).ThenByDescending(s => s.Confidence).Take(5).ToList();
        }
    }

    public class DateSuggestion
    {
        public DateTime SuggestedDate { get; set; }
        public double Confidence { get; set; }
        public string Reason { get; set; }
        public bool IsAIPredict { get; set; } = false;
    }
}
