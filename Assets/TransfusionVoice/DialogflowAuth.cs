using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class ServiceAccountKey
{
    public string project_id;
    public string private_key;
    public string client_email;
    public string token_uri;
}

[Serializable]
class TokenResponse
{
    public string access_token;
    public int expires_in;
}

/// <summary>
/// Obtiene y cachea un access token de Google a partir del JSON de una cuenta de servicio.
/// </summary>
public class DialogflowAuth
{
    const string Scope = "https://www.googleapis.com/auth/cloud-platform";

    readonly ServiceAccountKey key;
    readonly RSA rsa;
    string cachedToken;
    DateTime tokenExpiresUtc = DateTime.MinValue;

    public string ProjectId => key.project_id;
    public string Token => cachedToken;
    public bool HasValidToken => cachedToken != null && DateTime.UtcNow < tokenExpiresUtc;

    public DialogflowAuth(string serviceAccountJson)
    {
        key = JsonUtility.FromJson<ServiceAccountKey>(serviceAccountJson);
        if (key == null || string.IsNullOrEmpty(key.private_key) || string.IsNullOrEmpty(key.client_email))
            throw new ArgumentException("El JSON de la cuenta de servicio no es válido.");
        if (string.IsNullOrEmpty(key.token_uri))
            key.token_uri = "https://oauth2.googleapis.com/token";

        rsa = RSA.Create();
        rsa.ImportParameters(Pkcs8Parser.Parse(key.private_key));
    }

    public void Invalidate() => cachedToken = null;

    public IEnumerator EnsureToken(Action<string> onError)
    {
        if (HasValidToken) yield break;

        var form = new WWWForm();
        form.AddField("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer");
        form.AddField("assertion", BuildJwt());

        using (var req = UnityWebRequest.Post(key.token_uri, form))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Error obteniendo token ({req.responseCode}): {req.error}\n{req.downloadHandler.text}");
                yield break;
            }
            var tr = JsonUtility.FromJson<TokenResponse>(req.downloadHandler.text);
            cachedToken = tr.access_token;
            tokenExpiresUtc = DateTime.UtcNow.AddSeconds(tr.expires_in - 120);
        }
    }

    string BuildJwt()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 30; // margen por desfase de reloj
        string header = "{\"alg\":\"RS256\",\"typ\":\"JWT\"}";
        string claims = "{\"iss\":\"" + key.client_email + "\"," +
                        "\"scope\":\"" + Scope + "\"," +
                        "\"aud\":\"" + key.token_uri + "\"," +
                        "\"iat\":" + now + ",\"exp\":" + (now + 3600) + "}";
        string unsigned = B64Url(Encoding.UTF8.GetBytes(header)) + "." + B64Url(Encoding.UTF8.GetBytes(claims));
        byte[] sig = rsa.SignData(Encoding.UTF8.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return unsigned + "." + B64Url(sig);
    }

    static string B64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// Lee una clave privada PKCS#8 (formato de las cuentas de servicio de Google) sin librerías externas.
/// Funciona con Mono e IL2CPP (Quest).
/// </summary>
static class Pkcs8Parser
{
    public static RSAParameters Parse(string pem)
    {
        string b64 = pem.Replace("-----BEGIN PRIVATE KEY-----", "")
                        .Replace("-----END PRIVATE KEY-----", "")
                        .Replace("\\n", "").Replace("\n", "").Replace("\r", "").Trim();
        byte[] der = Convert.FromBase64String(b64);
        int pos = 0;

        Expect(der, ref pos, 0x30); ReadLength(der, ref pos);        // PrivateKeyInfo
        ReadInteger(der, ref pos);                                    // version
        Expect(der, ref pos, 0x30);                                   // AlgorithmIdentifier (se salta)
        int algLen = ReadLength(der, ref pos);
        pos += algLen;
        Expect(der, ref pos, 0x04); ReadLength(der, ref pos);        // OCTET STRING
        Expect(der, ref pos, 0x30); ReadLength(der, ref pos);        // RSAPrivateKey
        ReadInteger(der, ref pos);                                    // version

        byte[] n = ReadInteger(der, ref pos);
        byte[] e = ReadInteger(der, ref pos);
        byte[] d = ReadInteger(der, ref pos);
        byte[] p = ReadInteger(der, ref pos);
        byte[] q = ReadInteger(der, ref pos);
        byte[] dp = ReadInteger(der, ref pos);
        byte[] dq = ReadInteger(der, ref pos);
        byte[] iq = ReadInteger(der, ref pos);

        int half = (n.Length + 1) / 2;
        return new RSAParameters
        {
            Modulus = n, Exponent = e, D = Pad(d, n.Length),
            P = Pad(p, half), Q = Pad(q, half),
            DP = Pad(dp, half), DQ = Pad(dq, half), InverseQ = Pad(iq, half)
        };
    }

    static void Expect(byte[] d, ref int pos, byte tag)
    {
        if (d[pos++] != tag) throw new FormatException("Clave privada con formato inesperado.");
    }

    static int ReadLength(byte[] d, ref int pos)
    {
        int b = d[pos++];
        if ((b & 0x80) == 0) return b;
        int count = b & 0x7F, len = 0;
        for (int i = 0; i < count; i++) len = (len << 8) | d[pos++];
        return len;
    }

    static byte[] ReadInteger(byte[] d, ref int pos)
    {
        Expect(d, ref pos, 0x02);
        int len = ReadLength(d, ref pos);
        int start = pos;
        pos += len;
        while (len > 1 && d[start] == 0) { start++; len--; }
        var r = new byte[len];
        Buffer.BlockCopy(d, start, r, 0, len);
        return r;
    }

    static byte[] Pad(byte[] b, int len)
    {
        if (b.Length == len) return b;
        if (b.Length > len) throw new FormatException("Componente RSA más largo de lo esperado.");
        var r = new byte[len];
        Buffer.BlockCopy(b, 0, r, len - b.Length, b.Length);
        return r;
    }
}
