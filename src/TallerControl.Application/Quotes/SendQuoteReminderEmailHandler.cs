using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Application.Customers;
using TallerControl.Application.Settings;
using TallerControl.Domain.Quotes;

namespace TallerControl.Application.Quotes;

/// <summary>
/// Caso de uso: reenviar por email un presupuesto enviado que el cliente no respondió (Inicio, «Presupuestos sin
/// respuesta»). Emite un enlace nuevo (el anterior deja de funcionar, igual que en <see cref="GetQuotePublicLinkHandler"/>)
/// y encola el correo por el outbox. El remitente técnico es la cuenta de correo del sistema (D-04); el nombre visible
/// es el del taller y las respuestas del cliente van a quien lo reenvió (Reply-To).
/// </summary>
public sealed class SendQuoteReminderEmailHandler
{
    private readonly IQuoteRepository _quotes;
    private readonly ICustomerRepository _customers;
    private readonly ISettingsRepository _settings;
    private readonly IAuthRepository _auth;
    private readonly GetQuotePublicLinkHandler _links;
    private readonly IOutbox _outbox;
    private readonly IActorContext _actor;

    public SendQuoteReminderEmailHandler(
        IQuoteRepository quotes,
        ICustomerRepository customers,
        ISettingsRepository settings,
        IAuthRepository auth,
        GetQuotePublicLinkHandler links,
        IOutbox outbox,
        IActorContext actor)
    {
        _quotes = quotes;
        _customers = customers;
        _settings = settings;
        _auth = auth;
        _links = links;
        _outbox = outbox;
        _actor = actor;
    }

    public async Task<QuoteReminderEmailDto> HandleAsync(Guid quoteId, CancellationToken ct)
    {
        var quote = await _quotes.FindByIdAsync(quoteId, ct)
            ?? throw new NotFoundException("El presupuesto no existe.");

        if (quote.Status != QuoteStatus.Sent)
            throw new BusinessRuleException("Solo se puede reenviar un presupuesto enviado que todavía no tiene respuesta.");

        var customer = await _customers.FindActiveByIdAsync(quote.CustomerId, ct)
            ?? throw new BusinessRuleException("El cliente del presupuesto ya no existe.");

        if (customer.DoNotContact)
            throw new BusinessRuleException("El cliente pidió no ser contactado.");

        if (string.IsNullOrWhiteSpace(customer.Email))
            throw new BusinessRuleException("El cliente no tiene un email cargado.");

        var tenant = await _settings.FindTenantAsync(ct)
            ?? throw new NotFoundException();

        string? replyTo = null;
        if (_actor.ActorId is { } userId)
            replyTo = (await _auth.FindUserByIdAsync(userId, ct))?.Email;

        // Emite el enlace nuevo (revoca el anterior) y lo guarda; el correo se encola después.
        var link = await _links.HandleAsync(quoteId, ct);

        _outbox.Enqueue("email.quote_reminder", new
        {
            to = customer.Email,
            customerName = customer.Name,
            quoteNumber = quote.Number,
            total = quote.Total,
            token = link.TokenBase64Url,
            tenantName = tenant.Name,
            replyTo,
        });
        await _quotes.SaveChangesAsync(ct);

        return new QuoteReminderEmailDto(customer.Email);
    }
}

/// <summary>Respuesta de <c>POST /api/v1/quotes/{id}/reminder-email</c>: a qué dirección se encoló el correo.</summary>
public sealed record QuoteReminderEmailDto(string SentTo);
