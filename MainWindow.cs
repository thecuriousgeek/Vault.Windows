namespace TheCuriousGeek.Vault;
using System.Reflection;
using System.Drawing;
using System.IO;

public class MainWindow : Form
{
  public DataGridView LogView = new DataGridView();
  private NotifyIcon TrayIcon;
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
    this.Controls.Add(LogView);
    this.TrayIcon = new NotifyIcon() { Visible = true, Text = "MyVault", Icon = GetIcon("vault"), ContextMenuStrip = new ContextMenuStrip() };
    this.TrayIcon.Click += new EventHandler(this.OnTrayClick);
    this.TrayIcon.DoubleClick += new EventHandler(this.OnTrayDoubleClick);
  }

  private void OnTrayClick(object sender, EventArgs args)
  {
    this.TrayIcon.ContextMenuStrip.Items.Clear();
    foreach (var v in Vault.Instances)
    {
      ToolStripItem _Item = new ToolStripButton(v.Name);
      _Item.Tag = v;
      _Item.Image = GetIcon(v.Mounted ? "mounted" : @"unmounted").ToBitmap();
      _Item.Click += v.Mounted ? this.OnUnmount : this.OnMount;
      this.TrayIcon.ContextMenuStrip.Items.Add(_Item);
    }
    this.TrayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
    this.TrayIcon.ContextMenuStrip.Items.Add("New", null, this.OnNew);
    this.TrayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
    this.TrayIcon.ContextMenuStrip.Items.Add("Exit", null, this.OnExit);
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
    _Vault.Mount(_Password);
  }
  private void OnUnmount(object sender, EventArgs args)
  {
    var _Vault = ((ToolStripItem)sender).Tag as Vault;
    _Vault.Unmount();
  }
  private void OnNew(object sender, EventArgs args)
  {
    Vault.Create();
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
    foreach (var v in Vault.Instances)
    {
      if (v.Mounted)
      {
        var _Answer = MessageBox.Show($"Vault {v.Name} is mounted as {v.Drive}. Unmount and exit", "Vault Warning", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (_Answer == DialogResult.Cancel) return;
        if (_Answer == DialogResult.Yes) v.Unmount();
      }
    }
    if (Vault.Instances.All(x => !x.Mounted)) Environment.Exit(0);
  }
  private static Icon GetIcon(string pName)
  {
    var _Assembly = Assembly.GetExecutingAssembly();
    var _Name = $"{_Assembly.GetName().Name}.{pName}.ico";
    using (Stream _Stream = _Assembly.GetManifestResourceStream(_Name))
      return new Icon(_Stream);
  }
}
