using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace eComBox.Services
{
    /// <summary>
    /// 管理 AI 使用计数和付费检查（免费用户每天限额，付费用户不限）
    /// </summary>
    public static class AIUsageService
    {
        private const string UsageKeyPrefix = "AIUsageCount_"; // 后接日期 yyyy-MM-dd
        private const string AIProKey = "AIProPurchased"; // 本地标记用户已付费

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

        public static async Task<bool> IsProUserAsync()
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings;
                if (settings.Values.TryGetValue(AIProKey, out object value) && value is bool v && v)
                {
                    return true;
                }

                // 作为回退，可尝试检查 Store 中的购买（如果你已将商店内购ID对应为AI Premium）
                try
                {
                    bool purchased = await StoreService.IsConicSectionFeaturePurchasedAsync();
                    if (purchased)
                    {
                        settings.Values[AIProKey] = true;
                        return true;
                    }
                }
                catch
                {
                    // ignore
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> RequestPurchaseAIPremiumAsync()
        {
            try
            {
                // 调用 StoreService 进行购买流程
                bool ok = await StoreService.RequestPurchaseConicSectionFeatureAsync();
                if (ok)
                {
                    ApplicationData.Current.LocalSettings.Values[AIProKey] = true;
                }
                return ok;
            }
            catch
            {
                return false;
            }
        }

        // 判断当日是否可继续使用 AI（对免费用户限额限制）
        public static async Task<bool> CanUseAIAsync()
        {
            if (await IsProUserAsync()) return true;

            int used = await GetTodayUsageAsync();
            int limit = ConfigurationService.FreeUsageLimit;
            return used < limit;
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
