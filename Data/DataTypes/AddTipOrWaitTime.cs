namespace DispatchApp.Server.Data.DataTypes
{
    public class AddTipOrWaitTime
    {
        public int RideId { get; set; }
        public decimal Amount { get; set; }
    }

    public class ChangePriceDTO
    {

        public int RideId { get; set; }
        public decimal Amount { get; set; }
        public decimal DriversComp { get; set; }
    }
}
