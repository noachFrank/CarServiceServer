namespace DispatchApp.Server.Utils
{
    public static class DateExtensions
    {
        public static bool IsInThisWeek(this DateTime date)
        {
            DateTime today = DateTime.Today;

            int diff = (int)today.DayOfWeek;
            DateTime weekStart = today.AddDays(-diff).Date;
            DateTime weekEnd = weekStart.AddDays(7).Date;

            return date.Date >= weekStart && date.Date < weekEnd;
        }
    }
}
