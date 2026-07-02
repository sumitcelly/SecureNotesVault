using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SecureNotesVault.Core.Services;

public class AesGcmEncryptionService : IEncryptionService
{
    private readonly byte[] _masterKey;

    public AesGcmEncryptionService(IConfiguration configuration)
    {
        // Pulls a 256-bit base64-encoded key from the environment/settings
        var keyString = configuration["Security:MasterEncryptionKey"];
        
        if (string.IsNullOrEmpty(keyString))
        {
            // Fallback for development evaluation if no key is configured
            _masterKey = Encoding.UTF8.GetBytes("SuperSecret32ByteKeyForDevVault!"); 
        }
        else
        {
            _masterKey = Convert.FromBase64String(keyString);
        }

        if (_masterKey.Length != 32)
        {
            throw new CryptographicException("The Master Encryption Key must be exactly 32 bytes (256 bits).");
        }
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        
        // Generate a cryptographically secure 12-byte unique Nonce
        byte[] nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        byte[] cipherBytes = new byte[plainBytes.Length];
        byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using (var aesGcm = new AesGcm(_masterKey, tag.Length))
        {
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }

        // Combine Nonce + Tag + Ciphertext into one payload for single-column DB storage
        byte[] combinedPayload = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combinedPayload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combinedPayload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, combinedPayload, nonce.Length + tag.Length, cipherBytes.Length);

        return Convert.ToBase64String(combinedPayload);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        byte[] combinedPayload = Convert.FromBase64String(cipherText);

        int nonceSize = AesGcm.NonceByteSizes.MaxSize;
        int tagSize = AesGcm.TagByteSizes.MaxSize;
        int cipherSize = combinedPayload.Length - nonceSize - tagSize;

        if (cipherSize < 0)
        {
            throw new CryptographicException("Invalid ciphertext payload structure.");
        }

        byte[] nonce = new byte[nonceSize];
        byte[] tag = new byte[tagSize];
        byte[] cipherBytes = new byte[cipherSize];

        // Parse out components from the combined database record
        Buffer.BlockCopy(combinedPayload, 0, nonce, 0, nonceSize);
        Buffer.BlockCopy(combinedPayload, nonceSize, tag, 0, tagSize);
        Buffer.BlockCopy(combinedPayload, nonceSize + tagSize, cipherBytes, 0, cipherSize);

        byte[] plainBytes = new byte[cipherSize];

        using (var aesGcm = new AesGcm(_masterKey, tag.Length))
        {
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }
}
