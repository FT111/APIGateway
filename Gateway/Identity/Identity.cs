using System.Security.Cryptography;
using GatewayPluginContract.Entities;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;

namespace Gateway.Identity;

/// <summary>
/// Uses RSA key pairs to sign requests with the supervisor
/// Generates keys if none found in .pem
/// </summary>
/// <param name="configuration"></param>
public class Identity
{
    private readonly RSA _rsa;
    public Guid Id;

    public Identity(IConfiguration configuration)
    {
        var privateKeyPath = configuration["Identity:PrivateKeyPath"] ?? "./Identity/private_key.pem";
        var publicKeyPath = configuration["Identity:PublicKeyPath"] ?? "./Identity/public_key.pem";
        var idPath = configuration["Identity:IdPath"] ?? "./Identity/id";
        if (!File.Exists(privateKeyPath) || !File.Exists(publicKeyPath) || !File.Exists(idPath))
        {
            using var rsa = RSA.Create(2048);
            var id = Guid.NewGuid();
            var privateKey = rsa.ExportRSAPrivateKeyPem();
            var publicKey = rsa.ExportRSAPublicKeyPem();
            File.WriteAllText(privateKeyPath, privateKey);
            File.WriteAllText(publicKeyPath, publicKey);
            File.WriteAllText(idPath, id.ToString());
            this.Id = id;
            _rsa = rsa;
        }
        else
        {
            var idText = File.ReadAllText(idPath);
            var privateKeyText = File.ReadAllText(privateKeyPath);
            var publicKeyText = File.ReadAllText(publicKeyPath);
            var pemReader = new PemReader(new StringReader(privateKeyText));
            var keyPair = pemReader.ReadObject() as Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair;
            if (keyPair == null)
            {
                throw new Exception("Failed to read private key from PEM file.");
            }
            _rsa = RSA.Create();
            Id = Guid.Parse(idText);
            
            var privateKeyParams = DotNetUtilities.ToRSAParameters((Org.BouncyCastle.Crypto.Parameters.RsaPrivateCrtKeyParameters)keyPair.Private);
            _rsa.ImportParameters(privateKeyParams);
        }
    }
    
    public string Sign(string data)
    {
        var dataBytes = System.Text.Encoding.UTF8.GetBytes(data);
        var signatureBytes = _rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signatureBytes);
    }
    
    public byte[] GetPublicKey()
    {
        return _rsa.ExportRSAPublicKey() ?? throw new Exception("Failed to export public key.");
    }
    
    public Instance ToInstance()
    {
        return new Instance
        {
            Id = Id,
            PublicKey = GetPublicKey(),
            Status = "active",
        };
    }
    
    public static implicit operator Instance(Identity identity)
    {
        return identity.ToInstance();
    }
}