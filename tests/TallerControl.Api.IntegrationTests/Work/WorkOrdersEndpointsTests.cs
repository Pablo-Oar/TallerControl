using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Work;

/// <summary>
/// E5 bloque 5: tests de integración de órdenes de trabajo.
/// T-WO-01..14 (doc 08 §4.7), T-PERM-05 (doc 08 §3), REQ-12, REQ-14.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class WorkOrdersEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private WorkOrdersWebApplicationFactory _factory = null!;

    public WorkOrdersEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await WorkOrdersWebApplicationFactory.CreateAsync(_postgres);
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

    /// <summary>
    /// Crea una orden vía POST /api/v1/work-orders (autenticado como Owner) y devuelve el DTO.
    /// </summary>
    private async Task<JsonElement> CreateOrderAsync(
        HttpClient client,
        Guid? assignedMechanicId = null,
        int odometerKm = 0,
        object[]? items = null)
    {
        var csrf = await FetchCsrfAsync(client);
        var body = new
        {
            customerId = _factory.CustomerId,
            vehicleId = _factory.VehicleId,
            odometerKm,
            fuelLevel = (int?)null,
            reportedIssue = (string?)null,
            priority = "normal",
            assignedMechanicId,
            items = items ?? Array.Empty<object>(),
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/work-orders")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Cambia el estado de una orden vía POST /api/v1/work-orders/{id}/status.
    /// </summary>
    private static async Task<HttpResponseMessage> ChangeStatusAsync(
        HttpClient client,
        Guid id,
        string toStatus,
        string? cancelReason = null)
    {
        var csrf = await FetchCsrfAsync(client);
        var body = new { toStatus, cancelReason };
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/work-orders/{id}/status")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    // ── T-WO-01: GET /work-orders respeta visibilidad del mecánico ────────────

    [Fact(DisplayName = "T-WO-01: Owner ve todas las órdenes en el listado paginado")]
    public async Task Owner_ve_todas_las_ordenes_paginadas()
    {
        using var client = CreateClient();
        await LoginAsync(client, WorkOrdersWebApplicationFactory.OwnerEmail,
            WorkOrdersWebApplicationFactory.Password);

        // Crear dos órdenes para asegurar que hay al menos esa cantidad.
        await CreateOrderAsync(client);
        await CreateOrderAsync(client);

        var resp = await client.GetAsync("/api/v1/work-orders");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var page = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(page.GetProperty("total").GetInt32() >= 2);
        Assert.True(page.GetProperty("items").GetArrayLength() >= 2);
    }

    [Fact(DisplayName = "T-WO-01: Mecánico solo ve órdenes sin asignar o asignadas a él mismo")]
    public async Task Mecanico_solo_ve_sus_propias_ordenes_y_las_sin_asignar()
    {
        using var ownerClient = CreateClient();
        using var mechanicClient = CreateClient();

        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        await LoginAsync(mechanicClient, WorkOrdersWebApplicationFactory.MechanicEmail, WorkOrdersWebApplicationFactory.Password);

        // Orden sin asignar → mecánico la puede ver.
        var unassigned = await CreateOrderAsync(ownerClient);
        var unassignedId = unassigned.GetProperty("id").GetGuid();

        // Orden asignada al mecánico → mecánico la puede ver.
        var assigned = await CreateOrderAsync(ownerClient, assignedMechanicId: _factory.MechanicMembershipId);
        var assignedId = assigned.GetProperty("id").GetGuid();

        // Orden asignada al admin (otro role) → mecánico NO la puede ver.
        var adminOrder = await CreateOrderAsync(ownerClient, assignedMechanicId: _factory.AdminMembershipId);
        var adminOrderId = adminOrder.GetProperty("id").GetGuid();

        var listResp = await mechanicClient.GetAsync("/api/v1/work-orders");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var page = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = page.GetProperty("items");
        var ids = new HashSet<Guid>();
        foreach (var item in items.EnumerateArray())
            ids.Add(item.GetProperty("id").GetGuid());

        Assert.Contains(unassignedId, ids);
        Assert.Contains(assignedId, ids);
        Assert.DoesNotContain(adminOrderId, ids);
    }

    // ── T-WO-02: POST crea orden y descuenta stock ────────────────────────────

    [Fact(DisplayName = "T-WO-02: POST /work-orders con producto descuenta stock del catálogo")]
    public async Task Post_orden_con_producto_descuenta_stock()
    {
        using var client = CreateClient();
        await LoginAsync(client, WorkOrdersWebApplicationFactory.OwnerEmail,
            WorkOrdersWebApplicationFactory.Password);

        // Verificar stock antes.
        var itemBefore = await client.GetAsync($"/api/v1/catalog/{_factory.CatalogItemId}");
        itemBefore.EnsureSuccessStatusCode();
        var beforeDto = await itemBefore.Content.ReadFromJsonAsync<JsonElement>();
        var stockBefore = beforeDto.GetProperty("stockQty").GetDecimal();

        // Crear orden con 1 unidad del producto.
        var csrf = await FetchCsrfAsync(client);
        var items = new[]
        {
            new
            {
                kind           = "product",
                description    = "Filtro W-Test T-WO-02",
                catalogItemId  = _factory.CatalogItemId,
                quantity       = 1m,
                unitNetPrice   = 500m,
                vatRate        = 21m,
            },
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/work-orders")
        {
            Content = JsonContent.Create(new
            {
                customerId = _factory.CustomerId,
                vehicleId = _factory.VehicleId,
                odometerKm = 0,
                fuelLevel = (int?)null,
                reportedIssue = (string?)null,
                priority = "normal",
                assignedMechanicId = (Guid?)null,
                items,
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var orderDto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, orderDto.GetProperty("id").GetGuid());

        // Verificar que el stock bajó en 1.
        var itemAfter = await client.GetAsync($"/api/v1/catalog/{_factory.CatalogItemId}");
        itemAfter.EnsureSuccessStatusCode();
        var afterDto = await itemAfter.Content.ReadFromJsonAsync<JsonElement>();
        var stockAfter = afterDto.GetProperty("stockQty").GetDecimal();

        Assert.Equal(stockBefore - 1m, stockAfter);
    }

    // ── T-WO-03: POST actualiza km del vehículo cuando el nuevo es mayor ───────

    [Fact(DisplayName = "T-WO-03: POST /work-orders con odometerKm mayor actualiza el km del vehículo")]
    public async Task Post_orden_actualiza_km_vehiculo_si_es_mayor()
    {
        using var client = CreateClient();
        await LoginAsync(client, WorkOrdersWebApplicationFactory.OwnerEmail,
            WorkOrdersWebApplicationFactory.Password);

        const int nuevoKm = 12345;
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/work-orders")
        {
            Content = JsonContent.Create(new
            {
                customerId = _factory.CustomerId,
                vehicleId = _factory.VehicleId,
                odometerKm = nuevoKm,
                fuelLevel = (int?)null,
                reportedIssue = (string?)null,
                priority = "normal",
                assignedMechanicId = (Guid?)null,
                items = Array.Empty<object>(),
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        // Verificar que el vehículo tiene el nuevo odómetro.
        var vehicleResp = await client.GetAsync($"/api/v1/vehicles/{_factory.VehicleId}");
        Assert.Equal(HttpStatusCode.OK, vehicleResp.StatusCode);

        var vehicleDto = await vehicleResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(nuevoKm, vehicleDto.GetProperty("odometerKm").GetInt32());
    }

    // ── T-WO-04: PUT edita orden; If-Match requerido ──────────────────────────

    [Fact(DisplayName = "T-WO-04: PUT /work-orders/{id} sin If-Match devuelve 422")]
    public async Task Put_sin_if_match_devuelve_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, WorkOrdersWebApplicationFactory.OwnerEmail,
            WorkOrdersWebApplicationFactory.Password);

        var order = await CreateOrderAsync(client);
        var id = order.GetProperty("id").GetGuid();

        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/work-orders/{id}")
        {
            Content = JsonContent.Create(new
            {
                odometerKm = 100,
                fuelLevel = (int?)null,
                reportedIssue = "Sin If-Match",
                internalNotes = (string?)null,
                priority = "normal",
                assignedMechanicId = (Guid?)null,
                items = Array.Empty<object>(),
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact(DisplayName = "T-WO-04: PUT /work-orders/{id} con If-Match válido actualiza la orden → 200")]
    public async Task Put_con_if_match_actualiza_orden()
    {
        using var client = CreateClient();
        await LoginAsync(client, WorkOrdersWebApplicationFactory.OwnerEmail,
            WorkOrdersWebApplicationFactory.Password);

        var order = await CreateOrderAsync(client);
        var id = order.GetProperty("id").GetGuid();
        var version = order.GetProperty("version").GetUInt32();

        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/work-orders/{id}")
        {
            Content = JsonContent.Create(new
            {
                odometerKm = 999,
                fuelLevel = (int?)50,
                reportedIssue = "Revisión completa T-WO-04",
                internalNotes = (string?)null,
                priority = "high",
                assignedMechanicId = (Guid?)null,
                items = Array.Empty<object>(),
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        req.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var updated = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Revisión completa T-WO-04", updated.GetProperty("reportedIssue").GetString());
        Assert.Equal(999, updated.GetProperty("odometerKm").GetInt32());
    }

    // ── T-WO-05: GET /work-orders/{id} respeta visibilidad del mecánico ────────

    [Fact(DisplayName = "T-WO-05: Mecánico NO puede ver una orden asignada a otro rol → 404")]
    public async Task Mecanico_no_puede_ver_orden_asignada_a_otro()
    {
        using var ownerClient = CreateClient();
        using var mechanicClient = CreateClient();

        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        await LoginAsync(mechanicClient, WorkOrdersWebApplicationFactory.MechanicEmail, WorkOrdersWebApplicationFactory.Password);

        // Orden asignada al Admin (no al mecánico que consulta).
        var order = await CreateOrderAsync(ownerClient, assignedMechanicId: _factory.AdminMembershipId);
        var id = order.GetProperty("id").GetGuid();

        var resp = await mechanicClient.GetAsync($"/api/v1/work-orders/{id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact(DisplayName = "T-WO-05: Owner puede ver cualquier orden → 200")]
    public async Task Owner_puede_ver_cualquier_orden()
    {
        using var client = CreateClient();
        await LoginAsync(client, WorkOrdersWebApplicationFactory.OwnerEmail,
            WorkOrdersWebApplicationFactory.Password);

        var order = await CreateOrderAsync(client);
        var id = order.GetProperty("id").GetGuid();

        var resp = await client.GetAsync($"/api/v1/work-orders/{id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── T-WO-06: Cambio de estado Pending → InProgress ───────────────────────

    [Fact(DisplayName = "T-WO-06: POST /status Pending → InProgress devuelve 200 con estado actualizado")]
    public async Task Status_pending_a_in_progress_devuelve_200()
    {
        using var client = CreateClient();
        await LoginAsync(client, WorkOrdersWebApplicationFactory.OwnerEmail,
            WorkOrdersWebApplicationFactory.Password);

        var order = await CreateOrderAsync(client);
        var id = order.GetProperty("id").GetGuid();

        var resp = await ChangeStatusAsync(client, id, "in_progress");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var updated = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("in_progress", updated.GetProperty("status").GetString(),
            StringComparer.OrdinalIgnoreCase);
    }

    // ── T-WO-07: Transición inválida Pending → Delivered ─────────────────────

    [Fact(DisplayName = "T-WO-07: POST /status con transición inválida Pending → Delivered devuelve 422")]
    public async Task Status_transicion_invalida_devuelve_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, WorkOrdersWebApplicationFactory.OwnerEmail,
            WorkOrdersWebApplicationFactory.Password);

        var order = await CreateOrderAsync(client);
        var id = order.GetProperty("id").GetGuid();

        var resp = await ChangeStatusAsync(client, id, "delivered");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    // ── T-WO-08: Cancelar sin motivo → 422 ───────────────────────────────────

    [Fact(DisplayName = "T-WO-08: POST /status Cancelar sin CancelReason devuelve 422")]
    public async Task Status_cancelar_sin_motivo_devuelve_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, WorkOrdersWebApplicationFactory.OwnerEmail,
            WorkOrdersWebApplicationFactory.Password);

        var order = await CreateOrderAsync(client);
        var id = order.GetProperty("id").GetGuid();

        // Sin cancelReason.
        var resp = await ChangeStatusAsync(client, id, "cancelled", cancelReason: null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    // ── T-WO-09: Cancelar con motivo → 200 ───────────────────────────────────

    [Fact(DisplayName = "T-WO-09: POST /status Cancelar con CancelReason devuelve 200 y status=cancelled")]
    public async Task Status_cancelar_con_motivo_devuelve_200_y_cancelled()
    {
        using var client = CreateClient();
        await LoginAsync(client, WorkOrdersWebApplicationFactory.OwnerEmail,
            WorkOrdersWebApplicationFactory.Password);

        var order = await CreateOrderAsync(client);
        var id = order.GetProperty("id").GetGuid();

        var resp = await ChangeStatusAsync(client, id, "cancelled", cancelReason: "Cliente no se presentó");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var updated = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("cancelled", updated.GetProperty("status").GetString(),
            StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Cliente no se presentó", updated.GetProperty("cancelReason").GetString());
    }

    // ── REQ-12 CA2 / T-WO-10 (plan): cancelar repone stock y la orden deja de ser deuda ─────────────

    private async Task<decimal> GetStockAsync(HttpClient client)
    {
        var resp = await client.GetAsync($"/api/v1/catalog/{_factory.CatalogItemId}");
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("stockQty").GetDecimal();
    }

    private async Task<decimal> GetCustomerBalanceAsync(HttpClient client)
    {
        var resp = await client.GetAsync($"/api/v1/customers/{_factory.CustomerId}");
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("balance").GetDecimal();
    }

    [Fact(DisplayName = "T-WO-10: Cancelar una orden con producto repone el stock y deja saldo 0")]
    public async Task Cancelar_orden_con_producto_repone_stock_y_deja_saldo_cero()
    {
        using var client = CreateClient();
        await LoginAsync(client, WorkOrdersWebApplicationFactory.OwnerEmail,
            WorkOrdersWebApplicationFactory.Password);

        var stockBefore = await GetStockAsync(client);

        var order = await CreateOrderAsync(client, items: new object[]
        {
            new
            {
                kind = "product",
                description = "Filtro W-Test T-WO-10",
                catalogItemId = _factory.CatalogItemId,
                quantity = 2m,
                unitNetPrice = 500m,
                vatRate = 21m,
            },
        });
        var id = order.GetProperty("id").GetGuid();

        Assert.Equal(stockBefore - 2m, await GetStockAsync(client));

        var resp = await ChangeStatusAsync(client, id, "cancelled", cancelReason: "Prueba T-WO-10");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // El stock vuelve al valor inicial y queda un movimiento WorkOrderCancel por la cantidad repuesta.
        Assert.Equal(stockBefore, await GetStockAsync(client));
        var movements = await client.GetAsync($"/api/v1/catalog/{_factory.CatalogItemId}/stock-movements");
        movements.EnsureSuccessStatusCode();
        var json = await movements.Content.ReadAsStringAsync();
        Assert.Contains("work_order_cancel", json, StringComparison.OrdinalIgnoreCase);

        // Saldo de la orden = 0 (no genera deuda).
        var detail = await client.GetAsync($"/api/v1/work-orders/{id}");
        detail.EnsureSuccessStatusCode();
        Assert.Equal(0m, (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("balance").GetDecimal());
    }

    [Fact(DisplayName = "V2: El saldo del cliente suma las órdenes abiertas y excluye las canceladas")]
    public async Task Saldo_del_cliente_excluye_ordenes_canceladas()
    {
        using var client = CreateClient();
        await LoginAsync(client, WorkOrdersWebApplicationFactory.OwnerEmail,
            WorkOrdersWebApplicationFactory.Password);

        var before = await GetCustomerBalanceAsync(client);

        // 2 × 500 neto + IVA 21 % = 1.210
        var order = await CreateOrderAsync(client, items: new object[]
        {
            new { kind = "service", description = "Servicio saldo cliente", quantity = 2m, unitNetPrice = 500m, vatRate = 21m },
        });
        var id = order.GetProperty("id").GetGuid();

        Assert.Equal(before + 1210m, await GetCustomerBalanceAsync(client));

        // El listado de clientes usa la misma definición.
        var list = await client.GetAsync("/api/v1/customers?pageSize=100");
        list.EnsureSuccessStatusCode();
        var items = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        var row = items.EnumerateArray().First(i => i.GetProperty("id").GetGuid() == _factory.CustomerId);
        Assert.Equal(before + 1210m, row.GetProperty("balance").GetDecimal());

        var resp = await ChangeStatusAsync(client, id, "cancelled", cancelReason: "Prueba saldo");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(before, await GetCustomerBalanceAsync(client));
    }

    // ── T-WO-10: Mecánico intenta Deliver → 403 ──────────────────────────────

    /// <summary>
    /// El mecánico tiene workorders.status pero no workorders.deliver.
    /// La transición Completed → Delivered debe ser rechazada con 403
    /// (la lógica de dominio lanza UnauthorizedAccessException porque canDeliver = false).
    /// NOTA: el ApiExceptionHandler actual mapea UnauthorizedAccessException → 500;
    /// este test documentará esa brecha si falla con 500 en lugar de 403.
    /// </summary>
    [Fact(DisplayName = "T-WO-10: Mecánico intenta Deliver sin permiso workorders.deliver → 403")]
    public async Task Mecanico_no_puede_deliver_sin_permiso()
    {
        using var ownerClient = CreateClient();
        using var mechanicClient = CreateClient();

        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        await LoginAsync(mechanicClient, WorkOrdersWebApplicationFactory.MechanicEmail, WorkOrdersWebApplicationFactory.Password);

        // Llevar la orden a Completed (el mecánico puede hacer InProgress y Completed).
        var order = await CreateOrderAsync(ownerClient);
        var id = order.GetProperty("id").GetGuid();

        // Pending → InProgress (mecánico puede).
        var r1 = await ChangeStatusAsync(mechanicClient, id, "in_progress");
        r1.EnsureSuccessStatusCode();

        // InProgress → Completed (mecánico puede).
        var r2 = await ChangeStatusAsync(mechanicClient, id, "completed");
        r2.EnsureSuccessStatusCode();

        // Completed → Delivered (mecánico NO puede: no tiene workorders.deliver).
        var resp = await ChangeStatusAsync(mechanicClient, id, "delivered");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── T-WO-12: Mecánico se autoasigna ──────────────────────────────────────

    /// <summary>
    /// NOTA: el endpoint POST /work-orders/{id}/assign requiere workorders.write
    /// (que el mecánico no tiene, solo tiene workorders.assign_self).
    /// Este test documentará la brecha si falla con 403 en vez de 204.
    /// </summary>
    [Fact(DisplayName = "T-WO-12: Mecánico se autoasigna a una orden sin asignar → 204")]
    public async Task Mecanico_se_autoasigna_a_orden_sin_asignar()
    {
        using var ownerClient = CreateClient();
        using var mechanicClient = CreateClient();

        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        await LoginAsync(mechanicClient, WorkOrdersWebApplicationFactory.MechanicEmail, WorkOrdersWebApplicationFactory.Password);

        // Orden sin mecánico asignado.
        var order = await CreateOrderAsync(ownerClient);
        var id = order.GetProperty("id").GetGuid();

        // Mecánico se autoasigna: envía su propio MembershipId.
        var csrf = await FetchCsrfAsync(mechanicClient);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/work-orders/{id}/assign")
        {
            Content = JsonContent.Create(new { mechanicId = _factory.MechanicMembershipId }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);

        var resp = await mechanicClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    private static async Task<HttpResponseMessage> AssignAsync(HttpClient client, Guid orderId, object body)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/work-orders/{orderId}/assign")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    [Fact(DisplayName = "T-WO-12: El mecánico se autoasigna sin enviar su id (assignee = self)")]
    public async Task Mecanico_se_autoasigna_sin_enviar_su_id()
    {
        using var ownerClient = CreateClient();
        using var mechanicClient = CreateClient();
        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        await LoginAsync(mechanicClient, WorkOrdersWebApplicationFactory.MechanicEmail, WorkOrdersWebApplicationFactory.Password);

        var id = (await CreateOrderAsync(ownerClient)).GetProperty("id").GetGuid();

        var resp = await AssignAsync(mechanicClient, id, new { });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var detail = await ownerClient.GetAsync($"/api/v1/work-orders/{id}");
        detail.EnsureSuccessStatusCode();
        Assert.Equal(_factory.MechanicMembershipId,
            (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assignedMechanicId").GetGuid());

        // Una vez asignada, otro intento del mecánico se rechaza (ya está asignada).
        var again = await AssignAsync(mechanicClient, id, new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
    }

    [Fact(DisplayName = "T-WO-12: El dueño asigna un mecánico activo del taller; un id ajeno o de otro rol → 422")]
    public async Task Owner_asigna_mecanico_y_valida_el_destinatario()
    {
        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        var id = (await CreateOrderAsync(ownerClient)).GetProperty("id").GetGuid();

        // Sin mecánico → 422
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await AssignAsync(ownerClient, id, new { })).StatusCode);
        // Un id inexistente (o de otro taller, que el filtro global oculta) → 422
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await AssignAsync(ownerClient, id, new { mechanicId = Guid.NewGuid() })).StatusCode);
        // Un miembro que no es mecánico (el propio dueño) → 422
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await AssignAsync(ownerClient, id, new { mechanicId = _factory.OwnerMembershipId })).StatusCode);

        // Un mecánico activo → 204 y queda asignado
        Assert.Equal(HttpStatusCode.NoContent,
            (await AssignAsync(ownerClient, id, new { mechanicId = _factory.MechanicMembershipId })).StatusCode);
        var detail = await ownerClient.GetAsync($"/api/v1/work-orders/{id}");
        Assert.Equal(_factory.MechanicMembershipId,
            (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assignedMechanicId").GetGuid());
    }

    [Fact(DisplayName = "T-WO-12: GET /work-orders/mechanics lista los mecánicos activos (dueño sí, mecánico no)")]
    public async Task Listado_de_mecanicos_asignables()
    {
        using var ownerClient = CreateClient();
        using var mechanicClient = CreateClient();
        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        await LoginAsync(mechanicClient, WorkOrdersWebApplicationFactory.MechanicEmail, WorkOrdersWebApplicationFactory.Password);

        var ok = await ownerClient.GetAsync("/api/v1/work-orders/mechanics");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var list = await ok.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(list.EnumerateArray(), m => m.GetProperty("membershipId").GetGuid() == _factory.MechanicMembershipId);
        Assert.DoesNotContain(list.EnumerateArray(), m => m.GetProperty("membershipId").GetGuid() == _factory.OwnerMembershipId);

        Assert.Equal(HttpStatusCode.Forbidden, (await mechanicClient.GetAsync("/api/v1/work-orders/mechanics")).StatusCode);
    }

    [Fact(DisplayName = "T-WO-01: Las pestañas del mecánico filtran por asignación (assignment=mine / unassigned)")]
    public async Task Listado_del_mecanico_filtra_por_asignacion()
    {
        using var ownerClient = CreateClient();
        using var mechanicClient = CreateClient();
        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        await LoginAsync(mechanicClient, WorkOrdersWebApplicationFactory.MechanicEmail, WorkOrdersWebApplicationFactory.Password);

        var mine = (await CreateOrderAsync(ownerClient, assignedMechanicId: _factory.MechanicMembershipId)).GetProperty("id").GetGuid();
        var free = (await CreateOrderAsync(ownerClient)).GetProperty("id").GetGuid();
        var other = (await CreateOrderAsync(ownerClient, assignedMechanicId: _factory.AdminMembershipId)).GetProperty("id").GetGuid();

        async Task<List<Guid>> IdsAsync(string assignment)
        {
            var resp = await mechanicClient.GetAsync($"/api/v1/work-orders?assignment={assignment}&pageSize=100");
            resp.EnsureSuccessStatusCode();
            var items = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
            return items.EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        }

        var mineIds = await IdsAsync("mine");
        Assert.Contains(mine, mineIds);
        Assert.DoesNotContain(free, mineIds);
        Assert.DoesNotContain(other, mineIds);

        var freeIds = await IdsAsync("unassigned");
        Assert.Contains(free, freeIds);
        Assert.DoesNotContain(mine, freeIds);
        Assert.DoesNotContain(other, freeIds);

        // Una orden anulada no se ofrece al mecánico en "Sin asignar".
        var cancelled = (await CreateOrderAsync(ownerClient)).GetProperty("id").GetGuid();
        var cancelResp = await ChangeStatusAsync(ownerClient, cancelled, "cancelled", cancelReason: "Prueba");
        Assert.Equal(HttpStatusCode.OK, cancelResp.StatusCode);
        Assert.DoesNotContain(cancelled, await IdsAsync("unassigned"));
    }

    // ── REQ-07 / T-VEH-08: historial del vehículo ────────────────────────────

    [Theory(DisplayName = "T-WO-15: GET /work-orders ordena por total en el servidor (sort/dir)")]
    [InlineData("asc")]
    [InlineData("desc")]
    public async Task Listado_ordena_por_total_en_el_servidor(string dir)
    {
        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);

        object[] Item(decimal price) => new object[] { new { kind = "service", description = "Orden", quantity = 1m, unitNetPrice = price, vatRate = 21m } };
        var cheap = (await CreateOrderAsync(ownerClient, items: Item(100m))).GetProperty("id").GetGuid();
        var dear = (await CreateOrderAsync(ownerClient, items: Item(9000m))).GetProperty("id").GetGuid();

        var resp = await ownerClient.GetAsync($"/api/v1/work-orders?sort=total&dir={dir}&pageSize=100");
        resp.EnsureSuccessStatusCode();
        var ids = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items")
            .EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();

        var (first, second) = dir == "asc" ? (cheap, dear) : (dear, cheap);
        Assert.True(ids.IndexOf(first) < ids.IndexOf(second));
    }

    [Fact(DisplayName = "T-WO-15b: una clave de orden desconocida no rompe el listado (cae al orden por defecto)")]
    public async Task Listado_ignora_claves_de_orden_desconocidas()
    {
        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        await CreateOrderAsync(ownerClient);

        var resp = await ownerClient.GetAsync("/api/v1/work-orders?sort=1;drop table work_orders&dir=desc");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact(DisplayName = "T-VEH-08: El historial del vehículo mezcla órdenes y presupuestos por fecha desc, con km y total")]
    public async Task Historial_del_vehiculo_lista_ordenes_y_presupuestos_por_fecha()
    {
        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);

        var first = (await CreateOrderAsync(ownerClient, odometerKm: 10_000, items: new object[]
        {
            new { kind = "service", description = "Historial 1", quantity = 1m, unitNetPrice = 100m, vatRate = 21m },
        })).GetProperty("id").GetGuid();
        var second = (await CreateOrderAsync(ownerClient, odometerKm: 12_000)).GetProperty("id").GetGuid();

        var resp = await ownerClient.GetAsync($"/api/v1/vehicles/{_factory.VehicleId}/history?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var page = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.True(page.GetProperty("total").GetInt32() >= 2);

        // Orden descendente por fecha; las dos órdenes recién creadas aparecen con tipo, km y total.
        var dates = items.Select(i => i.GetProperty("date").GetDateTimeOffset()).ToList();
        Assert.Equal(dates.OrderByDescending(d => d).ToList(), dates);
        var e1 = items.First(i => i.GetProperty("id").GetGuid() == first);
        Assert.Equal("work_order", e1.GetProperty("type").GetString());
        Assert.Equal(10_000, e1.GetProperty("odometerKm").GetInt32());
        Assert.Equal(121m, e1.GetProperty("total").GetDecimal());
        Assert.StartsWith("OT-", e1.GetProperty("number").GetString());
        Assert.Contains(items, i => i.GetProperty("id").GetGuid() == second);

        // Paginación: pageSize=1 devuelve una sola fila pero el total completo.
        var one = await (await ownerClient.GetAsync($"/api/v1/vehicles/{_factory.VehicleId}/history?pageSize=1"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, one.GetProperty("items").GetArrayLength());
        Assert.Equal(page.GetProperty("total").GetInt32(), one.GetProperty("total").GetInt32());
    }

    [Fact(DisplayName = "T-VEH-08: Al mecánico (sin prices.view) el total del historial no viaja; otro taller → 404")]
    public async Task Historial_del_vehiculo_oculta_precios_al_mecanico()
    {
        using var ownerClient = CreateClient();
        using var mechanicClient = CreateClient();
        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        await LoginAsync(mechanicClient, WorkOrdersWebApplicationFactory.MechanicEmail, WorkOrdersWebApplicationFactory.Password);
        await CreateOrderAsync(ownerClient, items: new object[]
        {
            new { kind = "service", description = "Historial precios", quantity = 1m, unitNetPrice = 100m, vatRate = 21m },
        });

        var resp = await mechanicClient.GetAsync($"/api/v1/vehicles/{_factory.VehicleId}/history?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var items = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, i => AssertNullOrMissing(i, "total"));

        Assert.Equal(HttpStatusCode.NotFound,
            (await ownerClient.GetAsync($"/api/v1/vehicles/{Guid.NewGuid()}/history")).StatusCode);
    }

    // ── T-WO-13: Mecánico asignado actualiza sus notas ───────────────────────

    [Fact(DisplayName = "T-WO-13: Mecánico asignado puede actualizar sus notas de trabajo → 204")]
    public async Task Mecanico_asignado_actualiza_notas_mecanico()
    {
        using var ownerClient = CreateClient();
        using var mechanicClient = CreateClient();

        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        await LoginAsync(mechanicClient, WorkOrdersWebApplicationFactory.MechanicEmail, WorkOrdersWebApplicationFactory.Password);

        // Crear orden ya asignada al mecánico.
        var order = await CreateOrderAsync(ownerClient, assignedMechanicId: _factory.MechanicMembershipId);
        var id = order.GetProperty("id").GetGuid();

        var csrf = await FetchCsrfAsync(mechanicClient);
        var req = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/work-orders/{id}/mechanic-notes")
        {
            Content = JsonContent.Create(new { mechanicNotes = "Correa de distribución OK T-WO-13" }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);

        var resp = await mechanicClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // Verificar que las notas se guardaron (Owner las puede ver).
        var getResp = await ownerClient.GetAsync($"/api/v1/work-orders/{id}");
        getResp.EnsureSuccessStatusCode();
        var dto = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Correa de distribución OK T-WO-13", dto.GetProperty("mechanicNotes").GetString());
    }

    // ── T-WO-14: GET /work-orders/{id}/history ────────────────────────────────

    [Fact(DisplayName = "T-WO-14: GET /work-orders/{id}/history devuelve el historial de cambios de estado")]
    public async Task Get_history_devuelve_historial_de_estados()
    {
        using var client = CreateClient();
        await LoginAsync(client, WorkOrdersWebApplicationFactory.OwnerEmail,
            WorkOrdersWebApplicationFactory.Password);

        var order = await CreateOrderAsync(client);
        var id = order.GetProperty("id").GetGuid();

        // Cambiar estado para generar un segundo registro en el historial.
        var statusResp = await ChangeStatusAsync(client, id, "in_progress");
        statusResp.EnsureSuccessStatusCode();

        var histResp = await client.GetAsync($"/api/v1/work-orders/{id}/history");
        Assert.Equal(HttpStatusCode.OK, histResp.StatusCode);

        var history = await histResp.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(history);
        // Debe haber al menos 2 entradas: creación (→Pending) + cambio a InProgress.
        Assert.True(history.Length >= 2);
    }

    // ── T-PERM-05: Mecánico no ve campos de precio ────────────────────────────

    [Fact(DisplayName = "T-PERM-05: Mecánico no recibe campos de precio en el detalle de la orden")]
    public async Task Mecanico_no_ve_campos_de_precio_en_detalle()
    {
        using var ownerClient = CreateClient();
        using var mechanicClient = CreateClient();

        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        await LoginAsync(mechanicClient, WorkOrdersWebApplicationFactory.MechanicEmail, WorkOrdersWebApplicationFactory.Password);

        // Crear orden asignada al mecánico para que pueda verla.
        var order = await CreateOrderAsync(ownerClient, assignedMechanicId: _factory.MechanicMembershipId);
        var id = order.GetProperty("id").GetGuid();

        var resp = await mechanicClient.GetAsync($"/api/v1/work-orders/{id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // Los campos de precio deben ser null cuando el mecánico no tiene prices.view.
        AssertNullOrMissing(dto, "subtotalNet");
        AssertNullOrMissing(dto, "vatAmount");
        AssertNullOrMissing(dto, "discountAmount");
        AssertNullOrMissing(dto, "total");
        AssertNullOrMissing(dto, "paidAmount");
        AssertNullOrMissing(dto, "balance");
    }

    [Fact(DisplayName = "T-PERM-09: mecánico sin prices.view no ve montos en listado ni detalle de OT (doc 08 §3, V2-delta)")]
    public async Task Mecanico_sin_prices_view_no_ve_montos_en_listado_ni_detalle()
    {
        using var ownerClient = CreateClient();
        using var mechanicClient = CreateClient();

        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        await LoginAsync(mechanicClient, WorkOrdersWebApplicationFactory.MechanicEmail, WorkOrdersWebApplicationFactory.Password);

        // Crear orden con items para que haya totales.
        var csrf = await FetchCsrfAsync(ownerClient);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/work-orders")
        {
            Content = JsonContent.Create(new
            {
                customerId = _factory.CustomerId,
                vehicleId = _factory.VehicleId,
                odometerKm = 0,
                fuelLevel = (int?)null,
                reportedIssue = (string?)null,
                priority = "normal",
                assignedMechanicId = _factory.MechanicMembershipId,
                items = new[]
                {
                    new { kind = "product", description = "Repuesto T-PERM-09",
                          catalogItemId = _factory.CatalogItemId, quantity = 1m,
                          unitNetPrice = 1000m, vatRate = 21m },
                },
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var createResp = await ownerClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var orderId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Listado: total y balance deben ser null para el mecánico.
        var listResp = await mechanicClient.GetAsync("/api/v1/work-orders");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var page = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var listItem = page.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("id").GetGuid() == orderId);
        AssertNullOrMissing(listItem, "total");
        AssertNullOrMissing(listItem, "balance");

        // Detalle: todos los campos de precio deben ser null.
        var detailResp = await mechanicClient.GetAsync($"/api/v1/work-orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, detailResp.StatusCode);
        var dto = await detailResp.Content.ReadFromJsonAsync<JsonElement>();
        AssertNullOrMissing(dto, "subtotalNet");
        AssertNullOrMissing(dto, "vatAmount");
        AssertNullOrMissing(dto, "discountAmount");
        AssertNullOrMissing(dto, "total");
        AssertNullOrMissing(dto, "paidAmount");
        AssertNullOrMissing(dto, "balance");
    }

    private static void AssertNullOrMissing(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            Assert.Equal(JsonValueKind.Null, prop.ValueKind);
        }
        // Si la propiedad no aparece en el JSON, también se considera correcta (campo omitido).
    }

    // ── T-DOC-02: el PDF de la orden respeta visibilidad y precios (REQ-14) ────

    [Fact(DisplayName = "T-DOC-02: Mecánico no obtiene el PDF de una orden asignada a otro → 404")]
    public async Task Pdf_de_orden_ajena_para_mecanico_devuelve_404()
    {
        using var ownerClient = CreateClient();
        using var mechanicClient = CreateClient();
        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        await LoginAsync(mechanicClient, WorkOrdersWebApplicationFactory.MechanicEmail, WorkOrdersWebApplicationFactory.Password);

        var order = await CreateOrderAsync(ownerClient, assignedMechanicId: _factory.AdminMembershipId);
        var resp = await mechanicClient.GetAsync($"/api/v1/work-orders/{order.GetProperty("id").GetGuid()}/pdf");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact(DisplayName = "T-DOC-02: El PDF del mecánico sale sin importes (más corto que el del dueño)")]
    public async Task Pdf_para_mecanico_no_incluye_importes()
    {
        using var ownerClient = CreateClient();
        using var mechanicClient = CreateClient();
        await LoginAsync(ownerClient, WorkOrdersWebApplicationFactory.OwnerEmail, WorkOrdersWebApplicationFactory.Password);
        await LoginAsync(mechanicClient, WorkOrdersWebApplicationFactory.MechanicEmail, WorkOrdersWebApplicationFactory.Password);

        object[] items = { new { kind = "service", description = "Alineación y balanceo", quantity = 2m, unitNetPrice = 123456m, vatRate = 21m } };
        var order = await CreateOrderAsync(ownerClient, items: items, assignedMechanicId: _factory.MechanicMembershipId);
        var url = $"/api/v1/work-orders/{order.GetProperty("id").GetGuid()}/pdf";

        var ownerPdf = await (await ownerClient.GetAsync(url)).Content.ReadAsByteArrayAsync();
        var mechanicResp = await mechanicClient.GetAsync(url);
        var mechanicPdf = await mechanicResp.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, mechanicResp.StatusCode);
        Assert.True(mechanicPdf.Length < ownerPdf.Length,
            "El PDF del mecánico debería omitir subtotal, IVA y total.");
    }
}
