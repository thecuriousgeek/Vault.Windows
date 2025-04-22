namespace TheCuriousGeek.Vault;
using System.Runtime.InteropServices;

public class Config
{
  public static List<Config> Instances = new List<Config>();
  private static String Sign(String pPassword) { return (string.IsNullOrEmpty(pPassword)) ? "vault" : new Crypt.AES(pPassword).Encrypt("vault"); }

  public static void Load()
  {
    foreach (var _Folder in Directory.GetDirectories(Environment.CurrentDirectory))
      if (File.Exists(_Folder + "/.vault"))
      {
        var _Vault = new Config(Path.GetFileName(_Folder));
        Config.Instances.Add(_Vault);
      }
  }
  public static bool Create(String pName, string pPassword)
  {
    if (Directory.Exists(pName)) return false;
    Directory.CreateDirectory(pName);
    var _Signature = Config.Sign(pPassword);
    File.WriteAllText(pName + "/.vault", _Signature);
    var _Vault = new Config(pName);
    Config.Instances.Add(_Vault);
    return true;
  }
  public static Config Get(String pName)
  {
    var _Vault = Config.Instances.FirstOrDefault(v => v.Name == pName);
    return _Vault;
  }
  public Vault Open(String pPassword)
  {
    if (this.Signature.ToLower() != Sign(pPassword).ToLower()) return null;
    var _Vault = new Vault(this.Name, pPassword);
    return _Vault;
  }

  public string Name;
  public string Signature;
  public Config(string pName)
  {
    this.Name = pName;
    this.Signature = File.ReadAllText(pName + "/.vault");
  }
}

public class Vault
{
  #region Win32 Mounting
  public enum ResourceScope
  {
    RESOURCE_CONNECTED = 1,
    RESOURCE_GLOBALNET,
    RESOURCE_REMEMBERED,
    RESOURCE_RECENT,
    RESOURCE_CONTEXT
  }
  public enum ResourceType
  {
    RESOURCETYPE_ANY,
    RESOURCETYPE_DISK,
    RESOURCETYPE_PRINT,
    RESOURCETYPE_RESERVED
  }
  public enum ResourceUsage
  {
    RESOURCEUSAGE_CONNECTABLE = 0x00000001,
    RESOURCEUSAGE_CONTAINER = 0x00000002,
    RESOURCEUSAGE_NOLOCALDEVICE = 0x00000004,
    RESOURCEUSAGE_SIBLING = 0x00000008,
    RESOURCEUSAGE_ATTACHED = 0x00000010,
    RESOURCEUSAGE_ALL = (RESOURCEUSAGE_CONNECTABLE | RESOURCEUSAGE_CONTAINER | RESOURCEUSAGE_ATTACHED),
  }
  public enum ResourceDisplayType
  {
    RESOURCEDISPLAYTYPE_GENERIC,
    RESOURCEDISPLAYTYPE_DOMAIN,
    RESOURCEDISPLAYTYPE_SERVER,
    RESOURCEDISPLAYTYPE_SHARE,
    RESOURCEDISPLAYTYPE_FILE,
    RESOURCEDISPLAYTYPE_GROUP,
    RESOURCEDISPLAYTYPE_NETWORK,
    RESOURCEDISPLAYTYPE_ROOT,
    RESOURCEDISPLAYTYPE_SHAREADMIN,
    RESOURCEDISPLAYTYPE_DIRECTORY,
    RESOURCEDISPLAYTYPE_TREE,
    RESOURCEDISPLAYTYPE_NDSCONTAINER
  }
  [System.Flags]
  public enum AddConnectionOptions
  {
    CONNECT_UPDATE_PROFILE = 0x00000001,
    CONNECT_UPDATE_RECENT = 0x00000002,
    CONNECT_TEMPORARY = 0x00000004,
    CONNECT_INTERACTIVE = 0x00000008,
    CONNECT_PROMPT = 0x00000010,
    CONNECT_NEED_DRIVE = 0x00000020,
    CONNECT_REFCOUNT = 0x00000040,
    CONNECT_REDIRECT = 0x00000080,
    CONNECT_LOCALDRIVE = 0x00000100,
    CONNECT_CURRENT_MEDIA = 0x00000200,
    CONNECT_DEFERRED = 0x00000400,
    CONNECT_RESERVED = unchecked((int)0xFF000000),
    CONNECT_COMMANDLINE = 0x00000800,
    CONNECT_CMD_SAVECRED = 0x00001000,
    CONNECT_CRED_RESET = 0x00002000
  }
  [StructLayout(LayoutKind.Sequential)]
  private class NETRESOURCE
  {
    public ResourceScope dwScope = 0;
    //  change resource type as required
    public ResourceType dwType = ResourceType.RESOURCETYPE_DISK;
    public ResourceDisplayType dwDisplayType = 0;
    public ResourceUsage dwUsage = 0;
    public string lpLocalName = null;
    public string lpRemoteName = null;
    public string lpComment = null;
    public string lpProvider = null;
  }

  [DllImport("mpr.dll")]
  private static extern int WNetAddConnection2(NETRESOURCE lpNetResource, string lpPassword, string lpUsername, int dwFlags);
  [DllImport("mpr.dll")]
  private static extern int WNetCancelConnection2(string name, int flags, bool force);
  public static int MapNetworkDrive(string pPath, string pDrive, string pUser, string pPassword)
  {
    NETRESOURCE _Resource = new NETRESOURCE();
    _Resource.lpLocalName = pDrive;
    _Resource.lpRemoteName = pPath;
    _Resource.lpProvider = null;
    int result = WNetAddConnection2(_Resource, pPassword, pUser, (int)AddConnectionOptions.CONNECT_TEMPORARY);
    return result;
  }
  public static int UnmapNetworkDrive(string pDrive)
  {
    int result = WNetCancelConnection2(pDrive, (int)AddConnectionOptions.CONNECT_UPDATE_PROFILE, false);
    return result;
  }
  #endregion
  #region Mount/Unmount
  public bool Mount()
  {
    if (this.Drive != null) return true;
    for (var i = 0; i < 20; i++)
    {
      if (!Directory.Exists((char)('A' + i) + ":"))
      {
        this.Drive = (char)('A' + i) + ":";
        break;
      }
    }
    if (string.IsNullOrEmpty(this.Drive))
    {
      this.Log($"No drives available to mount");
      return false;
    }
    var _URL = WebDav.GetURL(this.Name);
    WebDav.Add(this);
    int _Status = MapNetworkDrive(_URL, this.Drive, null, null);
    if (_Status == 0)
    {
      this.Log($"{_URL} mapped to drive {this.Drive}");
      return true;
    }
    string _Error = new System.ComponentModel.Win32Exception(_Status).Message;
    this.Log($"Failed to map {_URL} to drive {this.Drive} - {_Error}");
    WebDav.Remove(this);
    return false;
  }
  public bool Unmount()
  {
    if (string.IsNullOrEmpty(this.Drive)) throw new Exception("Vault is not mounted");
    int _Status = UnmapNetworkDrive(this.Drive);
    if (_Status == 0)
    {
      this.Log($"Unmapped {this.Drive}");
      WebDav.Remove(this);
      this.Drive = null;
      return true;
    }
    string _Error = new System.ComponentModel.Win32Exception(_Status).Message;
    this.Log($"Failed to unmap {this.Drive} - {_Error}");
    return false;
  }
  #endregion
  public void Log(string pWhat) { MainWindow.Log(this.Name, pWhat); }

  public string Name;
  private Crypt.AbstractCrypt CryptoName, CryptoData;
  public string Drive;
  public DateTime LastUse;
  public Vault(String pName,String pPassword)
  {    
    this.Name = pName;
    this.LastUse = DateTime.MinValue;
    this.CryptoName = string.IsNullOrEmpty(pPassword) ? null : new Crypt.DES(pPassword);
    this.CryptoData = string.IsNullOrEmpty(pPassword) ? null : new Crypt.AES(pPassword);
    return;
  }

  #region Helpers
  private string Folder
  {
    get { return $"{Directory.GetCurrentDirectory()}/{this.Name}".Replace('\\', '/'); }
  }
  private string EncryptPath(string pPath)
  {
    if (this.CryptoName == null) return pPath;
    var _Result = new List<string>();
    foreach (var p in pPath.Replace('\\', '/').Split('/'))
      if (!String.IsNullOrEmpty(p)) _Result.Add(this.CryptoName.Encrypt(p));
    return "/" + string.Join('/', _Result);
  }
  private string DecryptPath(string pPath)
  {
    if (this.CryptoName == null) return pPath;
    var _Result = new List<string>();
    foreach (var p in pPath.Replace('\\', '/').Split('/'))
      if (!String.IsNullOrEmpty(p)) _Result.Add(this.CryptoName.Decrypt(p));
    return "/" + string.Join('/', _Result);
  }
  public string GetFileName(string pPath)
  {
    return $"{this.Folder}{this.EncryptPath(pPath)}".Replace('\\', '/');
  }
  public string GetPath(string pFileName)
  {
    return this.DecryptPath(pFileName.Replace('\\', '/').Replace(this.Folder, ""));
  }
  public bool IsHidden(string pPath)
  {
    return pPath == "/.vault";
  }
  public bool Exists(string pPath)
  {
    var _FileName = this.GetFileName(pPath);
    return Path.Exists(_FileName);
  }
  public IEnumerable<String> ScanDir(String pPath)
  {
    var _Folder = this.GetFileName(pPath);
    foreach (var _File in Directory.GetFileSystemEntries(_Folder))
    {
      if (!_File.EndsWith(".vault"))
      {
        var v = this.GetPath(_File);
        yield return v;
      }
    }
  }
  #endregion
  #region Operations
  public async Task CopyFrom(string pPath, Stream pDecrypted)
  {
    var _FileName = this.GetFileName(pPath);
    using (var _File = new FileStream(_FileName, FileMode.Open))
      if (this.CryptoData == null)
        await _File.CopyToAsync(pDecrypted);
      else
        await this.CryptoData.Decrypt(_File, pDecrypted);
  }
  public async Task CopyTo(Stream pDecrypted, string pPath)
  {
    var _FileName = this.GetFileName(pPath);
    if (!File.Exists(_FileName)) File.Create(_FileName).Close();
    using (var _File = new FileStream(_FileName, FileMode.Truncate))
      if (this.CryptoData == null)
        await pDecrypted.CopyToAsync(_File);
      else
        await this.CryptoData.Encrypt(pDecrypted, _File);
  }
  public void Delete(string pPath)
  {
    var _FileName = this.GetFileName(pPath);
    if (new FileInfo(_FileName).Attributes.HasFlag(FileAttributes.Archive))
      File.Delete(_FileName);
    else
      Directory.Delete(_FileName);
  }
  public bool Move(string pFrom, string pTo)
  {
    var _Source = this.GetFileName(pFrom);
    var _Target = this.GetFileName(pTo);
    if (!Path.Exists(_Source) || Path.Exists(_Target)) return false;
    if (new FileInfo(_Source).Attributes.HasFlag(FileAttributes.Archive))
      File.Move(_Source, _Target);
    else
      Directory.Move(_Source, _Target);
    return true;
  }
  public bool CreateDirectory(string pPath)
  {
    var _FileName = this.GetFileName(pPath);
    if (Path.Exists(_FileName)) return false;
    Directory.CreateDirectory(_FileName);
    return true;
  }
  public void Update(String pPath, String pCreated = null, String pModified = null, String pAccessed = null)
  {
    var _Info = new FileInfo(this.GetFileName(pPath));
    if (!String.IsNullOrEmpty(pCreated)) _Info.CreationTime = DateTime.Parse(pCreated);
    if (!String.IsNullOrEmpty(pModified)) _Info.LastWriteTime = DateTime.Parse(pModified);
    if (!String.IsNullOrEmpty(pAccessed)) _Info.LastAccessTime = DateTime.Parse(pAccessed);
  }
  #endregion
}
