using ClosedXML.Excel;
using TallerControl.Application.Reports;

namespace TallerControl.Infrastructure.Reports;

internal sealed class ClosedXmlExporter : IReportExporter
{
    public byte[] ExportRevenue(RevenueReportDto report)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Ingresos");

        ws.Cell(1, 1).Value = "Fecha";
        ws.Cell(1, 2).Value = "Cobros";
        ws.Cell(1, 3).Value = "Monto ($)";
        StyleHeader(ws.Row(1));

        int row = 2;
        foreach (var r in report.Rows)
        {
            ws.Cell(row, 1).Value = r.Date.ToString("dd/MM/yyyy");
            ws.Cell(row, 2).Value = r.PaymentCount;
            ws.Cell(row, 3).Value = (double)r.Amount;
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 3).Value = (double)report.Total;
        ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
        ws.Row(row).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
        return ToBytes(wb);
    }

    public byte[] ExportReceivables(ReceivablesReportDto report)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Cuentas por cobrar");

        ws.Cell(1, 1).Value = "N° OT";
        ws.Cell(1, 2).Value = "Cliente";
        ws.Cell(1, 3).Value = "Vehículo";
        ws.Cell(1, 4).Value = "Saldo ($)";
        ws.Cell(1, 5).Value = "Fecha";
        StyleHeader(ws.Row(1));

        int row = 2;
        foreach (var r in report.Rows)
        {
            ws.Cell(row, 1).Value = r.WorkOrderNumber;
            ws.Cell(row, 2).Value = r.CustomerName;
            ws.Cell(row, 3).Value = r.VehiclePlate;
            ws.Cell(row, 4).Value = (double)r.Balance;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 5).Value = r.CreatedAt.LocalDateTime.ToString("dd/MM/yyyy");
            row++;
        }

        ws.Cell(row, 3).Value = "TOTAL";
        ws.Cell(row, 4).Value = (double)report.Total;
        ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        ws.Row(row).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
        return ToBytes(wb);
    }

    public byte[] ExportStock(StockReportDto report)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Stock");

        ws.Cell(1, 1).Value = "Nombre";
        ws.Cell(1, 2).Value = "SKU";
        ws.Cell(1, 3).Value = "Tipo";
        ws.Cell(1, 4).Value = "Stock actual";
        ws.Cell(1, 5).Value = "Stock mínimo";
        ws.Cell(1, 6).Value = "Costo unit.";
        ws.Cell(1, 7).Value = "Estado";
        StyleHeader(ws.Row(1));

        int row = 2;
        foreach (var r in report.Rows)
        {
            ws.Cell(row, 1).Value = r.Name;
            ws.Cell(row, 2).Value = r.Sku ?? string.Empty;
            ws.Cell(row, 3).Value = r.Kind;
            ws.Cell(row, 4).Value = (double)r.StockQty;
            ws.Cell(row, 5).Value = r.StockMin.HasValue ? (double)r.StockMin.Value : (double?)null;
            ws.Cell(row, 6).Value = (double)r.UnitCost;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 7).Value = r.IsLowStock ? "Stock bajo" : "OK";
            if (r.IsLowStock)
                ws.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
            row++;
        }

        ws.Columns().AdjustToContents();
        return ToBytes(wb);
    }

    public byte[] ExportPurchases(PurchasesReportDto report)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Compras");

        ws.Cell(1, 1).Value = "N°";
        ws.Cell(1, 2).Value = "Proveedor";
        ws.Cell(1, 3).Value = "Fecha";
        ws.Cell(1, 4).Value = "Total ($)";
        ws.Cell(1, 5).Value = "Estado";
        StyleHeader(ws.Row(1));

        int row = 2;
        foreach (var r in report.Rows)
        {
            ws.Cell(row, 1).Value = r.PurchaseNumber;
            ws.Cell(row, 2).Value = r.SupplierName ?? string.Empty;
            ws.Cell(row, 3).Value = r.Date.ToString("dd/MM/yyyy");
            ws.Cell(row, 4).Value = (double)r.Total;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 5).Value = r.Status;
            row++;
        }

        ws.Cell(row, 3).Value = "TOTAL";
        ws.Cell(row, 4).Value = (double)report.Total;
        ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        ws.Row(row).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
        return ToBytes(wb);
    }

    private static void StyleHeader(IXLRow row)
    {
        row.Style.Font.Bold = true;
        row.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e40af");
        row.Style.Font.FontColor = XLColor.White;
    }

    private static byte[] ToBytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
