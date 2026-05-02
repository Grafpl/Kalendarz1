//"WZ_magazyn.sc","WZ - Eksport wydan magazynu","\Procedury",0,1.0.0,SYSTEM
// Magazynier wybiera date uboju, widzi wydane zamowienia bez WZ.
// Klika EKSPORTUJ - tworzy WZ-y w buforze Symfonii z FAKTYCZNIE WYDANYMI ilosciami
// (jezeli sa wpisy w dbo.ZamowienieWydanieRoznice - bierze stamtad, inaczej z ZamowieniaMiesoTowar).
// Po sukcesie zapisuje NumerWZ + DataWystawieniaWZ do LibraNet.
//
// Wymagana magazyn ID w Symfonii: MAGAZYN_ID (domyslnie 65559)

noOutput()

#define ANULUJ -1
#define OK 2
#define MAGAZYN_ID 65559

String mySrv
String myDb
String myUsr
String myPwd
String gConnStr
String datap
int wynikForm

int gPrzetworzone
int gPominiete
int gBledy
String gLogPominiete
String gLogBledow
String gLogSukces

dispatch grid

mySrv = "192.168.0.109"
myDb  = "LibraNet"
myUsr = "pronova"
myPwd = "pronova"
gConnStr = "Provider=SQLOLEDB;Data Source=" + mySrv + ";Initial Catalog=" + myDb + ";User ID=" + myUsr + ";Password=" + myPwd

// ============================================================
// EnsureSchema — tworzy kolumny NumerWZ + DataWystawieniaWZ jezeli nie istnieja
// (idempotentne, mozna wywolywac wielokrotnie)
// ============================================================
Dispatch gCnDdl
String gSqlDdl
gCnDdl = createObject("ADODB.Connection")
gCnDdl.connectionString = gConnStr
gCnDdl.open()
gSqlDdl = "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'NumerWZ') ALTER TABLE dbo.ZamowieniaMieso ADD NumerWZ NVARCHAR(50) NULL"
gCnDdl.execute(gSqlDdl)
gSqlDdl = "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'DataWystawieniaWZ') ALTER TABLE dbo.ZamowieniaMieso ADD DataWystawieniaWZ DATETIME NULL"
gCnDdl.execute(gSqlDdl)
gCnDdl.close()

// Domyslnie data dzisiejsza w polu daty uboju (uzytkownik moze zmienic)
datap = data()

// ============================================================
// EKRAN 1 — wybor daty + instrukcja
// ============================================================
form "Symfonia - Eksport wydan magazynu (WZ) z LibraNet", 740, 480
    ground 232, 248, 235

    // Naglowek
    Text "EKSPORT DOKUMENTOW WZ DO SYMFONII",                          20, 14, 700, 26
    Text "Wersja 1.0  |  Tworzy WZ w buforze - z RZECZYWISTYMI ilosciami z magazynu", 20, 42, 700, 16

    // Sekcja: instrukcja
    Group "  Co robi ten raport (krok po kroku)  ",                    15, 78, 710, 185
    Text "1. Wybierz DATE UBOJU zamowien w polu ponizej.",                                30, 102, 690, 18
    Text "2. Zobaczysz liste zamowien WYDANYCH (status='Wydany') ktore",                  30, 122, 690, 18
    Text "   nie maja jeszcze WZ.",                                                       30, 142, 690, 18
    Text "3. Grid pokazuje: ilosc ZAMOWIONA i FAKTYCZNIE WYDANA - od razu",               30, 162, 690, 18
    Text "   widzisz roznice (kolumna 'Roznice?').",                                      30, 182, 690, 18
    Text "4. Zaznacz wiersze (klik w LEWA KRAWEDZ) i kliknij EKSPORTUJ.",                 30, 202, 690, 18
    Text "5. WZ-y zostana utworzone w BUFORZE Symfonii z faktycznie wydanymi kg.",        30, 222, 690, 18
    Text "Po sukcesie LibraNet dostaje numer WZ i date wystawienia.",                     30, 242, 690, 18

    // Sekcja: parametry
    Group "  Wybierz date uboju  ",                                    15, 280, 710, 110
    Datedit "Data uboju:", datap,                                                          115, 317, 240, 32
    Text "(domyslnie dzisiaj - mozesz wpisac wczesniejsza)",                              370, 322, 350, 22
    Text "Pokazane zostana tylko zamowienia WYDANE z tej daty bez WZ.",                   115, 354, 600, 18

    // Akcje
    button "  &DALEJ - pokaz wydane zamowienia  >>  ",                                    100, 410, 380, 50, OK
    button "  &Anuluj  ",                                                                 540, 410, 150, 50, ANULUJ
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

String sub PobierzKodWZ(long pIdMg)
    Dispatch con
    Dispatch rs
    String sql
    String wynik
    String idStr
    idStr = using "%d", pIdMg
    con = getAdoConnection()
    rs  = "ADODB.Recordset"
    sql = "SELECT ISNULL(kod, '') AS NW FROM HM.MG WHERE id = " + idStr
    rs.open(sql, con)
    if rs.EOF then
        wynik = ""
    else
        wynik = rs.fields("NW").value
    endif
    rs.close()
    PobierzKodWZ = wynik
endsub

int sub OznaczWZwLibraNet(String pZamId, String pNumerWZ)
    Dispatch con
    String sql
    con = createObject("ADODB.Connection")
    con.connectionString = gConnStr
    con.open()
    // Tabele NumerWZ + DataWystawieniaWZ tworzy panel C# przy starcie - tu tylko UPDATE
    sql = "UPDATE dbo.ZamowieniaMieso SET NumerWZ = '" + pNumerWZ + "', DataWystawieniaWZ = GETDATE() WHERE Id = " + pZamId
    con.execute(sql)
    con.close()
    OznaczWZwLibraNet = 1
endsub

// UWAGA: zwraca LONG bo idMg z importMg() moze byc duzy (>32k = obcinany w int)
// Tworzy dokument WZ w buforze Symfonii z faktycznie wydanymi ilosciami.
long sub UtworzWZ(String pZamId, String pKlientId)
    IORec dok
    Dispatch con
    Dispatch rs
    String sql
    String shortcut
    String towarId
    String kodTow
    String iloscWydana
    long idMg
    int nPoz

    shortcut = PobierzShortcut(pKlientId)
    if shortcut == "" then
        UtworzWZ = 0
    else
        con = createObject("ADODB.Connection")
        con.connectionString = gConnStr
        con.open()
        rs = "ADODB.Recordset"

        // KLUCZOWE: pobieramy FAKTYCZNIE WYDANE ilosci (LEFT JOIN z roznicami)
        // Jesli wpis w ZamowienieWydanieRoznice -> IloscWydana
        // Jesli brak wpisu -> domyslnie ilosc zamowiona (pelne wydanie)
        // IloscWydana = 0 -> pozycja w ogole nie wydana, POMIJAMY
        // CAST kolumn na VARCHAR bo AmBasic nie auto-konwertuje long/decimal na String przy przypisaniu
        sql = "SELECT ISNULL(CAST(zmt.KodTowaru AS VARCHAR(20)),'0') AS TowarId, "
        sql = sql + "ISNULL(CAST(COALESCE(zwr.IloscWydana, zmt.Ilosc) AS VARCHAR(20)), '0') AS Ilosc "
        sql = sql + "FROM dbo.ZamowieniaMiesoTowar zmt "
        sql = sql + "LEFT JOIN dbo.ZamowienieWydanieRoznice zwr "
        sql = sql + "  ON zwr.ZamowienieId = zmt.ZamowienieId AND zwr.KodTowaru = zmt.KodTowaru "
        sql = sql + "WHERE zmt.ZamowienieId = " + pZamId + " "
        sql = sql + "  AND COALESCE(zwr.IloscWydana, zmt.Ilosc) > 0"
        rs.open(sql, con)

        if rs.EOF then
            rs.close()
            con.close()
            UtworzWZ = 0
        else
            // Naglowek WZ
            dok.setField("typDk",         "WZ")
            dok.setField("seria",         "sWZ")
            dok.setField("dataWystawienia", datap)
            dok.setField("dataOperacji",    datap)
            dok.setField("magazyn",         using "%d", MAGAZYN_ID)
            dok.setField("bufor",         "1")
            dok.setField("opis",          "WZ z LibraNet #" + pZamId)

            dok.beginSection("daneKh")
                dok.setField("khKod", shortcut)
            dok.endSection()

            nPoz = 0
            While !rs.EOF
                towarId     = rs.fields("TowarId").value
                iloscWydana = rs.fields("Ilosc").value
                kodTow      = PobierzKodTowaru(towarId)
                if kodTow != "" then
                    dok.beginSection("Pozycja dokumentu")
                        dok.setField("kod",   kodTow)
                        dok.setField("ilosc", iloscWydana)
                    dok.endSection()
                    nPoz = nPoz + 1
                endif
                rs.moveNext()
            Wend
            rs.close()
            con.close()

            if nPoz == 0 then
                UtworzWZ = 0
            else
                // KLUCZOWE: importMg() (metoda obiektu) bo to dokument MAGAZYNOWY
                // NIE ImportSP/ImportZK (te sa dla dokumentow handlowych w HM.DK)
                idMg = dok.importMg()
                if idMg > 0 then
                    UtworzWZ = idMg
                else
                    UtworzWZ = -1
                endif
            endif
        endif
    endif
endsub

long sub EksportujJednoZam(String pZamId, String pKlientId)
    long wynik
    long idMg
    String numerWZ
    String shortcut
    String linia
    int tmp

    wynik = UtworzWZ(pZamId, pKlientId)

    if wynik > 0 then
        idMg = wynik
        numerWZ = PobierzKodWZ(idMg)
        tmp = OznaczWZwLibraNet(pZamId, numerWZ)
        linia = "  Zam #" + pZamId + " -> " + numerWZ
        if gLogSukces == "" then
            gLogSukces = linia
        else
            gLogSukces = gLogSukces + "  |  " + linia
        endif
        gPrzetworzone = gPrzetworzone + 1
    else
        if wynik == 0 then
            shortcut = PobierzShortcut(pKlientId)
            if shortcut == "" then
                linia = "  Zam #" + pZamId + " - brak Shortcut (KlientId=" + pKlientId + ")"
            else
                linia = "  Zam #" + pZamId + " - brak pozycji do wydania (wszystko = 0?)"
            endif
            if gLogPominiete == "" then
                gLogPominiete = linia
            else
                gLogPominiete = gLogPominiete + "  |  " + linia
            endif
            gPominiete = gPominiete + 1
        else
            linia = "  Zam #" + pZamId + " - importMg zwrocil 0"
            if gLogBledow == "" then
                gLogBledow = linia
            else
                gLogBledow = gLogBledow + "  |  " + linia
            endif
            gBledy = gBledy + 1
        endif
    endif
    EksportujJednoZam = wynik
endsub

int sub ZaladujGrid()
    Dispatch con
    Dispatch rs
    String sql
    String klient
    String sumaZam
    String sumaWyd
    String zamId
    String klientId
    String maRoznice
    int n

    con = createObject("ADODB.Connection")
    con.connectionString = gConnStr
    con.open()
    rs = "ADODB.Recordset"

    // Pokazuje WYDANE zamowienia ktore jeszcze nie maja WZ.
    // Sumy: zamowiona vs faktycznie wydana - od razu widac roznice
    sql = "SELECT z.Id, z.KlientId, "
    sql = sql + "ISNULL(CAST(SUM(zmt.Ilosc) AS VARCHAR(20)), '0') AS SumaZam, "
    sql = sql + "ISNULL(CAST(SUM(COALESCE(zwr.IloscWydana, zmt.Ilosc)) AS VARCHAR(20)), '0') AS SumaWyd, "
    sql = sql + "CASE WHEN COUNT(zwr.Id) > 0 THEN 'TAK' ELSE 'nie' END AS MaRoznice "
    sql = sql + "FROM dbo.ZamowieniaMieso z "
    sql = sql + "LEFT JOIN dbo.ZamowieniaMiesoTowar zmt ON z.Id = zmt.ZamowienieId "
    sql = sql + "LEFT JOIN dbo.ZamowienieWydanieRoznice zwr "
    sql = sql + "  ON zwr.ZamowienieId = zmt.ZamowienieId AND zwr.KodTowaru = zmt.KodTowaru "
    sql = sql + "WHERE z.DataUboju = '" + datap + "' "
    sql = sql + "  AND z.Status = 'Wydany' "
    sql = sql + "  AND (z.NumerWZ IS NULL OR z.NumerWZ = '') "
    sql = sql + "GROUP BY z.Id, z.KlientId "
    sql = sql + "ORDER BY z.KlientId, z.Id"
    rs.open(sql, con)

    grid.RowCount = 0
    n = 0
    While !rs.EOF
        zamId    = using "%d", rs.fields("Id").value
        klientId = using "%d", rs.fields("KlientId").value
        sumaZam  = rs.fields("SumaZam").value
        sumaWyd  = rs.fields("SumaWyd").value
        maRoznice = rs.fields("MaRoznice").value
        klient   = PobierzShortcut(klientId)
        if klient == "" then
            klient = "?BRAK_KODU?"
        endif

        n = n + 1
        grid.InsertRow(grid.RowCount)
        grid.Rows(grid.RowCount-1).Value(0) = using "%d", n
        grid.Rows(grid.RowCount-1).Value(1) = zamId
        grid.Rows(grid.RowCount-1).Value(2) = klient
        grid.Rows(grid.RowCount-1).Value(3) = sumaZam
        grid.Rows(grid.RowCount-1).Value(4) = sumaWyd
        grid.Rows(grid.RowCount-1).Value(5) = maRoznice
        grid.Rows(grid.RowCount-1).Value(6) = klientId

        rs.moveNext()
    Wend

    rs.close()
    con.close()
    ZaladujGrid = n
endsub

int sub PokazPodsumowanie()
    String tytul
    String linia1
    String linia2
    String wszystkoOk
    String pominieci
    String bledny
    int dummy

    if gBledy > 0 then
        tytul = using "Wynik eksportu WZ - %d BLEDY (%d ok)", gBledy, gPrzetworzone
    else
        if gPominiete > 0 then
            tytul = using "Wynik eksportu WZ - %d ok, %d pominiete", gPrzetworzone, gPominiete
        else
            tytul = using "Wynik eksportu WZ - %d dokumentow OK", gPrzetworzone
        endif
    endif

    linia1 = using "Utworzono: %d   |   Pominieto: %d   |   Bledow: %d", gPrzetworzone, gPominiete, gBledy
    linia2 = "Dokumenty WZ sa w BUFORZE Symfonii - przejdz do menu Magazyn aby zatwierdzic."

    if gLogSukces == "" then
        wszystkoOk = "(brak utworzonych dokumentow)"
    else
        wszystkoOk = gLogSukces
    endif
    if gLogPominiete == "" then
        pominieci = "(brak)"
    else
        pominieci = gLogPominiete
    endif
    if gLogBledow == "" then
        bledny = "(brak)"
    else
        bledny = gLogBledow
    endif

    form tytul, 880, 540
        ground 240, 248, 240

        Text "PODSUMOWANIE EKSPORTU WZ",          20, 12, 840, 24
        Text linia1,                              20, 42, 840, 22

        Group "  Utworzone dokumenty WZ  ",       12, 80, 855, 165
        Text wszystkoOk,                          25, 110, 830, 130

        Group "  Pominiete  ",                    12, 256, 855, 90
        Text pominieci,                           25, 278, 830, 65

        Group "  Bledy  ",                        12, 358, 855, 90
        Text bledny,                              25, 380, 830, 65

        Text linia2,                              20, 462, 660, 22
        button "  &ZAMKNIJ  ",                    700, 460, 165, 38, OK

    dummy = ExecForm
    PokazPodsumowanie = 1
endsub

// ============================================================
// FUNKCJE PRZYCISKOW
// ============================================================

int sub EksportujZaznaczone()
    int i
    int zaznaczone
    long wynik
    int dummy
    String zamId
    String klientId

    zaznaczone = 0
    i = 0
    While i < grid.RowCount
        if grid.Rows(i).Tagged == -1 then
            zaznaczone = zaznaczone + 1
        endif
        i = i + 1
    Wend

    if zaznaczone == 0 then
        Message "Nie zaznaczono zadnych zamowien! Kliknij w lewa krawedz wiersza aby zaznaczyc."
        EksportujZaznaczone = 0
    else
        gPrzetworzone = 0
        gPominiete    = 0
        gBledy        = 0
        gLogSukces    = ""
        gLogPominiete = ""
        gLogBledow    = ""

        i = 0
        While i < grid.RowCount
            if grid.Rows(i).Tagged == -1 then
                zamId    = grid.Rows(i).Value(1)
                klientId = grid.Rows(i).Value(6)
                wynik = EksportujJednoZam(zamId, klientId)
            endif
            i = i + 1
        Wend

        dummy = PokazPodsumowanie()
        dummy = ZaladujGrid()
        EksportujZaznaczone = 0
    endif
endsub

int sub EksportujWszystkie()
    int i
    long wynik
    int dummy
    String zamId
    String klientId

    if grid.RowCount == 0 then
        Message "Lista jest pusta - brak wydan do eksportu."
        EksportujWszystkie = 0
    else
        gPrzetworzone = 0
        gPominiete    = 0
        gBledy        = 0
        gLogSukces    = ""
        gLogPominiete = ""
        gLogBledow    = ""

        i = 0
        While i < grid.RowCount
            zamId    = grid.Rows(i).Value(1)
            klientId = grid.Rows(i).Value(6)
            wynik = EksportujJednoZam(zamId, klientId)
            i = i + 1
        Wend

        dummy = PokazPodsumowanie()
        dummy = ZaladujGrid()
        EksportujWszystkie = 0
    endif
endsub

int sub OdswiezListe()
    int dummy
    dummy = ZaladujGrid()
    OdswiezListe = 0
endsub

// ============================================================
// HANDLER - inicjalizacja gridu
// ============================================================
int sub OnCommand(int id, int msg)
    int n

    if id == 0 then
        if msg == 0 then
            grid.rowHeader = 3
            grid.ColumnCount = 7
            grid.Locked = 1

            grid.Columns(0).name = "Lp"
            grid.Columns(1).name = "ID zam."
            grid.Columns(2).name = "Klient (Shortcut)"
            grid.Columns(3).name = "Zamow. kg"
            grid.Columns(4).name = "Wydane kg"
            grid.Columns(5).name = "Roznice?"
            grid.Columns(6).name = ""

            grid.Columns(0).width = 50
            grid.Columns(1).width = 80
            grid.Columns(2).width = 260
            grid.Columns(3).width = 100
            grid.Columns(4).width = 100
            grid.Columns(5).width = 80
            grid.Columns(6).width = 0

            n = ZaladujGrid()
        endif
    endif
    OnCommand = 0
endsub

// ============================================================
// EKRAN 2 — GRID + akcje
// ============================================================
String tytul
tytul = "Eksport WZ  -  Data uboju: " + datap + "  |  Magazyn ID: 65559"

Form tytul, 1120, 780
    ground 240, 246, 240

    // Pasek instrukcji nad gridem
    Text "Klik w LEWA KRAWEDZ wiersza = zaznaczenie. Sprawdz kolumne 'Roznice?' przed eksportem.", 15, 8, 1090, 18

    // Grid
    Control "grid", grid, 10, 32, 1100, 620

    // Sekcja: glowne akcje (eksport)
    Group "  Eksport WZ do bufora Symfonii  ",      10, 660, 660, 110
    Button "  &EKSPORTUJ ZAZNACZONE  ",                            25, 690, 290, 55, EksportujZaznaczone()
    Button "  EKSPORTUJ &WSZYSTKIE  ",                             325, 690, 290, 55, EksportujWszystkie()
    Text "WZ-y trafia do bufora z faktycznie wydanymi ilosciami.", 25, 750, 600, 16

    // Sekcja: pomocnicze
    Group "  Pomocnicze  ",                          680, 660, 430, 110
    Button "  &ODSWIEZ LISTE  ",                                   695, 690, 200, 55, OdswiezListe()
    Button "  ZAMKNIJ  ",                                          905, 690, 195, 55, ANULUJ
    Text "Odswiez gdy magazynier zatwierdzi nowe wydania.",        695, 750, 400, 16
ExecForm OnCommand

Error ""
