namespace TheCuriousGeek.Vault;
// using Microsoft.Win32;

static class Program
{
  public static bool BasicAuthentication = false;
  private static MainWindow LogWindow;
  public static void Log(string pID, string pMessage)
  {
    if (LogWindow.LogView.InvokeRequired)
      LogWindow.LogView.BeginInvoke(new MethodInvoker(delegate { LogWindow.LogView.Rows.Add(DateTime.Now, pID, pMessage); }));
    else
    {
      LogWindow.LogView.Rows.Add(DateTime.Now, pID, pMessage);
      var MAX_ROWS = 1000; var MIN_ROWS = 500;
      if (LogWindow.LogView.Rows.Count > MAX_ROWS)
      {
        LogWindow.SuspendLayout();
        var _Limit = LogWindow.LogView.Rows.Count - MIN_ROWS;
        while (LogWindow.LogView.Rows.Count > MIN_ROWS)
          LogWindow.LogView.Rows.RemoveAt(0);
        LogWindow.ResumeLayout();
        LogWindow.Refresh();
      }
      LogWindow.LogView.FirstDisplayedScrollingRowIndex = LogWindow.LogView.Rows.Count - 1;
    }
  }
  [STAThread]
  static void Main(String[] pArg)
  {
    // Crypt.Test();
    // Environment.Exit(0);
    //if basic authentication is permitted, use that instead for prompting for mount password
    // using (RegistryKey _Key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\\Services\\WebClient\\Parameters"))
    //   if (_Key != null && (int)_Key.GetValue("BasicAuthLevel") == 2)
    //     BasicAuthentication = true;
    if (pArg.Length > 0) Vault.Root = pArg[0];
    else Vault.Root = Directory.GetCurrentDirectory();
    Directory.SetCurrentDirectory(Vault.Root);
    ApplicationConfiguration.Initialize();
    LogWindow = new MainWindow();
    // var _Vault = new Vault("Test","Z:/");
    // var p = "Tat Twam Asi";
    // _Vault.CryptoName = string.IsNullOrEmpty(p) ? null : new Crypt.DES(p);
    // _Vault.CryptoData = string.IsNullOrEmpty(p) ? null : new Crypt.AES(p);
    LogWindow.Show();
    WebDav.Start();
    Vault.Load();
    Task.Run(() => Watcher());
    Application.Run();
  }
  private static void Watcher()
  {
    var _IniFile = new IniFile(Vault.Root+"/vault.ini");
    var s = _IniFile.Get("Setting", "Timeout");
    var _Timeout = string.IsNullOrEmpty(s) ? 600 : int.Parse(s);
    while (true)
    {
      Thread.Sleep(5000);
      var _Now = DateTime.Now;
      foreach (var _Vault in Vault.Instances)
      {
        if (_Vault.Mounted && (_Now - _Vault.LastUse).TotalSeconds > _Timeout)
        {
          Log("Watcher", $"Vault {_Vault.Name} has not been accessed for {_Timeout} seconds. Unmounting");
          _Vault.Unmount();
        }
      }
    }
  }
}