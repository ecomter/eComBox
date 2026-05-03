using System.Threading.Tasks;
using eComBox.Services;

namespace eComBox.Services
{
    /// <summary>
    /// AI 服务接口，定义日期预测能力
    /// </summary>
    public interface IAIService
    {
        /// <summary>
        /// 根据任务名称预测日期
        /// </summary>
        Task<DatePredictionResult> PredictDateFromTaskNameAsync(string taskName);
    }
}
