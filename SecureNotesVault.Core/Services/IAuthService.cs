using SecureNotesVault.Core.Models;

namespace SecureNotesVault.Core.Services;

public interface IAuthService
{
    Task<User?> RegisterAsync(string username, string password);
    Task<string?> LoginAsync(string username, string password);
}
