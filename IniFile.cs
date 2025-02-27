namespace TheCuriousGeek.Vault;
using System;
using System.IO;

public class IniFile
{
  private String FileName;
  private Dictionary<string, Dictionary<string, string>> Sections = new();
  public IniFile(String pFileName)
  {
    this.FileName = pFileName;
    if (!File.Exists(pFileName))
      return;
    var _Section = new Dictionary<string, string>();
    this.Sections["ROOT"] = _Section;
    using (TextReader _Reader = new StreamReader(pFileName))
    {
      while (_Reader.Peek() > 0)
      {
        string _Line = _Reader.ReadLine();
        _Line = _Line.Trim();
        if (_Line == "") continue;
        if (_Line.StartsWith("[") && _Line.EndsWith("]"))
        {
          _Section = new Dictionary<string, string>();
          this.Sections[_Line.Substring(1, _Line.Length - 2)] = _Section;
        }
        else
        {
          string[] _KeyValue = _Line.Split(new char[] { '=' }, 2);
          _Section[_KeyValue[0]] = _KeyValue.Length > 1 ? _KeyValue[1] : null;
        }
      }
    }
    if (this.Sections["ROOT"].Count==0) this.Sections.Remove("ROOT");
  }

  public string Get(String pSection, String pKey)
  {
    if (!this.Sections.ContainsKey(pSection)) return null;
    if (!this.Sections[pSection].ContainsKey(pKey)) return null;
    return this.Sections[pSection][pKey];
  }
  public String[] GetSections()
  {
    return this.Sections.Keys.ToArray();
  }
  public String[] GetKeys(String pSection)
  {
    if (!this.Sections.ContainsKey(pSection)) return [];
    return this.Sections[pSection].Keys.ToArray();
  }
  public void Add(String pSection, String pKey, String pValue)
  {
    if (!this.Sections.ContainsKey(pSection))
      this.Sections[pSection] = new Dictionary<string, string>();
    this.Sections[pSection][pKey] = pValue;
  }
  public void Delete(String pSection, String pKey)
  {
    if (this.Sections.ContainsKey(pSection))
    {
      if (string.IsNullOrEmpty(pKey)) this.Sections.Remove(pSection);
      else if (this.Sections[pSection].ContainsKey(pKey)) this.Sections[pSection].Remove(pKey);
    }
  }
  public void Save()
  {
    String _Buffer = "";
    foreach (var _Section in this.Sections)
    {
      _Buffer += $"[{_Section.Key}]\r\n";
      foreach (var _Item in _Section.Value)
        _Buffer += $"{_Item.Key}={_Item.Value}\r\n";
    }
    using (TextWriter _Writer = new StreamWriter(this.FileName))
      _Writer.Write(_Buffer);
  }
}