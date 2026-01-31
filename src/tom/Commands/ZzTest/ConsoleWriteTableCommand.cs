using System.CommandLine;
using Unlimitedinf.Utilities;

namespace Unlimitedinf.Tom.Commands.ZzTest
{
    internal static class ConsoleWriteTableCommand
    {
        public static Command Create()
        {
            Command command = new("zz-test-console-write-table", "Test the ConsoleFileProgressLogger to validate functionality.")
            {
                Hidden = true
            };
            command.SetAction(_ => Handle());
            return command;
        }

        private static void Handle()
        {
            // This is a sample CA cell phone billing for about a month
            var data = new InvoiceLineItemData[]
            {
                new()
                {
                    Id = 18,
                    Title = "Partial month of billing (Sept)",
                    Quantity = 1,
                    Price = 15.97,
                    Total = 15.97,
                    Modified = DateTimeOffset.Parse("2023-07-01")
                },
                new()
                {
                    Id = 19,
                    Title = "Monthly Charges",
                    Description = "5G Start (Sep 12 - Oct 11)",
                    Quantity = 1,
                    Price = 45,
                    Total = 45
                },
                new()
                {
                    Id = 20,
                    Title = "Multi-line discount",
                    Description = "Without this line on the plan, the other numbers wouldn't recevie a discount. Sep 12 - Oct 11.",
                    Quantity = 1,
                    Price = -10,
                    Total = -10
                },
                new()
                {
                    Id = 21,
                    Title = "Monthly Surcharges",
                    Description = "Fed Universal Service Charge ($0.75), Regulatory Charge ($0.09), Admin & Telco Recovery Charge ($3.30). Sep 12 - Oct 11.",
                    Quantity = 1,
                    Price = 4.14,
                    Total = 4.14
                },
                new()
                {
                    Id = 22,
                    Title = "Monthly Taxes and gov fees with a really long title to force multiple column wrapping",
                    Description = "CA State 911 Surcharge ($0.30), CA Teleconnect Fund Surchg ($0.02), CA State High Cost Fund (A) ($0.02), Lifeline Surcharge - CA ($0.13), CA Advanced Srvcs Fund (CASF) ($0.03), CA State PUC Fee ($0.01), CA Relay Srvc/Comm Device Fund ($0.03). Sep 12 - Oct 11.",
                    Quantity = 1,
                    Price = 0.54,
                    Total = 0.54
                }
            };

            // Based on default terminal sizing for ~100 chars, this should fit
            data.WriteTable(
                nameof(InvoiceLineItemData.Id),
                nameof(InvoiceLineItemData.Title),
                nameof(InvoiceLineItemData.Quantity),
                nameof(InvoiceLineItemData.Unit),
                nameof(InvoiceLineItemData.Price),
                nameof(InvoiceLineItemData.Total),
                nameof(InvoiceLineItemData.Modified));

            // Based on default terminal sizing for ~100 chars, this should require wrapped text
            data.WriteTable(
                nameof(InvoiceLineItemData.Id),
                nameof(InvoiceLineItemData.Title),
                nameof(InvoiceLineItemData.Description),
                nameof(InvoiceLineItemData.Quantity),
                nameof(InvoiceLineItemData.Unit),
                nameof(InvoiceLineItemData.Price),
                nameof(InvoiceLineItemData.Total));
        }

        private sealed class InvoiceLineItemData
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public double Quantity { get; set; }
            public string Unit { get; set; }
            public double Price { get; set; }
            public double Total { get; set; }
            public DateTimeOffset Modified { get; set; } = DateTimeOffset.Now;
        }
    }
}
