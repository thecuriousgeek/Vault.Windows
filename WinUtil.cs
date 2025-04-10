namespace TheCuriousGeek.Vault;

public static class WinUtil
{
  public static string InputDialog(string pPrompt,string pDefault="")
  {
    var _Size = new Size(500, 100);
    var _InputBox = new Form { FormBorderStyle = FormBorderStyle.FixedToolWindow, ClientSize = _Size, Text = pPrompt };
    var _TextBox = new TextBox() { Size = new Size(_Size.Width - 10, 30), Location = new Point(5, 5), Text = pDefault };
    _InputBox.Controls.Add(_TextBox);
    var _OkButton = new Button { DialogResult = DialogResult.OK, Size = new Size(75, 30), Text = "&OK", Location = new Point(_Size.Width - 80 - 80, 50) };
    _InputBox.Controls.Add(_OkButton);
    var _CancelButton = new Button { DialogResult = DialogResult.Cancel, Size = new Size(75, 30), Text = "&Cancel", Location = new Point(_Size.Width - 80, 50) };
    _InputBox.Controls.Add(_CancelButton);
    _InputBox.AcceptButton = _OkButton;
    _InputBox.CancelButton = _CancelButton;
    _InputBox.BringToFront();
    _InputBox.TopMost = true;
    var result = _InputBox.ShowDialog();
    if (result == DialogResult.OK) return _TextBox.Text;
    return null;
  }
}