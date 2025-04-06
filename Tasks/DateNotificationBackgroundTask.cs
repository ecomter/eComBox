using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace eComBox.Tasks
{
    public sealed class DateNotificationBackgroundTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            // 获取延迟对象，确保后台任务有足够时间完成
            var deferral = taskInstance.GetDeferral();

            try
            {
                // 调用App类中的通知方法
                await ((App)App.Current).CheckDateNotificationsAsync();
            }
            catch (Exception ex)
            {
                // 记录异常但不抛出
                System.Diagnostics.Debug.WriteLine($"后台任务出错: {ex.Message}");
            }
            finally
            {
                // 完成延迟
                deferral.Complete();
            }
        }
    }
}
