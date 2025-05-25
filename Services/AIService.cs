using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Azure.AI.OpenAI;
using Azure;
using Windows.Storage;
namespace eComBox.Services
{
    /// <summary>
    /// AI 服务的实现，带有使用限制和计费功能
    /// </summary>
    public class AIService : IAIService
    {
        private readonly AzureDatePredictionService _datePredictionService;

        /// <summary>
        /// 创建 AI 服务实例
        /// </summary>
        public AIService()
        {
            try
            {
                // 使用 ConfigurationService 获取配置值
                string openAIEndpoint = ConfigurationService.OpenAIEndpoint;
                string openAIKey = ConfigurationService.OpenAIApiKey;
                string deploymentName = ConfigurationService.OpenAIDeploymentName;

                // 确保配置不为空
                if (string.IsNullOrEmpty(openAIEndpoint) || string.IsNullOrEmpty(openAIKey) || string.IsNullOrEmpty(deploymentName))
                {
                    throw new InvalidOperationException("AI服务配置不完整，请检查设置");
                }

                _datePredictionService = new AzureDatePredictionService(
                    openAIEndpoint,
                    openAIKey,
                    deploymentName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化 AIService 失败: {ex.Message}");
                throw; // 重新抛出异常以便调用者处理
            }
        }

        /// <summary>
        /// 检查当前用户是否有权限使用 AI 功能
        /// </summary>
        public async Task<bool> HasAIPermissionAsync()
        {
            // 如果用户已购买高级版，始终返回 true
            if (await AIUsageService.IsAIPremiumPurchasedAsync())
            {
                return true;
            }

            // 否则检查是否达到每日免费使用限制
            return !AIUsageService.IsUsageLimitReached();
        }

        /// <summary>
        /// 获取当前用户的剩余 AI 使用次数
        /// </summary>
        public async Task<int> GetRemainingUsageCountAsync()
        {
            return await AIUsageService.GetRemainingUsageCountAsync();
        }

        /// <summary>
        /// 根据任务名称预测日期
        /// </summary>
        public async Task<DatePredictionResult> PredictDateFromTaskNameAsync(string taskName)
        {
            try
            {
                // 检查用户是否有权限使用 AI 功能
                if (!await HasAIPermissionAsync())
                {
                    return new DatePredictionResult
                    {
                        IsSuccessful = false,
                        ErrorMessage = "您今日的免费 AI 使用次数已用完。请明天再试或升级到高级版获取无限使用权限。"
                    };
                }

                // 增加使用计数（仅对免费用户）
                if (!await AIUsageService.IsAIPremiumPurchasedAsync())
                {
                    AIUsageService.IncrementUsageCount();
                }

                // 调用底层 Azure 服务进行预测
                return await _datePredictionService.PredictDateFromTaskNameAsync(taskName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AI 预测失败: {ex.Message}");
                return new DatePredictionResult
                {
                    IsSuccessful = false,
                    ErrorMessage = $"AI 预测过程中出错: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 提供用户反馈以改进 AI 模型
        /// </summary>
        public async Task ProvideUserFeedbackAsync(string taskName, DateTime selectedDate, bool wasUseful)
        {
            try
            {
                // 无论用户是否有高级版，都允许提供反馈
                await _datePredictionService.ProvideUserFeedbackAsync(taskName, selectedDate, wasUseful);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"提供用户反馈失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置用户区域
        /// </summary>
        public void SetUserRegion(string regionCode)
        {
            _datePredictionService.SetUserRegion(regionCode);
        }
    }
}
