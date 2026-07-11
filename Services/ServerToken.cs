namespace eComBox.Services
{
    internal static partial class ServerToken
    {
        internal static string Value
        {
            get
            {
                string value = string.Empty;
                SetLocalValue(ref value);
                return value;
            }
        }

        static partial void SetLocalValue(ref string value);
    }
}
