//"WZ.sc","WZ - Eksport faktur z LibraNet","\Procedury",0,2.1.0,SYSTEM
// Wersja 2.1.0 — lista 30 zamowien widoczna + tryb WSZYSTKIE bez limitu,
// zwraca numer faktury do LibraNet, pomija bezpiecznie zamowienia bez Shortcut.

String mySrv
String myDb
String myUsr
String myPwd
String gConnStr
String datap
int wynikForm
int gIleZam            // ile WIDOCZNYCH na liscie (max 30)
int gIleWszystkie      // ile faktycznie na bazie (moze byc > 30)
int gPrzetworzone
int gPominiete
int gBledy
String gLogPominiete
String gLogBledow
String gLogSukces

// 30 widocznych wierszy listy (klient + ID + suma)
String gZam1
String gZam2
String gZam3
String gZam4
String gZam5
String gZam6
String gZam7
String gZam8
String gZam9
String gZam10
String gZam11
String gZam12
String gZam13
String gZam14
String gZam15
String gZam16
String gZam17
String gZam18
String gZam19
String gZam20
String gZam21
String gZam22
String gZam23
String gZam24
String gZam25
String gZam26
String gZam27
String gZam28
String gZam29
String gZam30

mySrv = "192.168.0.109"
myDb  = "LibraNet"
myUsr = "pronova"
myPwd = "pronova"
gConnStr = "Provider=SQLOLEDB;Data Source=" + mySrv + ";Initial Catalog=" + myDb + ";User ID=" + myUsr + ";Password=" + myPwd

// ============================================================
// EKRAN 1 — wybor daty
// ============================================================
form "WZ - Eksport faktur (v2.1)", 760, 480
    ground 60,120,180
    Text " ", 20, 20, 720, 10
    Text "EKSPORT FAKTUR SPRZEDAZY", 20, 40, 720, 38
    Text " ", 20, 90, 720, 10
    Text "Tworzy faktury FVS w buforze Symfonii na podstawie zamowien", 20, 110, 720, 25
    Text "z LibraNet (dbo.ZamowieniaMieso). Niezafakturowane zamowienia", 20, 135, 720, 25
    Text "z wybranego dnia uboju zostana wystawione 1:1 (jedno zamowienie =", 20, 160, 720, 25
    Text "jedna faktura). Tylko PLN. Po sukcesie numer faktury wraca do LibraNet.", 20, 185, 720, 25
    Text " ", 20, 220, 720, 10
    Text "Co skrypt POMIJA:", 20, 240, 720, 25
    Text "  - zamowienia bez kodu (Shortcut) kontrahenta w Symfonii", 20, 265, 720, 25
    Text "  - zamowienia bez pozycji towarowych", 20, 290, 720, 25
    Text "  - zamowienia juz zafakturowane (CzyZafakturowane=1)", 20, 315, 720, 25
    Text " ", 20, 350, 720, 10
    Text "Wybierz date uboju:", 130, 380, 200, 30
    Datedit "", datap, 330, 375, 250, 40
    button "    &DALEJ  >>    ", 200, 425, 200, 40, 2
    button "    &ANULUJ    ", 430, 425, 150, 40, -1
wynikForm = ExecForm
if wynikForm < 1 then
    Error ""
endif

// ============================================================
// FUNKCJE POMOCNICZE
// ============================================================

String sub PobierzShortcut(String pKlientId)
    Dispatch con
    Dispatch rs
    String sql
    String wynik
    con = getAdoConnection()
    rs  = "ADODB.Recordset"
    sql = "SELECT ISNULL(Shortcut,'') AS S FROM SSCommon.STContractors WHERE Id = " + pKlientId
    rs.open(sql, con)
    if rs.EOF then
        wynik = ""
    else
        wynik = rs.fields("S").value
    endif
    rs.close()
    PobierzShortcut = wynik
endsub

String sub PobierzKodTowaru(String pTowarId)
    Dispatch con
    Dispatch rs
    String sql
    String wynik
    con = getAdoConnection()
    rs  = "ADODB.Recordset"
    sql = "SELECT ISNULL(kod,'') AS K FROM HM.TW WHERE ID = " + pTowarId
    rs.open(sql, con)
    if rs.EOF then
        wynik = ""
    else
        wynik = rs.fields("K").value
    endif
    rs.close()
    PobierzKodTowaru = wynik
endsub

// Numer faktury jest w HM.DK.kod (potwierdzone w SYMFONIA_AMBASIC_SQL_DOKUMENTACJA.md sek. 13.1)
String sub PobierzNumerFaktury(long pIdDok)
    Dispatch con
    Dispatch rs
    String sql
    String wynik
    String idStr
    idStr = using "%d", pIdDok
    con = getAdoConnection()
    rs  = "ADODB.Recordset"
    sql = "SELECT ISNULL(kod, '') AS NF FROM HM.DK WHERE id = " + idStr
    rs.open(sql, con)
    if rs.EOF then
        wynik = ""
    else
        wynik = rs.fields("NF").value
    endif
    rs.close()
    PobierzNumerFaktury = wynik
endsub

int sub AktualizujLibraNet(String pZamId, String pNumerFak)
    Dispatch con
    String sql
    con = createObject("ADODB.Connection")
    con.connectionString = gConnStr
    con.open()
    sql = "UPDATE dbo.ZamowieniaMieso SET CzyZafakturowane = 1"
    if pNumerFak != "" then
        sql = sql + ", NumerFaktury = '" + pNumerFak + "'"
    endif
    sql = sql + " WHERE Id = " + pZamId
    con.execute(sql)
    con.close()
    AktualizujLibraNet = 1
endsub

// Pobiera liste zamowien — wypelnia globale gZam1..gZam30 (max 30)
// Zwraca liczbe widocznych. gIleWszystkie = faktyczna liczba w bazie.
int sub PobierzListe()
    Dispatch con
    Dispatch rs
    Dispatch rsCnt
    String sql
    String klient
    String suma
    String zamId
    String klientId
    String linia
    int nr

    con = createObject("ADODB.Connection")
    con.connectionString = gConnStr
    con.open()

    // Najpierw faktyczna liczba (bez limitu)
    rsCnt = "ADODB.Recordset"
    sql = "SELECT COUNT(DISTINCT z.Id) AS N FROM dbo.ZamowieniaMieso z "
    sql = sql + "WHERE z.DataUboju = '" + datap + "' AND z.Status <> 'Anulowane' "
    sql = sql + "AND ISNULL(z.CzyZafakturowane, 0) = 0 AND ISNULL(z.Waluta,'PLN') = 'PLN'"
    rsCnt.open(sql, con)
    gIleWszystkie = 0
    if !rsCnt.EOF then
        gIleWszystkie = rsCnt.fields("N").value
    endif
    rsCnt.close()

    // Reset listy (30 wierszy)
    gZam1  = ""
    gZam2  = ""
    gZam3  = ""
    gZam4  = ""
    gZam5  = ""
    gZam6  = ""
    gZam7  = ""
    gZam8  = ""
    gZam9  = ""
    gZam10 = ""
    gZam11 = ""
    gZam12 = ""
    gZam13 = ""
    gZam14 = ""
    gZam15 = ""
    gZam16 = ""
    gZam17 = ""
    gZam18 = ""
    gZam19 = ""
    gZam20 = ""
    gZam21 = ""
    gZam22 = ""
    gZam23 = ""
    gZam24 = ""
    gZam25 = ""
    gZam26 = ""
    gZam27 = ""
    gZam28 = ""
    gZam29 = ""
    gZam30 = ""

    // SQL tylko z LibraNet (bez SSCommon — to inna baza/serwer)
    rs = "ADODB.Recordset"
    sql = "SELECT z.Id, z.KlientId, "
    sql = sql + "ISNULL(CAST(SUM(t.Ilosc) AS VARCHAR(20)), '0') AS Suma "
    sql = sql + "FROM dbo.ZamowieniaMieso z "
    sql = sql + "LEFT JOIN dbo.ZamowieniaMiesoTowar t ON z.Id = t.ZamowienieId "
    sql = sql + "WHERE z.DataUboju = '" + datap + "' AND z.Status <> 'Anulowane' "
    sql = sql + "AND ISNULL(z.CzyZafakturowane, 0) = 0 AND ISNULL(z.Waluta,'PLN') = 'PLN' "
    sql = sql + "GROUP BY z.Id, z.KlientId "
    sql = sql + "ORDER BY z.KlientId, z.Id"
    rs.open(sql, con)

    nr = 0
    While !rs.EOF
        if nr < 30 then
            nr = nr + 1
            zamId    = using "%d",    rs.fields("Id").value
            klientId = using "%d",    rs.fields("KlientId").value
            suma     = rs.fields("Suma").value
            // Klient (Shortcut) z bazy Symfonii — osobne polaczenie przez getAdoConnection()
            klient   = PobierzShortcut(klientId)
            if klient == "" then
                klient = "?BRAK_KODU?"
            endif
            linia = using "  %2d.   ID:%-7s   %-32s   Suma: %10s kg", nr, zamId, klient, suma

            if nr == 1 then
                gZam1 = linia
            endif
            if nr == 2 then
                gZam2 = linia
            endif
            if nr == 3 then
                gZam3 = linia
            endif
            if nr == 4 then
                gZam4 = linia
            endif
            if nr == 5 then
                gZam5 = linia
            endif
            if nr == 6 then
                gZam6 = linia
            endif
            if nr == 7 then
                gZam7 = linia
            endif
            if nr == 8 then
                gZam8 = linia
            endif
            if nr == 9 then
                gZam9 = linia
            endif
            if nr == 10 then
                gZam10 = linia
            endif
            if nr == 11 then
                gZam11 = linia
            endif
            if nr == 12 then
                gZam12 = linia
            endif
            if nr == 13 then
                gZam13 = linia
            endif
            if nr == 14 then
                gZam14 = linia
            endif
            if nr == 15 then
                gZam15 = linia
            endif
            if nr == 16 then
                gZam16 = linia
            endif
            if nr == 17 then
                gZam17 = linia
            endif
            if nr == 18 then
                gZam18 = linia
            endif
            if nr == 19 then
                gZam19 = linia
            endif
            if nr == 20 then
                gZam20 = linia
            endif
            if nr == 21 then
                gZam21 = linia
            endif
            if nr == 22 then
                gZam22 = linia
            endif
            if nr == 23 then
                gZam23 = linia
            endif
            if nr == 24 then
                gZam24 = linia
            endif
            if nr == 25 then
                gZam25 = linia
            endif
            if nr == 26 then
                gZam26 = linia
            endif
            if nr == 27 then
                gZam27 = linia
            endif
            if nr == 28 then
                gZam28 = linia
            endif
            if nr == 29 then
                gZam29 = linia
            endif
            if nr == 30 then
                gZam30 = linia
            endif
        endif
        rs.moveNext()
    Wend

    rs.close()
    con.close()
    gIleZam = nr
    PobierzListe = nr
endsub

// UWAGA: KlientId z LibraNet.ZamowieniaMieso = Id w SSCommon.STContractors w Symfonii
// (potwierdzone w SYMFONIA_AMBASIC_SQL_DOKUMENTACJA.md sek. 6.12 i 13.1).
// Dlatego uzywamy khId bezposrednio — bez lookup'u Shortcut. Walidacja Shortcut zostaje
// tylko po to zeby zapisac informacyjne pominiete jesli kontrahent rzeczywiscie nie istnieje
// w bazie Symfonii.
int sub UtworzFakture(String pZamId, String pKlientId)
    IORec dok
    Dispatch con
    Dispatch rs
    String sql
    String shortcut
    String towarId
    String kodTow
    String ilosc
    String cena
    long idDok
    int nPoz

    // Sprawdz czy kontrahent w ogole istnieje w Symfonii (nie potrzebujemy Shortcut do tworzenia,
    // ale puste = klient nie istnieje w bazie Handel — pomijamy)
    shortcut = PobierzShortcut(pKlientId)
    if shortcut == "" then
        UtworzFakture = 0
    else
        con = createObject("ADODB.Connection")
        con.connectionString = gConnStr
        con.open()
        rs = "ADODB.Recordset"
        sql = "SELECT ISNULL(CAST(KodTowaru AS VARCHAR(20)),'0') AS TowarId, "
        sql = sql + "ISNULL(CAST(Ilosc AS VARCHAR(20)),'0') AS Ilosc, "
        sql = sql + "ISNULL(REPLACE(CAST(Cena AS VARCHAR(20)),',','.'),'0') AS Cena "
        sql = sql + "FROM dbo.ZamowieniaMiesoTowar WHERE ZamowienieId = " + pZamId
        rs.open(sql, con)

        if rs.EOF then
            rs.close()
            con.close()
            UtworzFakture = 0
        else
            dok.setField("typDk", "FVS")
            dok.setField("seria", "sFVS")
            dok.setField("dataWystawienia", datap)
            dok.setField("dataOperacji", datap)
            dok.setField("bufor", "1")
            dok.setField("opis", "LibraNet #" + pZamId)
            dok.beginSection("daneKh")
                dok.setField("khId", pKlientId)
            dok.endSection()

            nPoz = 0
            While !rs.EOF
                towarId = rs.fields("TowarId").value
                ilosc   = rs.fields("Ilosc").value
                cena    = rs.fields("Cena").value
                kodTow  = PobierzKodTowaru(towarId)
                if kodTow != "" then
                    dok.beginSection("Pozycja dokumentu")
                        dok.setField("kod", kodTow)
                        dok.setField("ilosc", ilosc)
                        dok.setField("cena", cena)
                    dok.endSection()
                    nPoz = nPoz + 1
                endif
                rs.moveNext()
            Wend
            rs.close()
            con.close()

            if nPoz == 0 then
                UtworzFakture = 0
            else
                idDok = ImportSP(dok)
                if idDok then
                    UtworzFakture = idDok
                else
                    UtworzFakture = -1
                endif
            endif
        endif
    endif
endsub

// Eksportuje wszystkie z dnia LUB tylko jedno (gdy filtrId<>"")
// BEZ limitu 30 — iteruje po wszystkich z bazy
int sub EksportujWszystkie(String pFiltrId)
    Dispatch con
    Dispatch rs
    String sql
    String zamId
    String klientId
    String shortcut
    long idDok
    int wynik
    String numerFak
    String linia
    int tmp

    gPrzetworzone = 0
    gPominiete    = 0
    gBledy        = 0
    gLogPominiete = ""
    gLogBledow    = ""
    gLogSukces    = ""

    con = createObject("ADODB.Connection")
    con.connectionString = gConnStr
    con.open()
    rs = "ADODB.Recordset"

    sql = "SELECT z.Id, z.KlientId FROM dbo.ZamowieniaMieso z "
    sql = sql + "WHERE z.DataUboju = '" + datap + "' AND z.Status <> 'Anulowane' "
    sql = sql + "AND ISNULL(z.CzyZafakturowane, 0) = 0 AND ISNULL(z.Waluta,'PLN') = 'PLN'"
    if pFiltrId != "" then
        sql = sql + " AND z.Id = " + pFiltrId
    endif
    sql = sql + " ORDER BY z.KlientId, z.Id"
    rs.open(sql, con)

    While !rs.EOF
        zamId    = using "%d", rs.fields("Id").value
        klientId = using "%d", rs.fields("KlientId").value
        wynik = UtworzFakture(zamId, klientId)

        if wynik > 0 then
            idDok = wynik
            numerFak = PobierzNumerFaktury(idDok)
            tmp = AktualizujLibraNet(zamId, numerFak)
            linia = "  Zam #" + zamId + " -> " + numerFak
            if gLogSukces == "" then
                gLogSukces = linia
            else
                gLogSukces = gLogSukces + "  |  " + linia
            endif
            gPrzetworzone = gPrzetworzone + 1
        else
            if wynik == 0 then
                shortcut = PobierzShortcut(klientId)
                if shortcut == "" then
                    linia = "  Zam #" + zamId + " - brak Shortcut (KlientId=" + klientId + ")"
                else
                    linia = "  Zam #" + zamId + " - brak pozycji"
                endif
                if gLogPominiete == "" then
                    gLogPominiete = linia
                else
                    gLogPominiete = gLogPominiete + "  |  " + linia
                endif
                gPominiete = gPominiete + 1
            else
                linia = "  Zam #" + zamId + " - ImportSP zwrocil 0"
                if gLogBledow == "" then
                    gLogBledow = linia
                else
                    gLogBledow = gLogBledow + "  |  " + linia
                endif
                gBledy = gBledy + 1
            endif
        endif
        rs.moveNext()
    Wend

    rs.close()
    con.close()
    EksportujWszystkie = gPrzetworzone
endsub

// ============================================================
// EKRAN 2 — lista 30 + tryb akcji
// ============================================================
int sub main()
    int n
    int dummy
    int trybWszystkie
    String tytul
    String wybor
    String podsumowanie
    String wszystkoOk
    String pominieci
    String bledny
    String infoMax

    n = PobierzListe()

    if n == 0 then
        form "Brak zamowien", 600, 320
            ground 200,200,200
            Text " ", 20, 20, 560, 10
            Text "BRAK ZAMOWIEN DO FAKTUROWANIA", 20, 40, 560, 35
            Text " ", 20, 80, 560, 10
            Text "Na dzien:", 20, 110, 100, 25
            Text datap, 130, 110, 200, 25
            Text " ", 20, 145, 560, 10
            Text "Mozliwe przyczyny:", 20, 165, 560, 25
            Text "  - na ten dzien nie ma zamowien", 20, 190, 560, 25
            Text "  - wszystkie sa juz zafakturowane", 20, 215, 560, 25
            Text "  - zamowienia maja status Anulowane", 20, 240, 560, 25
            button "    &OK    ", 220, 270, 150, 40, 2
        dummy = ExecForm
        main = 0
    else
        wybor = "W"
        if gIleWszystkie > gIleZam then
            tytul = using "Zamowien w bazie: %d   |   Widocznych: %d (max 30)   |   Data: %s", gIleWszystkie, gIleZam, datap
        else
            tytul = using "Zamowien: %d   |   Data: %s", gIleZam, datap
        endif

        if gIleWszystkie > gIleZam then
            infoMax = "  >>> Tryb 'WSZYSTKIE' przetworzy WSZYSTKIE zamowienia z bazy (rowniez te niewidoczne na liscie). <<<"
        else
            infoMax = ""
        endif

        form "WZ - Wybor zamowien", 1000, 950
            ground 235,235,240
            Text " ", 20, 5, 960, 10
            Text tytul, 20, 15, 960, 28
            Text " ", 20, 45, 960, 5
            Text "   NR    ID            ODBIORCA                                  SUMA (kg)", 20, 55, 960, 22
            Text "   ----  --------    ------------------------------------------    ---------------", 20, 78, 960, 18
            Text gZam1,  20, 100, 960, 22
            Text gZam2,  20, 122, 960, 22
            Text gZam3,  20, 144, 960, 22
            Text gZam4,  20, 166, 960, 22
            Text gZam5,  20, 188, 960, 22
            Text gZam6,  20, 210, 960, 22
            Text gZam7,  20, 232, 960, 22
            Text gZam8,  20, 254, 960, 22
            Text gZam9,  20, 276, 960, 22
            Text gZam10, 20, 298, 960, 22
            Text gZam11, 20, 320, 960, 22
            Text gZam12, 20, 342, 960, 22
            Text gZam13, 20, 364, 960, 22
            Text gZam14, 20, 386, 960, 22
            Text gZam15, 20, 408, 960, 22
            Text gZam16, 20, 430, 960, 22
            Text gZam17, 20, 452, 960, 22
            Text gZam18, 20, 474, 960, 22
            Text gZam19, 20, 496, 960, 22
            Text gZam20, 20, 518, 960, 22
            Text gZam21, 20, 540, 960, 22
            Text gZam22, 20, 562, 960, 22
            Text gZam23, 20, 584, 960, 22
            Text gZam24, 20, 606, 960, 22
            Text gZam25, 20, 628, 960, 22
            Text gZam26, 20, 650, 960, 22
            Text gZam27, 20, 672, 960, 22
            Text gZam28, 20, 694, 960, 22
            Text gZam29, 20, 716, 960, 22
            Text gZam30, 20, 738, 960, 22
            Text " ", 20, 760, 960, 5
            Text "----------------------------------------------------------------------------------------------------", 20, 768, 960, 18
            Text infoMax, 20, 788, 960, 22
            Text "JAK WYBRAC:", 20, 815, 960, 22
            Text "  -  W  =  WSZYSTKIE zamowienia (rekomendowane, bez limitu)", 20, 837, 960, 22
            Text "  -  ID konkretne (np. 3825) = tylko jedno zamowienie", 20, 859, 960, 22
            Edit "TWOJ WYBOR (W lub ID):", wybor, 280, 882, 200, 35
            button "  &EKSPORTUJ DO BUFORA  ", 510, 875, 290, 50, 2
            button "  &ANULUJ  ", 820, 875, 130, 50, -1
        wynikForm = ExecForm

        if wynikForm < 1 then
            main = 0
        else
            // Eksport — bez OR (AmBasic moze nie obslugiwac w tej wersji), zamiast tego flaga
            trybWszystkie = 0
            if wybor == "W" then
                trybWszystkie = 1
            endif
            if wybor == "w" then
                trybWszystkie = 1
            endif
            if wybor == "" then
                trybWszystkie = 1
            endif

            if trybWszystkie == 1 then
                dummy = EksportujWszystkie("")
            else
                dummy = EksportujWszystkie(wybor)
            endif

            // Ekran 3 — podsumowanie
            podsumowanie = using "PRZETWORZONO: %d   |   POMINIETO: %d   |   BLEDY: %d", gPrzetworzone, gPominiete, gBledy
            wszystkoOk = ""
            pominieci  = ""
            bledny     = ""
            if gLogSukces != "" then
                wszystkoOk = "UTWORZONE FAKTURY:  " + gLogSukces
            endif
            if gLogPominiete != "" then
                pominieci = "POMINIETE:  " + gLogPominiete
            endif
            if gLogBledow != "" then
                bledny = "BLEDY:  " + gLogBledow
            endif

            form "Wynik eksportu", 900, 600
                ground 230,235,230
                Text " ", 20, 20, 860, 10
                Text "PODSUMOWANIE EKSPORTU", 20, 40, 860, 32
                Text " ", 20, 80, 860, 10
                Text podsumowanie, 20, 100, 860, 30
                Text " ", 20, 135, 860, 5
                Text "----------------------------------------------------------------------------------------", 20, 145, 860, 20
                Text wszystkoOk, 20, 170, 860, 130
                Text "----------------------------------------------------------------------------------------", 20, 305, 860, 20
                Text pominieci, 20, 330, 860, 90
                Text "----------------------------------------------------------------------------------------", 20, 425, 860, 20
                Text bledny, 20, 450, 860, 70
                Text " ", 20, 525, 860, 10
                Text "Faktury sa w BUFORZE Symfonii - sprawdz przed zatwierdzeniem.", 20, 540, 860, 25
                button "    &OK    ", 360, 565, 150, 25, 2
            dummy = ExecForm
            main = 1
        endif
    endif
endsub

noOutput()
main()
