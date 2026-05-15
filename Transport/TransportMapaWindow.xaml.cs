using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Windows;

namespace Kalendarz1.Transport
{
    public partial class TransportMapaWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private readonly DateTime _data;
        private readonly StringBuilder _debugLog = new();

        private const double BazaLat = 51.907335;
        private const double BazaLng = 19.678605;
        // ApiKey wycofany do Maps/GoogleMapsConfig (Faza 4-A) â€” czytane z %LOCALAPPDATA%\Kalendarz1\Maps\secrets.json
        private static string ApiKey => Kalendarz1.Maps.GoogleMapsConfig.ApiKey;

        private static readonly HttpClient _http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        { Timeout = TimeSpan.FromSeconds(15) };

        public TransportMapaWindow(string connLibra, string connHandel, DateTime data)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _connLibra = connLibra;
            _connHandel = connHandel;
            _data = data;
            txtSubtitle.Text = $"Wolne zamĂłwienia na {data:dd.MM.yyyy (dddd)}";

            Log($"â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•");
            Log($"  TRANSPORT MAPA DEBUGGER");
            Log($"â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•");
            Log($"Data wybrana: {data:yyyy-MM-dd (dddd)}");
            Log($"Teraz: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log($"ConnLibra: {MaskConn(connLibra)}");
            Log($"ConnHandel: {MaskConn(connHandel)}");
            Log("");

            Loaded += async (s, e) => await LoadAsync();
        }

        private void Log(string msg)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss.fff");
            _debugLog.AppendLine($"[{ts}] {msg}");
            Dispatcher.Invoke(() =>
            {
                txtDebugLog.Text = _debugLog.ToString();
                debugScroll.ScrollToEnd();
            });
        }

        private void BtnCopyDebug_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_debugLog.ToString());
                Log(">>> Skopiowano do schowka <<<");
            }
            catch (Exception ex)
            {
                Log($"!!! Clipboard BĹÄ„D: {ex.Message}");
            }
        }

        private string MaskConn(string conn)
        {
            // PokaĹĽ Server i Database, ukryj resztÄ™
            var parts = conn.Split(';');
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                var trimmed = p.Trim();
                if (trimmed.StartsWith("Server", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Database", StringComparison.OrdinalIgnoreCase))
                    sb.Append(trimmed).Append("; ");
                else if (trimmed.StartsWith("Password", StringComparison.OrdinalIgnoreCase))
                    sb.Append("Password=***; ");
            }
            return sb.ToString().TrimEnd(' ', ';');
        }

        private void BtnDebug_Click(object sender, RoutedEventArgs e)
        {
            if (debugPanel.Visibility == Visibility.Collapsed)
            {
                debugPanel.Visibility = Visibility.Visible;
                colDebug.Width = new GridLength(420);
            }
            else
            {
                debugPanel.Visibility = Visibility.Collapsed;
                colDebug.Width = new GridLength(0);
            }
        }

        private async Task LoadAsync()
        {
            try
            {
                // Pipeline zaĹ‚atwiony przez WolneZamowieniaMapaService (Faza 4-B):
                // SQL + Handel join + KodyPocztowe + Google fallback. Loga z kaĹĽdego etapu
                // przepuszczamy przez callback do txtDebugLog.
                txtLoading.Text = "Ĺadowanie zamĂłwieĹ„...";
                var svc = new Kalendarz1.Transport.Services.WolneZamowieniaMapaService(_connLibra, _connHandel, Log);
                var zamowienia = await svc.LoadMarkersAsync(_data, useGoogleFallback: true);

                txtLiczbaZam.Text = zamowienia.Count.ToString();
                txtSumaPoj.Text = zamowienia.Sum(z => z.Pojemniki).ToString();

                if (zamowienia.Count == 0)
                {
                    Log("STOP: Brak zamĂłwieĹ„. SprawdĹş datÄ™ i filtry SQL.");
                    txtLoading.Text = "Brak wolnych zamĂłwieĹ„ na ten dzieĹ„.";
                    debugPanel.Visibility = Visibility.Visible;
                    colDebug.Width = new GridLength(420);
                    return;
                }

                var naMapie = zamowienia.Count(z => z.Lat.HasValue);
                txtNaMapie.Text = naMapie.ToString();

                if (naMapie == 0)
                {
                    Log("STOP: 0 zamĂłwieĹ„ z wspĂłĹ‚rzÄ™dnymi. Mapa bÄ™dzie pusta.");
                    Log("SprawdĹş: kody pocztowe klientĂłw w Handel, tabelÄ™ KodyPocztowe, Google API key");
                    txtLoading.Text = $"Znaleziono {zamowienia.Count} zamĂłwieĹ„, ale brak wspĂłĹ‚rzÄ™dnych.";
                    debugPanel.Visibility = Visibility.Visible;
                    colDebug.Width = new GridLength(420);
                    return;
                }

                // PeĹ‚na tabela wynikowa
                Log("");
                Log("â•”â•â•â•â•â•â•â•¦â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•¦â•â•â•â•â•â•â•â•â•â•â•â•â•¦â•â•â•â•â•â•â•â•â•â•â•â•â•¦â•â•â•â•â•â•â•â•â•—");
                Log("â•‘  ID  â•‘ Klient                 â•‘ Lat        â•‘ Lng        â•‘ Status â•‘");
                Log("â• â•â•â•â•â•â•â•¬â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•¬â•â•â•â•â•â•â•â•â•â•â•â•â•¬â•â•â•â•â•â•â•â•â•â•â•â•â•¬â•â•â•â•â•â•â•â•â•Ł");
                foreach (var z in zamowienia)
                {
                    var klient = (z.Klient.Length > 22 ? z.Klient.Substring(0, 22) : z.Klient).PadRight(22);
                    var lat = z.Lat.HasValue ? z.Lat.Value.ToString("F4").PadRight(10) : "---       ";
                    var lng = z.Lng.HasValue ? z.Lng.Value.ToString("F4").PadRight(10) : "---       ";
                    var status = z.Lat.HasValue ? "  OK  " : " BRAK ";
                    Log($"â•‘ {z.Id,4} â•‘ {klient} â•‘ {lat} â•‘ {lng} â•‘ {status} â•‘");
                }
                Log("â•šâ•â•â•â•â•â•â•©â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•©â•â•â•â•â•â•â•â•â•â•â•â•â•©â•â•â•â•â•â•â•â•â•â•â•â•â•©â•â•â•â•â•â•â•â•â•ť");
                Log("");

                // ========== KROK 4: WebView2 + HTML ==========
                Log("â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•");
                Log("  KROK 4: Generowanie mapy (WebView2)");
                Log("â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•");
                txtLoading.Text = "Inicjalizacja mapy...";
                await webView.EnsureCoreWebView2Async();
                Log("WebView2 gotowy");

                var mapData = zamowienia.Where(z => z.Lat.HasValue).ToList();
                Log($"ZamĂłwienia do renderowania: {mapData.Count}");
                var html = GenerujHtml(mapData);
                Log($"HTML wygenerowany: {html.Length} znakĂłw");

                // PokaĹĽ fragment JSON (pierwsze 500 znakĂłw)
                var jsonStart = html.IndexOf("var orders = ");
                if (jsonStart >= 0)
                {
                    var jsonEnd = html.IndexOf("];", jsonStart);
                    if (jsonEnd > jsonStart)
                    {
                        var jsonSnippet = html.Substring(jsonStart, Math.Min(500, jsonEnd - jsonStart + 2));
                        Log($"JSON (fragment): {jsonSnippet}");
                    }
                }

                webView.NavigateToString(html);
                Log("NavigateToString() wywoĹ‚ane");
                Log("");
                Log("â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•");
                Log("  GOTOWE - mapa powinna siÄ™ wyĹ›wietliÄ‡");
                Log("â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•");

                loadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Log($"!!! WYJÄ„TEK: {ex.GetType().Name}: {ex.Message}");
                Log($"StackTrace: {ex.StackTrace}");
                txtLoading.Text = $"BĹ‚Ä…d: {ex.Message}";
                debugPanel.Visibility = Visibility.Visible;
                colDebug.Width = new GridLength(420);
            }
        }

        // PobierzZamowieniaAsync, PrzypiszWspolrzedneAsync, GeokodujBrakujaceAsync, GeokodujGoogle
        // przeniesione do Kalendarz1.Transport.Services.WolneZamowieniaMapaService (Faza 4-B).


        private string GenerujHtml(List<Kalendarz1.Transport.Services.ZamMapItem> zamowienia)
        {
            var ci = CultureInfo.GetCultureInfo("pl-PL");
            var inv = CultureInfo.InvariantCulture;
            var dataJson = JsonSerializer.Serialize(zamowienia.Select(z => new
            {
                id = z.Id,
                klientId = z.KlientId,
                klient = z.Klient,
                adres = z.Adres,
                miasto = z.Miasto,
                kod = z.Kod,
                pojemniki = z.Pojemniki,
                palety = z.Palety.ToString("N1", ci),
                godz = z.DataPrzyjazdu.ToString("HH:mm"),
                uboj = z.DataUboju.ToString("dd.MM"),
                handlowiec = z.Handlowiec,
                lat = z.Lat!.Value.ToString(inv),
                lng = z.Lng!.Value.ToString(inv)
            }), new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
            sb.AppendLine("<style>");
            sb.AppendLine("html,body,#map{height:100%;width:100%;margin:0;padding:0;font-family:'Segoe UI',sans-serif;}");
            sb.AppendLine(".iw{padding:0;min-width:260px;max-width:340px;font-family:'Segoe UI',sans-serif;}");
            sb.AppendLine(".iw-h{background:linear-gradient(135deg,#4F46E5,#6366F1);color:#fff;padding:12px 14px;border-radius:8px 8px 0 0;margin:-12px -12px 10px -12px;}");
            sb.AppendLine(".iw-h b{font-size:14px;display:block;margin-bottom:2px;}");
            sb.AppendLine(".iw-h small{opacity:.85;font-size:11px;display:block;line-height:1.3;}");
            sb.AppendLine(".iw-body{padding:0 14px 12px;}");
            sb.AppendLine(".iw-row{display:flex;justify-content:space-between;padding:4px 0;font-size:12px;color:#334155;}");
            sb.AppendLine(".iw-row span:first-child{color:#64748B;}");
            sb.AppendLine(".iw-row span:last-child{font-weight:600;}");
            sb.AppendLine(".iw-sep{border-top:1px solid #E2E8F0;margin:6px 0;}");
            sb.AppendLine(".iw-badge{display:inline-block;background:#EEF2FF;color:#4F46E5;font-size:10px;font-weight:600;padding:3px 10px;border-radius:4px;margin-top:4px;}");
            sb.AppendLine(".iw-badge-green{display:inline-block;background:#F0FDF4;color:#16A34A;font-size:10px;font-weight:600;padding:3px 10px;border-radius:4px;margin-top:4px;margin-left:4px;}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<div id='map'></div>");
            sb.AppendLine("<script>");

            sb.AppendLine($"var orders = {dataJson};");
            sb.AppendLine($"var bazaLat={BazaLat.ToString(inv)}, bazaLng={BazaLng.ToString(inv)};");

            sb.AppendLine(@"
var map, infoWindow;

function initMap(){
    map = new google.maps.Map(document.getElementById('map'),{
        center:{lat:bazaLat,lng:bazaLng},
        zoom:8,
        styles:[
            {featureType:'poi',stylers:[{visibility:'off'}]},
            {featureType:'transit',stylers:[{visibility:'off'}]}
        ],
        mapTypeControl:true,
        streetViewControl:false,
        fullscreenControl:true
    });

    infoWindow = new google.maps.InfoWindow();

    // Baza
    new google.maps.Marker({
        position:{lat:bazaLat,lng:bazaLng},
        map:map,
        title:'BAZA - KoziĂłĹ‚ki 40',
        icon:{
            path:google.maps.SymbolPath.CIRCLE,
            fillColor:'#16A34A',fillOpacity:1,
            strokeColor:'#fff',strokeWeight:3,
            scale:14
        },
        zIndex:9999
    }).addListener('click',function(){
        infoWindow.setContent('<div style=""padding:10px;font-weight:700;font-size:15px;"">&#127981; BAZA<br><span style=""font-weight:400;font-size:12px;color:#64748B;"">KoziĂłĹ‚ki 40, 95-061 Dmosin</span></div>');
        infoWindow.open(map,this);
    });

    var bounds = new google.maps.LatLngBounds();
    bounds.extend({lat:bazaLat,lng:bazaLng});

    // Grupuj zamĂłwienia po lokalizacji (ten sam klient/kod)
    var labels = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';

    for(var i=0; i<orders.length; i++){
        var o = orders[i];
        var lat = parseFloat(o.lat);
        var lng = parseFloat(o.lng);
        if(isNaN(lat) || isNaN(lng)) continue;

        var pos = {lat:lat, lng:lng};
        bounds.extend(pos);

        (function(o, pos, idx){
            var m = new google.maps.Marker({
                position: pos,
                map: map,
                title: o.klient + ' (' + o.pojemniki + ' poj.)',
                label:{
                    text: o.pojemniki.toString(),
                    color:'#fff',
                    fontSize:'9px',
                    fontWeight:'bold'
                },
                icon:{
                    path: google.maps.SymbolPath.CIRCLE,
                    fillColor:'#4F46E5', fillOpacity:0.9,
                    strokeColor:'#fff', strokeWeight:2,
                    scale:16
                }
            });
            m.addListener('click',function(){
                var html = '<div class=""iw"">'
                    +'<div class=""iw-h"">'
                    +'<b>'+esc(o.klient)+'</b>'
                    +'<small>'+esc(o.adres)+'</small>'
                    +(o.miasto ? '<small>'+esc(o.miasto)+'</small>' : '')
                    +'</div>'
                    +'<div class=""iw-body"">'
                    +'<div class=""iw-row""><span>ZamĂłwienie</span><span>#'+o.id+'</span></div>'
                    +'<div class=""iw-row""><span>ID klienta</span><span>'+o.klientId+'</span></div>'
                    +'<div class=""iw-row""><span>Kod pocztowy</span><span>'+esc(o.kod)+'</span></div>'
                    +'<div class=""iw-sep""></div>'
                    +'<div class=""iw-row""><span>Pojemniki</span><span style=""color:#4F46E5;font-size:14px;"">'+o.pojemniki+'</span></div>'
                    +'<div class=""iw-row""><span>Palety</span><span>'+o.palety+'</span></div>'
                    +'<div class=""iw-sep""></div>'
                    +'<div class=""iw-row""><span>Godz. przyjazdu</span><span>'+o.godz+'</span></div>'
                    +'<div class=""iw-row""><span>Data uboju</span><span>'+o.uboj+'</span></div>'
                    +(o.handlowiec ? '<div class=""iw-sep""></div><div class=""iw-badge"">&#128100; '+esc(o.handlowiec)+'</div>' : '')
                    +'</div></div>';
                infoWindow.setContent(html);
                infoWindow.open(map, m);
            });
        })(o, pos, i);
    }

    if(orders.length > 0) map.fitBounds(bounds, 60);
}

function esc(s){return s?s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/'/g,'&#39;').replace(/""/g,'&quot;'):'';}
");
            sb.AppendLine("</script>");
            sb.Append($"<script async defer src=\"https://maps.googleapis.com/maps/api/js?key={ApiKey}&callback=initMap\"></script>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();

        // ZamMapItem przeniesione do Kalendarz1.Transport.Services (Faza 4-B)
    }
}
