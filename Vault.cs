namespace TheCuriousGeek.Vault;
using System.Runtime.InteropServices;

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

  public static List<Vault> Instances = new();
  public static Vault Get(string pName) { return Instances.FirstOrDefault(x => x.Name == pName); }
  private Crypt.AbstractCrypt CryptoName, CryptoData;
  public string Name;
  public string Folder;
  public string Drive;
  public bool Mounted = false;
  public DateTime LastUse;
  public Vault(string pName, string pFolder)
  {
    Instances.Add(this);
    this.Name = pName;
    this.Folder = pFolder;
    this.LastUse = DateTime.MinValue;
    if (!File.Exists(pFolder + "/.vault"))
    {
      MessageBox.Show($"No vault in {this.Folder}", this.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
      Environment.Exit(1);
    }
    this.Log("Configured");
  }
  private void Log(string pWhat) { Program.Log(this.Name, pWhat); }
  public bool Validate(string pPassword)
  {
    if (!File.Exists(this.Folder + "/.vault")) return false;
    if (pPassword == null) return false;
    var _VaultHash = File.ReadAllText(this.Folder + "/.vault");
    var _InputHash = new Crypt.AES(pPassword).Encrypt("vault");
    return _VaultHash.ToLower() == _InputHash.ToLower();
  }
  public void Mount(String pPassword)
  {
    if (!this.Validate(pPassword))
    {
      MessageBox.Show($"Invalid password", this.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
      return;
    }
    this.CryptoName = string.IsNullOrEmpty(pPassword) ? null : new Crypt.DES(pPassword);
    this.CryptoData = string.IsNullOrEmpty(pPassword) ? null : new Crypt.AES(pPassword);
    for (var i = 0; i < 20; i++)
    {
      if (!Directory.Exists((char)('A' + i) + ":"))
      {
        this.Drive = (char)('A' + i) + "";
        break;
      }
    }
    var _Drive = Drive + ":";
    var _URL = WebDav.GetURL(this.Name);
    int status = MapNetworkDrive(_URL, _Drive, null, null);
    if (status == 0)
    {
      this.Log($"{_URL} mapped to drive {_Drive}");
      Mounted = true;
      MessageBox.Show($"Mounted as {this.Drive}", $"Vault {this.Name}", MessageBoxButtons.OK, MessageBoxIcon.Information);
      return;
    }
    string _Error = new System.ComponentModel.Win32Exception(status).Message;
    this.Log($"Failed to map {_URL} to drive {_Drive} - {_Error}");
    MessageBox.Show($"Error mounting as {this.Drive}", $"Vault {this.Name}", MessageBoxButtons.OK, MessageBoxIcon.Error);
  }
  public void Unmount()
  {
    var _Drive = Drive + ":";
    int status = UnmapNetworkDrive(_Drive);
    if (status == 0)
    {
      this.Log($"Unmapped {_Drive}");
      Mounted = false;
      this.CryptoName = null;
      this.CryptoData = null;
      MessageBox.Show($"Unmounted {this.Drive}", $"Vault {this.Name}", MessageBoxButtons.OK, MessageBoxIcon.Information);
      return;
    }
    string _Error = new System.ComponentModel.Win32Exception(status).Message;
    this.Log($"Failed to unmap {_Drive} - {_Error}");
    MessageBox.Show($"Error unmounting {this.Drive}", $"Vault {this.Name}", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
    return (this.Folder + this.EncryptPath(pPath)).Replace('\\', '/');
  }
  public string GetPath(string pFileName)
  {
    return this.DecryptPath(pFileName.Substring(this.Folder.Length).Replace('\\', '/'));
  }
  public bool IsHidden(string pPath)
  {
    return pPath == "/.vault";
  }
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
  public bool Exists(string pPath)
  {
    var _FileName = this.GetFileName(pPath);
    return Path.Exists(_FileName);
  }
  public IEnumerable<String> ScanDir(String pPath)
  {
    var _Folder = this.GetFileName(pPath);
    foreach (var _File in Directory.GetFileSystemEntries(_Folder))
      if (!_File.Replace('\\', '/').EndsWith("/.vault")) yield return this.GetPath(_File);
  }
  public void Update(String pPath, String pCreated = null, String pModified = null, String pAccessed = null)
  {
    var _Info = new FileInfo(this.GetFileName(pPath));
    if (!String.IsNullOrEmpty(pCreated)) _Info.CreationTime = DateTime.Parse(pCreated);
    if (!String.IsNullOrEmpty(pModified)) _Info.LastWriteTime = DateTime.Parse(pModified);
    if (!String.IsNullOrEmpty(pAccessed)) _Info.LastAccessTime = DateTime.Parse(pAccessed);
  }

  #region static helpers
  public static void Load()
  {
    var _IniFile = new IniFile("Vault.ini");
    foreach (var n in _IniFile.GetKeys("Vault"))
      new Vault(n, _IniFile.Get("Vault", n));
    if (Instances.Count == 0)
    {
      if (MessageBox.Show(@"No vaults are defined", "Create new vault", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
        Application.Exit();
      Create();
    }
    foreach (var _Vault in Instances)
    {
      if (MessageBox.Show("Do you want to open this vault", _Vault.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) continue;
      var _Password = GetPassword(_Vault);
      if (_Password == null) continue;
      _Vault.Mount(_Password);
    }
  }
  public static void Save()
  {
    var _IniFile = new IniFile("Vault.ini");
    foreach (var _Vault in Instances)
      _IniFile.Add("Vault", _Vault.Name, _Vault.Folder);
    _IniFile.Save();
  }
  public static void Create()
  {
    var _Name = "";
    var _Password = "";
    var _Dialog = new FolderBrowserDialog();
    _Dialog.Description = "Select the folder to save this vault";
    DialogResult result = _Dialog.ShowDialog();
    if (result != DialogResult.OK || string.IsNullOrWhiteSpace(_Dialog.SelectedPath))
      return;
    Program.Log("NewVault", "Selected " + _Dialog.SelectedPath);
    if (Directory.EnumerateFileSystemEntries(_Dialog.SelectedPath).Count() > 0)
    {
      if (!File.Exists(_Dialog.SelectedPath + "/.vault"))
      {
        MessageBox.Show("Folder is not empty", "New Vault", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }
      if (MessageBox.Show("Folder already has a vault. Do you want to open this vault", "Existing Vault", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No) return;
    }
    else
    {
      _Password = WinUtil.InputDialog("Enter the password for this vault");
      if (_Password == null) return;
      if (String.IsNullOrEmpty(_Password))
      {
        MessageBox.Show("Empty password, this vault will be unencrypted", "Unencrypted Vault", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }
      //Vaults have a file .vault that contains the hash of the encrypted password, even if empty
      using (var _Signature = File.CreateText(_Dialog.SelectedPath + "/.vault"))
        _Signature.Write(new Crypt.AES(_Password).Encrypt("vault"));
    }
    while (true)
    {
      _Name = WinUtil.InputDialog("The name for this Vault");
      if (!string.IsNullOrEmpty(_Name)) break;
      if (MessageBox.Show("Vault must have a name. Retry?", "New Vault", MessageBoxButtons.OKCancel, MessageBoxIcon.Error) == DialogResult.Cancel)
        return;
    }
    var v = new Vault(_Name, _Dialog.SelectedPath);
    Vault.Save();
  }
  public static String GetPassword(Vault pVault)
  {
    while (true)
    {
      String _Password = WinUtil.InputDialog($"Enter the password for {pVault.Name}");
      if (_Password == null) return null; //User pressed cancel
      if (pVault.Validate(_Password)) return _Password;
      if (MessageBox.Show("Invalid password. Retry?", pVault.Name, MessageBoxButtons.RetryCancel, MessageBoxIcon.Question) == DialogResult.Cancel) return null;
    }
  }
  #endregion
}
