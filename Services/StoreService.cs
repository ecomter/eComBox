// 添加到 Services 文件夹中的一个新文件 StoreService.cs
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Store;
using Windows.Services.Store;
using Windows.Storage;

namespace eComBox.Services
{
    public static class StoreService
    {
        // 内购产品的Store ID，需要与Microsoft Store中的产品ID匹配
        private const string ConicSectionFeatureId = "9NV3C9STGW4Z";

        // 在LocalSettings中存储购买状态的键
        private const string PurchaseStatusKey = "ConicSectionPurchased";

        // 检查用户是否已购买圆锥曲线功能
        public static async Task<bool> IsConicSectionFeaturePurchasedAsync()
        {
            // 首先从本地设置检查，避免每次都请求Store
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(PurchaseStatusKey, out object purchaseStatus))
            {
                if (purchaseStatus is bool purchased && purchased)
                {
                    return true;
                }
            }

            // 访问Store获取许可信息
            try
            {
                StoreContext storeContext = StoreContext.GetDefault();
                StoreAppLicense appLicense = await storeContext.GetAppLicenseAsync();

                // 检查应用的许可信息中是否包含该内购功能
                foreach (var addOn in appLicense.AddOnLicenses)
                {
                    if (addOn.Key == ConicSectionFeatureId && addOn.Value.IsActive)
                    {
                        // 保存购买状态到本地设置
                        ApplicationData.Current.LocalSettings.Values[PurchaseStatusKey] = true;
                        return true;
                    }
                }

                return false;
            }
            catch (Exception)
            {
                // 如果发生错误（例如网络问题），默认返回false
                return false;
            }
        }

        // 请求购买圆锥曲线功能
        public static async Task<bool> RequestPurchaseConicSectionFeatureAsync()
        {
            try
            {
                StoreContext storeContext = StoreContext.GetDefault();
                
                // 获取内购产品信息
                StoreProductQueryResult result = await storeContext.GetStoreProductsAsync(
                    new string[] { "Durable" }, new string[] { ConicSectionFeatureId });
               
                if (result.Products.TryGetValue(ConicSectionFeatureId, out StoreProduct product))
                {
                    // 启动购买流程
                    StorePurchaseResult purchaseResult = await product.RequestPurchaseAsync();

                    if (purchaseResult.Status == StorePurchaseStatus.Succeeded)
                    {
                        // 购买成功，保存状态
                        ApplicationData.Current.LocalSettings.Values[PurchaseStatusKey] = true;
                        return true;
                    }
                }

                return false;
                
                await CurrentAppSimulator.RequestProductPurchaseAsync("9NV3C9STGW4Z");
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // 重置本地存储的购买状态（仅用于测试）
        public static void ResetPurchaseStatus()
        {
            if (ApplicationData.Current.LocalSettings.Values.ContainsKey(PurchaseStatusKey))
            {
                ApplicationData.Current.LocalSettings.Values.Remove(PurchaseStatusKey);
            }
        }
    }
}
