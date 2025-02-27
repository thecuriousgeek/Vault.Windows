namespace TheCuriousGeek.Vault;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public class Hash
{
  public static byte[] Get(byte[] pWhat)
  {
    using (System.Security.Cryptography.SHA256 _Sha = System.Security.Cryptography.SHA256.Create())
    {
      byte[] _Result = _Sha.ComputeHash(pWhat);
      return _Result;
    }
  }
  public static string Get(string pWhat)
  {
      byte[] _Result = Get(Encoding.UTF8.GetBytes(pWhat));
      return Convert.ToHexString(_Result).ToLower();
  }
}
public class Crypt
{
  public abstract class AbstractCrypt
  {
    public abstract Task Encrypt(Stream pSrc, Stream pDst);
    public byte[] Encrypt(byte[] pWhat)
    {
      using (var _InStream = new MemoryStream())
      using (var _OutStream = new MemoryStream())
      {
        _InStream.Write(pWhat, 0, pWhat.Length);
        _InStream.Seek(0, SeekOrigin.Begin);
        Encrypt(_InStream, _OutStream);
        _OutStream.Seek(0, SeekOrigin.Begin);
        return _OutStream.ToArray();
      }
    }
    public string Encrypt(string pWhat)
    {
      var _Result = Encrypt(Encoding.UTF8.GetBytes(pWhat));
      return Convert.ToHexString(_Result).ToLower();
    }
    public abstract Task Decrypt(Stream pSrc, Stream pDst);
    public byte[] Decrypt(byte[] pWhat)
    {
      using (var _InStream = new MemoryStream())
      using (var _OutStream = new MemoryStream())
      {
        _InStream.Write(pWhat, 0, pWhat.Length);
        _InStream.Seek(0, SeekOrigin.Begin);
        Decrypt(_InStream, _OutStream);
        _OutStream.Seek(0, SeekOrigin.Begin);
        return _OutStream.ToArray();
      }

    }
    public string Decrypt(string pWhat)
    {
      return Encoding.UTF8.GetString(Decrypt(Convert.FromHexString(pWhat.ToLower())));
    }
  }
  public class AES : AbstractCrypt
  {
    ICryptoTransform Encryptor, Decryptor;
    public AES(byte[] pKey)
    {
      using (var _Crypto = Aes.Create())
      {
        _Crypto.Key = pKey.Take(32).ToArray();
        _Crypto.IV = pKey.Take(16).ToArray();
        _Crypto.Mode = CipherMode.CBC;
        _Crypto.Padding = PaddingMode.PKCS7;
        this.Encryptor = _Crypto.CreateEncryptor(_Crypto.Key, _Crypto.IV);
        this.Decryptor = _Crypto.CreateDecryptor(_Crypto.Key, _Crypto.IV);
      }
    }
    public AES(String pKey) : this(Hash.Get(Encoding.UTF8.GetBytes(pKey))) { }
    public override async Task Encrypt(Stream pSrc, Stream pDst)
    {
      using (var _CryptStream = new CryptoStream(pDst, this.Encryptor, CryptoStreamMode.Write, true))
        await pSrc.CopyToAsync(_CryptStream);
    }
    public override async Task Decrypt(Stream pSrc, Stream pDst)
    {
      using (var _CryptStream = new CryptoStream(pSrc, this.Decryptor, CryptoStreamMode.Read, true))
        await _CryptStream.CopyToAsync(pDst);
    }
  }
  public class DES : AbstractCrypt
  {
    ICryptoTransform Encryptor, Decryptor;
    public DES(byte[] pKey)
    {
      using (var _Crypto = System.Security.Cryptography.DES.Create())
      {
        this.Encryptor = _Crypto.CreateEncryptor(pKey.Take(8).ToArray(), pKey.Take(8).ToArray());
        this.Decryptor = _Crypto.CreateDecryptor(pKey.Take(8).ToArray(), pKey.Take(8).ToArray());
      }
    }
    public DES(String pKey) : this(Hash.Get(Encoding.UTF8.GetBytes(pKey))) { }
    public override async Task Encrypt(Stream pSrc, Stream pDst)
    {
      var _Src = pSrc as MemoryStream;
      var _Dst = pDst as MemoryStream;
      byte[] _Result = this.Encryptor.TransformFinalBlock(_Src.ToArray(), 0, (int)_Src.Length);
      await _Dst.WriteAsync(_Result, 0, _Result.Length);
    }
    public override async Task Decrypt(Stream pSrc, Stream pDst)
    {
      var _Src = pSrc as MemoryStream;
      var _Dst = pDst as MemoryStream;
      byte[] _Result = this.Decryptor.TransformFinalBlock(_Src.ToArray(), 0, (int)_Src.Length);
      await _Dst.WriteAsync(_Result, 0, _Result.Length);
    }
  }
  public static void Test()
  {
    var k = "Test";
    var h = Hash.Get(k);
    var b = Hash.Get(Encoding.UTF8.GetBytes(k));
    Console.WriteLine($"Hashed '{k}'/{k.Length} to {b.Length} coded '{h}'/{h.Length}");

    foreach (var c in new Crypt.AbstractCrypt[] { new Crypt.AES(k), new Crypt.DES(k) })
    {
      foreach (var s in new string[] { "1234","1234567", "12345678","1234567890123456","12345678901234567890123456789012","slightly larger.txt", "This is a very long line that needs multiple blocks" })
      {
        var e = c.Encrypt(s);
        var d = c.Decrypt(e);
        Console.WriteLine($"Encrypted {s}/{s.Length} to {e}/{e.Length}");
        Console.WriteLine($"Decrypted {e}/{e.Length} to {d}/{d.Length}");
      }
    }
  }
}