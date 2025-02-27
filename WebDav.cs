namespace TheCuriousGeek.Vault;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using System.Text.RegularExpressions;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using System.Diagnostics;

public class WebDav
{
  private static string SanitizeXml(String pWhat)
  {
    return pWhat.Replace("&", "&amp;").Replace("<", "&lt;");
  }
  protected class DavContext
  {
    private HttpContext Context;
    private string Verb;
    public Vault Vault;
    public string Path;
    private Stopwatch Timer;
    public DavContext(HttpContext pContext)
    {
      this.Timer = new Stopwatch();
      this.Timer.Start();
      this.Context = pContext;
      this.Verb = this.Context.Request.Method;
      var _Args = this.Context.Request.Path.Value.TrimStart('/').Split(new char[] { '/' }, 2);
      this.Vault = Vault.Get(_Args.Length > 0 ? _Args[0] : null);
      if (this.Vault == null) return;
      this.Vault.LastUse = DateTime.Now;
      this.Path = _Args.Length > 1 ? "/" + _Args[1] : "/";
    }
    public bool Valid
    {
      get
      {
        if (this.Vault != null) return true;
        this.NotFound();
        return false;
      }
    }
    public bool Exists
    {
      get
      {
        if (this.Valid && this.Vault.Exists(this.Path)) return true;
        this.NotFound();
        return false;
      }
    }
    public void Done()
    {
      this.Timer.Stop();
      var _Name = this.Vault == null ? this.Context.Request.Path.Value : this.Vault.Name;
      Program.Log(_Name, $"{this.Verb} {this.Path} - {this.Context.Response.StatusCode} in {this.Timer.Elapsed.TotalMicroseconds}ms");
    }
    public void NotFound()
    {
      this.Context.Response.StatusCode = 404;
      this.Context.Response.WriteAsync("Not Found");
      this.Done();
    }
  }
  #region Helpers
  private static WebDav Instance = new WebDav();
  public static string GetURL(string pVault) { return $"{Instance.URL}/{pVault}"; }
  public static void Start()
  {
    Instance.Server.Start();
    Program.Log("WebDAV", "Listening at " + Instance.URL);
  }
  private static string GetRegexMatch(string pWhat, string pPattern)
  {
    var _Matches = Regex.Match(pWhat, pPattern);
    if (_Matches.Groups?.Count > 1 && _Matches.Groups[1].Captures?.Count > 0)
      return _Matches.Groups[1].Captures[0].Value;
    return "";
  }
  private static IDictionary<string, string> ContentType = new FileExtensionContentTypeProvider().Mappings;
  private static List<string> Locks = new List<string>();
  #endregion
  private WebApplication Server;
  public string URL { get { return this.Server.Urls.First(); } }
  private WebDav()
  {
    var _Builder = WebApplication.CreateBuilder();
    _Builder.WebHost.ConfigureKestrel((ctx, opt) => { opt.Listen(IPAddress.Loopback, 5000); opt.Limits.MaxRequestBodySize = long.MaxValue; });
    _Builder.Logging.ClearProviders();
    this.Server = _Builder.Build();
    this.Server.MapMethods("/{*route}", ["OPTIONS"], async context => await OnOptions(context));
    this.Server.MapMethods("/{*route}", ["HEAD"], async context => await OnHead(context));
    this.Server.MapMethods("/{*route}", ["GET"], async context => await OnGet(context));
    this.Server.MapMethods("/{*route}", ["PUT"], async context => await OnPut(context));
    this.Server.MapMethods("/{*route}", ["DELETE"], async context => await OnDelete(context));
    this.Server.MapMethods("/{*route}", ["LOCK"], async context => await OnLock(context));
    this.Server.MapMethods("/{*route}", ["UNLOCK"], async context => await OnUnlock(context));
    this.Server.MapMethods("/{*route}", ["MKCOL"], async context => await OnMkdir(context));
    this.Server.MapMethods("/{*route}", ["MOVE"], async context => await OnMove(context));
    this.Server.MapMethods("/{*route}", ["PROPFIND"], async context => await OnFind(context));
    this.Server.MapMethods("/{*route}", ["PROPPATCH"], async context => await OnPatch(context));
  }
  private static async Task OnOptions(HttpContext pContext)
  {
    var _Context = new DavContext(pContext);
    if (!_Context.Valid) return;
    pContext.Response.Headers.Allow = "OPTIONS, LOCK, DELETE, PROPPATCH, COPY, MOVE, UNLOCK, PROPFIND";
    pContext.Response.Headers["Dav"] = "1, 2";
    await pContext.Response.WriteAsync("");
    _Context.Done();
  }
  private static async Task OnHead(HttpContext pContext)
  {
    var _Context = new DavContext(pContext);
    if (!_Context.Valid) return;
    if (!_Context.Exists) return;
    pContext.Response.StatusCode = 200;
    await pContext.Response.WriteAsync("");
    _Context.Done();
  }
  private static async Task OnGet(HttpContext pContext)
  {
    var _Context = new DavContext(pContext);
    if (!_Context.Valid) return;
    if (!_Context.Exists) return;
    pContext.Response.Clear();
    pContext.Response.Headers["Content-Disposition"] = "attachment;filename=" + Path.GetFileName(_Context.Path);
    pContext.Response.Headers["Content-Length"] = new FileInfo(_Context.Vault.GetFileName(_Context.Path)).Length.ToString();
    pContext.Response.Headers["Content-Transfer-Encoding"] = "binary";
    ContentType.TryGetValue(Path.GetExtension(_Context.Path), out var t);
    pContext.Response.ContentType = t ?? "application/octet-stream";
    pContext.Response.StatusCode = 200;
    await _Context.Vault.CopyFrom(_Context.Path, pContext.Response.Body);
    _Context.Done();
  }
  private static async Task OnPut(HttpContext pContext)
  {
    var _Context = new DavContext(pContext);
    if (!_Context.Valid) return;
    await _Context.Vault.CopyTo(pContext.Request.Body, _Context.Path);
    pContext.Response.StatusCode = 201;
    await pContext.Response.WriteAsync("OK");
    _Context.Done();
  }
  private static async Task OnDelete(HttpContext pContext)
  {
    var _Context = new DavContext(pContext);
    if (!_Context.Valid) return;
    if (!_Context.Exists) return;
    _Context.Vault.Delete(_Context.Path);
    await pContext.Response.WriteAsync("OK");
    _Context.Done();
  }
  private static async Task OnLock(HttpContext pContext)
  {
    var _Context = new DavContext(pContext);
    if (!_Context.Valid) return;
    if (!_Context.Exists) return;
    if (WebDav.Locks.Contains(_Context.Path))
    {
      // pContext.Response.StatusCode = 423;
      // await pContext.Response.WriteAsync("Already locked");
      // _Context.Done();
      // return;
    }
    else
      WebDav.Locks.Add(_Context.Path);
    string _Body;
    using (var _Reader = new StreamReader(pContext.Request.Body))
      _Body = await _Reader.ReadToEndAsync();
    //<D:owner><D:href>USERNAME</D:href></D:owner>
    var _Matches = GetRegexMatch(_Body, @"<D:owner><D:href>(.*)</D:href></D:owner>");
    var _Token = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    pContext.Response.ContentType = "application/xml; charset=utf-8";
    pContext.Response.Headers["Lock-Token"] = "<" + _Token + ">";
    var _Response = @"<?xml version=""1.0"" encoding=""utf-8""?><D:prop xmlns:D=""DAV:""><D:lockdiscovery><D:activelock><D:locktype><D:write/></D:locktype><D:lockscope><D:exclusive/></D:lockscope><D:depth>infinity</D:depth><D:owner><D:href>" + _Matches + "</D:href></D:owner><D:timeout>Second-3600</D:timeout><D:locktoken><D:href>" + _Token + "</D:href></D:locktoken><D:lockroot><D:href>" + SanitizeXml(_Context.Path) + "</D:href></D:lockroot></D:activelock></D:lockdiscovery></D:prop>";
    pContext.Response.StatusCode = 200;
    await pContext.Response.WriteAsync(_Response);
    _Context.Done();
  }
  private static async Task OnUnlock(HttpContext pContext)
  {
    var _Context = new DavContext(pContext);
    if (!_Context.Valid) return;
    if (!_Context.Exists) return;
    if (!WebDav.Locks.Contains(_Context.Path))
    {
      pContext.Response.StatusCode = 404;
      await pContext.Response.WriteAsync("Not locked");
      _Context.Done();
      return;
    }
    WebDav.Locks.Remove(_Context.Path);
    pContext.Response.StatusCode = 204;
    _Context.Done();
  }
  private static async Task OnMkdir(HttpContext pContext)
  {
    var _Context = new DavContext(pContext);
    if (!_Context.Valid) return;
    if (!_Context.Vault.CreateDirectory(_Context.Path))
    {
      pContext.Response.StatusCode = 409;
      await pContext.Response.WriteAsync("Exists");
    }
    else
    {
      pContext.Response.StatusCode = 201;
      await pContext.Response.WriteAsync("Created");
    }
    _Context.Done();
  }
  private static async Task OnMove(HttpContext pContext)
  {
    var _Context = new DavContext(pContext);
    if (!_Context.Valid) return;
    if (!_Context.Exists) return;
    var _Target = new Uri(pContext.Request.Headers["Destination"][0]).LocalPath.Substring(_Context.Vault.Name.Length + 1); //Include /Vault portion
    _Context.Vault.Move(_Context.Path, _Target);
    await pContext.Response.WriteAsync("Moved");
    _Context.Done();
  }
  private static async Task OnFind(HttpContext pContext)
  {
    var _Context = new DavContext(pContext);
    if (!_Context.Valid) return;
    if (!_Context.Exists) return;
    var _Deep = pContext.Request.Headers["Depth"] == "1";
    pContext.Response.StatusCode = 200;
    var _Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    List<string> _Files = [_Context.Path];
    if (_Deep)
      _Files.AddRange(_Context.Vault.ScanDir(_Context.Path));
    var _Response = @"<?xml version=""1.0"" encoding=""UTF-8""?><D:multistatus xmlns:D=""DAV:"">";
    foreach (var _File in _Files)
    {
      if (_Context.Vault.IsHidden(_File)) continue;
      var _Info = new FileInfo(_Context.Vault.GetFileName(_File));
      if (_Info.Attributes.HasFlag(FileAttributes.Directory))
      {
        _Response += @$"<D:response><D:href>/{SanitizeXml(_File)}</D:href><D:propstat><D:prop><D:resourcetype><D:collection xmlns:D=""DAV:""/></D:resourcetype><D:displayname>{SanitizeXml(Path.GetFileName(_File))}</D:displayname><D:getlastmodified>{_Info.LastWriteTime.ToString("yyyy-MM-ddTHH:mm:ssK")}</D:getlastmodified><D:supportedlock><D:lockentry xmlns:D=""DAV:""><D:lockscope><D:exclusive/></D:lockscope><D:locktype><D:write/></D:locktype></D:lockentry></D:supportedlock></D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat></D:response>";
      }
      else
      {
        _Response += @$"<D:response><D:href>/{SanitizeXml(_File)}</D:href><D:propstat><D:prop><D:resourcetype></D:resourcetype><D:displayname>{SanitizeXml(Path.GetFileName(_File))}</D:displayname><D:getcontentlength>{_Info.Length}</D:getcontentlength><D:creationdate>{_Info.CreationTime.ToString("yyyy-MM-ddTHH:mm:ssK")}</D:creationdate><D:getlastmodified>{_Info.LastWriteTime.ToString("yyyy-MM-ddTHH:mm:ssK")}</D:getlastmodified><D:supportedlock><D:lockentry xmlns:D=""DAV:""><D:lockscope><D:exclusive/></D:lockscope><D:locktype><D:write/></D:locktype></D:lockentry></D:supportedlock></D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat></D:response>";
      }
    }
    _Response += "</D:multistatus>";
    pContext.Response.ContentType = "application/xml; charset=utf-8";
    await pContext.Response.WriteAsync(_Response);
    _Context.Done();
  }
  private static async Task OnPatch(HttpContext pContext)
  {
    var _Context = new DavContext(pContext);
    if (!_Context.Valid) return;
    if (!_Context.Exists) return;
    string _Body;
    using (var _Reader = new StreamReader(pContext.Request.Body))
      _Body = await _Reader.ReadToEndAsync();
    //<Z:Win32CreationTime>Fri, 07 Feb 2025 17:04:41 GMT</Z:Win32CreationTime>
    var _CreationTime = GetRegexMatch(_Body, @"<Z:Win32CreationTime>(.*)</Z:Win32CreationTime>");
    var _AccessTime = GetRegexMatch(_Body, @"<Z:Win32LastAccessTime>(.*)</Z:Win32LastAccessTime>");
    var _ModifiedTime = GetRegexMatch(_Body, @"<Z:Win32LastModifiedTime>(.*)</Z:Win32LastModifiedTime>");
    var _FileAttributes = GetRegexMatch(_Body, @"<Z:Win32FileAttributes>(.*)</Z:Win32FileAttributes>");

    pContext.Response.ContentType = "application/xml; charset=utf-8";
    var _Response = @"<?xml version=""1.0"" encoding=""UTF-8""?><D:multistatus xmlns:D=""DAV:""><D:response><D:href>" + SanitizeXml(_Context.Path) + @"</D:href><D:propstat><D:prop>";
    var _Patches = "";
    if (new FileInfo(_Context.Vault.GetFileName(_Context.Path)).Attributes.HasFlag(FileAttributes.Archive))
    {
      if (!string.IsNullOrEmpty(_CreationTime))
      {
        _Response += @"<Win32CreationTime xmlns=""urn:schemas-microsoft-com:""></Win32CreationTime>";
        _Patches += "CreationTime,";
      }
      if (!string.IsNullOrEmpty(_AccessTime))
      {
        _Response += @"<Win32LastAccessTime xmlns=""urn:schemas-microsoft-com:""></Win32LastAccessTime>";
        _Patches += "AccessTime,";
      }
      if (!string.IsNullOrEmpty(_ModifiedTime))
      {
        _Response += @"<Win32LastModifiedTime xmlns=""urn:schemas-microsoft-com:""></Win32LastModifiedTime>";
        _Patches += "WriteTime,";
      }
      if (!string.IsNullOrEmpty(_FileAttributes))
      {
        _Response += @"<Win32FileAttributes xmlns=""urn:schemas-microsoft-com:""></Win32FileAttributes>";
        _Patches += "Attributes,";
      }
      _Context.Vault.Update(_Context.Path, _CreationTime, _ModifiedTime, _AccessTime);
    }
    _Response += @"</D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat></D:response></D:multistatus>";
    await pContext.Response.WriteAsync(_Response);
    _Context.Done();
  }
}