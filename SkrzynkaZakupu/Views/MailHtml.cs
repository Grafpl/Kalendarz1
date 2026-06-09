using System.Net;
using Kalendarz1.SkrzynkaZakupu.Models;

namespace Kalendarz1.SkrzynkaZakupu.Views
{
    /// <summary>Budowanie HTML do podglądu treści maila (wspólne dla podglądu i czytnika).</summary>
    public static class MailHtml
    {
        public static string Inner(MailBodyModel body)
        {
            if (!string.IsNullOrWhiteSpace(body.HtmlBody)) return body.HtmlBody;
            return "<pre style='white-space:pre-wrap;word-wrap:break-word;font-family:Segoe UI,Arial;'>"
                   + WebUtility.HtmlEncode(body.TextBody) + "</pre>";
        }

        public static string Wrap(string inner)
            => "<!DOCTYPE html><html><head><meta charset='utf-8'>" +
               "<meta name='viewport' content='width=device-width, initial-scale=1'>" +
               "<base target='_blank'>" +
               "<style>" +
               "html,body{margin:0;padding:0;}" +
               "body{font-family:'Segoe UI',Arial,sans-serif;font-size:14px;line-height:1.6;color:#1f2933;" +
               "padding:18px 22px;max-width:900px;-webkit-font-smoothing:antialiased;}" +
               "a{color:#2E7D32;}img{max-width:100%;height:auto;}" +
               "blockquote{margin:8px 0;padding:4px 14px;border-left:3px solid #E5E9EF;color:#64748b;}" +
               "pre{white-space:pre-wrap;word-wrap:break-word;font-family:'Segoe UI',Arial;}" +
               "table{max-width:100%;}" +
               "::-webkit-scrollbar{width:10px;height:10px;}::-webkit-scrollbar-thumb{background:#CBD5E1;border-radius:5px;}" +
               "</style></head><body>" + inner + "</body></html>";

        public static string Full(MailBodyModel body) => Wrap(Inner(body));
    }
}
