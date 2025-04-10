namespace TheCuriousGeek.Vault;

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
    //if basic authentication is permitted, use that instead for prompting for mount password
    // using (RegistryKey _Key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\\Services\\WebClient\\Parameters"))
    //   if (_Key != null && (int)_Key.GetValue("BasicAuthLevel") == 2)
    //     BasicAuthentication = true;
    if (pArg.Length > 0) Vault.Root = pArg[0].Replace('\\','/');
    else Vault.Root = Directory.GetCurrentDirectory().Replace('\\','/');
    Directory.SetCurrentDirectory(Vault.Root);
    ApplicationConfiguration.Initialize();
    LogWindow = new MainWindow();
    LogWindow.Show();
    WebDav.Start();
    Vault.Load();
    foreach (var _Vault in Vault.Instances)
    {
      if (MessageBox.Show("Do you want to open this vault", _Vault.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) continue;
      String _Password = WinUtil.InputDialog($"Enter the password for {_Vault.Name}");
      if (_Password == null) break;
      if (!_Vault.Validate(_Password))
      {
        MessageBox.Show("Invalid password", _Vault.Name, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        continue;
      }
      if (_Vault.Mount(_Password))
        MessageBox.Show($"Mounted as {_Vault.Drive}", $"Vault {_Vault.Name}", MessageBoxButtons.OK, MessageBoxIcon.Information);
      else
        MessageBox.Show($"Could not mount vault", $"Vault {_Vault.Name}", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    LogWindow.UpdateMenu();
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
        if (!string.IsNullOrEmpty(_Vault.Drive) && (_Now - _Vault.LastUse).TotalSeconds > _Timeout)
        {
          _Vault.Log("Vault has not been accessed for {_Timeout} seconds. Unmounting");
          _Vault.Unmount();
        }
      }
    }
  }
}