namespace TallerControl.Application.Invoicing;

public interface IIvaBookRepository
{
    Task<IvaBookDto> GetIvaBookAsync(int year, int month, CancellationToken ct);
    Task<(byte[] ZipBytes, string FileName)> GetCitiZipAsync(int year, int month, CancellationToken ct);
}
