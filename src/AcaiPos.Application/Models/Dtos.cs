using AcaiPos.Domain.Enums;

namespace AcaiPos.Application.Models;

public sealed record CategoryDto(Guid Id, string Name, string? Description, int SortOrder);

public sealed record CategoryManageDto(
    Guid Id,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive);

public sealed record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    Guid CategoryId,
    string CategoryName,
    decimal SalePrice,
    decimal StockQuantity,
    string UnitOfMeasure);

public sealed record ProductManageDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    Guid CategoryId,
    string CategoryName,
    decimal SalePrice,
    decimal? CostPrice,
    decimal StockQuantity,
    string UnitOfMeasure,
    bool IsActive);

public sealed record SaveCategoryRequest(
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive);

public sealed record SaveProductRequest(
    string Name,
    string Sku,
    string? Barcode,
    Guid CategoryId,
    decimal SalePrice,
    decimal? CostPrice,
    decimal StockQuantity,
    string UnitOfMeasure,
    bool IsActive);

public sealed record CartItemDto(
    Guid ProductId,
    string ProductName,
    string ProductSku,
    decimal Quantity,
    decimal UnitPrice,
    decimal Subtotal);

public sealed record PaymentDraftDto(
    PaymentMethod Method,
    decimal Amount,
    decimal? AmountReceived = null);

public sealed record SaleSummaryDto(
    Guid Id,
    string TicketNumber,
    DateTime SoldAt,
    decimal Total,
    SaleStatus Status,
    string OperatorName,
    IReadOnlyList<CartItemDto> Items,
    IReadOnlyList<PaymentInfoDto> Payments,
    decimal ChangeAmount);

public sealed record SaleHistoryItemDto(
    Guid Id,
    string TicketNumber,
    DateTime SoldAt,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    SaleStatus Status,
    string StatusLabel,
    string OperatorName,
    Guid OperatorId,
    string TerminalCode,
    Guid TerminalId,
    int ItemCount,
    string PaymentSummary,
    decimal ChangeAmount,
    string? CancellationReason = null,
    string? CancelledByName = null,
    DateTime? CancelledAt = null);

public sealed record CancelSaleRequest(
    Guid SaleId,
    string Reason,
    string? AuthorizerUsername = null,
    string? AuthorizerPin = null);

public sealed record SaleHistoryFilter(
    DateTime? FromUtc = null,
    DateTime? ToUtcExclusive = null,
    Guid? OperatorId = null,
    Guid? TerminalId = null,
    SaleStatus? Status = null,
    PaymentMethod? PaymentMethod = null,
    string? Search = null);

public sealed record SaleHistoryTotalsDto(
    int Count,
    int CompletedCount,
    int CancelledCount,
    decimal CompletedTotal,
    decimal AverageTicket);

public sealed record OperatorOptionDto(Guid Id, string Name);

public sealed record TerminalOptionDto(Guid Id, string Code, string Name);

public sealed record PaymentInfoDto(
    PaymentMethod Method,
    decimal Amount,
    decimal ChangeAmount);

public sealed record TicketItemDto(
    string ProductName,
    string ProductSku,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal Subtotal);

public sealed record TicketPaymentDto(
    PaymentMethod Method,
    string MethodLabel,
    decimal Amount,
    decimal? AmountReceived,
    decimal ChangeAmount);

public sealed record TicketDto(
    Guid SaleId,
    string TicketNumber,
    string CompanyName,
    string StoreCode,
    string StoreName,
    string TerminalCode,
    string OperatorName,
    DateTime SoldAt,
    SaleStatus Status,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    decimal ChangeAmount,
    IReadOnlyList<TicketItemDto> Items,
    IReadOnlyList<TicketPaymentDto> Payments,
    bool IsReprint);

public sealed record CashMovementDto(
    Guid Id,
    CashMovementType Type,
    string TypeLabel,
    decimal Amount,
    string Reason,
    DateTime OccurredAt,
    string OperatorName);

public sealed record CashRegisterSummaryDto(
    Guid Id,
    DateTime OpenedAt,
    decimal OpeningAmount,
    string OperatorName,
    string TerminalCode,
    decimal CashSales,
    decimal CardSales,
    decimal PixSales,
    decimal Supplies,
    decimal Withdrawals,
    decimal ExpectedCash);

public sealed record CloseCashRegisterResultDto(
    Guid Id,
    decimal OpeningAmount,
    decimal ExpectedAmount,
    decimal CountedAmount,
    decimal DifferenceAmount,
    DateTime ClosedAt);

public sealed record LoginResultDto(
    Guid UserId,
    string Name,
    string Username,
    UserRole Role,
    Guid TerminalId,
    string TerminalCode,
    Guid StoreId,
    string StoreName,
    bool HasOpenCashRegister);

public sealed record UserDto(
    Guid Id,
    string Name,
    string Username,
    UserRole Role,
    string RoleLabel,
    bool IsActive);

public sealed record SaveUserRequest(
    string Name,
    string Username,
    UserRole Role,
    string? Pin,
    bool IsActive);

public sealed record DayReportDto(
    DateTime Day,
    int SaleCount,
    int CancelledCount,
    decimal GrossTotal,
    decimal DiscountTotal,
    decimal NetTotal,
    IReadOnlyList<DayReportPaymentDto> ByPaymentMethod,
    IReadOnlyList<DayReportOperatorDto> ByOperator);

public sealed record DayReportPaymentDto(string MethodLabel, decimal Amount, int Count);

public sealed record DayReportOperatorDto(string OperatorName, int SaleCount, decimal Total);

