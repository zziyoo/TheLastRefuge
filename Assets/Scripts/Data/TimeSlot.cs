namespace LastRefuge.Data
{
    public enum TimeSlot
    {
        Morning,
        Planning,
        Action,
        Evening,
        Night,
        DayEnd
    }

    public static class TimeSlotExtensions
    {
        public static string GetDisplayName(this TimeSlot slot)
        {
            return slot switch
            {
                TimeSlot.Morning => "清晨",
                TimeSlot.Planning => "上午",
                TimeSlot.Action => "下午",
                TimeSlot.Evening => "傍晚",
                TimeSlot.Night => "夜晚",
                TimeSlot.DayEnd => "日结算",
                _ => slot.ToString()
            };
        }

        public static TimeSlot Next(this TimeSlot slot)
        {
            return slot switch
            {
                TimeSlot.Morning => TimeSlot.Planning,
                TimeSlot.Planning => TimeSlot.Action,
                TimeSlot.Action => TimeSlot.Evening,
                TimeSlot.Evening => TimeSlot.Night,
                TimeSlot.Night => TimeSlot.DayEnd,
                TimeSlot.DayEnd => TimeSlot.Morning,
                _ => TimeSlot.Morning
            };
        }
    }
}