using System;
using System.IO;
using Microsoft.Web.WebView2.Core;

namespace ZephyrDesktop.Services;

public sealed class WebViewEnvironmentService
{
    private CoreWebView2Environment? _environment;
    private readonly string _userDataFolder;

    public WebViewEnvironmentService(string? userDataFolder = null)
    {
        _userDataFolder = userDataFolder ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "ZephyrDesktop", "WebView2Data");
    }

    public async Task InitializeAsync()
    {
        if (_environment != null) return;

        var dir = Path.GetDirectoryName(_userDataFolder)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        _environment = await CoreWebView2Environment.CreateAsync(null, _userDataFolder, null);
    }

    public CoreWebView2Environment Environment => _environment
        ?? throw new InvalidOperationException("WebView2 环境未初始化，请先调用 InitializeAsync");

    public string EditorTemplateHtml { get; private set; } = "";

    public async Task LoadEditorTemplateAsync()
    {
        var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "editor-template.html");
        if (File.Exists(templatePath))
        {
            EditorTemplateHtml = await File.ReadAllTextAsync(templatePath);
        }
        else
        {
            EditorTemplateHtml = GetDefaultEditorTemplate();
        }
    }

    private static string GetDefaultEditorTemplate()
    {
        return @"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<style>
:root { --note-bg-light: #FFF9E5; --note-bg: #FEF3C7; }
* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: 'Microsoft YaHei', sans-serif; font-size: 14px; color: #1f2937; padding: 8px; outline: none; min-height: 100px; background: var(--note-bg-light); }
.toolbar { display: flex; gap: 4px; padding: 4px 0; border-bottom: 1px solid rgba(0,0,0,0.08); margin-bottom: 8px; background: var(--note-bg-light); }
.toolbar button { border: 1px solid rgba(0,0,0,0.1); background: var(--note-bg-light); border-radius: 4px; padding: 2px 8px; cursor: pointer; font-size: 13px; }
.toolbar button:hover { background: var(--note-bg); }
#editor { outline: none; min-height: 80px; line-height: 1.6; background: var(--note-bg-light); }
#editor:focus { outline: none; }
#editor b, #editor strong { font-weight: 600; }
#editor ul { padding-left: 20px; }
#editor a { color: #3b82f6; text-decoration: underline; }
</style>
</head>
<body>
<div class=""toolbar"">
<button onclick=""execCmd('bold')"" title=""加粗""><b>B</b></button>
<button onclick=""execCmd('italic')"" title=""斜体""><i>I</i></button>
<button onclick=""execCmd('insertUnorderedList')"" title=""列表"">·</button>
<button onclick=""insertLink()"" title=""链接"">🔗</button>
</div>
<div id=""editor"" contenteditable=""true""></div>
<script>
function execCmd(cmd) { document.execCommand(cmd, false, null); }
function insertLink() { var url = prompt('请输入链接地址:','http://'); if(url) document.execCommand('createLink',false,url); }
function setContent(html) { document.getElementById('editor').innerHTML = html; }
function getContent() { return document.getElementById('editor').innerHTML; }
function triggerEdit() { document.getElementById('editor').focus(); }
function sendHtml() { window.chrome.webview.postMessage(getContent()); }
function setNoteColors(bgLight, bg) { document.documentElement.style.setProperty('--note-bg-light', bgLight); document.documentElement.style.setProperty('--note-bg', bg); }
document.getElementById('editor').addEventListener('input', function() {
    window.chrome.webview.postMessage(JSON.stringify({type:'input',html:getContent()}));
});
document.getElementById('editor').addEventListener('focus', function() {
    window.chrome.webview.postMessage(JSON.stringify({type:'focus'}));
});
document.getElementById('editor').addEventListener('blur', function() {
    window.chrome.webview.postMessage(JSON.stringify({type:'blur',html:getContent()}));
});
</script>
</body>
</html>";
    }
}
