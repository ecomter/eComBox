using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace eComBox.Services
{
    /// <summary>
    /// 管理应用程序配置和敏感信息
    /// </summary>
    public static class ConfigurationService
    {
        // 配置键
        private const string OPENAI_ENDPOINT_KEY = "OpenAIEndpoint";
        private const string OPENAI_API_KEY_KEY = "OpenAIApiKey";
        private const string OPENAI_DEPLOYMENT_NAME_KEY = "OpenAIDeploymentName";
        private const string FREE_USAGE_LIMIT_KEY = "FreeUsageLimit";

        // 默认配置
        private static readonly string DEFAULT_OPENAI_ENDPOINT = "url";
        private static readonly string DEFAULT_OPENAI_API_KEY = "key";
        private static readonly string DEFAULT_OPENAI_DEPLOYMENT_NAME = "gpt-4o";
        private static readonly int DEFAULT_FREE_USAGE_LIMIT = 25;

        /// <summary>
        /// 获取 OpenAI 终结点
        /// </summary>
        public static string OpenAIEndpoint
        {
            get
            {
                var settings = ApplicationData.Current.RoamingSettings;
                if (settings.Values.TryGetValue(OPENAI_ENDPOINT_KEY, out object value) && value is string endpoint)
                {
                    return endpoint;
                }
                return DEFAULT_OPENAI_ENDPOINT;
            }
            set
            {
                var settings = ApplicationData.Current.RoamingSettings;
                settings.Values[OPENAI_ENDPOINT_KEY] = value;
            }
        }

        /// <summary>
        /// 获取 OpenAI API 密钥
        /// </summary>
        public static string OpenAIApiKey
        {
            get
            {
                var settings = ApplicationData.Current.RoamingSettings;
                if (settings.Values.TryGetValue(OPENAI_API_KEY_KEY, out object value) && value is string key)
                {
                    return key;
                }
                return DEFAULT_OPENAI_API_KEY;
            }
            set
            {
                var settings = ApplicationData.Current.RoamingSettings;
                settings.Values[OPENAI_API_KEY_KEY] = value;
            }
        }

        /// <summary>
        /// 获取 OpenAI 部署名称
        /// </summary>
        public static string OpenAIDeploymentName
        {
            get
            {
                var settings = ApplicationData.Current.RoamingSettings;
                if (settings.Values.TryGetValue(OPENAI_DEPLOYMENT_NAME_KEY, out object value) && value is string name)
                {
                    return name;
                }
                return DEFAULT_OPENAI_DEPLOYMENT_NAME;
            }
            set
            {
                var settings = ApplicationData.Current.RoamingSettings;
                settings.Values[OPENAI_DEPLOYMENT_NAME_KEY] = value;
            }
        }

        /// <summary>
        /// 获取免费用户每日使用限制
        /// </summary>
        public static int FreeUsageLimit
        {
            get
            {
                var settings = ApplicationData.Current.RoamingSettings;
                if (settings.Values.TryGetValue(FREE_USAGE_LIMIT_KEY, out object value) && value is int limit)
                {
                    return limit;
                }
                return DEFAULT_FREE_USAGE_LIMIT;
            }
            set
            {
                var settings = ApplicationData.Current.RoamingSettings;
                settings.Values[FREE_USAGE_LIMIT_KEY] = value;
            }
        }

        /// <summary>
        /// 重置所有配置为默认值
        /// </summary>
        public static void ResetToDefaults()
        {
            var settings = ApplicationData.Current.RoamingSettings;
            settings.Values.Remove(OPENAI_ENDPOINT_KEY);
            settings.Values.Remove(OPENAI_API_KEY_KEY);
            settings.Values.Remove(OPENAI_DEPLOYMENT_NAME_KEY);
            settings.Values.Remove(FREE_USAGE_LIMIT_KEY);
        }
    }
}
