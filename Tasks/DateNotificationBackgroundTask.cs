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
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            // 获取延迟对象，确保后台任务有足够时间完成
            var deferral = taskInstance.GetDeferral();

            try
            {
                // 使用Task.Run来包装异步操作
                Task.Run(async () =>
                {
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
                }).Wait(); // 等待异步操作完成
            }
            finally
            {
                // 完成延迟
                deferral.Complete();
            }
        }
    }
}
