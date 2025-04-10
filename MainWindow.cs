namespace TheCuriousGeek.Vault;
using System.Reflection;
using System.Drawing;
using System.IO;

public class MainWindow : Form
{
  public DataGridView LogView = new DataGridView();
  private NotifyIcon TrayIcon;
  private ContextMenuStrip Menu = new ContextMenuStrip();
  public MainWindow()
  {
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

  public void UpdateMenu()
  {
    this.Menu.Items.Clear();
    foreach (var v in Vault.Instances)
    {
      ToolStripItem _Item = new ToolStripButton(v.Name);
      _Item.Tag = v;
      _Item.Image = GetIcon(v.Mounted ? "mounted" : "unmounted").ToBitmap();
      _Item.Click += v.Mounted ? this.OnUnmount : this.OnMount;
      this.Menu.Items.Add(_Item);
    }
    this.Menu.Items.Add(new ToolStripSeparator());
    this.Menu.Items.Add("New", null, this.OnNew);
    this.Menu.Items.Add("Open", null, this.OnOpen);
    this.Menu.Items.Add(new ToolStripSeparator());
    this.Menu.Items.Add("Exit", null, this.OnExit);
  }
  private void OnTrayDoubleClick(object sender, EventArgs args)
  {
    this.Show();
    this.WindowState = FormWindowState.Maximized;
  }
  public void OnMount(object sender, EventArgs args)
  {
    var _Vault = ((ToolStripItem)sender).Tag as Vault;
    var _Password = WinUtil.InputDialog("Enter the password for this vault");
    if (_Password == null) return;
    Mount(_Vault, _Password);
    this.UpdateMenu();
  }
  private void Mount(Vault pVault, String pPassword)
  {
    if (pVault.Mount(pPassword))
      MessageBox.Show($"Mounted as {pVault.Drive}", $"Vault {pVault.Name}", MessageBoxButtons.OK, MessageBoxIcon.Information);
    else
      MessageBox.Show($"Could not mount vault", $"Vault {pVault.Name}", MessageBoxButtons.OK, MessageBoxIcon.Error);
  }
  private void OnUnmount(object sender, EventArgs args)
  {
    var _Vault = ((ToolStripItem)sender).Tag as Vault;
    if (_Vault.Unmount())
      MessageBox.Show($"Unmounted", $"Vault {_Vault.Name}", MessageBoxButtons.OK, MessageBoxIcon.Information);
    else
      MessageBox.Show($"Could not unmount vault", $"Vault {_Vault.Name}", MessageBoxButtons.OK, MessageBoxIcon.Error);
    this.UpdateMenu();
  }
  private void OnNew(object sender, EventArgs args)
  {
    var _Dialog = new FolderBrowserDialog();
    _Dialog.Description = "Select the folder to hold this vault";
    if (_Dialog.ShowDialog() != DialogResult.OK) return;
    var _Location = _Dialog.SelectedPath;
    Program.Log("NewVault", "Selected " + _Location);
    if (File.Exists(_Location + "/.vault"))
    {
      MessageBox.Show("Folder already has a vault", "New Vault", MessageBoxButtons.OK, MessageBoxIcon.Error);
      return;
    }
    var _Name = WinUtil.InputDialog("The name for this Vault");
    if (string.IsNullOrEmpty(_Name)) return;
    var _Password = WinUtil.InputDialog("Enter the password for this vault");
    if (_Password == null) return;
    if (String.IsNullOrEmpty(_Password))
    {
      MessageBox.Show("Empty password, this vault will be unencrypted", "New Vault", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
    var _Vault = Vault.Create(_Name, _Password, Path.Combine(_Location,_Name));
    Mount(_Vault, _Password);
    this.UpdateMenu();
  }
  private void OnOpen(object sender, EventArgs args)
  {
    var _Dialog = new FolderBrowserDialog();
    _Dialog.Description = "Select the folder containing the vault";
    if (_Dialog.ShowDialog() != DialogResult.OK) return;
    var _Location = _Dialog.SelectedPath;
    Program.Log("OpenVault", "Selected " + _Location);
    if (!File.Exists(_Dialog.SelectedPath + "/.vault"))
    {
      MessageBox.Show("Folder does not contain a vault", "Open Vault", MessageBoxButtons.OK, MessageBoxIcon.Warning);
      return;
    }
    var _Name = WinUtil.InputDialog("The name for this Vault",Path.GetFileName(_Location));
    if (string.IsNullOrEmpty(_Name)) return;
    var _Password = WinUtil.InputDialog("Enter the password for this vault");
    var _Vault = new Vault(_Name, _Location);
    if (!_Vault.Validate(_Password))
    {
      MessageBox.Show($"Invalid password", $"Open Vault {_Vault.Name}", MessageBoxButtons.OK, MessageBoxIcon.Error);
      return;
    }
    Vault.Instances.Add(_Vault);
    Vault.Save();
    Mount(_Vault, _Password);
    this.UpdateMenu();
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
    foreach (var _Vault in Vault.Instances)
    {
      if (_Vault.Mounted)
      {
        var _Answer = MessageBox.Show($"Vault {_Vault.Name} is mounted as {_Vault.Drive}. Unmount and exit", "Vault Warning", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (_Answer == DialogResult.Cancel) return;
        if (_Answer == DialogResult.Yes) _Vault.Unmount();
      }
    }
    if (Vault.Instances.All(x => !x.Mounted)) Environment.Exit(0);
  }
  private static Icon GetIcon(string pName)
  {
    var _Assembly = Assembly.GetExecutingAssembly();
    var _Name = $"{_Assembly.GetName().Name}.Icons.{pName}.ico";
    using (Stream _Stream = _Assembly.GetManifestResourceStream(_Name))
      return new Icon(_Stream);
  }
}
