using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Store;
using Windows.Services.Store;
using Windows.Storage;

namespace eComBox.Services
{
    /// <summary>
    /// 管理 AI 功能的使用限制和计费
    /// </summary>
    public class AIUsageService
    {
        // AI 产品的 Store ID，需要与 Microsoft Store 中的产品 ID 匹配
        private const string AI_PREMIUM_FEATURE_ID = "9NV3C9STGW4Z";

        // 在 LocalSettings 中存储购买状态的键
        private const string PURCHASE_STATUS_KEY = "AIPremiumPurchased";

        // 在 LocalSettings 中存储 AI 使用记录的键
        private const string AI_USAGE_DATE_KEY = "AIUsageDate";
        private const string AI_USAGE_COUNT_KEY = "AIUsageCount";

        // 每天免费使用的最大次数
        public const int FREE_USAGE_LIMIT_PER_DAY = 25;

        /// <summary>
        /// 检查用户是否已购买高级 AI 功能
        /// </summary>
        public static async Task<bool> IsAIPremiumPurchasedAsync()
        {
            try
            {
                // 首先从本地设置检查，避免每次都请求 Store
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(PURCHASE_STATUS_KEY, out object purchaseStatus))
                {
                    if (purchaseStatus is bool purchased && purchased)
                    {
                        return true;
                    }
                }

                // 尝试从 Store 获取许可信息
                try
                {
                    StoreContext storeContext = StoreContext.GetDefault();
                    StoreAppLicense appLicense = await storeContext.GetAppLicenseAsync();

                    // 检查应用的许可信息中是否包含该内购功能
                    foreach (var addOn in appLicense.AddOnLicenses)
                    {
                        if (addOn.Key == AI_PREMIUM_FEATURE_ID && addOn.Value.IsActive)
                        {
                            // 保存购买状态到本地设置
                            ApplicationData.Current.LocalSettings.Values[PURCHASE_STATUS_KEY] = true;
                            return true;
                        }
                    }
                }
                catch
                {
                    // 如果 Store API 发生异常，回退到本地检查
                    // 在开发环境或未部署到 Store 的情况下可能会发生
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查 AI 高级版购买状态时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 请求购买高级 AI 功能
        /// </summary>
        public static async Task<bool> RequestPurchaseAIPremiumAsync()
        {
            try
            {
                StoreContext storeContext = StoreContext.GetDefault();

                // 获取内购产品信息
                StoreProductQueryResult result = await storeContext.GetStoreProductsAsync(
                    new string[] { "Durable" }, new string[] { AI_PREMIUM_FEATURE_ID });

                if (result.Products.TryGetValue(AI_PREMIUM_FEATURE_ID, out StoreProduct product))
                {
                    // 启动购买流程
                    StorePurchaseResult purchaseResult = await product.RequestPurchaseAsync();

                    if (purchaseResult.Status == StorePurchaseStatus.Succeeded)
                    {
                        // 购买成功，保存状态
                        ApplicationData.Current.LocalSettings.Values[PURCHASE_STATUS_KEY] = true;
                        // 重置使用次数
                        ResetUsageCount();
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"购买 AI 高级版时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查当天 AI 使用次数是否已达到限制
        /// </summary>
        public static bool IsUsageLimitReached()
        {
            var settings = ApplicationData.Current.LocalSettings;
            var currentDate = DateTime.Now.Date.ToString("yyyy-MM-dd");

            // 检查是否有记录的日期
            if (settings.Values.TryGetValue(AI_USAGE_DATE_KEY, out object storedDateObj) &&
                storedDateObj is string storedDate)
            {
                // 如果是新的一天，重置计数
                if (storedDate != currentDate)
                {
                    settings.Values[AI_USAGE_DATE_KEY] = currentDate;
                    settings.Values[AI_USAGE_COUNT_KEY] = 0;
                    return false;
                }

                // 检查当天的使用次数
                if (settings.Values.TryGetValue(AI_USAGE_COUNT_KEY, out object countObj) &&
                    countObj is int count)
                {
                    // 如果已达到限制，返回 true
                    return count >= FREE_USAGE_LIMIT_PER_DAY;
                }
            }
            else
            {
                // 首次使用，初始化记录
                settings.Values[AI_USAGE_DATE_KEY] = currentDate;
                settings.Values[AI_USAGE_COUNT_KEY] = 0;
            }

            // 默认未达到限制
            return false;
        }

        /// <summary>
        /// 增加 AI 使用计数
        /// </summary>
        public static void IncrementUsageCount()
        {
            var settings = ApplicationData.Current.LocalSettings;
            var currentDate = DateTime.Now.Date.ToString("yyyy-MM-dd");

            // 确保日期已设置
            if (!settings.Values.TryGetValue(AI_USAGE_DATE_KEY, out object storedDateObj) ||
                !(storedDateObj is string storedDate) ||
                storedDate != currentDate)
            {
                settings.Values[AI_USAGE_DATE_KEY] = currentDate;
                settings.Values[AI_USAGE_COUNT_KEY] = 1;
                return;
            }

            // 增加计数
            int currentCount = 0;
            if (settings.Values.TryGetValue(AI_USAGE_COUNT_KEY, out object countObj) &&
                countObj is int count)
            {
                currentCount = count;
            }

            settings.Values[AI_USAGE_COUNT_KEY] = currentCount + 1;
            System.Diagnostics.Debug.WriteLine($"AI 使用计数: {currentCount + 1}/{FREE_USAGE_LIMIT_PER_DAY}");
        }

        /// <summary>
        /// 获取剩余的 AI 使用次数
        /// </summary>
        public static async Task<int> GetRemainingUsageCountAsync()
        {
            // 如果用户已购买高级版，返回无限制 (-1)
            if (await IsAIPremiumPurchasedAsync())
            {
                return -1; // -1 表示无限制
            }

            var settings = ApplicationData.Current.LocalSettings;
            var currentDate = DateTime.Now.Date.ToString("yyyy-MM-dd");

            // 检查日期并重置计数（如果需要）
            if (!settings.Values.TryGetValue(AI_USAGE_DATE_KEY, out object storedDateObj) ||
                !(storedDateObj is string storedDate) ||
                storedDate != currentDate)
            {
                settings.Values[AI_USAGE_DATE_KEY] = currentDate;
                settings.Values[AI_USAGE_COUNT_KEY] = 0;
                return FREE_USAGE_LIMIT_PER_DAY;
            }

            // 获取当前计数
            int currentCount = 0;
            if (settings.Values.TryGetValue(AI_USAGE_COUNT_KEY, out object countObj) &&
                countObj is int count)
            {
                currentCount = count;
            }

            return Math.Max(0, FREE_USAGE_LIMIT_PER_DAY - currentCount);
        }

        /// <summary>
        /// 重置使用计数（通常在购买高级版后调用）
        /// </summary>
        public static void ResetUsageCount()
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values[AI_USAGE_DATE_KEY] = DateTime.Now.Date.ToString("yyyy-MM-dd");
            settings.Values[AI_USAGE_COUNT_KEY] = 0;
        }

        /// <summary>
        /// 仅用于测试：重置本地存储的购买状态
        /// </summary>
        public static void ResetPurchaseStatus()
        {
            if (ApplicationData.Current.LocalSettings.Values.ContainsKey(PURCHASE_STATUS_KEY))
            {
                ApplicationData.Current.LocalSettings.Values.Remove(PURCHASE_STATUS_KEY);
            }
        }
    }
}
