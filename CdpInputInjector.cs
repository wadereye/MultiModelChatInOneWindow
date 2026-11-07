using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.WinForms;

namespace MultiModelChat
{
    /// <summary>
    /// 通过 WebView2 内置 CDP 通道注入文本并发送。
    /// 每个 WebView 拥有独立的渲染进程焦点，Input.insertText / dispatchKeyEvent
    /// 直达指定会话，不占用系统焦点与剪贴板，因此多个站点可安全并行调用。
    /// </summary>
    public static class CdpInputInjector
    {
        public class InjectResult
        {
            public bool Success { get; set; }
            public string Method { get; set; } = "";
            public string Reason { get; set; } = "";
        }

        public class PrepResult
        {
            public bool SidebarCollapsed { get; set; }
            public int PopupsClosed { get; set; }
            public string Reason { get; set; } = "";
        }

        private class SiteAdapter
        {
            public string[] Input { get; set; } = Array.Empty<string>();
            public string[] Send { get; set; } = Array.Empty<string>();
            public string[] Collapse { get; set; } = Array.Empty<string>();
        }

        // 按站点域名关键字匹配的已知选择器；失配时回退通用列表
        private static readonly Dictionary<string, SiteAdapter> Adapters = new Dictionary<string, SiteAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["chatgpt"] = new SiteAdapter
            {
                Input = new[] { "#prompt-textarea", "div[contenteditable='true'][role='textbox']", "textarea" },
                Send = new[] { "button[data-testid='send-button']", "#composer-submit-button", "button[aria-label*='Send']" }
            },
            ["deepseek"] = new SiteAdapter
            {
                Input = new[] { "textarea#chat-input", "textarea[placeholder]", "textarea" },
                Send = new[] { "div[role='button'][class*='send']", "button[type='submit']", "button[aria-label*='发送']" },
                // 左栏折叠：优先明确“收起/折叠”语义，其次侧栏切换按钮
                Collapse = new[] { "button[aria-label*='收起']", "button[title*='收起']", "button[aria-label*='折叠']", "[aria-label*='collapse' i]", "[title*='collapse' i]" }
            },
            ["gemini"] = new SiteAdapter
            {
                Input = new[] { "div.ql-editor[contenteditable='true']", "rich-textarea div[contenteditable='true']", "div[contenteditable='true']", "textarea" },
                Send = new[] { "button.send-button", "button[aria-label*='Send']", "button[aria-label*='发送']" }
            },
            ["kimi"] = new SiteAdapter
            {
                Input = new[] { "div[contenteditable='true'][role='textbox']", "div[contenteditable='true']", "textarea" },
                Send = new[] { "div[class*='send-button']", "button[aria-label*='发送']", "button[type='submit']" }
            },
            ["tongyi"] = new SiteAdapter
            {
                Input = new[] { "textarea[placeholder*='输入']", "div[contenteditable='true']", "textarea" },
                Send = new[] { "button[aria-label*='发送']", "button[type='submit']", "div[class*='send']" },
                // 千问（现 www.qianwen.com）：实测折叠按钮 aria-label="收起侧边栏"
                Collapse = new[] { "button[aria-label='收起侧边栏']", "button[aria-label*='收起']", "button[title*='收起']", "[aria-label*='collapse' i]" }
            },
            ["doubao"] = new SiteAdapter
            {
                Input = new[] { "textarea[placeholder*='输入']", "div[contenteditable='true']", "textarea" },
                Send = new[] { "button[data-testid*='send']", "button[aria-label*='发送']", "button[type='submit']" },
                // 豆包：侧栏切换按钮 aria-label 随状态变化（显示侧栏/恢复为侧栏），
                // 脚本会先确认侧栏处于展开态且标签不是“显示/展开”动作后才点击。
                Collapse = new[] { "button[aria-label*='收起']", "button[aria-label*='侧栏']", "button[aria-label*='sidebar' i]" }
            },
            ["yuanbao"] = new SiteAdapter
            {
                Input = new[] { "div[contenteditable='true']", "textarea[placeholder*='输入']", "textarea" },
                Send = new[] { "button[aria-label*='发送']", "div[class*='send']", "button[type='submit']" }
            }
        };

        private static readonly string[] GenericInput =
        {
            "textarea[placeholder*='输入']", "textarea[aria-label*='输入']",
            "textarea[placeholder*='Ask']", "textarea[placeholder*='Send']",
            "textarea[placeholder*='Message']", "textarea[placeholder*='Type']",
            "div[contenteditable='true'][role='textbox']", "div[contenteditable='true']", "textarea"
        };

        private static readonly string[] GenericSend =
        {
            "button[aria-label*='发送']", "button[aria-label*='Send']",
            "button[data-testid*='send']", "button[type='submit']"
        };

        private static readonly string[] GenericCollapse =
        {
            "button[aria-label*='收起']", "button[aria-label*='折叠']",
            "button[title*='收起']", "button[title*='折叠']",
            "button[aria-label*='collapse' i]", "button[title*='collapse' i]",
            "button[aria-label*='隐藏侧']", "button[aria-label*='hide sidebar' i]"
        };

        private static SiteAdapter ResolveAdapter(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                foreach (var kv in Adapters)
                {
                    if (url.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                        return kv.Value;
                }
            }
            return new SiteAdapter { Input = GenericInput, Send = GenericSend };
        }

        // 组合站点选择器与通用兜底，去重
        private static string[] Combine(string[] primary, string[] fallback)
        {
            var seen = new HashSet<string>();
            var list = new List<string>();
            foreach (var s in primary) if (seen.Add(s)) list.Add(s);
            foreach (var s in fallback) if (seen.Add(s)) list.Add(s);
            return list.ToArray();
        }

        /// <summary>
        /// 在单个 WebView 内完成：定位并聚焦输入框 → CDP 写入 → 校验 → 发送 → 校验清空。
        /// 失败时会清空已写入内容，保证回退路径从干净状态开始，避免重复发送。
        /// </summary>
        public static async Task<InjectResult> InjectAsync(WebView2 wv, string url, string question)
        {
            var result = new InjectResult();
            try
            {
                if (wv?.CoreWebView2 == null)
                {
                    result.Reason = "core_not_ready";
                    return result;
                }

                var adapter = ResolveAdapter(url);
                string inputSels = JsonSerializer.Serialize(Combine(adapter.Input, GenericInput));
                string sendSels = JsonSerializer.Serialize(Combine(adapter.Send, GenericSend));

                // 0. 先关闭广告/弹窗，避免其抢占焦点或挡住输入框；随后才去找输入框并回车
                try { await EvalJsonAsync(wv, BuildDismissPopupsScript()); } catch { }
                await Task.Delay(120);

                // 1. 定位 + 聚焦 + 全选（为覆盖写入做准备）
                // 刚导航的页面可能仍在加载，轮询等待输入框出现（最长约 10 秒）
                string focusScript = BuildFocusScript(inputSels);
                JsonDocument? focusInfo = null;
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    focusInfo = await EvalJsonAsync(wv, focusScript);
                    if (focusInfo != null && focusInfo.RootElement.TryGetProperty("found", out var f) && f.GetBoolean())
                        break;
                    focusInfo = null;
                    await Task.Delay(500);
                }
                if (focusInfo == null)
                {
                    result.Reason = "no_input";
                    return result;
                }
                await Task.Delay(80);

                // 2. CDP 写入（trusted beforeinput/input，React/ProseMirror 可识别）
                string insertParams = JsonSerializer.Serialize(new { text = question });
                await wv.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.insertText", insertParams);
                await Task.Delay(120);

                // 3. 读回校验
                var readBack = await EvalJsonAsync(wv, BuildReadScript(inputSels));
                int len = readBack?.RootElement.TryGetProperty("len", out var l) == true ? l.GetInt32() : 0;
                if (len <= 0)
                {
                    result.Reason = "insert_empty";
                    return result;
                }

                // 4. 发送：先 trusted Enter
                await DispatchEnterAsync(wv);
                await Task.Delay(300);
                if (await IsInputClearedAsync(wv, inputSels))
                {
                    result.Success = true;
                    result.Method = "cdp_enter";
                    return result;
                }

                // 5. Enter 未生效 → 点击发送按钮
                var clickInfo = await EvalJsonAsync(wv, BuildClickSendScript(sendSels));
                bool clicked = clickInfo?.RootElement.TryGetProperty("clicked", out var c) == true && c.GetBoolean();
                await Task.Delay(300);
                if (await IsInputClearedAsync(wv, inputSels))
                {
                    result.Success = true;
                    result.Method = clicked ? "cdp_button" : "cdp_enter_delayed";
                    return result;
                }

                // 未能确认发送：清空已写入内容，交回回退路径处理
                await EvalJsonAsync(wv, BuildClearScript(inputSels));
                result.Reason = "not_submitted";
                return result;
            }
            catch (Exception ex)
            {
                result.Reason = "exception:" + ex.Message;
                return result;
            }
        }

        /// <summary>
        /// 页面加载后的一次性准备：关闭广告/弹窗 + 折叠左侧栏（幂等，已折叠则跳过）。
        /// 由 NavigationCompleted 调用；内部会等待 SPA 渲染完成再尝试点击。
        /// </summary>
        public static async Task<PrepResult> PrepareAsync(WebView2 wv, string url)
        {
            var res = new PrepResult { Reason = "ok" };
            try
            {
                if (wv?.CoreWebView2 == null) { res.Reason = "core_not_ready"; return res; }

                // 等待主文档就绪
                for (int i = 0; i < 10; i++)
                {
                    var ready = await EvalJsonAsync(wv, "JSON.stringify({s:document.readyState})");
                    string st = ready != null && ready.RootElement.TryGetProperty("s", out var s) ? (s.GetString() ?? "") : "";
                    if (st == "complete" || st == "interactive") break;
                    await Task.Delay(300);
                }

                // 关闭广告/弹窗（可能叠多层，最多 4 轮）
                for (int i = 0; i < 4; i++)
                {
                    var d = await EvalJsonAsync(wv, BuildDismissPopupsScript());
                    int closed = d != null && d.RootElement.TryGetProperty("closed", out var c) ? c.GetInt32() : 0;
                    res.PopupsClosed += closed;
                    if (closed <= 0) break;
                    await Task.Delay(350);
                }
                await Task.Delay(150);

                // 折叠左侧栏（SPA 渲染有延迟，轮询重试；确认折叠后立即停止，避免反复切换）
                var adapter = ResolveAdapter(url);
                string collapseSels = JsonSerializer.Serialize(Combine(adapter.Collapse, GenericCollapse));
                for (int i = 0; i < 8; i++)
                {
                    var r = await EvalJsonAsync(wv, BuildCollapseSidebarScript(collapseSels));
                    bool did = r != null && r.RootElement.TryGetProperty("collapsed", out var cc) && cc.GetBoolean();
                    if (did)
                    {
                        res.SidebarCollapsed = true;
                        res.Reason = r!.RootElement.TryGetProperty("via", out var v) ? (v.GetString() ?? "ok") : "ok";
                        break;
                    }
                    await Task.Delay(500);
                }
                return res;
            }
            catch (Exception ex)
            {
                res.Reason = "exception:" + ex.Message;
                return res;
            }
        }

        // 关闭弹层中的关闭/跳过/同意类按钮；仅在弹层内部点击，避免误伤正文
        private static string BuildDismissPopupsScript()
        {
            return @"(function(){
                var overlaySel = '[role=dialog],[class*=modal],[class*=Modal],[class*=popup],[class*=Popup],[class*=overlay],[class*=Overlay],[class*=mask],[class*=Mask],[class*=dialog],[class*=Dialog]';
                function vis(el){ if(!el) return false; var r=el.getBoundingClientRect(); if(r.width<4||r.height<4) return false; if(r.right<=0||r.bottom<=0||r.left>=window.innerWidth||r.top>=window.innerHeight) return false; var s=getComputedStyle(el); return s.visibility!=='hidden' && s.display!=='none' && parseFloat(s.opacity||'1')>0.1; }
                function labelOf(b){ return ((b.getAttribute('aria-label')||'')+' '+(b.getAttribute('title')||'')+' '+((b.innerText||'').trim().slice(0,24))); }
                var tier1 = /(关闭|close|跳过|skip|知道了|我知道了|以后再说|暂不|稍后|later|not now|不需要)/i;
                var tier2 = /(同意|接受|accept|agree|允许|allow)/i;
                var clicked = 0;
                function clickFrom(scope, re){
                    var btns = scope.querySelectorAll('button,[role=button],[aria-label],[title]');
                    for (var i=0;i<btns.length;i++){ var b=btns[i]; if(!vis(b)) continue; if(re.test(labelOf(b))){ try{ b.click(); clicked++; return true; }catch(e){} } }
                    return false;
                }
                var overlays = [];
                var all = document.querySelectorAll(overlaySel);
                for (var i=0;i<all.length;i++){ if(vis(all[i])) overlays.push(all[i]); }
                for (var k=0;k<overlays.length && clicked<4;k++){
                    if (clickFrom(overlays[k], tier1)) continue;
                    clickFrom(overlays[k], tier2);
                }
                return JSON.stringify({closed: clicked});
            })();";
        }

        // 折叠左侧栏：优先明确“收起/折叠”按钮；否则仅当侧栏确为展开态才点侧栏切换按钮
        private static string BuildCollapseSidebarScript(string selsJson)
        {
            return @"(function(){
                var siteSels = " + selsJson + @";
                function vis(el){ if(!el) return false; var r=el.getBoundingClientRect(); if(r.width<4||r.height<4) return false; if(r.right<=0||r.bottom<=0||r.left>=window.innerWidth||r.top>=window.innerHeight) return false; var s=getComputedStyle(el); return s.visibility!=='hidden' && s.display!=='none' && parseFloat(s.opacity||'1')>0.1; }
                function labelOf(el){ return ((el.getAttribute('aria-label')||'')+' '+(el.getAttribute('title')||'')+' '+((el.innerText||'').trim().slice(0,12))); }
                var explicitKw = /(收起|折叠|collapse|hide|隐藏侧|关闭侧)/i;
                var openKw = /(显示|展开|show|expand)/i;
                var sideKw = /(侧栏|sidebar|侧边栏|menu|菜单)/i;
                // 2) 模糊侧栏切换：仅在左上角、且标签不是“显示/展开”动作时点击（这类标签才代表“收起”）
                for (var s2=0;s2<siteSels.length;s2++){
                    var ns=document.querySelectorAll(siteSels[s2]);
                    for (var i2=0;i2<ns.length;i2++){ var n2=ns[i2]; if(!vis(n2)) continue; var lb2=labelOf(n2); if(sideKw.test(lb2) && !openKw.test(lb2)){ var r2=n2.getBoundingClientRect(); if(r2.left<400 && r2.top<200){ try{ n2.click(); return JSON.stringify({collapsed:true, via:'toggle'}); }catch(e){} } } }
                }
                return JSON.stringify({collapsed:false, reason:'already_collapsed_or_no_button'});
            })();";
        }

        private static async Task DispatchEnterAsync(WebView2 wv)
        {
            const string down = "{\"type\":\"rawKeyDown\",\"key\":\"Enter\",\"code\":\"Enter\",\"windowsVirtualKeyCode\":13,\"nativeVirtualKeyCode\":13}";
            const string up = "{\"type\":\"keyUp\",\"key\":\"Enter\",\"code\":\"Enter\",\"windowsVirtualKeyCode\":13,\"nativeVirtualKeyCode\":13}";
            await wv.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", down);
            await wv.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", up);
        }

        private static async Task<bool> IsInputClearedAsync(WebView2 wv, string inputSels)
        {
            var info = await EvalJsonAsync(wv, BuildReadScript(inputSels));
            if (info == null) return false;
            if (!info.RootElement.TryGetProperty("found", out var f) || !f.GetBoolean()) return true;
            return info.RootElement.TryGetProperty("len", out var l) && l.GetInt32() == 0;
        }

        // 定位第一个可见输入框并聚焦、全选
        private static string BuildFocusScript(string selsJson)
        {
            return @"(function(){
                var sels = " + selsJson + @";
                var el = null;
                for (var s = 0; s < sels.length && !el; s++){
                    var nodes = document.querySelectorAll(sels[s]);
                    for (var i = 0; i < nodes.length; i++){
                        var n = nodes[i];
                        if (n.offsetHeight > 0 && n.offsetWidth > 0){ el = n; break; }
                    }
                }
                if (!el) return JSON.stringify({found:false});
                el.focus();
                var tag = el.tagName.toLowerCase();
                if (tag === 'textarea' || tag === 'input'){
                    el.select();
                } else {
                    try {
                        var range = document.createRange();
                        range.selectNodeContents(el);
                        var sel = window.getSelection();
                        sel.removeAllRanges();
                        sel.addRange(range);
                    } catch(e){}
                }
                return JSON.stringify({found:true, tag:tag});
            })();";
        }

        // 读回输入框内容长度
        private static string BuildReadScript(string selsJson)
        {
            return @"(function(){
                var sels = " + selsJson + @";
                var el = null;
                for (var s = 0; s < sels.length && !el; s++){
                    var nodes = document.querySelectorAll(sels[s]);
                    for (var i = 0; i < nodes.length; i++){
                        var n = nodes[i];
                        if (n.offsetHeight > 0 && n.offsetWidth > 0){ el = n; break; }
                    }
                }
                if (!el) return JSON.stringify({found:false, len:0});
                var tag = el.tagName.toLowerCase();
                var v = (tag === 'textarea' || tag === 'input') ? (el.value || '') : (el.textContent || '');
                return JSON.stringify({found:true, len:v.trim().length});
            })();";
        }

        // 点击第一个可用发送按钮
        private static string BuildClickSendScript(string selsJson)
        {
            return @"(function(){
                var sels = " + selsJson + @";
                for (var s = 0; s < sels.length; s++){
                    var nodes = document.querySelectorAll(sels[s]);
                    for (var i = 0; i < nodes.length; i++){
                        var n = nodes[i];
                        if (n.offsetHeight > 0 && !n.disabled && n.getAttribute('aria-disabled') !== 'true'){
                            n.click();
                            return JSON.stringify({clicked:true});
                        }
                    }
                }
                return JSON.stringify({clicked:false});
            })();";
        }

        // 清空输入框（失败回退前复位）
        private static string BuildClearScript(string selsJson)
        {
            return @"(function(){
                var sels = " + selsJson + @";
                var el = null;
                for (var s = 0; s < sels.length && !el; s++){
                    var nodes = document.querySelectorAll(sels[s]);
                    for (var i = 0; i < nodes.length; i++){
                        var n = nodes[i];
                        if (n.offsetHeight > 0 && n.offsetWidth > 0){ el = n; break; }
                    }
                }
                if (!el) return JSON.stringify({cleared:false});
                el.focus();
                var tag = el.tagName.toLowerCase();
                if (tag === 'textarea' || tag === 'input'){
                    var setter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, 'value')
                              || Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value');
                    if (setter && setter.set) setter.set.call(el, ''); else el.value = '';
                    el.dispatchEvent(new Event('input', {bubbles:true}));
                } else {
                    try {
                        var range = document.createRange();
                        range.selectNodeContents(el);
                        var sel = window.getSelection();
                        sel.removeAllRanges(); sel.addRange(range);
                        document.execCommand('delete', false, null);
                    } catch(e){ el.textContent = ''; }
                    el.dispatchEvent(new Event('input', {bubbles:true}));
                }
                return JSON.stringify({cleared:true});
            })();";
        }

        // ExecuteScriptAsync 返回的是 JSON 编码后的字符串，先解外层再解析内层 JSON
        private static async Task<JsonDocument?> EvalJsonAsync(WebView2 wv, string script)
        {
            try
            {
                string raw = await wv.ExecuteScriptAsync(script);
                if (string.IsNullOrEmpty(raw) || raw == "null") return null;
                string? inner = JsonSerializer.Deserialize<string>(raw);
                if (string.IsNullOrEmpty(inner)) return null;
                return JsonDocument.Parse(inner);
            }
            catch
            {
                return null;
            }
        }
    }
}