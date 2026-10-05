using AcaiPos.Application.Abstractions;
using AcaiPos.Application.Models;
using AcaiPos.Application.Tickets;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AcaiPos.Application.Services;

public sealed class AuthService
{
    private readonly IPosDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ICurrentSession _session;
    private readonly IAuditService _audit;

    public AuthService(
        IPosDbContext db,
        IPasswordHasher hasher,
        ICurrentSession session,
        IAuditService audit)
    {
        _db = db;
        _hasher = hasher;
        _session = session;
        _audit = audit;
    }

    public async Task<LoginResultDto> LoginAsync(string username, string pin, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        var user = _db.Users.FirstOrDefault(u => u.Username == normalized && u.DeletedAt == null)
            ?? throw new DomainException("Usuário ou PIN inválido.");

        if (!user.IsActive)
            throw new DomainException("Usuário inativo.");

        if (!_hasher.Verify(pin, user.PinHash))
            throw new DomainException("Usuário ou PIN inválido.");

        var terminal = _db.Terminals.FirstOrDefault(t => t.IsActive && t.DeletedAt == null)
            ?? throw new DomainException("Nenhum terminal configurado.");

        var store = _db.Stores.FirstOrDefault(s => s.Id == terminal.StoreId)
            ?? throw new DomainException("Loja do terminal não encontrada.");

        var openCash = _db.CashRegisters.FirstOrDefault(c =>
            c.TerminalId == terminal.Id && c.Status == CashRegisterStatus.Open);

        _session.SetUser(user);
        _session.SetTerminal(terminal, store);
        _session.SetCashRegister(openCash);

        await _audit.LogAsync("LOGIN", nameof(user), user.Id, $"Usuário {user.Username} autenticado.", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new LoginResultDto(
            user.Id,
            user.Name,
            user.Username,
            user.Role,
            terminal.Id,
            terminal.Code,
            store.Id,
            store.Name,
            openCash is not null);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (_session.CurrentUser is not null)
        {
            await _audit.LogAsync(
                "LOGOUT",
                "User",
                _session.CurrentUser.Id,
                $"Usuário {_session.CurrentUser.Username} saiu.",
                cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        _session.Clear();
    }
}

public sealed class CatalogService
{
    private readonly IPosDbContext _db;
    private readonly ICurrentSession _session;
    private readonly IAuditService _audit;

    public CatalogService(IPosDbContext db, ICurrentSession session, IAuditService audit)
    {
        _db = db;
        _session = session;
        _audit = audit;
    }

    public Task<IReadOnlyList<CategoryDto>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var items = _db.Categories
            .Where(c => c.IsActive && c.DeletedAt == null)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.SortOrder))
            .ToList();

        return Task.FromResult<IReadOnlyList<CategoryDto>>(items);
    }

    public Task<IReadOnlyList<CategoryManageDto>> GetManagedCategoriesAsync(CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        var items = _db.Categories
            .Where(c => c.DeletedAt == null)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryManageDto(c.Id, c.Name, c.Description, c.SortOrder, c.IsActive))
            .ToList();

        return Task.FromResult<IReadOnlyList<CategoryManageDto>>(items);
    }

    public Task<IReadOnlyList<ProductDto>> GetSellableProductsAsync(
        Guid? categoryId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Products.Where(p => p.IsActive && p.DeletedAt == null);

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Sku.ToLower().Contains(term) ||
                (p.Barcode != null && p.Barcode.Contains(term)));
        }

        var categories = _db.Categories.ToDictionary(c => c.Id, c => c.Name);

        var items = query
            .OrderBy(p => p.Name)
            .AsEnumerable()
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Sku,
                p.Barcode,
                p.CategoryId,
                categories.GetValueOrDefault(p.CategoryId, "Sem categoria"),
                p.SalePrice,
                p.StockQuantity,
                p.UnitOfMeasure))
            .ToList();

        return Task.FromResult<IReadOnlyList<ProductDto>>(items);
    }

    public Task<IReadOnlyList<ProductManageDto>> GetManagedProductsAsync(
        Guid? categoryId = null,
        string? search = null,
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        var query = _db.Products.Where(p => p.DeletedAt == null);
        if (!includeInactive)
            query = query.Where(p => p.IsActive);

        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Sku.ToLower().Contains(term) ||
                (p.Barcode != null && p.Barcode.Contains(term)));
        }

        var categories = _db.Categories.ToDictionary(c => c.Id, c => c.Name);
        var items = query
            .OrderBy(p => p.Name)
            .AsEnumerable()
            .Select(p => MapManageProduct(p, categories.GetValueOrDefault(p.CategoryId, "Sem categoria")))
            .ToList();

        return Task.FromResult<IReadOnlyList<ProductManageDto>>(items);
    }

    public Task<ProductDto?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        var product = _db.Products.FirstOrDefault(p =>
            p.IsActive && p.DeletedAt == null && p.Barcode == barcode);

        if (product is null)
            return Task.FromResult<ProductDto?>(null);

        var categoryName = _db.Categories.FirstOrDefault(c => c.Id == product.CategoryId)?.Name ?? "Sem categoria";
        return Task.FromResult<ProductDto?>(new ProductDto(
            product.Id,
            product.Name,
            product.Sku,
            product.Barcode,
            product.CategoryId,
            categoryName,
            product.SalePrice,
            product.StockQuantity,
            product.UnitOfMeasure));
    }

    public async Task<CategoryManageDto> CreateCategoryAsync(
        SaveCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        EnsureUniqueCategoryName(request.Name);

        var category = Domain.Entities.Category.Create(request.Name, request.Description, request.SortOrder);
        if (!request.IsActive)
            category.Update(category.Name, category.Description, category.SortOrder, false);

        _db.Add(category);
        await _audit.LogAsync("CATEGORY_CREATE", "Category", category.Id, category.Name, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new CategoryManageDto(category.Id, category.Name, category.Description, category.SortOrder, category.IsActive);
    }

    public async Task<CategoryManageDto> UpdateCategoryAsync(
        Guid categoryId,
        SaveCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        var category = _db.Categories.FirstOrDefault(c => c.Id == categoryId && c.DeletedAt == null)
            ?? throw new DomainException("Categoria não encontrada.");

        EnsureUniqueCategoryName(request.Name, categoryId);
        category.Update(request.Name, request.Description, request.SortOrder, request.IsActive);

        await _audit.LogAsync("CATEGORY_UPDATE", "Category", category.Id, category.Name, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new CategoryManageDto(category.Id, category.Name, category.Description, category.SortOrder, category.IsActive);
    }

    public async Task DeactivateCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        var category = _db.Categories.FirstOrDefault(c => c.Id == categoryId && c.DeletedAt == null)
            ?? throw new DomainException("Categoria não encontrada.");

        var hasActiveProducts = _db.Products.Any(p =>
            p.CategoryId == categoryId && p.IsActive && p.DeletedAt == null);

        if (hasActiveProducts)
            throw new DomainException("Existem produtos ativos nesta categoria. Desative-os antes.");

        category.Update(category.Name, category.Description, category.SortOrder, false);
        await _audit.LogAsync("CATEGORY_DEACTIVATE", "Category", category.Id, category.Name, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProductManageDto> CreateProductAsync(
        SaveProductRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        EnsureCategoryExists(request.CategoryId);
        EnsureUniqueSku(request.Sku);
        EnsureUniqueBarcode(request.Barcode);

        var product = Domain.Entities.Product.Create(
            request.Name,
            request.Sku,
            request.CategoryId,
            request.SalePrice,
            request.Barcode,
            request.CostPrice,
            request.StockQuantity,
            request.UnitOfMeasure);

        if (!request.IsActive)
        {
            product.Update(
                product.Name,
                product.Sku,
                product.CategoryId,
                product.SalePrice,
                product.Barcode,
                product.CostPrice,
                product.UnitOfMeasure,
                false);
        }

        _db.Add(product);
        await _audit.LogAsync("PRODUCT_CREATE", "Product", product.Id, $"{product.Sku} — {product.Name}", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var categoryName = _db.Categories.First(c => c.Id == product.CategoryId).Name;
        return MapManageProduct(product, categoryName);
    }

    public async Task<ProductManageDto> UpdateProductAsync(
        Guid productId,
        SaveProductRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        EnsureCategoryExists(request.CategoryId, requireActive: false);
        EnsureUniqueSku(request.Sku, productId);
        EnsureUniqueBarcode(request.Barcode, productId);

        var product = _db.Products.FirstOrDefault(p => p.Id == productId && p.DeletedAt == null)
            ?? throw new DomainException("Produto não encontrado.");

        var stockDelta = request.StockQuantity - product.StockQuantity;

        product.Update(
            request.Name,
            request.Sku,
            request.CategoryId,
            request.SalePrice,
            request.Barcode,
            request.CostPrice,
            request.UnitOfMeasure,
            request.IsActive);

        if (stockDelta != 0)
        {
            var movement = Domain.Entities.StockMovement.Create(
                product,
                StockMovementType.Adjustment,
                stockDelta,
                "Ajuste manual no cadastro",
                operatorId: _session.CurrentUser!.Id);
            _db.Add(movement);
        }

        await _audit.LogAsync("PRODUCT_UPDATE", "Product", product.Id, $"{product.Sku} — {product.Name}", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var categoryName = _db.Categories.First(c => c.Id == product.CategoryId).Name;
        return MapManageProduct(product, categoryName);
    }

    public async Task DeactivateProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        var product = _db.Products.FirstOrDefault(p => p.Id == productId && p.DeletedAt == null)
            ?? throw new DomainException("Produto não encontrado.");

        product.Update(
            product.Name,
            product.Sku,
            product.CategoryId,
            product.SalePrice,
            product.Barcode,
            product.CostPrice,
            product.UnitOfMeasure,
            false);

        await _audit.LogAsync("PRODUCT_DEACTIVATE", "Product", product.Id, product.Name, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static ProductManageDto MapManageProduct(Domain.Entities.Product product, string categoryName)
        => new(
            product.Id,
            product.Name,
            product.Sku,
            product.Barcode,
            product.CategoryId,
            categoryName,
            product.SalePrice,
            product.CostPrice,
            product.StockQuantity,
            product.UnitOfMeasure,
            product.IsActive);

    private void EnsureCanManage()
    {
        if (_session.CurrentUser is null || !_session.CurrentUser.CanManageProducts)
            throw new DomainException("Sem permissão para gerenciar o catálogo.");
    }

    private void EnsureCategoryExists(Guid categoryId, bool requireActive = true)
    {
        var category = _db.Categories.FirstOrDefault(c => c.Id == categoryId && c.DeletedAt == null)
            ?? throw new DomainException("Categoria inválida.");

        if (requireActive && !category.IsActive)
            throw new DomainException("Categoria inválida ou inativa.");
    }

    private void EnsureUniqueCategoryName(string name, Guid? ignoreId = null)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var exists = _db.Categories.Any(c =>
            c.DeletedAt == null &&
            c.Name.ToLower() == normalized &&
            (!ignoreId.HasValue || c.Id != ignoreId.Value));

        if (exists)
            throw new DomainException("Já existe uma categoria com este nome.");
    }

    private void EnsureUniqueSku(string sku, Guid? ignoreId = null)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        var exists = _db.Products.Any(p =>
            p.DeletedAt == null &&
            p.Sku == normalized &&
            (!ignoreId.HasValue || p.Id != ignoreId.Value));

        if (exists)
            throw new DomainException("SKU já cadastrado.");
    }

    private void EnsureUniqueBarcode(string? barcode, Guid? ignoreId = null)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return;

        var normalized = barcode.Trim();
        var exists = _db.Products.Any(p =>
            p.DeletedAt == null &&
            p.Barcode == normalized &&
            (!ignoreId.HasValue || p.Id != ignoreId.Value));

        if (exists)
            throw new DomainException("Código de barras já cadastrado.");
    }
}

public sealed class CashRegisterService
{
    private readonly IPosDbContext _db;
    private readonly ICurrentSession _session;
    private readonly IAuditService _audit;

    public CashRegisterService(IPosDbContext db, ICurrentSession session, IAuditService audit)
    {
        _db = db;
        _session = session;
        _audit = audit;
    }

    public async Task<CashRegisterSummaryDto> OpenAsync(decimal openingAmount, CancellationToken cancellationToken = default)
    {
        EnsureSession();

        if (_session.HasOpenCashRegister)
            throw new DomainException("Já existe um caixa aberto neste terminal.");

        var existing = _db.CashRegisters.FirstOrDefault(c =>
            c.TerminalId == _session.CurrentTerminal!.Id && c.Status == CashRegisterStatus.Open);

        if (existing is not null)
            throw new DomainException("Já existe um caixa aberto neste terminal.");

        var cash = Domain.Entities.CashRegister.Open(
            _session.CurrentTerminal!.Id,
            _session.CurrentUser!.Id,
            openingAmount);

        _db.Add(cash);
        _session.SetCashRegister(cash);

        await _audit.LogAsync(
            "CASH_OPEN",
            "CashRegister",
            cash.Id,
            $"Abertura com {openingAmount:C}.",
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return await GetSummaryAsync(cash.Id, cancellationToken);
    }

    public async Task AddMovementAsync(
        CashMovementType type,
        decimal amount,
        string reason,
        CancellationToken cancellationToken = default)
    {
        EnsureOpenCash();
        var cash = _session.OpenCashRegister!;
        var movement = cash.AddMovement(_session.CurrentUser!.Id, type, amount, reason);
        _db.Add(movement);

        await _audit.LogAsync(
            $"CASH_{type.ToString().ToUpperInvariant()}",
            "CashMovement",
            movement.Id,
            $"{type}: {amount:C} — {reason}",
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<CashMovementDto>> GetOpenMovementsAsync(CancellationToken cancellationToken = default)
    {
        if (!_session.HasOpenCashRegister)
            return Task.FromResult<IReadOnlyList<CashMovementDto>>([]);

        var cashId = _session.OpenCashRegister!.Id;
        var operators = _db.Users.ToDictionary(u => u.Id, u => u.Name);

        var items = _db.CashMovements
            .Where(m => m.CashRegisterId == cashId)
            .OrderByDescending(m => m.OccurredAt)
            .AsEnumerable()
            .Select(m => new CashMovementDto(
                m.Id,
                m.Type,
                m.Type switch
                {
                    CashMovementType.Supply => "Suprimento",
                    CashMovementType.Withdrawal => "Sangria",
                    CashMovementType.Adjustment => "Ajuste",
                    _ => m.Type.ToString()
                },
                m.Amount,
                m.Reason,
                m.OccurredAt,
                operators.GetValueOrDefault(m.OperatorId, "—")))
            .ToList();

        return Task.FromResult<IReadOnlyList<CashMovementDto>>(items);
    }

    public async Task<CloseCashRegisterResultDto> CloseAsync(
        decimal countedAmount,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        EnsureOpenCash();
        var cash = _session.OpenCashRegister!;

        var completedSales = _db.Sales
            .Where(s => s.CashRegisterId == cash.Id && s.Status == SaleStatus.Completed)
            .ToList();

        var saleIds = completedSales.Select(s => s.Id).ToHashSet();
        var payments = _db.Payments
            .Where(p => saleIds.Contains(p.SaleId) && p.Status == PaymentStatus.Confirmed)
            .ToList();

        var cashSales = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        var movements = _db.CashMovements.Where(m => m.CashRegisterId == cash.Id).ToList();
        var supplies = movements.Where(m => m.Type == CashMovementType.Supply).Sum(m => m.Amount);
        var withdrawals = movements.Where(m => m.Type == CashMovementType.Withdrawal).Sum(m => m.Amount);

        cash.Close(_session.CurrentUser!.Id, countedAmount, cashSales, supplies, withdrawals, notes);

        await _audit.LogAsync(
            "CASH_CLOSE",
            "CashRegister",
            cash.Id,
            $"Fechamento. Contado {countedAmount:C}, esperado {cash.ExpectedAmount:C}, diferença {cash.DifferenceAmount:C}.",
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        _session.SetCashRegister(null);

        return new CloseCashRegisterResultDto(
            cash.Id,
            cash.OpeningAmount,
            cash.ExpectedAmount!.Value,
            cash.CountedAmount!.Value,
            cash.DifferenceAmount!.Value,
            cash.ClosedAt!.Value);
    }

    public Task<CashRegisterSummaryDto?> GetOpenSummaryAsync(CancellationToken cancellationToken = default)
    {
        if (!_session.HasOpenCashRegister)
            return Task.FromResult<CashRegisterSummaryDto?>(null);

        return GetSummaryAsync(_session.OpenCashRegister!.Id, cancellationToken)!;
    }

    private Task<CashRegisterSummaryDto> GetSummaryAsync(Guid cashRegisterId, CancellationToken cancellationToken)
    {
        var cash = _db.CashRegisters.First(c => c.Id == cashRegisterId);
        var operatorName = _db.Users.FirstOrDefault(u => u.Id == cash.OperatorId)?.Name ?? "—";
        var terminalCode = _db.Terminals.FirstOrDefault(t => t.Id == cash.TerminalId)?.Code ?? "—";

        var completedSales = _db.Sales
            .Where(s => s.CashRegisterId == cash.Id && s.Status == SaleStatus.Completed)
            .ToList();
        var saleIds = completedSales.Select(s => s.Id).ToHashSet();
        var payments = _db.Payments
            .Where(p => saleIds.Contains(p.SaleId) && p.Status == PaymentStatus.Confirmed)
            .ToList();

        var cashSales = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        var cardSales = payments.Where(p => p.Method is PaymentMethod.DebitCard or PaymentMethod.CreditCard).Sum(p => p.Amount);
        var pixSales = payments.Where(p => p.Method == PaymentMethod.Pix).Sum(p => p.Amount);

        var movements = _db.CashMovements.Where(m => m.CashRegisterId == cash.Id).ToList();
        var supplies = movements.Where(m => m.Type == CashMovementType.Supply).Sum(m => m.Amount);
        var withdrawals = movements.Where(m => m.Type == CashMovementType.Withdrawal).Sum(m => m.Amount);
        var expected = cash.OpeningAmount + cashSales + supplies - withdrawals;

        return Task.FromResult(new CashRegisterSummaryDto(
            cash.Id,
            cash.OpenedAt,
            cash.OpeningAmount,
            operatorName,
            terminalCode,
            cashSales,
            cardSales,
            pixSales,
            supplies,
            withdrawals,
            expected));
    }

    private void EnsureSession()
    {
        if (!_session.IsAuthenticated || _session.CurrentTerminal is null)
            throw new DomainException("Sessão inválida. Faça login novamente.");
    }

    private void EnsureOpenCash()
    {
        EnsureSession();
        if (!_session.HasOpenCashRegister)
            throw new DomainException("Não há caixa aberto.");
    }
}

public sealed class SaleService
{
    private readonly IPosDbContext _db;
    private readonly ICurrentSession _session;
    private readonly ITicketNumberGenerator _tickets;
    private readonly IAuditService _audit;
    private readonly IPasswordHasher _hasher;

    public SaleService(
        IPosDbContext db,
        ICurrentSession session,
        ITicketNumberGenerator tickets,
        IAuditService audit,
        IPasswordHasher hasher)
    {
        _db = db;
        _session = session;
        _tickets = tickets;
        _audit = audit;
        _hasher = hasher;
    }

    public bool CurrentUserCanCancelSales => _session.CurrentUser?.CanCancelSales == true;
    public bool CurrentUserCanApplyDiscount => _session.CurrentUser?.CanApplyDiscount == true;

    private void EnsureDiscountAuthorized(string? authorizerUsername, string? authorizerPin)
    {
        if (_session.CurrentUser!.CanApplyDiscount)
            return;

        if (string.IsNullOrWhiteSpace(authorizerUsername) || string.IsNullOrWhiteSpace(authorizerPin))
            throw new DomainException("Desconto exige autorização de gerente ou administrador.");

        var normalized = authorizerUsername.Trim().ToLowerInvariant();
        var authorizer = _db.Users.FirstOrDefault(u => u.Username == normalized && u.DeletedAt == null)
            ?? throw new DomainException("Autorizador de desconto inválido.");

        if (!authorizer.IsActive || !_hasher.Verify(authorizerPin, authorizer.PinHash) || !authorizer.CanApplyDiscount)
            throw new DomainException("Autorização de desconto inválida.");
    }

    public async Task<SaleSummaryDto> CompleteSaleAsync(
        IReadOnlyList<CartItemDto> cartItems,
        IReadOnlyList<PaymentDraftDto> payments,
        decimal discountAmount = 0,
        string? discountAuthorizerUsername = null,
        string? discountAuthorizerPin = null,
        CancellationToken cancellationToken = default)
    {
        if (!_session.IsAuthenticated || _session.CurrentTerminal is null)
            throw new DomainException("Sessão inválida.");
        if (!_session.HasOpenCashRegister)
            throw new DomainException("Abra o caixa antes de vender.");
        if (cartItems.Count == 0)
            throw new DomainException("Carrinho vazio.");
        if (payments.Count == 0)
            throw new DomainException("Informe ao menos uma forma de pagamento.");

        if (discountAmount > 0)
            EnsureDiscountAuthorized(discountAuthorizerUsername, discountAuthorizerPin);

        var ticket = await _tickets.NextAsync(_session.CurrentTerminal.Id, cancellationToken);
        var sale = Domain.Entities.Sale.Create(
            _session.OpenCashRegister!.Id,
            _session.CurrentTerminal.Id,
            _session.CurrentUser!.Id,
            ticket);

        foreach (var cartItem in cartItems)
        {
            var product = _db.Products.FirstOrDefault(p => p.Id == cartItem.ProductId)
                ?? throw new DomainException($"Produto {cartItem.ProductName} não encontrado.");

            sale.AddItem(product, cartItem.Quantity);
        }

        if (discountAmount > 0)
            sale.ApplyDiscount(discountAmount);

        var paymentSum = decimal.Round(payments.Sum(p => p.Amount), 2);
        if (Math.Abs(paymentSum - sale.Total) > 0.009m)
            throw new DomainException($"Pagamentos ({paymentSum:C}) devem totalizar a venda ({sale.Total:C}).");

        foreach (var payment in payments)
            sale.AddPayment(payment.Method, payment.Amount, payment.AmountReceived);

        sale.Complete();

        foreach (var item in sale.Items)
        {
            var product = _db.Products.First(p => p.Id == item.ProductId);
            var movement = Domain.Entities.StockMovement.Create(
                product,
                StockMovementType.Sale,
                -item.Quantity,
                $"Venda {sale.TicketNumber}",
                sale.Id,
                _session.CurrentUser.Id);
            _db.Add(movement);
        }

        _db.Add(sale);

        await _audit.LogAsync(
            "SALE_COMPLETE",
            "Sale",
            sale.Id,
            $"Ticket {sale.TicketNumber} total {sale.Total:C}.",
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return MapSale(sale);
    }

    public async Task CancelSaleAsync(CancelSaleRequest request, CancellationToken cancellationToken = default)
    {
        if (!_session.IsAuthenticated)
            throw new DomainException("Sessão inválida.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new DomainException("Motivo do cancelamento é obrigatório.");

        var canceller = ResolveCanceller(request.AuthorizerUsername, request.AuthorizerPin);

        var sale = _db.Sales.FirstOrDefault(s => s.Id == request.SaleId)
            ?? throw new DomainException("Venda não encontrada.");

        if (sale.Status != SaleStatus.Completed)
            throw new DomainException("Somente vendas concluídas podem ser canceladas.");

        var items = _db.SaleItems.Where(i => i.SaleId == request.SaleId).ToList();
        var payments = _db.Payments.Where(p => p.SaleId == request.SaleId).ToList();

        sale.Cancel(canceller.Id, request.Reason);
        sale.CancelPayments(payments);

        foreach (var item in items)
        {
            var product = _db.Products.First(p => p.Id == item.ProductId);
            var movement = Domain.Entities.StockMovement.Create(
                product,
                StockMovementType.SaleReversal,
                item.Quantity,
                $"Cancelamento {sale.TicketNumber}: {request.Reason}",
                sale.Id,
                canceller.Id);
            _db.Add(movement);
        }

        await _audit.LogAsync(
            "SALE_CANCEL",
            "Sale",
            sale.Id,
            $"Cancelado por {canceller.Username}: {request.Reason}",
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
    }

    private Domain.Entities.User ResolveCanceller(string? authorizerUsername, string? authorizerPin)
    {
        if (_session.CurrentUser!.CanCancelSales)
            return _session.CurrentUser;

        if (string.IsNullOrWhiteSpace(authorizerUsername) || string.IsNullOrWhiteSpace(authorizerPin))
            throw new DomainException("Cancelamento exige autorização de gerente ou administrador.");

        var normalized = authorizerUsername.Trim().ToLowerInvariant();
        var authorizer = _db.Users.FirstOrDefault(u =>
            u.Username == normalized && u.DeletedAt == null)
            ?? throw new DomainException("Usuário autorizador inválido.");

        if (!authorizer.IsActive)
            throw new DomainException("Usuário autorizador inativo.");

        if (!_hasher.Verify(authorizerPin, authorizer.PinHash))
            throw new DomainException("PIN do autorizador inválido.");

        if (!authorizer.CanCancelSales)
            throw new DomainException("O autorizador não possui permissão para cancelar vendas.");

        return authorizer;
    }

    public Task<IReadOnlyList<SaleSummaryDto>> GetTodaySalesAsync(CancellationToken cancellationToken = default)
    {
        var start = DateTime.UtcNow.Date;
        var end = start.AddDays(1);

        var sales = _db.Sales
            .Where(s => s.SoldAt >= start && s.SoldAt < end)
            .OrderByDescending(s => s.SoldAt)
            .ToList();

        var result = sales.Select(MapSale).ToList();
        return Task.FromResult<IReadOnlyList<SaleSummaryDto>>(result);
    }

    public Task<IReadOnlyList<SaleHistoryItemDto>> SearchHistoryAsync(
        SaleHistoryFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (!_session.IsAuthenticated)
            throw new DomainException("Sessão inválida.");

        var query = _db.Sales.AsQueryable();

        if (filter.FromUtc.HasValue)
            query = query.Where(s => s.SoldAt >= filter.FromUtc.Value);

        if (filter.ToUtcExclusive.HasValue)
            query = query.Where(s => s.SoldAt < filter.ToUtcExclusive.Value);

        if (filter.OperatorId.HasValue && filter.OperatorId.Value != Guid.Empty)
            query = query.Where(s => s.OperatorId == filter.OperatorId.Value);

        if (filter.TerminalId.HasValue && filter.TerminalId.Value != Guid.Empty)
            query = query.Where(s => s.TerminalId == filter.TerminalId.Value);

        if (filter.Status.HasValue)
            query = query.Where(s => s.Status == filter.Status.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(s => s.TicketNumber.ToLower().Contains(term));
        }

        var sales = query.OrderByDescending(s => s.SoldAt).Take(500).ToList();

        if (filter.PaymentMethod.HasValue)
        {
            var method = filter.PaymentMethod.Value;
            var matchingSaleIds = _db.Payments
                .Where(p => p.Method == method && p.Status == PaymentStatus.Confirmed)
                .Select(p => p.SaleId)
                .Distinct()
                .ToHashSet();

            sales = sales.Where(s => matchingSaleIds.Contains(s.Id)).ToList();
        }

        return Task.FromResult<IReadOnlyList<SaleHistoryItemDto>>(sales.Select(MapHistoryItem).ToList());
    }

    public Task<SaleHistoryTotalsDto> GetHistoryTotalsAsync(
        IReadOnlyList<SaleHistoryItemDto> items,
        CancellationToken cancellationToken = default)
    {
        var completed = items.Where(i => i.Status == SaleStatus.Completed).ToList();
        var cancelled = items.Count(i => i.Status == SaleStatus.Cancelled);
        var completedTotal = completed.Sum(i => i.Total);
        var average = completed.Count == 0 ? 0m : decimal.Round(completedTotal / completed.Count, 2);

        return Task.FromResult(new SaleHistoryTotalsDto(
            items.Count,
            completed.Count,
            cancelled,
            completedTotal,
            average));
    }

    public Task<IReadOnlyList<OperatorOptionDto>> GetOperatorOptionsAsync(CancellationToken cancellationToken = default)
    {
        var items = _db.Users
            .Where(u => u.DeletedAt == null)
            .OrderBy(u => u.Name)
            .Select(u => new OperatorOptionDto(u.Id, u.Name))
            .ToList();

        return Task.FromResult<IReadOnlyList<OperatorOptionDto>>(items);
    }

    public Task<IReadOnlyList<TerminalOptionDto>> GetTerminalOptionsAsync(CancellationToken cancellationToken = default)
    {
        var items = _db.Terminals
            .Where(t => t.DeletedAt == null)
            .OrderBy(t => t.Code)
            .Select(t => new TerminalOptionDto(t.Id, t.Code, t.Name))
            .ToList();

        return Task.FromResult<IReadOnlyList<TerminalOptionDto>>(items);
    }

    private SaleHistoryItemDto MapHistoryItem(Domain.Entities.Sale sale)
    {
        var operatorName = _db.Users.FirstOrDefault(u => u.Id == sale.OperatorId)?.Name ?? "—";
        var terminalCode = _db.Terminals.FirstOrDefault(t => t.Id == sale.TerminalId)?.Code ?? "—";
        var items = _db.SaleItems.Where(i => i.SaleId == sale.Id).ToList();
        if (items.Count == 0 && sale.Items.Count > 0)
            items = sale.Items.ToList();

        var payments = _db.Payments
            .Where(p => p.SaleId == sale.Id && p.Status != PaymentStatus.Cancelled)
            .ToList();
        if (payments.Count == 0 && sale.Payments.Count > 0)
            payments = sale.Payments.Where(p => p.Status != PaymentStatus.Cancelled).ToList();

        var paymentSummary = payments.Count == 0
            ? "—"
            : string.Join(" + ", payments
                .GroupBy(p => p.Method)
                .Select(g => $"{MethodShort(g.Key)} {g.Sum(x => x.Amount):C}"));

        return new SaleHistoryItemDto(
            sale.Id,
            sale.TicketNumber,
            sale.SoldAt,
            sale.Subtotal,
            sale.DiscountAmount,
            sale.Total,
            sale.Status,
            StatusLabel(sale.Status),
            operatorName,
            sale.OperatorId,
            terminalCode,
            sale.TerminalId,
            items.Count,
            paymentSummary,
            sale.ChangeAmount,
            sale.CancellationReason,
            sale.CancelledByOperatorId is null
                ? null
                : _db.Users.FirstOrDefault(u => u.Id == sale.CancelledByOperatorId)?.Name,
            sale.CancelledAt);
    }

    private static string StatusLabel(SaleStatus status) => status switch
    {
        SaleStatus.Open => "Aberta",
        SaleStatus.Completed => "Concluída",
        SaleStatus.Cancelled => "Cancelada",
        _ => status.ToString()
    };

    private static string MethodShort(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Dinheiro",
        PaymentMethod.DebitCard => "Débito",
        PaymentMethod.CreditCard => "Crédito",
        PaymentMethod.Pix => "PIX",
        _ => method.ToString()
    };

    private SaleSummaryDto MapSale(Domain.Entities.Sale sale)
    {
        var operatorName = _db.Users.FirstOrDefault(u => u.Id == sale.OperatorId)?.Name ?? "—";
        var items = _db.SaleItems.Where(i => i.SaleId == sale.Id).ToList();
        var payments = _db.Payments.Where(p => p.SaleId == sale.Id).ToList();

        // When sale was just created in memory, use navigation collections
        if (items.Count == 0 && sale.Items.Count > 0)
            items = sale.Items.ToList();
        if (payments.Count == 0 && sale.Payments.Count > 0)
            payments = sale.Payments.ToList();

        return new SaleSummaryDto(
            sale.Id,
            sale.TicketNumber,
            sale.SoldAt,
            sale.Total,
            sale.Status,
            operatorName,
            items.Select(i => new CartItemDto(i.ProductId, i.ProductName, i.ProductSku, i.Quantity, i.UnitPrice, i.Subtotal)).ToList(),
            payments.Select(p => new PaymentInfoDto(p.Method, p.Amount, p.ChangeAmount)).ToList(),
            sale.ChangeAmount);
    }

    public Task<DayReportDto> GetDayReportAsync(DateTime localDay, CancellationToken cancellationToken = default)
    {
        if (!_session.IsAuthenticated)
            throw new DomainException("Sessão inválida.");

        var fromUtc = DateTime.SpecifyKind(localDay.Date, DateTimeKind.Local).ToUniversalTime();
        var toUtc = fromUtc.AddDays(1);

        var sales = _db.Sales
            .Where(s => s.SoldAt >= fromUtc && s.SoldAt < toUtc)
            .ToList();

        if (_session.CurrentTerminal is not null)
            sales = sales.Where(s => s.TerminalId == _session.CurrentTerminal.Id).ToList();

        var completed = sales.Where(s => s.Status == SaleStatus.Completed).ToList();
        var cancelled = sales.Count(s => s.Status == SaleStatus.Cancelled);
        var completedIds = completed.Select(s => s.Id).ToHashSet();

        var payments = _db.Payments
            .Where(p => completedIds.Contains(p.SaleId) && p.Status == PaymentStatus.Confirmed)
            .ToList();

        var byPayment = payments
            .GroupBy(p => p.Method)
            .Select(g => new DayReportPaymentDto(MethodShort(g.Key), g.Sum(x => x.Amount), g.Count()))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var users = _db.Users.ToDictionary(u => u.Id, u => u.Name);
        var byOperator = completed
            .GroupBy(s => s.OperatorId)
            .Select(g => new DayReportOperatorDto(
                users.GetValueOrDefault(g.Key, "—"),
                g.Count(),
                g.Sum(x => x.Total)))
            .OrderByDescending(x => x.Total)
            .ToList();

        return Task.FromResult(new DayReportDto(
            localDay.Date,
            completed.Count,
            cancelled,
            completed.Sum(s => s.Subtotal),
            completed.Sum(s => s.DiscountAmount),
            completed.Sum(s => s.Total),
            byPayment,
            byOperator));
    }
}

public sealed class UserService
{
    private readonly IPosDbContext _db;
    private readonly ICurrentSession _session;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditService _audit;

    public UserService(
        IPosDbContext db,
        ICurrentSession session,
        IPasswordHasher hasher,
        IAuditService audit)
    {
        _db = db;
        _session = session;
        _hasher = hasher;
        _audit = audit;
    }

    public bool CurrentUserCanManageUsers => _session.CurrentUser?.CanManageUsers == true;

    public Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        var items = _db.Users
            .Where(u => u.DeletedAt == null)
            .OrderBy(u => u.Name)
            .AsEnumerable()
            .Select(Map)
            .ToList();
        return Task.FromResult<IReadOnlyList<UserDto>>(items);
    }

    public async Task<UserDto> CreateAsync(SaveUserRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        if (string.IsNullOrWhiteSpace(request.Pin))
            throw new DomainException("PIN é obrigatório para novo usuário.");

        EnsureUniqueUsername(request.Username);
        var user = Domain.Entities.User.Create(
            request.Name,
            request.Username,
            _hasher.Hash(request.Pin),
            request.Role);

        if (!request.IsActive)
            user.Deactivate();

        _db.Add(user);
        await _audit.LogAsync("USER_CREATE", "User", user.Id, user.Username, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task<UserDto> UpdateAsync(Guid userId, SaveUserRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        var user = _db.Users.FirstOrDefault(u => u.Id == userId && u.DeletedAt == null)
            ?? throw new DomainException("Usuário não encontrado.");

        user.Update(request.Name, request.Role, request.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Pin))
            user.UpdatePin(_hasher.Hash(request.Pin));

        await _audit.LogAsync("USER_UPDATE", "User", user.Id, user.Username, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task DeactivateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        var user = _db.Users.FirstOrDefault(u => u.Id == userId && u.DeletedAt == null)
            ?? throw new DomainException("Usuário não encontrado.");

        if (user.Id == _session.CurrentUser!.Id)
            throw new DomainException("Não é possível desativar o usuário logado.");

        user.Deactivate();
        await _audit.LogAsync("USER_DEACTIVATE", "User", user.Id, user.Username, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private void EnsureCanManage()
    {
        if (_session.CurrentUser is null || !_session.CurrentUser.CanManageUsers)
            throw new DomainException("Sem permissão para gerenciar usuários.");
    }

    private void EnsureUniqueUsername(string username, Guid? ignoreId = null)
    {
        var normalized = username.Trim().ToLowerInvariant();
        var exists = _db.Users.Any(u =>
            u.DeletedAt == null &&
            u.Username == normalized &&
            (!ignoreId.HasValue || u.Id != ignoreId.Value));
        if (exists)
            throw new DomainException("Login já cadastrado.");
    }

    private static UserDto Map(Domain.Entities.User user) => new(
        user.Id,
        user.Name,
        user.Username,
        user.Role,
        user.Role switch
        {
            UserRole.Operator => "Operador",
            UserRole.Manager => "Gerente",
            UserRole.Administrator => "Administrador",
            _ => user.Role.ToString()
        },
        user.IsActive);
}

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ICurrentSession, CurrentSession>();
        services.AddSingleton<AuthService>();
        services.AddSingleton<CatalogService>();
        services.AddSingleton<CashRegisterService>();
        services.AddSingleton<SaleService>();
        services.AddSingleton<UserService>();
        services.AddSingleton<TicketService>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<ITicketFormatter, TextTicketFormatter>();
        return services;
    }
}

public sealed class CurrentSession : ICurrentSession
{
    public Domain.Entities.User? CurrentUser { get; private set; }
    public Domain.Entities.Terminal? CurrentTerminal { get; private set; }
    public Domain.Entities.Store? CurrentStore { get; private set; }
    public Domain.Entities.CashRegister? OpenCashRegister { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;
    public bool HasOpenCashRegister => OpenCashRegister is { Status: CashRegisterStatus.Open };

    public void SetUser(Domain.Entities.User user) => CurrentUser = user;
    public void SetTerminal(Domain.Entities.Terminal terminal, Domain.Entities.Store store)
    {
        CurrentTerminal = terminal;
        CurrentStore = store;
    }

    public void SetCashRegister(Domain.Entities.CashRegister? cashRegister) => OpenCashRegister = cashRegister;

    public void Clear()
    {
        CurrentUser = null;
        CurrentTerminal = null;
        CurrentStore = null;
        OpenCashRegister = null;
    }
}

public sealed class AuditService : IAuditService
{
    private readonly IPosDbContext _db;
    private readonly ICurrentSession _session;

    public AuditService(IPosDbContext db, ICurrentSession session)
    {
        _db = db;
        _session = session;
    }

    public Task LogAsync(
        string action,
        string entityName,
        Guid? entityId = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var entry = Domain.Entities.AuditEntry.Create(
            action,
            entityName,
            entityId,
            _session.CurrentUser?.Id,
            _session.CurrentTerminal?.Id,
            details);

        _db.Add(entry);
        return Task.CompletedTask;
    }
}
