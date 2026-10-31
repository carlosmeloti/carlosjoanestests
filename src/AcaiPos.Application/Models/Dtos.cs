using AcaiPos.Domain.Enums;

namespace AcaiPos.Application.Models;

public sealed record CategoryDto(Guid Id, string Name, string? Description, int SortOrder, bool IsDefault = false);

public sealed record CategoryManageDto(
    Guid Id,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    bool IsDefault = false);

public sealed record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    IReadOnlyList<Guid> CategoryIds,
    string CategoryName,
    decimal SalePrice,
    decimal StockQuantity,
    string UnitOfMeasure);

public sealed record ProductManageDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    IReadOnlyList<Guid> CategoryIds,
    string CategoryName,
    decimal SalePrice,
    decimal? CostPrice,
    decimal StockQuantity,
    string UnitOfMeasure,
    bool IsActive,
    string Ncm,
    string Cfop,
    int Origem,
    string Csosn,
    string? Cest,
    string PisCst,
    string CofinsCst,
    string IbsCbsCst = "000",
    string CClassTrib = "000001");

public sealed record SaveCategoryRequest(
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    bool IsDefault);

public sealed record SaveProductRequest(
    string Name,
    string Sku,
    string? Barcode,
    IReadOnlyList<Guid> CategoryIds,
    decimal SalePrice,
    decimal? CostPrice,
    decimal StockQuantity,
    string UnitOfMeasure,
    bool IsActive,
    string Ncm,
    string Cfop,
    int Origem,
    string Csosn,
    string? Cest,
    string PisCst,
    string CofinsCst,
    string IbsCbsCst = "000",
    string CClassTrib = "000001");

public sealed record CartItemDto(
    Guid ProductId,
    string ProductName,
    string ProductSku,
    decimal Quantity,
    decimal UnitPrice,
    decimal Subtotal);

public sealed record PaymentDraftDto(
    Guid FormaPagamentoId,
    string Descricao,
    bool CalculaTroco,
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
    decimal SurchargeAmount,
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
    DateTime? CancelledAt = null,
    bool TemNfce = false,
    string NfceStatusLabel = "—",
    int? NfceSerie = null,
    int? NfceNumero = null,
    string? NfceChave = null,
    string? NfceProtocolo = null,
    string? NfceCStat = null,
    string? NfceMotivo = null,
    bool PodeEnviarNfce = false,
    bool PodeExportarXml = false,
    bool CancelaNaSefaz = false,
    bool CancelaContingencia = false);

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
    string? PaymentDescription = null,
    string? Search = null,
    Guid? CashRegisterId = null,
    StatusDocumentoFiscal? NfceStatus = null,
    bool SemNfce = false);

public sealed record SaleHistoryTotalsDto(
    int Count,
    int CompletedCount,
    int CancelledCount,
    decimal CompletedTotal,
    decimal AverageTicket);

public sealed record OperatorOptionDto(Guid Id, string Name);

public sealed record FormaPagamentoDto(
    Guid Id,
    string Descricao,
    bool CalculaTroco,
    bool IsParcela,
    string CorRgbBotao,
    int Ordem,
    bool IsEnable);

public sealed record TerminalOptionDto(Guid Id, string Code, string Name);

public sealed record PaymentInfoDto(
    string Descricao,
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
    decimal SurchargeAmount,
    decimal Total,
    decimal ChangeAmount,
    IReadOnlyList<TicketItemDto> Items,
    IReadOnlyList<TicketPaymentDto> Payments,
    bool IsReprint,
    bool TemCupomFiscal = false,
    string? ChaveAcesso = null,
    string? Protocolo = null,
    string? QrCode = null,
    string? UrlConsulta = null,
    bool Contingencia = false,
    string? CpfConsumidor = null,
    string? DocumentoEmitente = null,
    string? EnderecoEmitente = null,
    string? InscricaoEstadual = null,
    int? NumeroNfce = null,
    int? SerieNfce = null,
    bool Homologacao = false,
    string? CnpjEmitente = null,
    decimal? TributoIbs = null,
    decimal? TributoCbs = null);

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
    decimal Supplies,
    decimal Withdrawals,
    decimal ExpectedCash);

public sealed record CashSessionListItemDto(
    Guid Id,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    string OperatorName,
    bool IsOpen,
    string PeriodPrefix,
    string StatusText,
    string OperatorCaption);

public sealed record CashSessionMovementDto(
    Guid Id,
    decimal Amount,
    string Reason,
    DateTime OccurredAt,
    string OperatorName);

public sealed record PaymentTotalDto(string Label, decimal Amount);

public sealed record CashSessionDetailDto(
    Guid Id,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    bool IsOpen,
    string OperatorName,
    string TerminalCode,
    decimal OpeningAmount,
    decimal TotalSold,
    decimal Supplies,
    decimal Withdrawals,
    decimal CashBalance,
    decimal GeneralBalance,
    IReadOnlyList<PaymentTotalDto> PaymentTotals,
    IReadOnlyList<CashSessionMovementDto> WithdrawalsList,
    IReadOnlyList<CashSessionMovementDto> SuppliesList);

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
    decimal SurchargeTotal,
    decimal NetTotal,
    IReadOnlyList<DayReportPaymentDto> ByPaymentMethod,
    IReadOnlyList<DayReportOperatorDto> ByOperator);

public sealed record DayReportPaymentDto(string MethodLabel, decimal Amount, int Count);

public sealed record DayReportOperatorDto(string OperatorName, int SaleCount, decimal Total);

