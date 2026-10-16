namespace TallerControl.Domain.Qr;

public enum QrScanEventType
{
    Scan,
    OwnerChallenge,
    OtpSent,
    OtpFailed,
    OwnerSession,
    FoundKeys
}
