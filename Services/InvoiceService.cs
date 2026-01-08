using DispatchApp.Server.Data.DataTypes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DispatchApp.Server.Services
{
    public class InvoiceService
    {
        static InvoiceService()
        {
            // QuestPDF requires license configuration
            // Community license is free for companies with less than $1M annual revenue
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateInvoice(Driver driver, List<Ride> rides, string invoiceNumber)
        {
            // Calculate totals
            decimal totalOwedToDriver = 0; // CC payments - company owes driver
            decimal totalOwedByDriver = 0; // Cash/Zelle - driver owes company (collected but keeps comp)

            foreach (var ride in rides)
            {
                var driverComp = ride.DriversCompensation ?? 0;
                if (ride.PaymentType == "cash" || ride.PaymentType == "zelle")
                {
                    // Driver collected cash, owes company the difference
                    totalOwedByDriver += (ride.Cost - driverComp);
                }
                else
                {
                    // CC payment - company owes driver their compensation
                    totalOwedToDriver += driverComp;
                }
            }

            var netAmount = totalOwedToDriver - totalOwedByDriver;
            var driverOwesCompany = netAmount < 0;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    // Header
                    page.Header().Element(ComposeHeader);

                    // Content
                    page.Content().Element(content => ComposeContent(content, driver, rides, invoiceNumber,
                        totalOwedToDriver, totalOwedByDriver, netAmount, driverOwesCompany));

                    // Footer
                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container)
        {
            container.PaddingBottom(20).Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Shia's Transportation")
                        .FontSize(24).Bold().FontColor(Colors.Blue.Darken2);
                    column.Item().Text("Invoice")
                        .FontSize(14).FontColor(Colors.Grey.Darken1);
                });

                row.ConstantItem(150).Column(column =>
                {
                    column.Item().AlignRight().Text(DateTime.Now.ToString("MMMM dd, yyyy"))
                        .FontSize(10).FontColor(Colors.Grey.Darken1);
                });
            });
        }

        private void ComposeContent(IContainer container, Driver driver, List<Ride> rides, string invoiceNumber,
            decimal totalOwedToDriver, decimal totalOwedByDriver, decimal netAmount, bool driverOwesCompany)
        {
            container.Column(column =>
            {
                // Invoice Info & Driver Info
                column.Item().Row(row =>
                {
                    row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                    {
                        col.Item().Text("Invoice Details").Bold().FontSize(12);
                        col.Item().PaddingTop(5).Text($"Invoice #: {invoiceNumber}");
                        col.Item().Text($"Date: {DateTime.Now:MM/dd/yyyy}");
                        col.Item().Text($"Total Rides: {rides.Count}");
                    });

                    row.ConstantItem(20);

                    row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                    {
                        col.Item().Text("Driver Information").Bold().FontSize(12);
                        col.Item().PaddingTop(5).Text($"Name: {driver.Name}");
                        col.Item().Text($"ID: #{driver.Id}");
                        col.Item().Text($"Email: {driver.Email}");
                        col.Item().Text($"Phone: {driver.PhoneNumber}");
                    });
                });

                column.Item().PaddingTop(20);

                // Rides Table
                column.Item().Text("Ride Details").Bold().FontSize(12);
                column.Item().PaddingTop(5);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(50);  // Ride ID
                        columns.RelativeColumn(2);   // Date
                        columns.RelativeColumn(3);   // Route
                        columns.ConstantColumn(60);  // Payment
                        columns.ConstantColumn(55);  // Cost
                        columns.ConstantColumn(55);  // Driver Comp
                        columns.ConstantColumn(65);  // Settlement
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("ID").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Date").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Route").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Payment").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignRight()
                            .Text("Cost").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignRight()
                            .Text("Comp").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignRight()
                            .Text("Amt Owed").FontColor(Colors.White).Bold().FontSize(9);
                    });

                    // Rows
                    var isAlternate = false;
                    foreach (var ride in rides.OrderBy(r => r.ScheduledFor))
                    {
                        var bgColor = isAlternate ? Colors.Grey.Lighten4 : Colors.White;
                        var driverComp = ride.DriversCompensation ?? 0;
                        var isCashOrZelle = ride.PaymentType == "cash" || ride.PaymentType == "zelle";
                        var settlementAmount = isCashOrZelle
                            ? -(ride.Cost - driverComp)  // Driver owes this
                            : driverComp;                 // Company owes this

                        var route = ride.Route != null
                            ? $"{TruncateAddress(ride.Route.Pickup)} → {TruncateAddress(ride.Route.DropOff)}"
                            : "N/A";

                        table.Cell().Background(bgColor).Padding(4).Text(ride.RideId.ToString()).FontSize(8);
                        table.Cell().Background(bgColor).Padding(4).Text(ride.ScheduledFor.ToString("MM/dd HH:mm")).FontSize(8);
                        table.Cell().Background(bgColor).Padding(4).Text(route).FontSize(7);
                        table.Cell().Background(bgColor).Padding(4).Text(FormatPaymentType(ride.PaymentType)).FontSize(8);
                        table.Cell().Background(bgColor).Padding(4).AlignRight().Text($"${ride.Cost:F2}").FontSize(8);
                        table.Cell().Background(bgColor).Padding(4).AlignRight().Text($"${driverComp:F2}").FontSize(8);
                        table.Cell().Background(bgColor).Padding(4).AlignRight()
                            .Text($"{(settlementAmount < 0 ? "-" : "")}${Math.Abs(settlementAmount):F2}")
                            .FontSize(8)
                            .FontColor(settlementAmount < 0 ? Colors.Red.Darken1 : Colors.Green.Darken1);

                        isAlternate = !isAlternate;
                    }
                });

                column.Item().PaddingTop(20);

                // Summary Box
                column.Item().AlignRight().Width(250).Border(2).BorderColor(Colors.Blue.Darken2).Column(summaryCol =>
                {
                    summaryCol.Item().Background(Colors.Blue.Darken2).Padding(10)
                        .Text("Summary").FontColor(Colors.White).Bold().FontSize(12).AlignCenter();

                    summaryCol.Item().Padding(10).Column(innerCol =>
                    {
                        innerCol.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Company owes driver:").FontSize(10);
                            r.ConstantItem(80).AlignRight().Text($"${totalOwedToDriver:F2}")
                                .FontColor(Colors.Green.Darken1).FontSize(10);
                        });

                        innerCol.Item().PaddingTop(5).Row(r =>
                        {
                            r.RelativeItem().Text("Driver owes company:").FontSize(10);
                            r.ConstantItem(80).AlignRight().Text($"${totalOwedByDriver:F2}")
                                .FontColor(Colors.Red.Darken1).FontSize(10);
                        });

                        innerCol.Item().PaddingTop(10).BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(10).Row(r =>
                        {
                            r.RelativeItem().Text("NET AMOUNT:").Bold().FontSize(12);
                            r.ConstantItem(80).AlignRight().Text($"{(driverOwesCompany ? "-" : "")}${Math.Abs(netAmount):F2}")
                                .Bold()
                                .FontSize(12)
                                .FontColor(driverOwesCompany ? Colors.Red.Darken1 : Colors.Green.Darken1);
                        });

                        innerCol.Item().PaddingTop(5).AlignCenter()
                            .Text(driverOwesCompany
                                ? "Driver owes company"
                                : "Company owes driver")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });

                column.Item().PaddingTop(30);

                // Notes
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(notesCol =>
                {
                    notesCol.Item().Text("Notes").Bold().FontSize(10);
                    notesCol.Item().PaddingTop(5).Text("• CC payments: Company collected payment, driver is owed their compensation.")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    notesCol.Item().Text("• Cash/Zelle: Driver collected full amount, owes company (Cost - Compensation).")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    notesCol.Item().Text("• Positive amounts = Company pays driver. Negative = Driver pays company.")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(text =>
            {
                text.Span("Generated on ").FontSize(8).FontColor(Colors.Grey.Medium);
                text.Span(DateTime.Now.ToString("MM/dd/yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium);
                text.Span(" | Shia's Transportation").FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }

        private string TruncateAddress(string? address)
        {
            if (string.IsNullOrEmpty(address)) return "N/A";
            // Get just the street part, truncate if too long
            var parts = address.Split(',');
            var street = parts[0].Trim();
            return street.Length > 25 ? street.Substring(0, 22) + "..." : street;
        }

        private string FormatPaymentType(string? paymentType)
        {
            return paymentType?.ToLower() switch
            {
                "cash" => "Cash",
                "zelle" => "Zelle",
                "dispatchercc" => "CC On File",
                "drivercc" => "CC",
                _ => paymentType ?? "N/A"
            };
        }

        public static string GenerateInvoiceNumber(int driverId)
        {
            return $"INV-{driverId}-{DateTime.Now:yyyyMMddHHmmss}";
        }
    }
}
