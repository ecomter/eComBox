using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace eComBox.Services
{
    /// <summary>
    /// 管理 AI 使用计数和付费检查（免费用户每天限额，付费用户不限）
    /// 购买流程委托给 StoreService
    /// </summary>
    public static class AIUsageService
    {
        private const string UsageKeyPrefix = "AIUsageCount_"; // 后接日期 yyyy-MM-dd

        public static async Task<int> GetTodayUsageAsync()
        {
            try
            {
                string key = UsageKeyPrefix + DateTime.UtcNow.ToString("yyyy-MM-dd");
                var settings = ApplicationData.Current.LocalSettings;
                if (settings.Values.TryGetValue(key, out object value) && value is int count)
                {
                    return count;
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        public static async Task IncrementUsageAsync()
        {
            try
            {
                string key = UsageKeyPrefix + DateTime.UtcNow.ToString("yyyy-MM-dd");
                var settings = ApplicationData.Current.LocalSettings;
                int current = 0;
                if (settings.Values.TryGetValue(key, out object value) && value is int count)
                {
                    current = count;
                }
                settings.Values[key] = current + 1;
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// 检查是否为 Pro 用户（委托 StoreService，本地缓存 + Store 回退 + 超时保护）
        /// </summary>
        public static Task<bool> IsProUserAsync()
            => StoreService.IsAIPremiumPurchasedAsync();

        /// <summary>
        /// 发起 AI 高级版购买（委托 StoreService），返回 (是否成功, 错误描述)
        /// </summary>
        public static Task<(bool Success, string Error)> RequestPurchaseAIPremiumAsync()
            => StoreService.RequestPurchaseAIPremiumAsync();

        /// <summary>
        /// 判断当日是否可继续使用 AI（免费用户限额限制）
        /// 仅检查本地缓存，不查询 Store API，避免阻塞 UI
        /// </summary>
        public static async Task<bool> CanUseAIAsync()
        {
            // 快速本地检查：Pro 用户直接放行（不触发 Store API）
            if (IsProUserLocally())
            {
                return true;
            }

            int used = await GetTodayUsageAsync();
            int limit = ConfigurationService.FreeUsageLimit;
            return used < limit;
        }

        /// <summary>
        /// 仅检查本地缓存的 Pro 状态，不访问 Store（毫秒级）
        /// </summary>
        public static bool IsProUserLocally()
        {
            var settings = ApplicationData.Current.LocalSettings;
            // 同时检查 AIUsageService 旧 key 和 StoreService 新 key
            return (settings.Values.TryGetValue("AIProPurchased", out object v1) && v1 is bool b1 && b1)
                || (settings.Values.TryGetValue("AIPremiumPurchased", out object v2) && v2 is bool b2 && b2);
        }

        // 用于测试/调试：重置今日计数
        public static void ResetTodayUsage()
        {
            try
            {
                string key = UsageKeyPrefix + DateTime.UtcNow.ToString("yyyy-MM-dd");
                var settings = ApplicationData.Current.LocalSettings;
                if (settings.Values.ContainsKey(key)) settings.Values.Remove(key);
            }
            catch { }
        }
    }
}
