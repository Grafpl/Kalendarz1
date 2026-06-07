//"KreatorPaszyHodowcom.sc","Kreator: zakup paszy od paszarni → sprzedaz hodowcy z marza","\Procedury\Kreatory\Pasza",0,2.0.0,SYSTEM
// =====================================================================
// KREATOR PASZA: PASZARNIA -> HODOWCA  (z marza stala)
// =====================================================================
// v2.0.0 (nowosc): #1 dedup po (paszarnia+data) przed FVZ, #3 termin liczony od data wystawienia (SQL),
//                  #4 wybor paszarni/towaru/hodowcy z listy (grid + filtr) zamiast wpisywania khKod recznie.
// v1.0.0:          textboxy + jeden ekran inputow.
//
// FLOW (6 ekranow):
//   EKRAN 1: pick PASZARNI z grida (STContractors, filtr po nazwie/Shortcut/NIP)
//   EKRAN 2: pick TOWARU z grida (HM.TW, filtr po nazwie/kod, tylko aktywne)
//   EKRAN 3: pick HODOWCY z grida (jak EKRAN 1)
//   EKRAN 4: ilosc, cena zakupu, marza, VAT, numer obcy, data, termin
//   EKRAN 5: podglad (cena sprz., wartosci, marza laczna) -> potwierdz lub anuluj
//   EKRAN 6: utworzone dokumenty (4 numery)
//
// TWORZY w buforze Symfonii:
//   sPZ  — Przyjecie od paszarni                        (importMg, ceny netto)
//   sFVZ — Faktura zakupu od paszarni                   (ImportZK, numer obcy + termin)
//   sWZ  — Wydanie do hodowcy                           (importMg, ceny netto = ewid. zakup)
//   sFPP — Faktura sprzedazy do hodowcy                 (ImportSP, ceny BRUTTO)
//
// PRZED FVZ: dedup check (#1) — zapytanie do HM.DK czy nie ma juz FVZ od tej paszarni z tego dnia.
// TERMIN (#3): liczony jako DATA_WYSTAWIENIA + N dni przez SQL DATEADD (a nie .today()+N jak w v1).
// PICKER (#4): kazdy form z gridem ma filtr (Edit + "Filtruj" btn) + "WYBIERZ" btn + "ANULUJ".
//              Klik w lewa krawedz wiersza = zaznaczenie (Tagged=-1), klik "WYBIERZ" zatwierdza.
//
// KONWENCJE AmBasic (z v2 .sc):
//   - dispatch grid + Control "grid" + OnCommand handler dla setup kolumn (id=0/msg=0)
//   - Klik lewa krawedz wiersza -> grid.Rows(i).Tagged = -1
//   - Funkcje wywolane z Button-actions (Button "...",x,y,w,h,Fn()) -> zwracana wartosc = form exit code

noOutput()

#define OK 2
#define ANULUJ -1

dispatch grid

// ===== Konfiguracja (zmien jezeli inne magazyny niz na screenach) =====
String DZIAL_MAG_PASZA
String DZIAL_MAG_FAKTURY
DZIAL_MAG_PASZA   = "Pasza"
DZIAL_MAG_FAKTURY = "MAG"

// ===== Wynik wyboru z 3 pickerow =====
String pszKod          // Shortcut paszarni
String pszNazwa        // pelna nazwa paszarni (do wyswietlenia)
String pszNip
String hodKod
String hodNazwa
String hodNip
String twrKod
String twrNazwa
String twrJm           // jednostka miary (np. "t")

// ===== Zmienne formularza #4 (inputy) =====
String numerObcy
String datap
String sIloscT
String sCenaZak
String sMarza
String sVatProc
String sTerminDni

// ===== Filtry w pickerach =====
String sFiltrPasz
String sFiltrHod
String sFiltrTwr

int    wynik_form

// ===== Parsowane / wyliczone =====
float iloscT, cenaZakNetto, marza, vatProc
int   terminDni
float cenaSprzNetto, cenaSprzBrutto
float wartZakNetto, wartSprzNetto, wartSprzBrutto, marzaLaczna

// ===== Wyniki tworzenia =====
long   gIdPZ, gIdFVZ, gIdWZ, gIdFPP
String gNrPZ, gNrFVZ, gNrWZ, gNrFPP

// ===== Pomocnicze stringi (musza byc na top-level w AmBasic) =====
String blad
String l1, l2, l3, l4, l5, l6, l7, l8
String sIlosc, sCenaZ, sCenaSB
String sTermin
String opisZak, opisSpz
String duplikat
String dupTxt
String s1, s2, s3, s4, s5, s6, s7

// ===== IORec dla 4 dokumentow (deklaracje top-level) =====
IORec dokPz
IORec dokFvz
IORec dokWz
IORec dokFpp

// ===== Defaulty =====
datap      = data()
sTerminDni = "45"
sIloscT    = "0"
sCenaZak   = "0"
sMarza     = "0"
sVatProc   = "8"
sFiltrPasz = ""
sFiltrHod  = ""
sFiltrTwr  = ""
pszKod = ""
hodKod = ""
twrKod = ""

// =====================================================================
// HELPER: numery dokumentow po imporcie (port z v2)
// =====================================================================
String sub PobierzNumerDok(long pIdDok)
    Dispatch con
    Dispatch rs
    String sql
    String wynik
    String idStr
    idStr = using "%d", pIdDok
    con = getAdoConnection()
    rs  = "ADODB.Recordset"
    sql = "SELECT ISNULL(kod,'') AS kod FROM HM.DK WHERE id = " + idStr
    rs.open(sql, con)
    if rs.EOF then
        wynik = ""
    else
        wynik = rs.fields("kod").value
    endif
    rs.close()
    PobierzNumerDok = wynik
endsub

String sub PobierzNumerMG(long pIdDok)
    Dispatch con
    Dispatch rs
    String sql
    String wynik
    String idStr
    idStr = using "%d", pIdDok
    con = getAdoConnection()
    rs  = "ADODB.Recordset"
    sql = "SELECT ISNULL(kod,'') AS kod FROM HM.MG WHERE id = " + idStr
    rs.open(sql, con)
    if rs.EOF then
        wynik = ""
    else
        wynik = rs.fields("kod").value
    endif
    rs.close()
    PobierzNumerMG = wynik
endsub

// =====================================================================
// HELPER #3: termin = data_wystawienia + N dni (przez SQL DATEADD)
//   Bezpieczne — nie zalezy od Date.fromStr() ktore moze nie istniec w AmBasic.
// =====================================================================
String sub ObliczTermin(String pData, int pDni)
    Dispatch con
    Dispatch rs
    String sql
    String wynik
    String dniStr
    dniStr = using "%d", pDni
    con = getAdoConnection()
    rs  = "ADODB.Recordset"
    sql = "SELECT CONVERT(NVARCHAR(10), DATEADD(DAY, " + dniStr + ", CAST('" + pData + "' AS DATE)), 120) AS t"
    rs.open(sql, con)
    if rs.EOF then
        wynik = pData
    else
        wynik = rs.fields("t").value
    endif
    rs.close()
    ObliczTermin = wynik
endsub

// =====================================================================
// HELPER #1: dedup — czy juz jest FVZ od tej paszarni z tego dnia?
//   Zwraca: "" jezeli brak, kod istniejacego dokumentu jezeli jest.
//   Sprawdza tylko niezanulowane.
// =====================================================================
String sub SprawdzDuplikatFV(String pKhKod, String pData)
    Dispatch con
    Dispatch rs
    String sql
    String wynik
    // UWAGA: zalozenie ze pKhKod nie zawiera apostrofu (AmBasic traktuje ' jako poczatek komentarza
    //        nawet w stringu — replace(...,"'","''") jest niedostepne). Praktycznie nazwy paszarni
    //        w Symfonii nie maja apostrofow.
    con = getAdoConnection()
    rs  = "ADODB.Recordset"
    sql = "SELECT TOP 1 ISNULL(dk.kod,'') AS dok"
    sql = sql + " FROM HM.DK dk"
    sql = sql + " JOIN SSCommon.STContractors k ON dk.khid = k.Id"
    sql = sql + " WHERE dk.typ_dk = 'FVZ'"
    sql = sql + "   AND ISNULL(dk.anulowany,0) = 0"
    sql = sql + "   AND k.Shortcut = '" + pKhKod + "'"
    sql = sql + "   AND CAST(dk.data AS DATE) = '" + pData + "'"
    sql = sql + " ORDER BY dk.id DESC"
    rs.open(sql, con)
    if rs.EOF then
        wynik = ""
    else
        wynik = rs.fields("dok").value
    endif
    rs.close()
    SprawdzDuplikatFV = wynik
endsub

// =====================================================================
// HELPER #4: laduj kontrahentow do grida (paszarnia/hodowca — ta sama tabela)
//   filter: jezeli "" -> wszyscy (TOP 200); inaczej LIKE %filter% na Shortcut/Name/NIP.
// =====================================================================
int sub LoadKontrahentow(String pFiltr)
    Dispatch con
    Dispatch rs
    String sql
    int n
    n = 0
    grid.RowCount = 0
    con = getAdoConnection()
    rs  = "ADODB.Recordset"
    if pFiltr == "" then
        sql = "SELECT TOP 200 ISNULL(Shortcut,'') AS Shortcut, ISNULL(Name,'') AS Name, ISNULL(NIP,'') AS NIP FROM SSCommon.STContractors ORDER BY Name"
    else
        sql = "SELECT TOP 200 ISNULL(Shortcut,'') AS Shortcut, ISNULL(Name,'') AS Name, ISNULL(NIP,'') AS NIP FROM SSCommon.STContractors"
        sql = sql + " WHERE Shortcut LIKE '%" + pFiltr + "%'"
        sql = sql + "    OR Name     LIKE '%" + pFiltr + "%'"
        sql = sql + "    OR REPLACE(REPLACE(ISNULL(NIP,''),'-',''),' ','') LIKE '%" + pFiltr + "%'"
        sql = sql + " ORDER BY Name"
    endif
    rs.open(sql, con)
    while rs.EOF == 0
        grid.InsertRow(grid.RowCount)
        grid.Rows(grid.RowCount-1).Value(0) = rs.fields("Shortcut").value
        grid.Rows(grid.RowCount-1).Value(1) = rs.fields("Name").value
        grid.Rows(grid.RowCount-1).Value(2) = rs.fields("NIP").value
        rs.moveNext()
        n = n + 1
    wend
    rs.close()
    LoadKontrahentow = n
endsub

// Laduj towary (HM.TW aktywne, filtr po kod/nazwa)
int sub LoadTowarow(String pFiltr)
    Dispatch con
    Dispatch rs
    String sql
    int n
    n = 0
    grid.RowCount = 0
    con = getAdoConnection()
    rs  = "ADODB.Recordset"
    if pFiltr == "" then
        sql = "SELECT TOP 300 ISNULL(kod,'') AS kod, ISNULL(nazwa,'') AS nazwa, ISNULL(jm,'') AS jm FROM HM.TW WHERE ISNULL(aktywny,0) = 1 ORDER BY nazwa"
    else
        sql = "SELECT TOP 300 ISNULL(kod,'') AS kod, ISNULL(nazwa,'') AS nazwa, ISNULL(jm,'') AS jm FROM HM.TW"
        sql = sql + " WHERE ISNULL(aktywny,0) = 1"
        sql = sql + "   AND (kod LIKE '%" + pFiltr + "%' OR nazwa LIKE '%" + pFiltr + "%')"
        sql = sql + " ORDER BY nazwa"
    endif
    rs.open(sql, con)
    while rs.EOF == 0
        grid.InsertRow(grid.RowCount)
        grid.Rows(grid.RowCount-1).Value(0) = rs.fields("kod").value
        grid.Rows(grid.RowCount-1).Value(1) = rs.fields("nazwa").value
        grid.Rows(grid.RowCount-1).Value(2) = rs.fields("jm").value
        rs.moveNext()
        n = n + 1
    wend
    rs.close()
    LoadTowarow = n
endsub

// =====================================================================
// AKCJE BUTTONS w pickerach: Filtruj* (zostaje w formie) i Wybierz* (zamyka form OK lub zostaje)
// =====================================================================
int sub FiltrujPaszarni()
    int n
    n = LoadKontrahentow(sFiltrPasz)
    FiltrujPaszarni = 0
endsub

int sub FiltrujHodowcow()
    int n
    n = LoadKontrahentow(sFiltrHod)
    FiltrujHodowcow = 0
endsub

int sub FiltrujTowary()
    int n
    n = LoadTowarow(sFiltrTwr)
    FiltrujTowary = 0
endsub

int sub WybierzPaszarniZGridu()
    int i
    int found
    found = 0
    i = 0
    while i < grid.RowCount
        if grid.Rows(i).Tagged == -1 then
            pszKod   = grid.Rows(i).Value(0)
            pszNazwa = grid.Rows(i).Value(1)
            pszNip   = grid.Rows(i).Value(2)
            found = 1
            i = grid.RowCount
        endif
        i = i + 1
    wend
    if found == 1 then
        WybierzPaszarniZGridu = OK
    else
        WybierzPaszarniZGridu = 0
    endif
endsub

int sub WybierzHodowcaZGridu()
    int i
    int found
    found = 0
    i = 0
    while i < grid.RowCount
        if grid.Rows(i).Tagged == -1 then
            hodKod   = grid.Rows(i).Value(0)
            hodNazwa = grid.Rows(i).Value(1)
            hodNip   = grid.Rows(i).Value(2)
            found = 1
            i = grid.RowCount
        endif
        i = i + 1
    wend
    if found == 1 then
        WybierzHodowcaZGridu = OK
    else
        WybierzHodowcaZGridu = 0
    endif
endsub

int sub WybierzTowarZGridu()
    int i
    int found
    found = 0
    i = 0
    while i < grid.RowCount
        if grid.Rows(i).Tagged == -1 then
            twrKod   = grid.Rows(i).Value(0)
            twrNazwa = grid.Rows(i).Value(1)
            twrJm    = grid.Rows(i).Value(2)
            found = 1
            i = grid.RowCount
        endif
        i = i + 1
    wend
    if found == 1 then
        WybierzTowarZGridu = OK
    else
        WybierzTowarZGridu = 0
    endif
endsub

// =====================================================================
// HANDLERY OnCommand — setup kolumn grida (id=0,msg=0)
// =====================================================================
int sub OnCmdPickKontrahent(int id, int msg)
    int n
    if id == 0 then
        if msg == 0 then
            grid.rowHeader   = 3
            grid.ColumnCount = 3
            grid.Locked      = 1
            grid.Columns(0).name  = "Shortcut (khKod)"
            grid.Columns(1).name  = "Nazwa"
            grid.Columns(2).name  = "NIP"
            grid.Columns(0).width = 220
            grid.Columns(1).width = 420
            grid.Columns(2).width = 140
            n = LoadKontrahentow("")
        endif
    endif
    OnCmdPickKontrahent = 0
endsub

int sub OnCmdPickTowar(int id, int msg)
    int n
    if id == 0 then
        if msg == 0 then
            grid.rowHeader   = 3
            grid.ColumnCount = 3
            grid.Locked      = 1
            grid.Columns(0).name  = "Kod"
            grid.Columns(1).name  = "Nazwa"
            grid.Columns(2).name  = "Jm"
            grid.Columns(0).width = 220
            grid.Columns(1).width = 480
            grid.Columns(2).width = 80
            n = LoadTowarow("")
        endif
    endif
    OnCmdPickTowar = 0
endsub

// =====================================================================
// EKRAN 1 — wybor PASZARNI
// =====================================================================
Form "Krok 1/4 — wybierz PASZARNIE  (kontrahent w Symfonii)", 1020, 740
    ground 248, 250, 252

    Text "===========================================================================================", 15,  8, 990, 14
    Text "    K R O K   1 / 4      W Y B I E R Z   P A S Z A R N I E",                                  15, 24, 990, 22
    Text "===========================================================================================", 15, 50, 990, 14

    Text "1) Wpisz fragment nazwy/Shortcut/NIP i klik 'Filtruj'.  2) Klik LEWA KRAWEDZ wiersza = zaznaczenie.  3) 'WYBIERZ ZAZNACZONA'.", 15, 75, 990, 18

    Edit   "Filtruj (nazwa / Shortcut / NIP):", sFiltrPasz, 15, 105, 750, 28
    Button "  &Filtruj  ",                                 775, 105, 220, 28, FiltrujPaszarni()

    Control "grid", grid, 15, 145, 990, 510

    Button "  >>>  &WYBIERZ ZAZNACZONA  >>>  ",  15, 670, 540, 55, WybierzPaszarniZGridu()
    Button "  &ANULUJ  ",                       575, 670, 430, 55, ANULUJ

wynik_form = ExecForm OnCmdPickKontrahent

if wynik_form < 1 then
    Error ""
endif
if pszKod == "" then
    Error ""
endif

// =====================================================================
// EKRAN 2 — wybor TOWARU
// =====================================================================
sFiltrTwr = ""

Form "Krok 2/4 — wybierz TOWAR (pasze)", 1020, 740
    ground 248, 250, 252

    Text "===========================================================================================", 15,  8, 990, 14
    Text "    K R O K   2 / 4      W Y B I E R Z   T O W A R   (pasza)",                                15, 24, 990, 22
    Text "===========================================================================================", 15, 50, 990, 14

    Text "Paszarnia: " + pszNazwa,                                15,  75, 990, 18
    Text "Wpisz fragment kodu lub nazwy (np. 'Brojler Finiszer'). 'Filtruj' -> 'WYBIERZ ZAZNACZONY'.", 15, 95, 990, 18

    Edit   "Filtruj (kod / nazwa):", sFiltrTwr,  15, 125, 750, 28
    Button "  &Filtruj  ",                      775, 125, 220, 28, FiltrujTowary()

    Control "grid", grid, 15, 165, 990, 490

    Button "  >>>  &WYBIERZ ZAZNACZONY  >>>  ",  15, 670, 540, 55, WybierzTowarZGridu()
    Button "  &ANULUJ  ",                       575, 670, 430, 55, ANULUJ

wynik_form = ExecForm OnCmdPickTowar

if wynik_form < 1 then
    Error ""
endif
if twrKod == "" then
    Error ""
endif

// =====================================================================
// EKRAN 3 — wybor HODOWCY
// =====================================================================
sFiltrHod = ""

Form "Krok 3/4 — wybierz HODOWCE (odbiorca paszy)", 1020, 740
    ground 248, 250, 252

    Text "===========================================================================================", 15,  8, 990, 14
    Text "    K R O K   3 / 4      W Y B I E R Z   H O D O W C E",                                      15, 24, 990, 22
    Text "===========================================================================================", 15, 50, 990, 14

    Text "Paszarnia: " + pszNazwa + "       Towar: " + twrNazwa,    15,  75, 990, 18

    Edit   "Filtruj (nazwa / Shortcut / NIP):", sFiltrHod,  15, 105, 750, 28
    Button "  &Filtruj  ",                                  775, 105, 220, 28, FiltrujHodowcow()

    Control "grid", grid, 15, 145, 990, 510

    Button "  >>>  &WYBIERZ ZAZNACZONA  >>>  ",  15, 670, 540, 55, WybierzHodowcaZGridu()
    Button "  &ANULUJ  ",                       575, 670, 430, 55, ANULUJ

wynik_form = ExecForm OnCmdPickKontrahent

if wynik_form < 1 then
    Error ""
endif
if hodKod == "" then
    Error ""
endif

// =====================================================================
// EKRAN 4 — INPUTY (ilosc, ceny, marza, VAT, numer obcy, data, termin)
// =====================================================================
Form "Krok 4/4 — DANE FAKTURY  (ilosc, ceny, marza, numer obcy)", 800, 620
    ground 248, 250, 252

    Text "===========================================================================", 15,  8, 770, 14
    Text "    K R O K   4 / 4      D A N E   F A K T U R Y",                              15, 24, 770, 22
    Text "===========================================================================", 15, 50, 770, 14

    Text "Paszarnia: " + pszNazwa,                                15,  78, 770, 18
    Text "Hodowca:   " + hodNazwa,                                15, 100, 770, 18
    Text "Towar:     " + twrNazwa + "   [" + twrJm + "]",         15, 122, 770, 18

    Group "  >>>  Z A K U P   ( od paszarni )  ", 15, 150, 770, 100
        Edit "Ilosc [" + twrJm + "] (np. 9.98):",                sIloscT,   30, 180, 350, 28
        Edit "Cena zakupu [zl/" + twrJm + " netto] (np. 1625.00):", sCenaZak,  30, 215, 720, 28

    Group "  >>>  S P R Z E D A Z   ( do hodowcy z marza )  ", 15, 260, 770, 95
        Edit "Marza [zl/" + twrJm + "]:",     sMarza,   30, 290, 350, 28
        Edit "Stawka VAT [%]:",                sVatProc, 400, 290, 350, 28
        Text "(cena sprz. netto = zakup + marza; brutto = netto * (1 + VAT/100))", 30, 325, 720, 18

    Group "  >>>  N U M E R   O B C Y   +   T E R M I N   ", 15, 365, 770, 130
        Edit    "Numer obcy faktury paszarni (np. 'SKD/FV/1291/05/26'):", numerObcy, 30, 395, 720, 28
        Datedit "Data wystawienia:",       datap,      30, 430, 350, 28
        Edit    "Termin platnosci [dni]:", sTerminDni, 400, 430, 350, 28
        Text "(termin = data wystawienia + N dni — SQL DATEADD, nie 'dzis + N')", 30, 465, 720, 18

    Button "  >>>  &DALEJ ->  P O D G L A D  ->  >>>  ", 15, 530, 540, 55, OK
    Button "  &ANULUJ  ",                                575, 530, 210, 55, ANULUJ

wynik_form = ExecForm

if wynik_form < 1 then
    Error ""
endif

// =====================================================================
// WALIDACJA INPUTOW
// =====================================================================
iloscT       = val(sIloscT)
cenaZakNetto = val(sCenaZak)
marza        = val(sMarza)
vatProc      = val(sVatProc)
terminDni    = val(sTerminDni)
if terminDni <= 0 then
    terminDni = 45
endif

blad = ""
if iloscT <= 0 then
    blad = "Ilosc musi byc > 0  (wpisz np. 9.98)."
else
    if cenaZakNetto <= 0 then
        blad = "Cena zakupu musi byc > 0."
    else
        if vatProc < 0 then
            blad = "VAT nie moze byc ujemny."
        endif
    endif
endif

if blad <> "" then
    Form "Blad walidacji", 600, 180
        ground 254, 226, 226
        Text "BLAD WALIDACJI:", 20, 15, 560, 22
        Text blad,               20, 50, 560, 60
        Button "  &OK  ", 230, 120, 140, 40, OK
    ExecForm
    Error ""
endif

// =====================================================================
// WYLICZENIE CEN  (PODGLAD)
// =====================================================================
cenaSprzNetto  = cenaZakNetto + marza
cenaSprzBrutto = cenaSprzNetto * (1 + vatProc / 100)
wartZakNetto   = iloscT * cenaZakNetto
wartSprzNetto  = iloscT * cenaSprzNetto
wartSprzBrutto = iloscT * cenaSprzBrutto
marzaLaczna    = marza   * iloscT

// =====================================================================
// EKRAN 5 — PODGLAD / POTWIERDZENIE
// =====================================================================
l1 = using "Paszarnia:  %s",                       pszNazwa
l2 = using "Hodowca:    %s",                       hodNazwa
l3 = using "Towar:      %s   [%s]   |   Ilosc:  %.3f %s   |   Nr obcy:  %s",  twrNazwa, twrJm, iloscT, twrJm, numerObcy
l4 = using "Cena zakupu netto:   %10.2f zl/%s       Wartosc zakupu netto:    %12.2f zl", cenaZakNetto,  twrJm, wartZakNetto
l5 = using "Marza:               %10.2f zl/%s       Marza laczna (przyrost): %12.2f zl", marza,         twrJm, marzaLaczna
l6 = using "Cena sprz. netto:    %10.2f zl/%s       Wartosc sprz. netto:     %12.2f zl", cenaSprzNetto, twrJm, wartSprzNetto
l7 = using "Cena sprz. brutto:   %10.2f zl/%s       Wartosc sprz. brutto:    %12.2f zl    (VAT %.0f%%)", cenaSprzBrutto, twrJm, wartSprzBrutto, vatProc
l8 = using "Data wystawienia: %s   |   Termin: %d dni  ->  %s   |   Plan: sPZ + sFVZ + sWZ + sFPP",  datap, terminDni, ObliczTermin(datap, terminDni)

Form "Podglad — potwierdz utworzenie 4 dokumentow", 980, 420
    ground 248, 250, 252
    Text "==========================================================================================", 15,  8, 950, 14
    Text "                    P O D G L A D   D O K U M E N T O W                                  ", 15, 24, 950, 22
    Text "==========================================================================================", 15, 50, 950, 14
    Text l1, 15,  78, 950, 22
    Text l2, 15, 101, 950, 22
    Text l3, 15, 124, 950, 22
    Text "---------- Z A K U P --------------------------------------------------------------------", 15, 152, 950, 14
    Text l4, 15, 172, 950, 22
    Text "---------- S P R Z E D A Z --------------------------------------------------------------", 15, 200, 950, 14
    Text l5, 15, 220, 950, 22
    Text l6, 15, 243, 950, 22
    Text l7, 15, 266, 950, 22
    Text "-------------------------------------------------------------------------------------------", 15, 296, 950, 14
    Text l8, 15, 314, 950, 22

    Button "  >>>  &UTWORZ 4 DOKUMENTY  >>>  ", 15, 355, 600, 50, OK
    Button "  &ANULUJ  ",                       625, 355, 340, 50, ANULUJ
wynik_form = ExecForm

if wynik_form < 1 then
    Error ""
endif

// =====================================================================
// TWORZENIE 4 DOKUMENTOW
// =====================================================================

// Format liczb (string — bezpieczniej w setField)
sIlosc  = using "%.3f", iloscT
sCenaZ  = using "%.2f", cenaZakNetto
sCenaSB = using "%.2f", cenaSprzBrutto

// Termin platnosci (#3 fix: SQL DATEADD od data wystawienia, nie .today()+N)
sTermin = ObliczTermin(datap, terminDni)

opisZak = using "Zakup paszy %s (nrobcy %s)",                twrKod, numerObcy
opisSpz = using "Sprzedaz paszy %s do %s (marza %.2f zl/t)", twrKod, hodKod, marza

// ---------- 1) sPZ — Przyjecie od paszarni ----------
dokPz.setField("typDk", "PZ")
dokPz.setField("seria", "sPZ")
dokPz.setField("dataWystawienia",     datap)
dokPz.setField("dataOperacji",        datap)
dokPz.setField("dataDostawy",         datap)
dokPz.setField("dataDokumentuObcego", datap)
dokPz.setField("dataZakupu",          datap)
dokPz.setField("dzial",               DZIAL_MAG_PASZA)
dokPz.setField("bufor", "1")
dokPz.setField("opis", opisZak)
dokPz.setField("khKod",    pszKod)
dokPz.setField("kod",      twrKod)
dokPz.setField("ilosc",    sIlosc)
dokPz.setField("cena",     sCenaZ)
dokPz.setField("jednostka", twrJm)

gIdPZ = dokPz.importMg()
if gIdPZ <= 0 then
    Form "Blad importu PZ", 600, 160
        ground 254, 226, 226
        Text "BLAD: nie udalo sie utworzyc sPZ.",                          20, 20, 560, 22
        Text "Sprawdz khKod paszarni i kod towaru w Symfonii.",            20, 50, 560, 22
        Button "  &OK  ", 230, 100, 140, 40, OK
    ExecForm
    Error ""
endif
gNrPZ = PobierzNumerMG(gIdPZ)

// ---------- 1b) DEDUP check (#1) — czy juz jest FVZ od tej paszarni z tego dnia? ----------
duplikat = SprawdzDuplikatFV(pszKod, datap)
if duplikat <> "" then
    dupTxt = using "Od paszarni '%s' z dnia %s istnieje juz FVZ: %s", pszNazwa, datap, duplikat
    Form "DUPLIKAT FVZ — uwaga!", 700, 240
        ground 254, 226, 226
        Text "UWAGA: mozliwy DUPLIKAT faktury zakupu!",  20,  15, 660, 22
        Text dupTxt,                                     20,  45, 660, 22
        Text "PZ zostal juz utworzony (" + gNrPZ + "). Co dalej?", 20, 80, 660, 22
        Text "TAK = utworz mimo to drugi FVZ (np. inny numer obcy).", 20, 110, 660, 18
        Text "NIE = przerwij — usun PZ recznie z bufora i sprawdz.",  20, 130, 660, 18
        Button "  &TAK, kontynuuj  ",  20, 170, 320, 50, OK
        Button "  &NIE, przerwij  ", 350, 170, 320, 50, ANULUJ
    wynik_form = ExecForm
    if wynik_form < 1 then
        Error ""
    endif
endif

// ---------- 2) sFVZ — Faktura zakupu od paszarni ----------
dokFvz.setField("typDk", "FVZ")
dokFvz.setField("seria", "sFVZ")
dokFvz.setField("dataWystawienia",     datap)
dokFvz.setField("dataOperacji",        datap)
dokFvz.setField("dataDokumentuObcego", datap)
dokFvz.setField("dataDostawy",         datap)
dokFvz.setField("dataZakupu",          datap)
dokFvz.setField("termin",              sTermin)
dokFvz.setField("bufor", "1")
dokFvz.setField("dzial",               DZIAL_MAG_FAKTURY)
dokFvz.setField("opis", opisZak)
dokFvz.setField("numerObcy", numerObcy)
dokFvz.setField("khKod",    pszKod)
dokFvz.setField("kod",      twrKod)
dokFvz.setField("ilosc",    sIlosc)
dokFvz.setField("cena",     sCenaZ)
dokFvz.setField("jednostka", twrJm)

gIdFVZ = ImportZK(dokFvz)
if gIdFVZ <= 0 then
    Form "Blad importu FVZ", 600, 160
        ground 254, 226, 226
        Text "BLAD: nie udalo sie utworzyc sFVZ (faktura zakupu).", 20, 20, 560, 22
        Text "Sprawdz khKod paszarni, kod towaru, numer obcy.",      20, 50, 560, 22
        Button "  &OK  ", 230, 100, 140, 40, OK
    ExecForm
    Error ""
endif
gNrFVZ = PobierzNumerDok(gIdFVZ)

// ---------- 3) sWZ — Wydanie do hodowcy ----------
dokWz.setField("typDk", "WZ")
dokWz.setField("seria", "sWZ")
dokWz.setField("dataWystawienia", datap)
dokWz.setField("dataOperacji",    datap)
dokWz.setField("dataDostawy",     datap)
dokWz.setField("dzial",           DZIAL_MAG_PASZA)
dokWz.setField("bufor", "1")
dokWz.setField("opis", opisSpz)
dokWz.setField("khKod",    hodKod)
dokWz.setField("kod",      twrKod)
dokWz.setField("ilosc",    sIlosc)
dokWz.setField("cena",     sCenaZ)
dokWz.setField("jednostka", twrJm)

gIdWZ = dokWz.importMg()
if gIdWZ <= 0 then
    Form "Blad importu WZ", 600, 160
        ground 254, 226, 226
        Text "BLAD: nie udalo sie utworzyc sWZ (wydanie do hodowcy).", 20, 20, 560, 22
        Text "Sprawdz khKod hodowcy oraz stan magazynowy paszy.",       20, 50, 560, 22
        Button "  &OK  ", 230, 100, 140, 40, OK
    ExecForm
    Error ""
endif
gNrWZ = PobierzNumerMG(gIdWZ)

// ---------- 4) sFPP — Faktura sprzedazy do hodowcy (BRUTTO) ----------
dokFpp.setField("typDk", "FPP")
dokFpp.setField("seria", "sFPP")
dokFpp.setField("dataWystawienia", datap)
dokFpp.setField("dataOperacji",    datap)
dokFpp.setField("dataSprzedazy",   datap)
dokFpp.setField("termin",          sTermin)
dokFpp.setField("bufor", "1")
dokFpp.setField("dzial",           DZIAL_MAG_FAKTURY)
dokFpp.setField("opis", opisSpz)
dokFpp.setField("khKod",    hodKod)
dokFpp.setField("kod",      twrKod)
dokFpp.setField("ilosc",    sIlosc)
dokFpp.setField("cena",     sCenaSB)
dokFpp.setField("jednostka", twrJm)

gIdFPP = ImportSP(dokFpp)
if gIdFPP <= 0 then
    Form "Blad importu FPP", 600, 160
        ground 254, 226, 226
        Text "BLAD: nie udalo sie utworzyc sFPP (faktura sprzedazy).", 20, 20, 560, 22
        Text "Sprawdz khKod hodowcy i kod towaru.",                     20, 50, 560, 22
        Button "  &OK  ", 230, 100, 140, 40, OK
    ExecForm
    Error ""
endif
gNrFPP = PobierzNumerDok(gIdFPP)

// =====================================================================
// EKRAN 6 — PODSUMOWANIE
// =====================================================================
s1 = using "sPZ:   %s         (przyjecie od %s)",            gNrPZ,  pszNazwa
s2 = using "sFVZ:  %s         (faktura zakupu, nrobcy %s)",  gNrFVZ, numerObcy
s3 = using "sWZ:   %s         (wydanie do %s)",              gNrWZ,  hodNazwa
s4 = using "sFPP:  %s         (faktura sprzedazy do %s)",    gNrFPP, hodNazwa
s5 = using "Towar: %s   |   Ilosc: %.3f %s   |   Data: %s    Termin: %s",  twrNazwa, iloscT, twrJm, datap, sTermin
s6 = using "Zakup: %.2f zl netto   |   Sprzedaz: %.2f zl netto (%.2f zl brutto)",  wartZakNetto, wartSprzNetto, wartSprzBrutto
s7 = using "MARZA LACZNA: %.2f zl (%.2f zl/t)",                                    marzaLaczna, marza

Form "OK — utworzono 4 dokumenty w buforze Symfonii", 940, 380
    ground 220, 252, 231
    Text "==========================================================================================",  15,  8, 910, 14
    Text "          U T W O R Z O N E   D O K U M E N T Y   ( bufor Symfonii )",                        15, 24, 910, 22
    Text "==========================================================================================",  15, 50, 910, 14
    Text s1, 15,  78, 910, 22
    Text s2, 15, 102, 910, 22
    Text s3, 15, 126, 910, 22
    Text s4, 15, 150, 910, 22
    Text "------------------------------------------------------------------------------------------",  15, 180, 910, 14
    Text s5, 15, 200, 910, 22
    Text s6, 15, 224, 910, 22
    Text s7, 15, 248, 910, 22
    Button "  &ZAMKNIJ  ", 380, 295, 200, 50, OK
ExecForm
