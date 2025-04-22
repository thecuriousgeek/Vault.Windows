namespace TheCuriousGeek.Vault;

static class Program
{
  public static bool BasicAuthentication = false;
  [STAThread]
  static void Main(String[] pArg)
  {
    if (pArg.Length > 0) Directory.SetCurrentDirectory(pArg[0]);
    ApplicationConfiguration.Initialize();
    var _MainWindow = new MainWindow();
    _MainWindow.Show();
    WebDav.Start();
    Config.Load();
    foreach (var _Config in Config.Instances)
    {
      if (MessageBox.Show("Do you want to open this vault", _Config.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) continue;
      _MainWindow.OnMount(_Config.Name, null);
    }
    MainWindow.UpdateMenu();
    Task.Run(() => Watcher());
    Application.Run();
  }
  private static void Watcher()
  {
    var _IniFile = new IniFile("vault.ini");
    var s = _IniFile.Get("Setting", "Timeout");
    var _Timeout = string.IsNullOrEmpty(s) ? 600 : int.Parse(s);
    while (true)
    {
      Thread.Sleep(5000);
      var _Now = DateTime.Now;
      var _Expired = WebDav.Vaults.Where(v => (v.LastUse - _Now).TotalSeconds > _Timeout).ToList();
      foreach (var _Vault in _Expired)
      {
        _Vault.Log("Vault has not been accessed for {_Timeout} seconds. Unmounting");
        MainWindow.Instance.Invoke(new System.Windows.Forms.MethodInvoker(delegate { MainWindow.Instance.OnUnmount(_Vault.Name, null); }));
      }
    }
  }
}