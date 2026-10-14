using TallerControl.Domain.Qr;

namespace TallerControl.Application.Qr;

public record QrCardListDto(
    Guid Id,
    string Code,
    QrCardStatus Status,
    Guid? VehicleId,
    string? VehiclePlate,
    DateTimeOffset? AssignedAt,
    DateTimeOffset? RevokedAt);

public record QrCardDto(
    Guid Id,
    string Code,
    QrCardStatus Status,
    Guid? VehicleId,
    string? VehiclePlate,
    string? VehicleBrand,
    string? VehicleModel,
    DateTimeOffset? AssignedAt,
    DateTimeOffset? RevokedAt,
    string? RevokeReason);

public record QrEventDto(
    Guid Id,
    QrScanEventType EventType,
    string? Outcome,
    string? IpTruncated,
    DateTimeOffset CreatedAt);

public record GenerateBatchRequest(int Quantity);
public record GenerateBatchResult(Guid BatchId, int Count);

public record AssignQrCardRequest(Guid VehicleId);
public record AssignByCodeRequest(string Code, Guid VehicleId);
public record UnassignQrCardRequest(string Reason);
public record MarkLostRequest(bool Lost);
public record RevokeQrCardRequest(string Reason);

// Público
public record ScanResultDto(WorkshopInfoDto Workshop, string CardState);
public record WorkshopInfoDto(string Name, string? LogoUrl);

public record OwnerChallengeRequest(string Plate, string? CustomerCode, string TurnstileToken);
public record OwnerChallengeResult(Guid ChallengeId, string Channel, string? MaskedDestination);

public record ValidateOwnerOtpRequest(Guid ChallengeId, string Otp);

public record OwnerVehicleDto(
    string Plate,
    string? Brand,
    string? Model,
    int OdometerKm,
    IReadOnlyList<OwnerWorkOrderDto> WorkOrders,
    IReadOnlyList<OwnerQuoteDto> ActiveQuotes,
    decimal PendingBalance);

public record OwnerWorkOrderDto(
    int Number,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    decimal? Total);

public record OwnerQuoteDto(
    int Number,
    string Status,
    string? PublicLink,
    DateTimeOffset? ValidUntil);

public record OwnerAccessCodeDto(string Code);
