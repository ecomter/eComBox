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
        private const string ALI_BAIREN_ENDPOINT_KEY = "AliBairenEndpoint";
        private const string ALI_BAIREN_API_KEY_KEY = "AliBairenApiKey";
        private const string APP_SERVER_TOKEN_KEY = "AppServerToken";

        // 默认配置
        private static readonly string DEFAULT_OPENAI_ENDPOINT = "url";
        private static readonly string DEFAULT_OPENAI_API_KEY = "key";
        private static readonly string DEFAULT_OPENAI_DEPLOYMENT_NAME = "gpt-4o";
        private static readonly int DEFAULT_FREE_USAGE_LIMIT = 25;
        private static readonly string DEFAULT_ALI_BAIREN_ENDPOINT = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";

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
        /// 阿里云百炼终结点（可选）
        /// </summary>
        public static string AliBairenEndpoint
        {
            get
            {
                var settings = ApplicationData.Current.RoamingSettings;
                if (settings.Values.TryGetValue(ALI_BAIREN_ENDPOINT_KEY, out object value) && value is string endpoint && !string.IsNullOrWhiteSpace(endpoint))
                {
                    return endpoint;
                }
                // 默认使用华北（北京）地域的 dashscope 兼容接口
                return DEFAULT_ALI_BAIREN_ENDPOINT;
            }
            set
            {
                var settings = ApplicationData.Current.RoamingSettings;
                settings.Values[ALI_BAIREN_ENDPOINT_KEY] = value;
            }
        }

        /// <summary>
        /// 阿里云百炼 API 密钥（可选）
        /// </summary>
        public static string AliBairenApiKey
        {
            get
            {
                var settings = ApplicationData.Current.RoamingSettings;
                if (settings.Values.TryGetValue(ALI_BAIREN_API_KEY_KEY, out object value) && value is string key)
                {
                    return key;
                }
                return string.Empty;
            }
            set
            {
                var settings = ApplicationData.Current.RoamingSettings;
                settings.Values[ALI_BAIREN_API_KEY_KEY] = value;
            }
        }

        /// <summary>
        /// Token used between the app and the eComBox server. This is not the AI provider key.
        /// </summary>
        public static string AppServerToken
        {
            get
            {
                var settings = ApplicationData.Current.LocalSettings;
                return settings.Values.TryGetValue(APP_SERVER_TOKEN_KEY, out object value) && value is string token
                    ? token
                    : ServerToken.Value;
            }
            set => ApplicationData.Current.LocalSettings.Values[APP_SERVER_TOKEN_KEY] = value;
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
            settings.Values.Remove(ALI_BAIREN_ENDPOINT_KEY);
            settings.Values.Remove(ALI_BAIREN_API_KEY_KEY);
            ApplicationData.Current.LocalSettings.Values.Remove(APP_SERVER_TOKEN_KEY);
        }
    }
}
