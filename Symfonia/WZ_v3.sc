//"WZ_v3.sc","WZ - Eksport faktur (v3 - grid)","\Procedury",0,3.3.0,SYSTEM
// Wersja 3.3.0 - usunieto pole opisu z pierwszego ekranu, opis = stale "WZ".
//                Numer faktury dalej jest zwracany do LibraNet (NumerFaktury).
// Wersja 3.1.0 - bez dialogu potwierdzenia (Message z buttons nie dziala),
// klik EKSPORTUJ od razu wykonuje. Czyste, bez debug spam.

noOutput()

#define ANULUJ -1
#define OK 2

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

// Domyslnie data dzisiejsza w polu daty uboju (uzytkownik moze zmienic)
datap = data()

// ============================================================
// EKRAN 1 — wybor daty + instrukcja
// ============================================================
form "Symfonia - Eksport faktur sprzedazy (FVS) z LibraNet", 740, 470
    ground 232, 240, 250

    // Naglowek
    Text "EKSPORT FAKTUR FVS DO SYMFONII",                           20, 14, 700, 26
    Text "Wersja 3.3  |  Tworzy faktury w buforze do akceptacji",    20, 42, 700, 16

    // Sekcja: instrukcja krok po kroku
    Group "  Co robi ten raport (krok po kroku)  ",  15, 78, 710, 165
    Text "1. Wybierz DATE UBOJU zamowien w polu ponizej.",                       30, 102, 690, 18
    Text "2. Na nastepnym ekranie zobaczysz liste zamowien z tej daty.",         30, 122, 690, 18
    Text "   Filtry: status != 'Anulowane', niezafakturowane, w PLN, z cenami.", 30, 142, 690, 18
    Text "3. Zaznacz wiersze (klik w LEWA KRAWEDZ) i kliknij EKSPORTUJ.",        30, 162, 690, 18
    Text "4. Faktury FVS zostana utworzone w BUFORZE Symfonii.",                 30, 182, 690, 18
    Text "5. Zatwierdz je w Symfonii lub usun gdy cos nie tak.",                 30, 202, 690, 18
    Text "Po sukcesie LibraNet dostaje numer faktury i flage 'zafakturowane'.",  30, 222, 690, 18

    // Sekcja: parametry
    Group "  Wybierz date uboju  ",                  15, 258, 710, 110
    Datedit "Data uboju:", datap,                                                115, 295, 240, 32
    Text "(domyslnie dzisiaj - mozesz wpisac wczesniejsza)",                     370, 300, 350, 22
    Text "Pokazane zostana tylko zamowienia z tego konkretnego dnia.",           115, 332, 600, 18

    // Akcje
    button "  &DALEJ - pokaz zamowienia  >>  ",  100, 400, 350, 50, OK
    button "  &Anuluj  ",                        540, 400, 150, 50, ANULUJ
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

// UWAGA: zwraca LONG bo idDok z ImportSP moze byc duzy (>32k = obcinany w int)
long sub UtworzFakture(String pZamId, String pKlientId)
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
    Date dTermin
    String terminStr
    String dbgInfo

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
            // Termin platnosci = data wystawienia + 14 dni (dla FA wymagane!)
            dTermin.today()
            dTermin.addDays(14)
            terminStr = dTermin.toStr()

            dok.setField("typDk", "FVS")
            dok.setField("seria", "sFVS")
            dok.setField("dataWystawienia", datap)
            dok.setField("dataOperacji", datap)
            dok.setField("dataDokumentuObcego", datap)
            dok.setField("termin", terminStr)
            dok.setField("bufor", "1")
            dok.setField("opis", "WZ")

            // Identyfikacja kontrahenta - probojemy obu sposobow:
            // shortcut z PobierzShortcut (juz mamy) jest pewniejszy w razie gdyby
            // khId nie matchowal w naszej konfiguracji.
            dok.beginSection("daneKh")
                dok.setField("khKod", shortcut)
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
                // UWAGA: w AmBasic 'if idDok then' nie dziala jak boolean dla long.
                // Trzeba sprawdzac jawnie '> 0' (jak w dokumentacji sek 6.4).
                if idDok > 0 then
                    UtworzFakture = idDok
                else
                    UtworzFakture = -1
                endif
            endif
        endif
    endif
endsub

long sub EksportujJednoZam(String pZamId, String pKlientId)
    long wynik
    long idDok
    String numerFak
    String shortcut
    String linia
    int tmp

    wynik = UtworzFakture(pZamId, pKlientId)

    if wynik > 0 then
        idDok = wynik
        numerFak = PobierzNumerFaktury(idDok)
        tmp = AktualizujLibraNet(pZamId, numerFak)
        linia = "  Zam #" + pZamId + " -> " + numerFak
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
                linia = "  Zam #" + pZamId + " - brak pozycji"
            endif
            if gLogPominiete == "" then
                gLogPominiete = linia
            else
                gLogPominiete = gLogPominiete + "  |  " + linia
            endif
            gPominiete = gPominiete + 1
        else
            linia = "  Zam #" + pZamId + " - ImportSP zwrocil 0"
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
    String suma
    String zamId
    String klientId
    String maCeny
    int n

    con = createObject("ADODB.Connection")
    con.connectionString = gConnStr
    con.open()
    rs = "ADODB.Recordset"

    sql = "SELECT z.Id, z.KlientId, "
    sql = sql + "ISNULL(CAST(SUM(t.Ilosc) AS VARCHAR(20)), '0') AS Suma, "
    sql = sql + "CASE WHEN COUNT(t.Id) = 0 THEN 'Nie' "
    sql = sql + "WHEN SUM(CASE WHEN t.Cena IS NULL OR t.Cena = '' OR CAST(t.Cena AS DECIMAL(18,2)) = 0 THEN 1 ELSE 0 END) = 0 THEN 'Tak' "
    sql = sql + "ELSE 'Nie' END AS MaCeny "
    sql = sql + "FROM dbo.ZamowieniaMieso z "
    sql = sql + "LEFT JOIN dbo.ZamowieniaMiesoTowar t ON z.Id = t.ZamowienieId "
    sql = sql + "WHERE z.DataUboju = '" + datap + "' AND z.Status <> 'Anulowane' "
    sql = sql + "AND ISNULL(z.CzyZafakturowane, 0) = 0 AND ISNULL(z.Waluta,'PLN') = 'PLN' "
    sql = sql + "GROUP BY z.Id, z.KlientId "
    sql = sql + "ORDER BY z.KlientId, z.Id"
    rs.open(sql, con)

    grid.RowCount = 0
    n = 0
    While !rs.EOF
        zamId    = using "%d", rs.fields("Id").value
        klientId = using "%d", rs.fields("KlientId").value
        suma     = rs.fields("Suma").value
        maCeny   = rs.fields("MaCeny").value
        klient   = PobierzShortcut(klientId)
        if klient == "" then
            klient = "?BRAK_KODU?"
        endif

        n = n + 1
        grid.InsertRow(grid.RowCount)
        grid.Rows(grid.RowCount-1).Value(0) = using "%d", n
        grid.Rows(grid.RowCount-1).Value(1) = zamId
        grid.Rows(grid.RowCount-1).Value(2) = klient
        grid.Rows(grid.RowCount-1).Value(3) = suma
        grid.Rows(grid.RowCount-1).Value(4) = maCeny
        grid.Rows(grid.RowCount-1).Value(5) = klientId

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

    // Tytul okna z liczba sukcesow w naglowku - od razu widac wynik
    if gBledy > 0 then
        tytul = using "Wynik eksportu - %d BLEDY (%d ok)", gBledy, gPrzetworzone
    else
        if gPominiete > 0 then
            tytul = using "Wynik eksportu - %d ok, %d pominiete", gPrzetworzone, gPominiete
        else
            tytul = using "Wynik eksportu - %d faktur OK", gPrzetworzone
        endif
    endif

    linia1 = using "Utworzono: %d   |   Pominieto: %d   |   Bledow: %d", gPrzetworzone, gPominiete, gBledy
    linia2 = "Faktury sa w BUFORZE Symfonii - przejdz do menu Sprzedaz aby zatwierdzic."

    if gLogSukces == "" then
        wszystkoOk = "(brak utworzonych faktur)"
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
        ground 238, 245, 240

        // Naglowek
        Text "PODSUMOWANIE EKSPORTU FAKTUR FVS",  20, 12, 840, 24
        Text linia1,                              20, 42, 840, 22

        // Sekcja: utworzone (najwiecej miejsca)
        Group "  Utworzone faktury  ",            12, 80, 855, 165
        Text wszystkoOk,                          25, 110, 830, 130

        // Sekcja: pominiete
        Group "  Pominiete  ",                    12, 256, 855, 90
        Text pominieci,                           25, 278, 830, 65

        // Sekcja: bledy
        Group "  Bledy  ",                        12, 358, 855, 90
        Text bledny,                              25, 380, 830, 65

        // Stopka + przycisk
        Text linia2,                              20, 462, 660, 22
        button "  &ZAMKNIJ  ",                    700, 460, 165, 38, OK

    dummy = ExecForm
    PokazPodsumowanie = 1
endsub

// ============================================================
// FUNKCJE PRZYCISKOW (wywolywane bezposrednio z Button)
// ============================================================

int sub EksportujZaznaczone()
    int i
    int zaznaczone
    long wynik
    int dummy
    String zamId
    String klientId

    // Policz zaznaczone
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
        // BEZ POTWIERDZENIA - od razu eksportujemy
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
                klientId = grid.Rows(i).Value(5)
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
        Message "Lista jest pusta - nie ma co eksportowac."
        EksportujWszystkie = 0
    else
        // BEZ POTWIERDZENIA - od razu
        gPrzetworzone = 0
        gPominiete    = 0
        gBledy        = 0
        gLogSukces    = ""
        gLogPominiete = ""
        gLogBledow    = ""

        i = 0
        While i < grid.RowCount
            zamId    = grid.Rows(i).Value(1)
            klientId = grid.Rows(i).Value(5)
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
// HANDLER - tylko inicjalizacja gridu (id=0, msg=0)
// ============================================================
int sub OnCommand(int id, int msg)
    int n

    if id == 0 then
        if msg == 0 then
            grid.rowHeader = 3
            grid.ColumnCount = 6
            grid.Locked = 1

            grid.Columns(0).name = "Lp"
            grid.Columns(1).name = "ID zam."
            grid.Columns(2).name = "Klient (Shortcut)"
            grid.Columns(3).name = "Suma kg"
            grid.Columns(4).name = "Ceny?"
            grid.Columns(5).name = ""

            grid.Columns(0).width = 50
            grid.Columns(1).width = 80
            grid.Columns(2).width = 280
            grid.Columns(3).width = 100
            grid.Columns(4).width = 70
            grid.Columns(5).width = 0

            n = ZaladujGrid()
        endif
    endif
    OnCommand = 0
endsub

// ============================================================
// EKRAN 2 — GRID + akcje
// ============================================================
String tytul
tytul = "Eksport faktur FVS  -  Data uboju: " + datap

Form tytul, 1120, 780
    ground 238, 242, 248

    // Pasek instrukcji nad gridem
    Text "Klik w LEWA KRAWEDZ wiersza = zaznaczenie. Mozesz zaznaczyc dowolnie wiele.", 15, 8, 1090, 18

    // Grid z lista zamowien
    Control "grid", grid, 10, 32, 1100, 620

    // Sekcja: glowne akcje (eksport)
    Group "  Eksport faktur do bufora Symfonii  ",  10, 660, 660, 110
    Button "  &EKSPORTUJ ZAZNACZONE  ",                            25, 690, 290, 55, EksportujZaznaczone()
    Button "  EKSPORTUJ &WSZYSTKIE  ",                             325, 690, 290, 55, EksportujWszystkie()
    Text "Po sukcesie - faktury w buforze, czekaja na akceptacje.", 25, 750, 600, 16

    // Sekcja: pomocnicze
    Group "  Pomocnicze  ",                          680, 660, 430, 110
    Button "  &ODSWIEZ LISTE  ",                                   695, 690, 200, 55, OdswiezListe()
    Button "  ZAMKNIJ  ",                                          905, 690, 195, 55, ANULUJ
    Text "Odswiez po zmianach w LibraNet.",                        695, 750, 400, 16
ExecForm OnCommand

Error ""
