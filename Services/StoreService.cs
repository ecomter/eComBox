// 添加到 Services 文件夹中的一个新文件 StoreService.cs
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Services.Store;
using Windows.Storage;

namespace eComBox.Services
{
    public static class StoreService
    {
        // ========== 产品 ID（需与 Microsoft Store 中配置的产品 ID 匹配）==========
        private const string AIPremiumFeatureId = "9NV3C9STGW4Z";

        // ========== 本地缓存键 ==========
        private const string AIPremiumPurchasedKey = "AIPremiumPurchased";

        // ========== 通用：检查本地缓存是否已购买 ==========
        private static bool IsLocallyPurchased(string key)
        {
            return ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out object value)
                   && value is bool b && b;
        }

        private static void SetLocallyPurchased(string key)
        {
            ApplicationData.Current.LocalSettings.Values[key] = true;
        }

        // ========== 通用：检查 Store 许可证 ==========
        private static async Task<bool> IsPurchasedFromStoreAsync(string featureId)
        {
            try
            {
                var storeContext = StoreContext.GetDefault();
                var appLicense = await storeContext.GetAppLicenseAsync();

                foreach (var addOn in appLicense.AddOnLicenses)
                {
                    if (addOn.Key == featureId && addOn.Value.IsActive)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // ========== 通用：发起购买（返回结果+错误信息） ==========
        private static async Task<(bool Success, string Error)> RequestPurchaseAsync(string featureId)
        {
            try
            {
                var storeContext = StoreContext.GetDefault();
                Debug.WriteLine($"[StoreService] 查询产品: {featureId}");

                var result = await storeContext.GetStoreProductsAsync(
                    new[] { "Durable" }, new[] { featureId });

                if (result.ExtendedError != null)
                {
                    string err = GetStoreErrorMessage(result.ExtendedError);
                    Debug.WriteLine($"[StoreService] {err}");
                    return (false, err);
                }

                if (!result.Products.TryGetValue(featureId, out var product))
                {
                    string err = $"未找到产品 ID '{featureId}'。请确认该产品已在 Microsoft Store 中配置，且应用已关联 Store。";
                    Debug.WriteLine($"[StoreService] {err}");
                    return (false, err);
                }

                Debug.WriteLine($"[StoreService] 发起购买: {product.Title} ({product.Price.FormattedPrice})");
                var purchaseResult = await product.RequestPurchaseAsync();

                if (purchaseResult.Status == StorePurchaseStatus.Succeeded)
                {
                    Debug.WriteLine($"[StoreService] 购买成功");
                    return (true, null);
                }

                string failReason;
                switch (purchaseResult.Status)
                {
                    case StorePurchaseStatus.AlreadyPurchased:
                        failReason = "已购买过此产品";
                        break;
                    case StorePurchaseStatus.NotPurchased:
                        failReason = "用户取消或购买未完成";
                        break;
                    case StorePurchaseStatus.NetworkError:
                        failReason = "网络错误，请检查网络连接后重试";
                        break;
                    case StorePurchaseStatus.ServerError:
                        failReason = "Store 服务器错误，请稍后重试";
                        break;
                    default:
                        failReason = $"购买失败 (状态: {purchaseResult.Status})";
                        break;
                }
                Debug.WriteLine($"[StoreService] {failReason}");
                return (false, failReason);
            }
            catch (Exception ex)
            {
                string err = $"购买异常: {ex.Message}";
                Debug.WriteLine($"[StoreService] {err}");
                return (false, err);
            }
        }

        // ========== 通用：检查是否已购买（本地缓存 + Store 回退，带超时） ==========
        private static async Task<bool> IsFeaturePurchasedAsync(string featureId, string localKey)
        {
            // 1. 本地缓存命中 → 直接返回
            if (IsLocallyPurchased(localKey))
                return true;

            // 2. 回退查 Store（带 2 秒超时）
            try
            {
                var storeTask = IsPurchasedFromStoreAsync(featureId);
                var timeoutTask = Task.Delay(2000);
                if (await Task.WhenAny(storeTask, timeoutTask) == storeTask)
                {
                    bool purchased = await storeTask;
                    if (purchased)
                    {
                        SetLocallyPurchased(localKey);
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        // ========== 通用：发起购买并缓存 ==========
        private static async Task<(bool Success, string Error)> PurchaseFeatureAsync(string featureId, string localKey)
        {
            var (success, error) = await RequestPurchaseAsync(featureId);
            if (success)
            {
                SetLocallyPurchased(localKey);
            }
            return (success, error);
        }

        // ========== AI 高级版功能 ==========
        public static Task<bool> IsAIPremiumPurchasedAsync()
            => IsFeaturePurchasedAsync(AIPremiumFeatureId, AIPremiumPurchasedKey);

        /// <summary>
        /// 发起 AI 高级版购买，返回 (是否成功, 错误描述)
        /// </summary>
        public static Task<(bool Success, string Error)> RequestPurchaseAIPremiumAsync()
            => PurchaseFeatureAsync(AIPremiumFeatureId, AIPremiumPurchasedKey);

        // ========== 测试用：重置购买状态 ==========
        public static void ResetPurchaseStatus()
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.ContainsKey(AIPremiumPurchasedKey))
                settings.Values.Remove(AIPremiumPurchasedKey);
        }

        private static string GetStoreErrorMessage(Exception error)
        {
            const int StoreProductUnavailable = unchecked((int)0x803F6107);
            if (error.HResult == StoreProductUnavailable)
            {
                return "Store 无法识别此购买项目。请确认应用由 Microsoft Store 安装，且发布包使用 Partner Center 分配的包身份。";
            }

            return $"Store 查询失败: {error.Message}";
        }
    }
}
