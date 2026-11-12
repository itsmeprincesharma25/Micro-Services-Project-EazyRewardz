using System.Security.Cryptography;
using System.Text;

namespace Fundoo.User.Api.Services;

public sealed class InternalServiceKeyValidator : IInternalServiceKeyValidator
{
    private readonly IConfiguration _configuration;

    public InternalServiceKeyValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsValid(string? suppliedKey)
    {
        var configuredKey = _configuration["InternalApi:NotesServiceKey"];
        if (string.IsNullOrWhiteSpace(configuredKey) || configuredKey.Length < 32)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(suppliedKey))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(configuredKey);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);
        if (expectedBytes.Length != suppliedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
