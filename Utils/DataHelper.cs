namespace DispatchApp.Server.Utils
{
    public static class DataHelper
    {
        /// <summary>
        /// Checks if any of the provided objects are null.
        /// </summary>
        public static bool IsNull(this object objects)
        {
            return objects == null;// || objects.Any(o => o == null);
        }
    }
}
