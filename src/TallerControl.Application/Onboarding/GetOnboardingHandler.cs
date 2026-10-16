namespace TallerControl.Application.Onboarding;

public sealed class GetOnboardingHandler
{
    private readonly IOnboardingRepository _repository;

    public GetOnboardingHandler(IOnboardingRepository repository)
    {
        _repository = repository;
    }

    public async Task<OnboardingDto> HandleAsync(CancellationToken ct)
    {
        var tasks = await Task.WhenAll(
            _repository.HasLogoAsync(ct),
            _repository.HasCustomersAsync(ct),
            _repository.HasVehiclesAsync(ct),
            _repository.HasCatalogItemsAsync(ct),
            _repository.HasWorkOrdersAsync(ct),
            _repository.HasPaymentsAsync(ct));

        var steps = new List<OnboardingStepDto>
        {
            new("logo",       "Subir el logo del taller",      tasks[0]),
            new("customers",  "Agregar el primer cliente",     tasks[1]),
            new("vehicles",   "Registrar el primer vehículo",  tasks[2]),
            new("catalog",    "Cargar el catálogo de servicios/productos", tasks[3]),
            new("workorders", "Crear la primera orden de trabajo", tasks[4]),
            new("payments",   "Registrar el primer cobro",     tasks[5]),
        };

        var completed = steps.Count(s => s.Completed);
        return new OnboardingDto(steps, completed, steps.Count, completed == steps.Count);
    }
}
