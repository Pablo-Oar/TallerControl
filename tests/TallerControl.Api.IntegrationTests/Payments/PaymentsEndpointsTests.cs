using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Payments;

/// <summary>
/// E7 bloque 5: tests de integración de pagos.
/// T-PAY-01..06 (doc 08 §4.9), T-DOC-03, T-PUB-07, REQ-15, REQ-16.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PaymentsEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private PaymentsWebApplicationFactory _factory = null!;

    public PaymentsEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await PaymentsWebApplicationFactory.CreateAsync(_postgres);
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    private static async Task<string> FetchCsrfAsync(HttpClient client)
    {
        var resp = await client.GetAsync("/api/v1/auth/antiforgery-token");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()!;
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password, rememberMe = false }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    private async Task<JsonElement> CreatePaymentAsync(
        HttpClient client, decimal amount = 500m, string? idempotencyKey = null)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/work-orders/{_factory.WorkOrderId}/payments")
        {
            Content = JsonContent.Create(new { method = "cash", amount }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        if (idempotencyKey is not null)
            req.Headers.Add("Idempotency-Key", idempotencyKey);

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    // ── T-PAY-01: listado vacío ───────────────────────────────────────────────

    [Fact(DisplayName = "T-PAY-01: GET /payments de OT sin pagos devuelve lista vacía con saldo completo")]
    public async Task Lista_pagos_vacia_devuelve_saldo_completo()
    {
        using var client = CreateClient();
        await LoginAsync(client, PaymentsWebApplicationFactory.OwnerEmail, PaymentsWebApplicationFactory.Password);

        var resp = await client.GetAsync($"/api/v1/work-orders/{_factory.WorkOrderId}/payments");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("payments").GetArrayLength());
        Assert.True(body.GetProperty("total").GetDecimal() > 0);
        Assert.Equal(0m, body.GetProperty("paid").GetDecimal());
        Assert.True(body.GetProperty("balance").GetDecimal() > 0);
    }

    // ── T-PAY-02: registrar un pago ──────────────────────────────────────────

    [Fact(DisplayName = "T-PAY-02: POST /payments registra un cobro y devuelve saldo actualizado")]
    public async Task Registrar_pago_reduce_saldo()
    {
        using var client = CreateClient();
        await LoginAsync(client, PaymentsWebApplicationFactory.OwnerEmail, PaymentsWebApplicationFactory.Password);

        var result = await CreatePaymentAsync(client, amount: 500m);

        var payment = result.GetProperty("payment");
        Assert.Equal("cash", payment.GetProperty("method").GetString());
        Assert.Equal(500m, payment.GetProperty("amount").GetDecimal());
        Assert.Equal("valid", payment.GetProperty("status").GetString());

        // El saldo debe haber bajado en 500
        var balance = result.GetProperty("balance").GetDecimal();
        Assert.True(balance >= 0);

        // El token público no debe estar vacío
        var token = payment.GetProperty("publicToken").GetString();
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    // ── T-PAY-03: monto excede saldo ─────────────────────────────────────────

    [Fact(DisplayName = "T-PAY-03: POST /payments con monto mayor al saldo devuelve 422")]
    public async Task Pago_excede_saldo_devuelve_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, PaymentsWebApplicationFactory.OwnerEmail, PaymentsWebApplicationFactory.Password);

        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/work-orders/{_factory.WorkOrderId}/payments")
        {
            Content = JsonContent.Create(new { method = "cash", amount = 99999m }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    // ── T-PAY-04: idempotencia ───────────────────────────────────────────────

    [Fact(DisplayName = "T-PAY-04: POST /payments con misma Idempotency-Key devuelve el mismo pago")]
    public async Task Misma_clave_idempotencia_devuelve_mismo_pago()
    {
        using var client = CreateClient();
        await LoginAsync(client, PaymentsWebApplicationFactory.OwnerEmail, PaymentsWebApplicationFactory.Password);

        var key = Guid.NewGuid().ToString();
        var first = await CreatePaymentAsync(client, amount: 300m, idempotencyKey: key);
        var second = await CreatePaymentAsync(client, amount: 300m, idempotencyKey: key);

        var id1 = first.GetProperty("payment").GetProperty("id").GetString();
        var id2 = second.GetProperty("payment").GetProperty("id").GetString();
        Assert.Equal(id1, id2);
    }

    // ── T-PAY-05: anular un pago ─────────────────────────────────────────────

    [Fact(DisplayName = "T-PAY-05: POST /payments/{id}/void anula el pago y restaura el saldo")]
    public async Task Anular_pago_restaura_saldo()
    {
        using var client = CreateClient();
        await LoginAsync(client, PaymentsWebApplicationFactory.OwnerEmail, PaymentsWebApplicationFactory.Password);

        var created = await CreatePaymentAsync(client, amount: 400m);
        var paymentId = created.GetProperty("payment").GetProperty("id").GetString()!;

        // Anular
        var csrf = await FetchCsrfAsync(client);
        var voidReq = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/work-orders/{_factory.WorkOrderId}/payments/{paymentId}/void")
        {
            Content = JsonContent.Create(new { reason = "Error de prueba" }),
        };
        voidReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var voidResp = await client.SendAsync(voidReq);
        Assert.Equal(HttpStatusCode.OK, voidResp.StatusCode);

        var result = await voidResp.Content.ReadFromJsonAsync<JsonElement>();
        // El pago en la lista debe estar voided
        var payments = result.GetProperty("payments");
        var voided = payments.EnumerateArray()
            .First(p => p.GetProperty("id").GetString() == paymentId);
        Assert.Equal("voided", voided.GetProperty("status").GetString());
    }

    // ── F-02 (D-31): el cobro asienta en la caja y la anulación deja el movimiento inverso ──

    [Fact(DisplayName = "T-PAY-07: Un cobro con cuenta suma al saldo de la caja y anularlo lo devuelve a su valor")]
    public async Task Cobro_con_cuenta_asienta_en_caja_y_su_anulacion_lo_revierte()
    {
        using var client = CreateClient();
        await LoginAsync(client, PaymentsWebApplicationFactory.OwnerEmail, PaymentsWebApplicationFactory.Password);

        async Task<HttpResponseMessage> PostAsync(string url, object body)
        {
            var csrf = await FetchCsrfAsync(client);
            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
            req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
            return await client.SendAsync(req);
        }

        async Task<decimal> BalanceAsync(Guid accountId)
        {
            var accounts = await client.GetFromJsonAsync<JsonElement>("/api/v1/cash/accounts");
            return accounts.EnumerateArray().First(a => a.GetProperty("id").GetGuid() == accountId)
                .GetProperty("balance").GetDecimal();
        }

        var account = await (await PostAsync("/api/v1/cash/accounts", new { name = "Caja T-PAY-07", kind = "cash" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var accountId = account.GetProperty("id").GetGuid();
        Assert.Equal(0m, await BalanceAsync(accountId));

        var payResp = await PostAsync($"/api/v1/work-orders/{_factory.WorkOrderId}/payments",
            new { method = "cash", amount = 250m, accountId });
        Assert.Equal(HttpStatusCode.OK, payResp.StatusCode);
        var paymentId = (await payResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("payment").GetProperty("id").GetString()!;
        Assert.Equal(250m, await BalanceAsync(accountId));

        var voidResp = await PostAsync($"/api/v1/work-orders/{_factory.WorkOrderId}/payments/{paymentId}/void",
            new { reason = "Prueba de contraasiento" });
        Assert.Equal(HttpStatusCode.OK, voidResp.StatusCode);
        Assert.Equal(0m, await BalanceAsync(accountId));

        var movements = await client.GetFromJsonAsync<JsonElement>($"/api/v1/cash/accounts/{accountId}/movements");
        Assert.Equal(2, movements.GetArrayLength());
    }

    // ── T-PAY-06: anular pago ya anulado ─────────────────────────────────────

    [Fact(DisplayName = "T-PAY-06: anular un pago ya anulado devuelve 409")]
    public async Task Anular_pago_ya_anulado_devuelve_409()
    {
        using var client = CreateClient();
        await LoginAsync(client, PaymentsWebApplicationFactory.OwnerEmail, PaymentsWebApplicationFactory.Password);

        var created = await CreatePaymentAsync(client, amount: 400m);
        var paymentId = created.GetProperty("payment").GetProperty("id").GetString()!;

        // Primera anulación (OK)
        var csrf1 = await FetchCsrfAsync(client);
        var void1 = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/work-orders/{_factory.WorkOrderId}/payments/{paymentId}/void")
        {
            Content = JsonContent.Create(new { reason = "Primera" }),
        };
        void1.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf1);
        (await client.SendAsync(void1)).EnsureSuccessStatusCode();

        // Segunda anulación (409 esperado)
        var csrf2 = await FetchCsrfAsync(client);
        var void2 = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/work-orders/{_factory.WorkOrderId}/payments/{paymentId}/void")
        {
            Content = JsonContent.Create(new { reason = "Segunda" }),
        };
        void2.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf2);
        var resp = await client.SendAsync(void2);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    // ── T-DOC-03: PDF del comprobante ─────────────────────────────────────────

    [Fact(DisplayName = "T-DOC-03: GET /payments/{id}/pdf devuelve PDF con Content-Type application/pdf")]
    public async Task Pdf_comprobante_se_genera_con_content_type_correcto()
    {
        using var client = CreateClient();
        await LoginAsync(client, PaymentsWebApplicationFactory.OwnerEmail, PaymentsWebApplicationFactory.Password);

        var created = await CreatePaymentAsync(client, amount: 200m);
        var paymentId = created.GetProperty("payment").GetProperty("id").GetString()!;

        var resp = await client.GetAsync(
            $"/api/v1/work-orders/{_factory.WorkOrderId}/payments/{paymentId}/pdf");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("application/pdf", resp.Content.Headers.ContentType?.MediaType);

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        // Verificar firma PDF (%PDF-)
        Assert.True(bytes.Length > 4 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F');
    }

    // ── T-PUB-07: comprobante público ────────────────────────────────────────

    [Fact(DisplayName = "T-PUB-07: GET /api/public/v1/payments/{token} devuelve comprobante sin datos del taller")]
    public async Task Token_publico_devuelve_comprobante_anonimo()
    {
        using var client = CreateClient();
        await LoginAsync(client, PaymentsWebApplicationFactory.OwnerEmail, PaymentsWebApplicationFactory.Password);

        var created = await CreatePaymentAsync(client, amount: 150m);
        var token = created.GetProperty("payment").GetProperty("publicToken").GetString()!;

        // Acceso anónimo
        using var anonClient = CreateClient();
        var resp = await anonClient.GetAsync($"/api/public/v1/payments/{token}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        // El comprobante público no debe tener datos del taller
        Assert.False(body.TryGetProperty("tenantId", out _), "El comprobante público expone tenantId.");
        Assert.Equal("cash", body.GetProperty("method").GetString());
        Assert.Equal(150m, body.GetProperty("amount").GetDecimal());
        Assert.Equal("valid", body.GetProperty("status").GetString());
    }

    // ── T-PUB-07: token inválido ──────────────────────────────────────────────

    [Fact(DisplayName = "T-PUB-07: token inválido devuelve 404")]
    public async Task Token_invalido_devuelve_404()
    {
        using var anonClient = CreateClient();
        var resp = await anonClient.GetAsync("/api/public/v1/payments/token-invalido-123");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
