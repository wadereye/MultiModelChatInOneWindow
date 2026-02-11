using System;
using System.Collections.Generic;

namespace MultiModelChat
{
    public enum Language
    {
        Chinese,
        English
    }

    public static class LanguageManager
    {
        private static Language _currentLanguage = Language.Chinese;
        public static event Action LanguageChanged;

        public static Language CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    LanguageChanged?.Invoke();
                }
            }
        }

        private static readonly Dictionary<string, Dictionary<Language, string>> _resources = new Dictionary<string, Dictionary<Language, string>>
        {
            // 窗体标题
            ["AppTitle"] = new Dictionary<Language, string> { { Language.Chinese, "AI一问多答" }, { Language.English, "AI Multi-Model Q&A" } },
            
            // 菜单
            ["MenuFile"] = new Dictionary<Language, string> { { Language.Chinese, "文件(&F)" }, { Language.English, "&File" } },
            ["MenuConfig"] = new Dictionary<Language, string> { { Language.Chinese, "配置网页(&C)" }, { Language.English, "&Configure Websites" } },
            ["MenuHistory"] = new Dictionary<Language, string> { { Language.Chinese, "查看历史(&H)" }, { Language.English, "View &History" } },
            ["MenuExit"] = new Dictionary<Language, string> { { Language.Chinese, "退出(&X)" }, { Language.English, "E&xit" } },
            
            ["MenuAction"] = new Dictionary<Language, string> { { Language.Chinese, "操作(&A)" }, { Language.English, "&Action" } },
            ["MenuSendQuery"] = new Dictionary<Language, string> { { Language.Chinese, "发送查询(&S)" }, { Language.English, "&Send Query" } },
            ["MenuClearInput"] = new Dictionary<Language, string> { { Language.Chinese, "清除输入(&L)" }, { Language.English, "C&lear Input" } },
            ["MenuRestoreView"] = new Dictionary<Language, string> { { Language.Chinese, "恢复分栏视图(&R)" }, { Language.English, "&Restore Split View" } },
            
            ["MenuView"] = new Dictionary<Language, string> { { Language.Chinese, "视图(&V)" }, { Language.English, "&View" } },
            ["MenuExpand"] = new Dictionary<Language, string> { { Language.Chinese, "展开" }, { Language.English, "Expand" } },
            ["MenuCollapse"] = new Dictionary<Language, string> { { Language.Chinese, "收缩" }, { Language.English, "Collapse" } },
            
            ["MenuHelp"] = new Dictionary<Language, string> { { Language.Chinese, "帮助(&H)" }, { Language.English, "&Help" } },
            ["MenuAbout"] = new Dictionary<Language, string> { { Language.Chinese, "关于(&A)" }, { Language.English, "&About" } },
            ["MenuLanguage"] = new Dictionary<Language, string> { { Language.Chinese, "语言(&L)" }, { Language.English, "&Language" } },
            
            // 工具栏
            ["ToolSearch"] = new Dictionary<Language, string> { { Language.Chinese, "查询" }, { Language.English, "Search" } },
            ["ToolSearchTip"] = new Dictionary<Language, string> { { Language.Chinese, "发送问题到所有选中的模型" }, { Language.English, "Send question to all selected models" } },
            ["ToolClear"] = new Dictionary<Language, string> { { Language.Chinese, "清除" }, { Language.English, "Clear" } },
            ["ToolClearTip"] = new Dictionary<Language, string> { { Language.Chinese, "清除输入框内容" }, { Language.English, "Clear input content" } },
            ["ToolConfig"] = new Dictionary<Language, string> { { Language.Chinese, "配置" }, { Language.English, "Config" } },
            ["ToolConfigTip"] = new Dictionary<Language, string> { { Language.Chinese, "配置要打开的网页列表" }, { Language.English, "Configure website list" } },
            ["ToolHistory"] = new Dictionary<Language, string> { { Language.Chinese, "历史" }, { Language.English, "History" } },
            ["ToolHistoryTip"] = new Dictionary<Language, string> { { Language.Chinese, "查看查询历史记录" }, { Language.English, "View query history" } },
            ["ToolSplit"] = new Dictionary<Language, string> { { Language.Chinese, "分栏" }, { Language.English, "Split" } },
            ["ToolSplitTip"] = new Dictionary<Language, string> { { Language.Chinese, "恢复分栏视图" }, { Language.English, "Restore split view" } },
            ["ToolExpand"] = new Dictionary<Language, string> { { Language.Chinese, "展开" }, { Language.English, "Expand" } },
            ["ToolCollapse"] = new Dictionary<Language, string> { { Language.Chinese, "收缩" }, { Language.English, "Collapse" } },
            ["ToolExpandTip"] = new Dictionary<Language, string> { { Language.Chinese, "展开到全屏" }, { Language.English, "Expand to fullscreen" } },
            ["ToolCollapseTip"] = new Dictionary<Language, string> { { Language.Chinese, "恢复分栏视图" }, { Language.English, "Restore split view" } },
            ["ToolCopy"] = new Dictionary<Language, string> { { Language.Chinese, "复制" }, { Language.English, "Copy" } },
            ["ToolTranslate"] = new Dictionary<Language, string> { { Language.Chinese, "翻译" }, { Language.English, "Translate" } },
            
            // 输入框
            ["InputPlaceholder"] = new Dictionary<Language, string> { { Language.Chinese, "请输入您的问题..." }, { Language.English, "Enter your question..." } },
            
            // 对话框
            ["ConfigTitle"] = new Dictionary<Language, string> { { Language.Chinese, "配置网页列表" }, { Language.English, "Configure Websites" } },
            ["HistoryTitle"] = new Dictionary<Language, string> { { Language.Chinese, "查询历史" }, { Language.English, "Query History" } },
            ["AboutTitle"] = new Dictionary<Language, string> { { Language.Chinese, "关于" }, { Language.English, "About" } },
            ["AboutAuthor"] = new Dictionary<Language, string> { { Language.Chinese, "软件作者：跋涉者" }, { Language.English, "Author: Bashe" } },
            ["AboutEmail"] = new Dictionary<Language, string> { { Language.Chinese, "邮箱：wader129@qq.com" }, { Language.English, "Email: wader129@qq.com" } },
            ["AboutVersion"] = new Dictionary<Language, string> { { Language.Chinese, "当前版本：V2.0" }, { Language.English, "Version: V2.0" } },
            
            // 按钮
            ["BtnSave"] = new Dictionary<Language, string> { { Language.Chinese, "保存配置" }, { Language.English, "Save" } },
            ["BtnCancel"] = new Dictionary<Language, string> { { Language.Chinese, "取消" }, { Language.English, "Cancel" } },
            ["BtnAdd"] = new Dictionary<Language, string> { { Language.Chinese, "添加条目" }, { Language.English, "Add" } },
            ["BtnDelete"] = new Dictionary<Language, string> { { Language.Chinese, "删除选中" }, { Language.English, "Delete" } },
            ["BtnOK"] = new Dictionary<Language, string> { { Language.Chinese, "确定" }, { Language.English, "OK" } },
            
            // 表格列
            ["ColEnabled"] = new Dictionary<Language, string> { { Language.Chinese, "启用" }, { Language.English, "Enabled" } },
            ["ColName"] = new Dictionary<Language, string> { { Language.Chinese, "名称" }, { Language.English, "Name" } },
            ["ColUrl"] = new Dictionary<Language, string> { { Language.Chinese, "网页地址" }, { Language.English, "URL" } },
            
            // 消息
            ["MsgEnterQuestion"] = new Dictionary<Language, string> { { Language.Chinese, "请输入问题内容" }, { Language.English, "Please enter your question" } },
            ["MsgTip"] = new Dictionary<Language, string> { { Language.Chinese, "提示" }, { Language.English, "Info" } },
            ["MsgError"] = new Dictionary<Language, string> { { Language.Chinese, "错误" }, { Language.English, "Error" } },
            ["MsgWarning"] = new Dictionary<Language, string> { { Language.Chinese, "警告" }, { Language.English, "Warning" } },
            ["MsgUrlInvalid"] = new Dictionary<Language, string> { { Language.Chinese, "URL 格式不正确" }, { Language.English, "Invalid URL format" } },
            ["MsgTooManySites"] = new Dictionary<Language, string> { { Language.Chinese, "您选中了超过3个网页，可能会导致界面非常拥挤，是否继续？" }, { Language.English, "You have selected more than 3 websites, which may cause the interface to be crowded. Continue?" } },
            ["MsgNewSite"] = new Dictionary<Language, string> { { Language.Chinese, "新网页" }, { Language.English, "New Site" } },
            
            // 左侧面板相关
            ["ToggleSidebar"] = new Dictionary<Language, string> { { Language.Chinese, "\u25C0 \u6536\u7f29" }, { Language.English, "\u25C0 Collapse" } },
            ["ExpandSidebar"] = new Dictionary<Language, string> { { Language.Chinese, "\u25B6" }, { Language.English, "\u25B6" } },
            ["SectionNetwork"] = new Dictionary<Language, string> { { Language.Chinese, "网络查询" }, { Language.English, "Web Query" } },
            
            // 搜索相关
            ["BtnSearch"] = new Dictionary<Language, string> { { Language.Chinese, "搜索" }, { Language.English, "Search" } },
        };

        public static string Get(string key)
        {
            if (_resources.TryGetValue(key, out var langDict))
            {
                if (langDict.TryGetValue(_currentLanguage, out var value))
                {
                    return value;
                }
            }
            return key; // 返回key作为fallback
        }
    }
}
