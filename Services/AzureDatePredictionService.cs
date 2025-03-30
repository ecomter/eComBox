using Azure;
using Azure.AI.TextAnalytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using eComBox.Views;
using System.Net.Http;
using System.Diagnostics;

namespace eComBox.Services
{
    public class AzureDatePredictionService
    {
        private readonly TextAnalyticsClient _client;
        private List<UserDateSelection> _userDateHistory = new List<UserDateSelection>();
        private List<HolidayInfo> _holidays = new List<HolidayInfo>();
        private string _userRegion = "CN"; // 默认为中国，可以从设置中读取

        public AzureDatePredictionService(string endpoint, string apiKey)
        {
            // 初始化 Azure AI 客户端
            AzureKeyCredential credential = new AzureKeyCredential(apiKey);
            _client = new TextAnalyticsClient(new Uri(endpoint), credential);

            // 初始化节假日数据
            InitializeHolidays();

            // 加载用户历史数据
            _ = LoadUserHistoryAsync();
        }

        private void InitializeHolidays()
        {
            // 这里可以从Azure Blob存储或应用内资源加载各地区节假日
            // 简化版中直接硬编码一些中国节假日示例

            int currentYear = DateTime.Now.Year;
            int nextYear = currentYear + 1;

            // 添加固定日期的节假日
            _holidays.Add(new HolidayInfo
            {
                Name = "元旦",
                Date = new DateTime(currentYear, 1, 1),
                Region = "CN",
                Category = "NationalHoliday"
            });

            _holidays.Add(new HolidayInfo
            {
                Name = "劳动节",
                Date = new DateTime(currentYear, 5, 1),
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

            // 添加明年的相同节日
            _holidays.Add(new HolidayInfo
            {
                Name = "元旦",
                Date = new DateTime(nextYear, 1, 1),
                Region = "CN",
                Category = "NationalHoliday"
            });

            // 加载或计算农历节日
            // 实际应用中应使用更准确的农历转换逻辑
            AddChineseLunarHolidays(currentYear);
            AddChineseLunarHolidays(nextYear);
        }

        private void AddChineseLunarHolidays(int year)
        {
            // 这里应该使用农历转换库计算确切日期
            // 以下仅为示例，日期不准确

            // 春节通常在1-2月
            DateTime springFestival = new DateTime(year, 2, 12);
            _holidays.Add(new HolidayInfo
            {
                Name = "春节",
                Date = springFestival,
                Region = "CN",
                Category = "TraditionalHoliday"
            });

            // 清明节通常在4月初
            _holidays.Add(new HolidayInfo
            {
                Name = "清明节",
                Date = new DateTime(year, 4, 5),
                Region = "CN",
                Category = "TraditionalHoliday"
            });

            // 中秋节通常在9-10月
            _holidays.Add(new HolidayInfo
            {
                Name = "中秋节",
                Date = new DateTime(year, 9, 21),
                Region = "CN",
                Category = "TraditionalHoliday"
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

                // 1. 基于关键词匹配的预测
                AddKeywordBasedPredictions(taskName, result);

                // 2. 基于用户历史的预测
                AddHistoryBasedPredictions(taskName, result);

                // 3. 节假日预测
                AddHolidayPredictions(taskName, result);

                // 4. 使用Azure AI进行分析
                await AddAzureAIPredictions(taskName, result);

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

        private void AddKeywordBasedPredictions(string taskName, DatePredictionResult result)
        {
            // 常见日期关键词映射
            var keywordMap = new Dictionary<string, int>
            {
                { "生日", 365 },
                { "周年", 365 },
                { "纪念日", 365 },
                { "开学", 30 },
                { "考试", 14 },
                { "会议", 7 },
                { "deadline", 10 },
                { "截止", 10 },
                { "演唱会", 60 },
                { "复查", 90 },
                { "还款", 30 }
            };

            foreach (var keyword in keywordMap)
            {
                if (taskName.Contains(keyword.Key))
                {
                    result.AddPrediction(
                        DateTime.Now.AddDays(keyword.Value),
                        0.7,
                        $"基于{ keyword.Key}关键词预测"
                    );
                }
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
            AnalyzeUserDatePattern(result);
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

        private void AnalyzeUserDatePattern(DatePredictionResult result)
        {
            if (_userDateHistory.Count < 3) return;

            // 分析用户常选择的星期几
            var dayOfWeekPreference = _userDateHistory
                .GroupBy(h => h.SelectedDate.DayOfWeek)
                .OrderByDescending(g => g.Count())
                .First().Key;

            // 寻找未来符合该星期几的日期
            DateTime nextPreferredDay = DateTime.Now.Date;
            while (nextPreferredDay.DayOfWeek != dayOfWeekPreference)
            {
                nextPreferredDay = nextPreferredDay.AddDays(1);
            }

            result.AddPrediction(
                nextPreferredDay,
                0.6,
                $"基于您经常选择{GetChineseDayOfWeek(dayOfWeekPreference)}的偏好"
            );

            // 分析用户常选择的日期
            var dayOfMonthPreference = _userDateHistory
                .GroupBy(h => h.SelectedDate.Day)
                .OrderByDescending(g => g.Count())
                .First().Key;

            // 构建下个月相同日期
            var nextMonthSameDay = new DateTime(
                DateTime.Now.Year,
                DateTime.Now.Month,
                1
            ).AddMonths(1);

            // 确保日期有效
            int daysInMonth = DateTime.DaysInMonth(nextMonthSameDay.Year, nextMonthSameDay.Month);
            int targetDay = Math.Min(dayOfMonthPreference, daysInMonth);

            nextMonthSameDay = nextMonthSameDay.AddDays(targetDay - 1);

            result.AddPrediction(
                nextMonthSameDay,
                0.6,
                $"基于您经常选择每月{targetDay}日的偏好"
            );
        }

        private string GetChineseDayOfWeek(DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Monday: return "星期一";
                case DayOfWeek.Tuesday: return "星期二";
                case DayOfWeek.Wednesday: return "星期三";
                case DayOfWeek.Thursday: return "星期四";
                case DayOfWeek.Friday: return "星期五";
                case DayOfWeek.Saturday: return "星期六";
                case DayOfWeek.Sunday: return "星期日";
                default: return day.ToString();
            }
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
                        $"即将到来的{ holiday.Name}"
                    );
                }
                else
                {
                    // 添加为普通建议
                    result.AddPrediction(
                        holiday.Date,
                        0.5,
                       $"即将到来的{holiday.Name}"
                    );
                }
            }
        }
        private async Task AddAzureAIPredictions(string taskName, DatePredictionResult result)
{
    // 使用 try-catch 块单独捕获 Azure AI 服务的异常
    try
    {
        // 在调用 Azure 服务前检查网络连接
        
        // 1. 分析文本情感
        var sentimentResponse = await _client.AnalyzeSentimentAsync(taskName);

        // 2. 提取实体（查找时间相关信息）
        var entitiesResponse = await _client.RecognizeEntitiesAsync(taskName);

        // 添加日期预测结果，确保不会添加 null 日期
        if (sentimentResponse.Value.Sentiment == TextSentiment.Positive)
        {
            // 积极情感（如节日、纪念日）通常在未来较远时间
            result.AddPrediction(DateTime.Now.AddDays(30), 0.6, "可能是重要的积极事件");
        }
        else if (sentimentResponse.Value.Sentiment == TextSentiment.Negative)
        {
            // 负面情感（如截止日期）通常在近期
            result.AddPrediction(DateTime.Now.AddDays(7), 0.6, "可能是近期截止事件");
        }

        // 寻找实体中的日期/时间信息
        foreach (var entity in entitiesResponse.Value)
        {
            if (entity.Category == "DateTime" || entity.Category == "Quantity")
            {
                // 尝试提取数值作为天数
                if (TryExtractDays(entity.Text, out int days) && days > 0 && days < 365)
                {
                    result.AddPrediction(
                        DateTime.Now.AddDays(days),
                        0.8,
                        $"根据{entity.Text}预测"
                    );
                }
            }
        }
    }
    catch (RequestFailedException azureEx)
    {
        // 专门处理 Azure 服务的异常
        System.Diagnostics.Debug.WriteLine($"Azure AI 服务请求失败: {azureEx.Message}. 状态码: {azureEx.Status}");
    }
    catch (Exception ex)
    {
        // 捕获其他所有异常
        System.Diagnostics.Debug.WriteLine($"Azure AI 分析异常: {ex.Message}");
        // 发生异常时继续使用其他预测结果
    }
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

        public void AddPrediction(DateTime date, double confidence, string reason)
        {
            // 避免添加重复的日期
            if (!Suggestions.Any(s => s.SuggestedDate.Date == date.Date))
            {
                Suggestions.Add(new DateSuggestion
                {
                    SuggestedDate = date,
                    Confidence = confidence,
                    Reason = reason
                });
            }
        }

        // 获取排序后的建议列表
        public List<DateSuggestion> GetSortedSuggestions()
        {
            return Suggestions
                .OrderByDescending(s => s.Confidence)
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
    }
}
