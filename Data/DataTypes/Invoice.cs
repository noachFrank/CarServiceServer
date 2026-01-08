using System.Text.Json.Serialization;

namespace DispatchApp.Server.Data.DataTypes
{
    public class Invoice
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public int DriverId { get; set; }
        public string DriverName { get; set; }
        public string DriverUsername { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int RideCount { get; set; }
        public decimal TotalOwedToDriver { get; set; }
        public decimal TotalOwedByDriver { get; set; }
        public decimal NetAmount { get; set; }
        public bool DriverOwesCompany { get; set; }
        public string? FilePath { get; set; } // Legacy - kept for backwards compatibility
        public bool EmailSent { get; set; }
        public DateTime? LastEmailSentAt { get; set; }

        [JsonIgnore]
        public byte[]? PdfData { get; set; } // PDF stored in database

        [JsonIgnore]
        public Driver Driver { get; set; }
    }
}
