using System;

namespace eComBox.Models
{
    public class CountdownCardModel
    {
        public int Title { get; set; }

        public string TaskName { get; set; }

        public DateTime? TargetDate { get; set; }

        public string DisplayText { get; set; }

        public string BorderColorHex { get; set; } = string.Empty;

        public bool EnableDateNotification { get; set; }
    }
}
