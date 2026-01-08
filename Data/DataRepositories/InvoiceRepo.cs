using DispatchApp.Server.data;
using DispatchApp.Server.Data.DataTypes;
using Microsoft.EntityFrameworkCore;

namespace DispatchApp.Server.Data.DataRepositories
{
    public class InvoiceRepo
    {
        private readonly string _connectionString;

        public InvoiceRepo(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void AddInvoice(Invoice invoice)
        {
            using var context = new DispatchDbContext(_connectionString);
            context.Invoices.Add(invoice);
            context.SaveChanges();
        }

        public Invoice GetByInvoiceNumber(string invoiceNumber)
        {
            using var context = new DispatchDbContext(_connectionString);
            return context.Invoices
                .FirstOrDefault(i => i.InvoiceNumber == invoiceNumber);
        }

        public Invoice GetById(int id)
        {
            using var context = new DispatchDbContext(_connectionString);
            return context.Invoices
                .FirstOrDefault(i => i.Id == id);
        }

        public List<Invoice> GetInvoicesByDriver(int driverId)
        {
            using var context = new DispatchDbContext(_connectionString);
            // Exclude PdfData from list queries to improve performance
            return context.Invoices
                .Where(i => i.DriverId == driverId)
                .Select(i => new Invoice
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    DriverId = i.DriverId,
                    DriverName = i.DriverName,
                    DriverUsername = i.DriverUsername,
                    CreatedAt = i.CreatedAt,
                    PeriodStart = i.PeriodStart,
                    PeriodEnd = i.PeriodEnd,
                    RideCount = i.RideCount,
                    TotalOwedToDriver = i.TotalOwedToDriver,
                    TotalOwedByDriver = i.TotalOwedByDriver,
                    NetAmount = i.NetAmount,
                    DriverOwesCompany = i.DriverOwesCompany,
                    EmailSent = i.EmailSent,
                    LastEmailSentAt = i.LastEmailSentAt
                    // PdfData intentionally excluded
                })
                .OrderByDescending(i => i.CreatedAt)
                .ToList();
        }

        public List<DriverInvoiceSummary> GetDriversWithInvoiceCounts()
        {
            using var context = new DispatchDbContext(_connectionString);
            return context.Invoices
                .GroupBy(i => new { i.DriverId, i.DriverName, i.DriverUsername })
                .Select(g => new DriverInvoiceSummary
                {
                    DriverId = g.Key.DriverId,
                    DriverName = g.Key.DriverName,
                    DriverUsername = g.Key.DriverUsername,
                    InvoiceCount = g.Count(),
                    LatestInvoiceDate = g.Max(i => i.CreatedAt)
                })
                .OrderByDescending(d => d.LatestInvoiceDate)
                .ToList();
        }

        public void UpdateEmailSent(string invoiceNumber)
        {
            using var context = new DispatchDbContext(_connectionString);
            var invoice = context.Invoices.FirstOrDefault(i => i.InvoiceNumber == invoiceNumber);
            if (invoice != null)
            {
                invoice.EmailSent = true;
                invoice.LastEmailSentAt = DateTime.Now;
                context.SaveChanges();
            }
        }
    }

    public class DriverInvoiceSummary
    {
        public int DriverId { get; set; }
        public string DriverName { get; set; }
        public string DriverUsername { get; set; }
        public int InvoiceCount { get; set; }
        public DateTime LatestInvoiceDate { get; set; }
    }
}
