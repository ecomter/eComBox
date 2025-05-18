using System;
using System.Threading.Tasks;
using eComBox.Services;

namespace eComBox.Services
{
    /// <summary>
    /// AI 服务接口，定义 AI 功能的基本操作
    /// </summary>
    public interface IAIService
    {
        /// <summary>
        /// 根据任务名称预测日期
        /// </summary>
        Task<DatePredictionResult> PredictDateFromTaskNameAsync(string taskName);

        /// <summary>
        /// 提供用户反馈以改进 AI 模型
        /// </summary>
        Task ProvideUserFeedbackAsync(string taskName, DateTime selectedDate, bool wasUseful);

        /// <summary>
        /// 设置用户区域
        /// </summary>
        void SetUserRegion(string regionCode);

        /// <summary>
        /// 获取当前用户的剩余使用次数
        /// </summary>
        Task<int> GetRemainingUsageCountAsync();

        /// <summary>
        /// 检查用户是否有权限使用 AI 功能
        /// </summary>
        Task<bool> HasAIPermissionAsync();
    }
}
