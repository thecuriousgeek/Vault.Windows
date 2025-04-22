namespace TheCuriousGeek.Vault;
using System.Windows.Forms;
using System.Reflection;
using System.Drawing;
using System.IO;

public class MainWindow : Form
{
  public static MainWindow Instance = null;
  public DataGridView LogView = new DataGridView();
  private NotifyIcon TrayIcon;
  private ContextMenuStrip Menu = new ContextMenuStrip();
  public MainWindow()
  {
    MainWindow.Instance = this;
    this.Icon = GetIcon("vault");
    this.Text = "Vault - Logs";
    this.WindowState = FormWindowState.Maximized;
    this.LogView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
    this.LogView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
    this.LogView.Dock = DockStyle.Fill;
    this.LogView.Columns.Add("When", "Time");
    this.LogView.Columns.Add("Who", "ID");
    this.LogView.Columns.Add("What", "Message");
    this.LogView.Size = this.ClientSize;
    this.LogView.Location = new Point(0, 0);
    this.LogView.ContextMenuStrip = this.Menu;
    this.Controls.Add(LogView);
    this.TrayIcon = new NotifyIcon() { Visible = true, Text = "MyVault", Icon = GetIcon("vault"), ContextMenuStrip = this.Menu };
    this.TrayIcon.DoubleClick += new EventHandler(this.OnTrayDoubleClick);
  }

  private void OnTrayDoubleClick(object sender, EventArgs args)
  {
    this.Show();
    this.WindowState = FormWindowState.Maximized;
  }
  public void OnMount(object sender, EventArgs args)
  {
    var _Name = (sender is string) ? sender as string : ((ToolStripItem)sender).Tag as string;
    if (!Config.Vaults.Contains(_Name))
    {
      MessageBox.Show($"No such vault", $"Vault {_Name}", MessageBoxButtons.OK, MessageBoxIcon.Error);
      return;
    }
    var _Password = WinUtil.InputDialog($"Enter the password for {_Name}");
    if (_Password == null) return;
    var _Vault = Config.Open(_Name,_Password);
    if (_Vault == null)
    {
      MessageBox.Show($"Invalid password", $"Vault {_Name}", MessageBoxButtons.OK, MessageBoxIcon.Error);
      return;
    }
    if (_Vault.Mount())
      MessageBox.Show($"Mounted as {_Vault.Drive}", $"Vault {_Vault.Name}", MessageBoxButtons.OK, MessageBoxIcon.Information);
    else
      MessageBox.Show($"Could not mount vault", $"Vault {_Name}", MessageBoxButtons.OK, MessageBoxIcon.Error);
    MainWindow.UpdateMenu();
  }
  public void OnUnmount(object sender, EventArgs args)
  {
    var _Name = (sender is string) ? sender as string : ((ToolStripItem)sender).Tag as string;
    var _Vault = WebDav.Vaults.FirstOrDefault(v => v.Name == _Name);
    if (_Vault == null)
    {
      MessageBox.Show($"No such vault", $"Vault {_Name}", MessageBoxButtons.OK, MessageBoxIcon.Error);
      return;
    }
    if (_Vault.Unmount())
      MessageBox.Show($"Unmounted", $"Vault {_Name}", MessageBoxButtons.OK, MessageBoxIcon.Information);
    else
      MessageBox.Show($"Could not unmount vault", $"Vault {_Name}", MessageBoxButtons.OK, MessageBoxIcon.Error);
    MainWindow.UpdateMenu();
  }
  private void OnNew(object sender, EventArgs args)
  {
    var _Name = WinUtil.InputDialog("The name for this Vault");
    if (string.IsNullOrEmpty(_Name)) return;
    if (File.Exists(_Name + "/.vault"))
    {
      MessageBox.Show("Folder already has a vault", "New Vault", MessageBoxButtons.OK, MessageBoxIcon.Error);
      return;
    }
    if (Directory.Exists(_Name))
    {
      MessageBox.Show("Folder already exists", "New Vault", MessageBoxButtons.OK, MessageBoxIcon.Error);
      return;
    }
    var _Password = WinUtil.InputDialog("Enter the password for this vault (blank for unencrypted vault)");
    if (_Password == null) return;
    if (String.IsNullOrEmpty(_Password))
    {
      MessageBox.Show("Empty password, this vault will be unencrypted", "New Vault", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
    if (!Config.Create(_Name, _Password)) return;
    MainWindow.UpdateMenu();
    MessageBox.Show("Created", $"Vault {_Name}", MessageBoxButtons.OK, MessageBoxIcon.Warning);
  }
  protected override void OnResize(EventArgs e)
  {
    if (FormWindowState.Minimized == this.WindowState)
    {
      this.Hide();
      return;
    }
    LogView.Size = this.ClientSize;
  }
  protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
  {
    this.Hide();
    e.Cancel = true;
  }
  public void OnExit(object sender, EventArgs e)
  {
    var _Vaults = WebDav.Vaults.Where(v => true).ToList(); //Clone since Id be modifying the list
    foreach (var _Vault in _Vaults)
    {
      var _Answer = MessageBox.Show($"Vault {_Vault.Name} is mounted as {_Vault.Drive}. Unmount and exit", "Vault Warning", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
      if (_Answer == DialogResult.Cancel) return;
      if (_Answer == DialogResult.Yes) this.OnUnmount(_Vault.Name, null);
    }
    if (WebDav.Vaults.Count==0) Environment.Exit(0);
  }
  private static Icon GetIcon(string pName)
  {
    var _Assembly = Assembly.GetExecutingAssembly();
    var _Name = $"{_Assembly.GetName().Name}.Icons.{pName}.ico";
    using (Stream _Stream = _Assembly.GetManifestResourceStream(_Name))
      return new Icon(_Stream);
  }
  public static void UpdateMenu()
  {
    if (Instance.InvokeRequired)
    {
      Instance.Invoke(new System.Windows.Forms.MethodInvoker(delegate { MainWindow.UpdateMenu(); }));
      return;
    }
    Instance.Menu.Items.Clear();
    foreach (var _Name in Config.Vaults)
    {
      ToolStripItem _Item = new ToolStripButton(_Name);
      _Item.Tag = _Name;
      var _Mounted = WebDav.Vaults.Any(x => x.Name == _Name);
      _Item.Image = GetIcon(_Mounted ? "mounted" : "unmounted").ToBitmap();
      _Item.Click += _Mounted ? Instance.OnUnmount : Instance.OnMount;
      Instance.Menu.Items.Add(_Item);
    }
    Instance.Menu.Items.Add(new ToolStripSeparator());
    Instance.Menu.Items.Add("New", null, Instance.OnNew);
    Instance.Menu.Items.Add(new ToolStripSeparator());
    Instance.Menu.Items.Add("Exit", null, Instance.OnExit);
  }
  public static void Log(String pID, string pMessage)
  {
    if (Instance.InvokeRequired)
    {
      Instance.BeginInvoke(new System.Windows.Forms.MethodInvoker(delegate { MainWindow.Log(pID, pMessage); }));
      return;
    }
    System.Console.WriteLine($"{pID}: {pMessage}");
    Instance.LogView.Rows.Add(DateTime.Now, pID, pMessage);
    var MAX_ROWS = 1000; var MIN_ROWS = 500;
    if (Instance.LogView.Rows.Count > MAX_ROWS)
    {
      Instance.SuspendLayout();
      var _Limit = Instance.LogView.Rows.Count - MIN_ROWS;
      while (Instance.LogView.Rows.Count > MIN_ROWS)
        Instance.LogView.Rows.RemoveAt(0);
      Instance.ResumeLayout();
      Instance.Refresh();
    }
    Instance.LogView.FirstDisplayedScrollingRowIndex = Instance.LogView.Rows.Count - 1;
  }
}
