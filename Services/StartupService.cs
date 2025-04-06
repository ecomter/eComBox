using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;

namespace eComBox.Services
{
    public static class StartupService
    {
        private const string StartupTaskId = "eComBoxStartupTask";
        private static StartupTask _startupTask;

        public static async Task<bool> IsStartupEnabled()
        {
            try
            {
                _startupTask = await StartupTask.GetAsync(StartupTaskId);
                return _startupTask.State == StartupTaskState.Enabled;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<StartupTaskState> EnableStartupAsync()
        {
            try
            {
                // 获取启动任务
                _startupTask = await StartupTask.GetAsync(StartupTaskId);

                // 如果启动任务已经启用，直接返回
                if (_startupTask.State == StartupTaskState.Enabled)
                {
                    return StartupTaskState.Enabled;
                }

                // 请求启用启动任务
                StartupTaskState newState = await _startupTask.RequestEnableAsync();
                return newState;
            }
            catch (Exception)
            {
                return StartupTaskState.DisabledByUser;
            }
        }

        public static void DisableStartup()
        {
            try
            {
                if (_startupTask != null && _startupTask.State == StartupTaskState.Enabled)
                {
                    _startupTask.Disable();
                }
            }
            catch
            {
                // 忽略异常
            }
        }

        // 注册后台通知任务
        public static async Task RegisterBackgroundNotificationTask()
        {
            // 取消所有现有的后台任务
            foreach (var existingTask in BackgroundTaskRegistration.AllTasks)
            {
                if (existingTask.Value.Name == "DateNotificationTask")
                {
                    existingTask.Value.Unregister(true);
                }
            }

            // 获取访问权限
            var status = await BackgroundExecutionManager.RequestAccessAsync();
            if (status == BackgroundAccessStatus.DeniedByUser ||
                status == BackgroundAccessStatus.DeniedBySystemPolicy)
            {
                // 用户或系统拒绝了后台任务访问权限
                return;
            }

            // 创建新的后台触发器
            var builder = new BackgroundTaskBuilder
            {
                Name = "DateNotificationTask",
                TaskEntryPoint = "eComBox.Tasks.DateNotificationBackgroundTask"
            };

            // 添加启动触发器
            builder.SetTrigger(new SystemTrigger(SystemTriggerType.UserPresent, false));

            // 添加时间触发器（每天运行一次）
            builder.SetTrigger(new TimeTrigger(1440, false)); // 1440分钟=24小时

            // 注册后台任务
            BackgroundTaskRegistration task = builder.Register();
        }
    }
}
