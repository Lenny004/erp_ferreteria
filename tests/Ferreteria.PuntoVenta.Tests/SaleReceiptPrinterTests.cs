using System.Text.Json;
using Ferreteria.PuntoVenta.Models;
using Ferreteria.PuntoVenta.Services;
using Ferreteria.PuntoVenta.Services.Domain;
using Ferreteria.PuntoVenta.Services.Printing;
using Ferreteria.PuntoVenta.Services.SalesHistory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ferreteria.PuntoVenta.Tests;

/// <summary>Pruebas unitarias de la orquestación segura de reimpresiones.</summary>
public sealed class SaleReceiptPrinterTests
{
    /// <summary>Una reimpresión exitosa incrementa y audita después del envío.</summary>
    [Fact]
    public async Task ReprintSuccess_IncrementsAndAudits()
    {
        var doubles = new TestDoubles();
        var printer = doubles.CreateSystem();

        var result = await printer.PrintSaleAsync(doubles.OrderId, isReprint: true);

        Assert.Equal(SaleReceiptPrintStatus.Printed, result.Status);
        Assert.Equal(1, doubles.History.IncrementCalls);
        var audit = Assert.Single(doubles.Audit.Records);
        Assert.Equal(SalesDomainConstants.SalesHistoryAuditActions.ReceiptReprint, audit.Action);
        Assert.Contains(SalesDomainConstants.SalesHistoryAuditActions.ReceiptReprintEvent, JsonSerializer.Serialize(audit.NewData));
    }

    /// <summary>Un fallo de impresora no cambia contador ni bitácora.</summary>
    [Fact]
    public async Task PrinterFailure_DoesNotIncrementOrAudit()
    {
        var doubles = new TestDoubles { PrinterThrows = true };
        var printer = doubles.CreateSystem();

        var result = await printer.PrintSaleAsync(doubles.OrderId, isReprint: true);

        Assert.Equal(SaleReceiptPrintStatus.PrinterFailed, result.Status);
        Assert.Equal(0, doubles.History.IncrementCalls);
        Assert.Empty(doubles.Audit.Records);
    }

    /// <summary>Sin impresora predeterminada no se intenta imprimir ni auditar.</summary>
    [Fact]
    public async Task MissingDefaultPrinter_DoesNotIncrementOrAudit()
    {
        var doubles = new TestDoubles { HasDefaultPrinter = false };
        var printer = doubles.CreateSystem();

        var result = await printer.PrintSaleAsync(doubles.OrderId, isReprint: true);

        Assert.Equal(SaleReceiptPrintStatus.NoDefaultPrinter, result.Status);
        Assert.Equal(0, doubles.Printing.Calls);
        Assert.Equal(0, doubles.History.IncrementCalls);
        Assert.Empty(doubles.Audit.Records);
    }

    /// <summary>Una impresión normal no actualiza la trazabilidad de reimpresión.</summary>
    [Fact]
    public async Task NormalPrint_DoesNotIncrementOrAuditReprint()
    {
        var doubles = new TestDoubles();
        var printer = doubles.CreateSystem();

        var result = await printer.PrintSaleAsync(doubles.OrderId);

        Assert.Equal(SaleReceiptPrintStatus.Printed, result.Status);
        Assert.Equal(1, doubles.Printing.Calls);
        Assert.Equal(0, doubles.History.IncrementCalls);
        Assert.Empty(doubles.Audit.Records);
    }

    private sealed class TestDoubles
    {
        public Guid OrderId { get; } = Guid.NewGuid();

        public bool HasDefaultPrinter { get; init; } = true;

        public bool PrinterThrows { get; init; }

        public PrintingDouble Printing { get; } = new();

        public HistoryDouble History { get; } = new();

        public AuditDouble Audit { get; } = new();

        public SaleReceiptPrinter CreateSystem()
        {
            Printing.ThrowOnPrint = PrinterThrows;
            return new SaleReceiptPrinter(
                new PrinterConfigDouble(HasDefaultPrinter),
                new CompositionDouble(),
                Printing,
                NullLogger<SaleReceiptPrinter>.Instance,
                History,
                Audit,
                new SessionDouble());
        }
    }

    private sealed class PrinterConfigDouble(bool hasDefaultPrinter) : IPrinterConfigService
    {
        private readonly bool _hasDefaultPrinter = hasDefaultPrinter;

        public Task<IReadOnlyList<Printer>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Printer>>(Array.Empty<Printer>());
        }

        public Task<Printer?> GetDefaultAsync(CancellationToken cancellationToken = default)
        {
            Printer? printer = _hasDefaultPrinter
                ? new Printer { Name = "PRUEBA", ConnectionType = PrinterConfigurationRules.Usb, PaperWidth = 80, IsDefault = true }
                : null;
            return Task.FromResult(printer);
        }

        public Task<Printer> SaveAsync(PrinterInput input, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetDefaultAsync(Guid printerId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class CompositionDouble : IReceiptCompositionService
    {
        public Task<ReceiptDocument?> ComposeForOrderAsync(Guid orderId, bool isReprint = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ReceiptDocument?>(new ReceiptDocument(
                "Ferretería",
                null,
                "NIT",
                "NRC",
                "Dirección",
                null,
                "COMPROBANTE INTERNO",
                ReceiptDocumentTypes.InternalReceipt,
                orderId.ToString(),
                Guid.NewGuid().ToString(),
                null,
                "00",
                DateTime.UtcNow,
                "Cajero",
                "Cliente",
                null,
                Array.Empty<TicketLineItem>(),
                1m,
                0.13m,
                1.13m,
                "UN DÓLAR",
                SalesDomainConstants.PaymentMethods.Cash,
                1.13m,
                0m,
                string.Empty,
                false,
                null,
                isReprint)
            {
                OrderId = orderId
            });
        }
    }

    private sealed class PrintingDouble : IReceiptPrintService
    {
        public int Calls { get; private set; }

        public bool ThrowOnPrint { get; set; }

        public IReadOnlyList<string> GetInstalledWindowsPrinters() => Array.Empty<string>();

        public Task PrintReceiptAsync(ReceiptDocument document, PrinterConfig printer, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (ThrowOnPrint)
            {
                throw new PrinterException("Fallo controlado de prueba.");
            }

            return Task.CompletedTask;
        }

        public Task PrintTestPageAsync(PrinterConfig printer, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PrintTextReportAsync(string title, string body, PrinterConfig printer, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class HistoryDouble : ISalesHistoryService
    {
        public int IncrementCalls { get; private set; }

        public Task<SalesHistoryPage> SearchAsync(SalesHistoryFilter filter, Guid employeeId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SalesHistoryDetail?> GetDetailAsync(Guid orderId, Guid employeeId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> IncrementReprintsAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            IncrementCalls++;
            return Task.FromResult(true);
        }
    }

    private sealed class AuditDouble : IAuditService
    {
        public List<AuditRecord> Records { get; } = new();

        public Task RecordLoginAsync(Employee employee, string module, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RecordLogoutAsync(Employee employee, string? module, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RecordChangeAsync(string action, string tableName, string recordId, object? oldData, object? newData, Guid? userId, CancellationToken cancellationToken = default)
        {
            Records.Add(new AuditRecord(action, tableName, recordId, oldData, newData, userId));
            return Task.CompletedTask;
        }
    }

    private sealed record AuditRecord(string Action, string TableName, string RecordId, object? OldData, object? NewData, Guid? UserId);

    private sealed class SessionDouble : ICurrentSessionService
    {
        public Employee? CurrentEmployee { get; } = new() { Id = Guid.NewGuid() };

        public OperationalModule? ActiveModule => OperationalModule.Caja;

        public string? CurrentModule => OperationalModule.Caja.ToString();

        public DateTime? StartedAtUtc => DateTime.UtcNow;

        public bool IsActive => true;

        public void StartSession(Employee employee, OperationalModule module, string initialSection)
        {
            throw new NotSupportedException();
        }

        public string ResolveInitialSection() => string.Empty;

        public void EndSession()
        {
            throw new NotSupportedException();
        }
    }
}
