//"ImportPaszaZKolejki.sc","Import kolejki PaszaImportQueue - czytelny grid + 4 akcje","\Procedury\Pasza\Import z kolejki",0,2.0.0,SYSTEM
// =====================================================================
// IMPORT PASZY: kolejka WPF (LibraNet) -> bufor Symfonii (PZ + FVZ + WZ + FPP)
// =====================================================================
// v2.0.0: czytelny grid z aktualnym stanem kolejki (wszystkie statusy),
//         multi-select via Tagged, 4 akcje (zaznaczone / wszystkie NOWY / odswiez / zamknij),
//         podsumowanie w osobnej formie.
// v1.0.0: prosta petla bez gridu.
//
// CO WIDZISZ NA EKRANIE:
//   Grid: Lp / Status / Data / Paszarnia / Hodowca / Towar / Ilosc /
//         Cena zak. / Marza / Wart. zak. / Wart. brutto / Dok. Symfonii (po imporcie)
//   Status: "NOWY" (do importu), "+IMPORT" (juz zaimportowane), "!BLAD", "-ANUL".
//   Akcje:
//     IMPORTUJ ZAZNACZONE - tylko wiersze z Tagged=-1 + Status=NOWY
//     IMPORTUJ WSZYSTKIE NOWE - wszystkie ze statusem NOWY
//     ODSWIEZ - przeladuj grid (po zewnetrznych zmianach)
//     ZAMKNIJ
//
// AmBasic gotchas (pamietamy z v2 i poprzednich rund):
//   - Wszystkie deklaracje top-level NA POCZATKU skryptu (zadne w if/while/sub-body).
//   - Brak elseif - tylko zagniezdzone if/else.
//   - Brak inline if-then-endif. Brak chr/replace.
//   - String compare przez '=', int compare przez '=='.
//   - Brak unicode (emoji, em-dash) w stringach - tylko CP1250 / polskie ogonki OK.
//   - Brak `return` - przypisanie do nazwy funkcji + implicit zwrot na endsub.
//   - 2 osobne ADODB.Connection (SELECT vs UPDATE) - zeby nie blokowac otwartego rs.

noOutput()

#define OK 2
#define ANULUJ -1

dispatch grid

// ====== TOP-LEVEL DEKLARACJE ======
String CONN_LIBRA
String DZIAL_MAG_PASZA
String DZIAL_MAG_FAKTURY

Dispatch conLibra
Dispatch conLibraUpdate
Dispatch rs
Dispatch rs2
Dispatch rsg
Dispatch rsc

int wynik_form
int gCntNowy
int gCntImp
int gCntBlad
int gCntAnul
int gImportedThisRun
int gErrorsThisRun
int dummy
int n
int i
int found
int rowsImported

// Per-row vars (reused w ImportujRekord)
long sId
String sPszKod
String sPszNazwa
String sHodKod
String sHodNazwa
String sTwrKod
String sTwrNazwa
String sJm
String sIlosc
String sCenaZak
String sMarza
String sCenaSprzBr
String sNumerObcy
String sData
String sTermin
String sStat
String sStatTxt
String sIdStr
String sIdStr2
long pId

long idPz
long idFvz
long idWz
long idFpp
String nrPz
String nrFvz
String nrWz
String nrFpp
String bladRek
String opisZak
String opisSpz
String sNumerObcyPz
// UWAGA: IORec dokPz/dokFvz/dokWz/dokFpp deklarowane lokalnie w ImportujRekord(),
// zeby kazdy wiersz dostal swiezy IORec. Trzymanie ich top-level powoduje "wycieki" pozycji
// pomiedzy wierszami (poprzednia pozycja zostaje w sekcji "Pozycja dokumentu").
String sql
String sqlUpd
String sqlR
String sqlg
String sqlc

// EKRAN labels
String hTytul1
String hBanner
String hCnt1
String hCnt2
String hCnt3
String hCnt4
String hLeg1
String hLeg2
String hLeg3
String hLicz1
String hLicz2
String hLicz3
String hLicz4
String gOstatniBlad
String gOstatniDokOk

// Init
CONN_LIBRA = "Provider=SQLOLEDB;Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;"
DZIAL_MAG_PASZA   = "Pasza"
DZIAL_MAG_FAKTURY = "MAG"
gImportedThisRun = 0
gErrorsThisRun = 0
gOstatniBlad = ""
gOstatniDokOk = ""

// =====================================================================
// HELPERY: numery dokumentow po imporcie (port z v2)
// =====================================================================
String sub PobierzNumerDok(long pIdDok)
    Dispatch conSage
    Dispatch rsSage
    String sqlS
    String wynik
    String idStr
    idStr = using "%d", pIdDok
    conSage = getAdoConnection()
    rsSage  = "ADODB.Recordset"
    sqlS = "SELECT ISNULL(kod,'') AS kod FROM HM.DK WHERE id = " + idStr
    rsSage.open(sqlS, conSage)
    if rsSage.EOF then
        wynik = ""
    else
        wynik = rsSage.fields("kod").value
    endif
    rsSage.close()
    PobierzNumerDok = wynik
endsub

String sub PobierzNumerMG(long pIdDok)
    Dispatch conSage
    Dispatch rsSage
    String sqlS
    String wynik
    String idStr
    idStr = using "%d", pIdDok
    conSage = getAdoConnection()
    rsSage  = "ADODB.Recordset"
    sqlS = "SELECT ISNULL(kod,'') AS kod FROM HM.MG WHERE id = " + idStr
    rsSage.open(sqlS, conSage)
    if rsSage.EOF then
        wynik = ""
    else
        wynik = rsSage.fields("kod").value
    endif
    rsSage.close()
    PobierzNumerMG = wynik
endsub

// =====================================================================
// PRZELICZ LICZNIKI per status
// =====================================================================
int sub PrzeliczLiczniki()
    String sStatLoc
    int cLoc

    gCntNowy = 0
    gCntImp  = 0
    gCntBlad = 0
    gCntAnul = 0

    rsc = "ADODB.Recordset"
    rsc.open("SELECT Status, COUNT(*) AS cnt FROM dbo.PaszaImportQueue GROUP BY Status", conLibra)
    while rsc.EOF == 0
        sStatLoc = rsc.fields("Status").value
        cLoc = rsc.fields("cnt").value
        if sStatLoc = "NOWY" then
            gCntNowy = cLoc
        else
            if sStatLoc = "IMPORTOWANE" then
                gCntImp = cLoc
            else
                if sStatLoc = "BLAD" then
                    gCntBlad = cLoc
                else
                    if sStatLoc = "ANULOWANE" then
                        gCntAnul = cLoc
                    endif
                endif
            endif
        endif
        rsc.moveNext()
    wend
    rsc.close()
    PrzeliczLiczniki = 0
endsub

// =====================================================================
// ZALADUJ GRID z calej kolejki (wszystkie statusy, ostatnie 200)
// =====================================================================
int sub ZaladujGrid()
    String sStatRaw
    int nLoc
    int iLoc

    nLoc = 0
    grid.RowCount = 0

    rsg = "ADODB.Recordset"
    sqlg = "SELECT TOP 200 Id, Status, PaszarniaNazwa, HodowcaNazwa, TowarNazwa,"
    sqlg = sqlg + " CAST(CAST(Ilosc AS DECIMAL(12,3)) AS VARCHAR(20)) + ' ' + ISNULL(TowarJm,'t') AS IloscStr,"
    sqlg = sqlg + " CAST(CAST(CenaZakNetto AS DECIMAL(10,2)) AS VARCHAR(20)) AS CenaZakStr,"
    sqlg = sqlg + " CAST(CAST(MarzaKwota AS DECIMAL(10,2)) AS VARCHAR(20)) AS MarzaStr,"
    sqlg = sqlg + " CAST(CAST(WartoscZakNetto AS DECIMAL(12,2)) AS VARCHAR(30)) AS WartZakStr,"
    sqlg = sqlg + " CAST(CAST(WartoscSprzBrutto AS DECIMAL(12,2)) AS VARCHAR(30)) AS WartBrStr,"
    sqlg = sqlg + " CONVERT(VARCHAR(10), DataWystawienia, 120) AS DataStr,"
    sqlg = sqlg + " ISNULL(NrPZ,'') AS NrPZ, ISNULL(NrFVZ,'') AS NrFVZ"
    sqlg = sqlg + " FROM dbo.PaszaImportQueue"
    sqlg = sqlg + " ORDER BY UtworzonoKiedy DESC"

    rsg.open(sqlg, conLibra)
    while rsg.EOF == 0
        grid.InsertRow(grid.RowCount)
        iLoc = grid.RowCount - 1
        sStatRaw = rsg.fields("Status").value

        // Status z prefixem (tylko ASCII, czytelne):
        //   NOWY        -> ">> NOWY"
        //   IMPORTOWANE -> "+OK   IMPORTOWANE"
        //   BLAD        -> "!!    BLAD"
        //   ANULOWANE   -> "--    ANUL"
        if sStatRaw = "NOWY" then
            sStatTxt = ">> NOWY"
        else
            if sStatRaw = "IMPORTOWANE" then
                sStatTxt = "+OK IMPORTOWANE"
            else
                if sStatRaw = "BLAD" then
                    sStatTxt = "!! BLAD"
                else
                    sStatTxt = "-- ANUL"
                endif
            endif
        endif

        grid.Rows(iLoc).Value(0)  = using "%d", nLoc + 1
        grid.Rows(iLoc).Value(1)  = sStatTxt
        grid.Rows(iLoc).Value(2)  = rsg.fields("DataStr").value
        grid.Rows(iLoc).Value(3)  = rsg.fields("PaszarniaNazwa").value
        grid.Rows(iLoc).Value(4)  = rsg.fields("HodowcaNazwa").value
        grid.Rows(iLoc).Value(5)  = rsg.fields("TowarNazwa").value
        grid.Rows(iLoc).Value(6)  = rsg.fields("IloscStr").value
        grid.Rows(iLoc).Value(7)  = rsg.fields("CenaZakStr").value
        grid.Rows(iLoc).Value(8)  = rsg.fields("MarzaStr").value
        grid.Rows(iLoc).Value(9)  = rsg.fields("WartZakStr").value
        grid.Rows(iLoc).Value(10) = rsg.fields("WartBrStr").value
        grid.Rows(iLoc).Value(11) = rsg.fields("NrPZ").value + " / " + rsg.fields("NrFVZ").value
        grid.Rows(iLoc).Value(12) = using "%d", rsg.fields("Id").value

        rsg.moveNext()
        nLoc = nLoc + 1
    wend
    rsg.close()

    ZaladujGrid = nLoc
endsub

// =====================================================================
// IMPORT POJEDYNCZEGO REKORDU (4 dokumenty)
// =====================================================================
int sub ImportujRekord(long pIdR)
    // IORec deklarowane lokalnie - fresh per wiersz (wzorzec v2)
    IORec dokPz
    IORec dokFvz
    IORec dokWz
    IORec dokFpp
    IORec dokFvs
    long idFvsFallback
    String typSprzedazUsed
    String sIdStr3

    sIdStr2 = using "%d", pIdR

    rs2 = "ADODB.Recordset"
    sqlR = "SELECT PaszarniaKhKod, PaszarniaNazwa, HodowcaKhKod, HodowcaNazwa,"
    sqlR = sqlR + " TowarKod, TowarNazwa, TowarJm,"
    sqlR = sqlR + " CAST(Ilosc AS VARCHAR(20)) AS IloscStr,"
    sqlR = sqlR + " CAST(CenaZakNetto AS VARCHAR(20)) AS CenaZakStr,"
    sqlR = sqlR + " CAST(CenaSprzBrutto AS VARCHAR(20)) AS CenaSprzBrStr,"
    sqlR = sqlR + " ISNULL(NumerObcy,'') AS NumerObcy,"
    sqlR = sqlR + " CONVERT(VARCHAR(10), DataWystawienia, 120) AS DataStr,"
    sqlR = sqlR + " CONVERT(VARCHAR(10), DATEADD(DAY, TerminDni, DataWystawienia), 120) AS TerminStr"
    sqlR = sqlR + " FROM dbo.PaszaImportQueue WHERE Id = " + sIdStr2
    rs2.open(sqlR, conLibra)

    if rs2.EOF then
        rs2.close()
        ImportujRekord = 0
    else
        sPszKod     = rs2.fields("PaszarniaKhKod").value
        sPszNazwa   = rs2.fields("PaszarniaNazwa").value
        sHodKod     = rs2.fields("HodowcaKhKod").value
        sHodNazwa   = rs2.fields("HodowcaNazwa").value
        sTwrKod     = rs2.fields("TowarKod").value
        sTwrNazwa   = rs2.fields("TowarNazwa").value
        sJm         = rs2.fields("TowarJm").value
        sIlosc      = rs2.fields("IloscStr").value
        sCenaZak    = rs2.fields("CenaZakStr").value
        sCenaSprzBr = rs2.fields("CenaSprzBrStr").value
        sNumerObcy  = rs2.fields("NumerObcy").value
        sData       = rs2.fields("DataStr").value
        sTermin     = rs2.fields("TerminStr").value
        rs2.close()

        nrPz = ""
        nrFvz = ""
        nrWz = ""
        nrFpp = ""
        bladRek = ""

        // PZ opis = "Dla <NazwaHodowcy>" (wg jawnego zyczenia Sergiusza).
        // PZ numerObcy = "<oryginalny nr FV paszarni> - <Hodowca>" (zeby wiadomo komu pasza poszla, wg screenu sPZ).
        // FVZ opis = "Dla <NazwaHodowcy>", numerObcy = "<oryginalny>" (bez suffixa).
        // WZ opis = "Od paszarni <Paszarnia>".
        // FPP opis = "Pasza <Paszarnia>".
        // dzial: TYLKO PZ + WZ (magazynowe). FVZ + FPP - BEZ dzial (v2 i dok. sec 6.11/6.12 tego nie ustawiaja - moglo to odrzucac dokumenty).
        opisZak = "Dla " + sHodNazwa
        opisSpz = "Od paszarni " + sPszNazwa
        sNumerObcyPz = sNumerObcy + " - " + sHodKod

        // ---- 1) PZ (Przyjecie magazynowe od paszarni; dzial=Pasza; importMg) ----
        // NAGLOWEK
        dokPz.setField("typDk", "PZ")
        dokPz.setField("seria", "sPZ")
        dokPz.setField("dataWystawienia",     sData)
        dokPz.setField("dataOperacji",        sData)
        dokPz.setField("dataDostawy",         sData)
        dokPz.setField("dataDokumentuObcego", sData)
        dokPz.setField("dataZakupu",          sData)
        dokPz.setField("dzial",               DZIAL_MAG_PASZA)
        dokPz.setField("bufor", "1")
        dokPz.setField("opis",                opisZak)
        dokPz.setField("numerObcy",           sNumerObcyPz)
        // KONTRAHENT
        dokPz.beginSection("daneKh")
            dokPz.setField("khKod", sPszKod)
        dokPz.endSection()
        // POZYCJA
        dokPz.beginSection("Pozycja dokumentu")
            dokPz.setField("kod",      sTwrKod)
            dokPz.setField("ilosc",    sIlosc)
            dokPz.setField("cena",     sCenaZak)
            dokPz.setField("jednostka", sJm)
        dokPz.endSection()
        idPz = dokPz.importMg()
        if idPz <= 0 then
            bladRek = "PZ failed"
        else
            nrPz = PobierzNumerMG(idPz)
        endif

        // ---- 2) FVZ (Faktura VAT zakupu od paszarni; ImportZK) ----
        // Wzorzec: ExportPZLibraNet_v2.sc ImportujFakture (PROVEN dla FVR/FVZ).
        // KLUCZOWE: v2 NIE USTAWIA dzial dla faktur (sprawdzone awk-em na zywym v2).
        if bladRek = "" then
            dokFvz.setField("typDk", "FVZ")
            dokFvz.setField("seria", "sFVZ")
            dokFvz.setField("dataWystawienia",     sData)
            dokFvz.setField("dataOperacji",        sData)
            dokFvz.setField("dataDokumentuObcego", sData)
            dokFvz.setField("dataDostawy",         sData)
            dokFvz.setField("dataZakupu",          sData)
            dokFvz.setField("termin",              sTermin)
            dokFvz.setField("bufor", "1")
            dokFvz.setField("opis",                opisZak)
            dokFvz.setField("numerObcy",           sNumerObcy)
            // KONTRAHENT
            dokFvz.beginSection("daneKh")
                dokFvz.setField("khKod", sPszKod)
            dokFvz.endSection()
            // POZYCJA
            dokFvz.beginSection("Pozycja dokumentu")
                dokFvz.setField("kod",      sTwrKod)
                dokFvz.setField("ilosc",    sIlosc)
                dokFvz.setField("cena",     sCenaZak)
                dokFvz.setField("jednostka", sJm)
            dokFvz.endSection()
            idFvz = ImportZK(dokFvz)
            if idFvz <= 0 then
                // Diagnostyka: pokaz dokladnie ze ImportZK zwrocil ze FVZ padl
                sIdStr = using "%d", idFvz
                bladRek = "FVZ failed (ImportZK ret=" + sIdStr + "; PZ ok: " + nrPz + "; nrObcy=" + sNumerObcy + "; khKod=" + sPszKod + "; twrKod=" + sTwrKod + ")"
            else
                nrFvz = PobierzNumerDok(idFvz)
            endif
        endif

        // ---- 3) WZ (Wydanie magazynowe do hodowcy; dzial=Pasza; importMg) ----
        // KLUCZOWE: pelen naglowek z 5 datami jak PZ (PZ dziala).
        // dzial=Pasza skoro PZ dziala z tym samym dzialem.
        if bladRek = "" then
            dokWz.setField("typDk", "WZ")
            dokWz.setField("seria", "sWZ")
            dokWz.setField("dataWystawienia",     sData)
            dokWz.setField("dataOperacji",        sData)
            dokWz.setField("dataDostawy",         sData)
            dokWz.setField("dataDokumentuObcego", sData)
            dokWz.setField("dataZakupu",          sData)
            dokWz.setField("dzial",               DZIAL_MAG_PASZA)
            dokWz.setField("bufor", "1")
            dokWz.setField("opis",                opisSpz)
            // KONTRAHENT (hodowca)
            dokWz.beginSection("daneKh")
                dokWz.setField("khKod", sHodKod)
            dokWz.endSection()
            // POZYCJA
            dokWz.beginSection("Pozycja dokumentu")
                dokWz.setField("kod",      sTwrKod)
                dokWz.setField("ilosc",    sIlosc)
                dokWz.setField("cena",     sCenaZak)
                dokWz.setField("jednostka", sJm)
            dokWz.endSection()
            idWz = dokWz.importMg()
            if idWz <= 0 then
                sIdStr = using "%d", idWz
                bladRek = "WZ failed (importMg ret=" + sIdStr + "; PZ: " + nrPz + "; FVZ: " + nrFvz + "; khKod hod=" + sHodKod + ")"
            else
                nrWz = PobierzNumerMG(idWz)
            endif
        endif

        // ---- 4) FPP (Faktura sprzedazy do hodowcy; ImportSP) ----
        // KLUCZOWE: BEZ dzial (v2 ImportujFakture nie ustawia, WZ_v3.sc tez nie).
        if bladRek = "" then
            dokFpp.setField("typDk", "FPP")
            dokFpp.setField("seria", "sFPP")
            dokFpp.setField("dataWystawienia", sData)
            dokFpp.setField("dataOperacji",    sData)
            dokFpp.setField("dataSprzedazy",   sData)
            dokFpp.setField("termin",          sTermin)
            dokFpp.setField("bufor", "1")
            dokFpp.setField("opis",            opisSpz)
            // KONTRAHENT (hodowca)
            dokFpp.beginSection("daneKh")
                dokFpp.setField("khKod", sHodKod)
            dokFpp.endSection()
            // POZYCJA (cena BRUTTO)
            dokFpp.beginSection("Pozycja dokumentu")
                dokFpp.setField("kod",      sTwrKod)
                dokFpp.setField("ilosc",    sIlosc)
                dokFpp.setField("cena",     sCenaSprzBr)
                dokFpp.setField("jednostka", sJm)
            dokFpp.endSection()
            idFpp = ImportSP(dokFpp)
            if idFpp <= 0 then
                // FALLBACK: jezeli FPP nie istnieje w Sage Sergiusza, sprobuj FVS
                // (WZ_v3.sc Sergiusza dla sprzedazy uzywa FVS+sFVS)
                sIdStr = using "%d", idFpp
                typSprzedazUsed = "FPP"
                dokFvs.setField("typDk", "FVS")
                dokFvs.setField("seria", "sFVS")
                dokFvs.setField("dataWystawienia", sData)
                dokFvs.setField("dataOperacji",    sData)
                dokFvs.setField("dataSprzedazy",   sData)
                dokFvs.setField("termin",          sTermin)
                dokFvs.setField("bufor", "1")
                // BEZ dzial - v2 ImportujFakture i WZ_v3.sc tez nie ustawiaja dla faktur
                dokFvs.setField("opis",            opisSpz)
                dokFvs.beginSection("daneKh")
                    dokFvs.setField("khKod", sHodKod)
                dokFvs.endSection()
                dokFvs.beginSection("Pozycja dokumentu")
                    dokFvs.setField("kod",      sTwrKod)
                    dokFvs.setField("ilosc",    sIlosc)
                    dokFvs.setField("cena",     sCenaSprzBr)
                    dokFvs.setField("jednostka", sJm)
                dokFvs.endSection()
                idFvsFallback = ImportSP(dokFvs)
                sIdStr3 = using "%d", idFvsFallback
                if idFvsFallback <= 0 then
                    bladRek = "FPP+FVS failed (FPP ret=" + sIdStr + "; FVS ret=" + sIdStr3 + "; PZ: " + nrPz + "; FVZ: " + nrFvz + "; WZ: " + nrWz + "; khKod hod=" + sHodKod + ")"
                else
                    nrFpp = PobierzNumerDok(idFvsFallback)
                    typSprzedazUsed = "FVS"
                endif
            else
                nrFpp = PobierzNumerDok(idFpp)
                typSprzedazUsed = "FPP"
            endif
        endif

        // ---- UPDATE statusu ----
        // bladRek przed SQL: zamien apostrofy na pojedyncze backticki zeby nie zlamac UPDATE.
        // (BladKomunikat ma rozmiar 4000 znakow w schemie.)
        if bladRek = "" then
            gOstatniDokOk = "OK Id=" + sIdStr2 + ": PZ=" + nrPz + " FVZ=" + nrFvz + " WZ=" + nrWz + " FPP/FVS=" + nrFpp
            sqlUpd = "UPDATE dbo.PaszaImportQueue SET"
            sqlUpd = sqlUpd + " Status = 'IMPORTOWANE',"
            sqlUpd = sqlUpd + " NrPZ = '"  + nrPz  + "',"
            sqlUpd = sqlUpd + " NrFVZ = '" + nrFvz + "',"
            sqlUpd = sqlUpd + " NrWZ = '"  + nrWz  + "',"
            sqlUpd = sqlUpd + " NrFPP = '" + nrFpp + "',"
            sqlUpd = sqlUpd + " ImportowanoKiedy = GETDATE()"
            sqlUpd = sqlUpd + " WHERE Id = " + sIdStr2
            conLibraUpdate.execute(sqlUpd)
            gImportedThisRun = gImportedThisRun + 1
        else
            // Zapamietaj OSTATNI blad zeby pokazac w Form podsumowania (bez SQL gmerania)
            gOstatniBlad = "Id=" + sIdStr2 + ": " + bladRek
            sqlUpd = "UPDATE dbo.PaszaImportQueue SET"
            sqlUpd = sqlUpd + " Status = 'BLAD',"
            sqlUpd = sqlUpd + " NrPZ = '"  + nrPz  + "',"
            sqlUpd = sqlUpd + " NrFVZ = '" + nrFvz + "',"
            sqlUpd = sqlUpd + " NrWZ = '"  + nrWz  + "',"
            sqlUpd = sqlUpd + " NrFPP = '" + nrFpp + "',"
            sqlUpd = sqlUpd + " BladKomunikat = '" + bladRek + "'"
            sqlUpd = sqlUpd + " WHERE Id = " + sIdStr2
            conLibraUpdate.execute(sqlUpd)
            gErrorsThisRun = gErrorsThisRun + 1
        endif

        ImportujRekord = 1
    endif
endsub

// =====================================================================
// AKCJE BUTTONS
// =====================================================================
int sub ImportujZaznaczone()
    int iLoc
    long pIdLoc
    String sStatLoc

    gImportedThisRun = 0
    gErrorsThisRun = 0
    rowsImported = 0

    iLoc = 0
    while iLoc < grid.RowCount
        if grid.Rows(iLoc).Tagged == -1 then
            sStatLoc = grid.Rows(iLoc).Value(1)
            // Tylko status NOWY mozemy importowac (skipujemy juz przetworzone)
            if sStatLoc = ">> NOWY" then
                pIdLoc = val(grid.Rows(iLoc).Value(12))
                dummy = ImportujRekord(pIdLoc)
                rowsImported = rowsImported + 1
            endif
        endif
        iLoc = iLoc + 1
    wend

    if rowsImported == 0 then
        Form "Brak zaznaczonych NOWY", 480, 170
            ground 248, 250, 252
            Text "Nie zaznaczyles zadnego wiersza ze statusem NOWY.",                   20, 30, 440, 22
            Text "Klik LEWA KRAWEDZ wiersza, ktory chcesz zaimportowac, potem ponow.",  20, 55, 440, 22
            Button "  OK  ", 170, 110, 140, 40, OK
        ExecForm
        ImportujZaznaczone = 0
    else
        dummy = PrzeliczLiczniki()
        dummy = ZaladujGrid()
        ImportujZaznaczone = OK
    endif
endsub

int sub ImportujWszystkieNowy()
    int iLoc
    long pIdLoc
    String sStatLoc

    gImportedThisRun = 0
    gErrorsThisRun = 0

    if gCntNowy == 0 then
        Form "Brak NOWY", 460, 170
            ground 248, 250, 252
            Text "Kolejka NIE MA pozycji ze statusem NOWY do importu.",  20, 30, 420, 22
            Text "Nie ma co importowac. Zamknij okno albo odswiez.",     20, 55, 420, 22
            Button "  OK  ", 160, 110, 140, 40, OK
        ExecForm
        ImportujWszystkieNowy = 0
    else
        iLoc = 0
        while iLoc < grid.RowCount
            sStatLoc = grid.Rows(iLoc).Value(1)
            if sStatLoc = ">> NOWY" then
                pIdLoc = val(grid.Rows(iLoc).Value(12))
                dummy = ImportujRekord(pIdLoc)
            endif
            iLoc = iLoc + 1
        wend
        dummy = PrzeliczLiczniki()
        dummy = ZaladujGrid()
        ImportujWszystkieNowy = OK
    endif
endsub

int sub OdswiezAkcja()
    dummy = PrzeliczLiczniki()
    dummy = ZaladujGrid()
    OdswiezAkcja = 0
endsub

// =====================================================================
// OnCommand HANDLER - setup grida przy otwarciu formy (id=0, msg=0)
// =====================================================================
int sub OnCommandMain(int id, int msg)
    int nLoc
    if id == 0 then
        if msg == 0 then
            grid.rowHeader   = 3
            grid.ColumnCount = 13
            grid.Locked      = 1

            grid.Columns(0).name  = "Lp"
            grid.Columns(0).width = 50
            grid.Columns(1).name  = "Status"
            grid.Columns(1).width = 150
            grid.Columns(2).name  = "Data"
            grid.Columns(2).width = 100
            grid.Columns(3).name  = "Paszarnia"
            grid.Columns(3).width = 250
            grid.Columns(4).name  = "-> Hodowca"
            grid.Columns(4).width = 250
            grid.Columns(5).name  = "Towar"
            grid.Columns(5).width = 200
            grid.Columns(6).name  = "Ilosc"
            grid.Columns(6).width = 90
            grid.Columns(7).name  = "Cena zak."
            grid.Columns(7).width = 95
            grid.Columns(8).name  = "Marza"
            grid.Columns(8).width = 80
            grid.Columns(9).name  = "Wart. zak. zl"
            grid.Columns(9).width = 115
            grid.Columns(10).name = "Wart. brutto zl"
            grid.Columns(10).width = 125
            grid.Columns(11).name = "Dok. Symfonii (PZ/FVZ)"
            grid.Columns(11).width = 200
            grid.Columns(12).name = ""
            grid.Columns(12).width = 0

            dummy = PrzeliczLiczniki()
            nLoc = ZaladujGrid()
        endif
    endif
    OnCommandMain = 0
endsub

// =====================================================================
// MAIN FLOW
// =====================================================================
conLibra = "ADODB.Connection"
conLibra.Open(CONN_LIBRA)
conLibraUpdate = "ADODB.Connection"
conLibraUpdate.Open(CONN_LIBRA)
rs = "ADODB.Recordset"

// Wstepny przelicz (zeby Tytul mogl pokazac stan)
dummy = PrzeliczLiczniki()

hLicz1 = using "Nowych:  %d",         gCntNowy
hLicz2 = using "Importowanych:  %d",  gCntImp
hLicz3 = using "Z bledem:  %d",       gCntBlad
hLicz4 = using "Anulowanych:  %d",    gCntAnul

hTytul1 = "ZPSP  >>>  Symfonia    PASZA - import z kolejki LibraNet  (bufor)"

Form hTytul1, 1620, 1020
    ground 248, 250, 252

    Text "==========================================================================================================================================================", 10,   8, 1600, 14
    Text "                              I M P O R T   P A S Z Y   z   K O L E J K I   L i b r a N e t",                                                                10,  22, 1600, 22
    Text "==========================================================================================================================================================", 10,  46, 1600, 14

    // LICZNIKI w naglowku
    Group "  STAN KOLEJKI (przy otwarciu)  ", 10, 70, 1600, 60
        Text hLicz1, 25,  92, 300, 22
        Text hLicz2, 340, 92, 340, 22
        Text hLicz3, 700, 92, 280, 22
        Text hLicz4, 1000, 92, 300, 22

    // LEGENDA
    Group "  LEGENDA  ", 10, 140, 1600, 80
        Text "  *  Klik LEWA KRAWEDZ wiersza = zaznaczenie do importu (mozesz zaznaczyc wiele).",                                                                       25, 160, 1570, 18
        Text "  *  'IMPORTUJ WSZYSTKIE NOWE' nie wymaga zaznaczania - bierze WSZYSTKIE wiersze ze statusem NOWY.",                                                       25, 180, 1570, 18
        Text "  *  Statusy: '>> NOWY' = do importu, '+OK IMPORTOWANE' = juz w buforze, '!! BLAD' = sprawdz BladKomunikat w SQL, '-- ANUL' = pominiete.",                  25, 200, 1570, 18

    // GRID
    Control "grid", grid, 10, 235, 1600, 690

    // AKCJE
    Group "  A K C J E  ", 10, 935, 1600, 65
        Button "  >>>  &IMPORTUJ ZAZNACZONE  >>>  ",  20, 952, 380, 45, ImportujZaznaczone()
        Button "  &WSZYSTKIE NOWE  ->  bufor  ",      410, 952, 380, 45, ImportujWszystkieNowy()
        Button "  &ODSWIEZ  ",                        800, 952, 260, 45, OdswiezAkcja()
        Button "  X   Z &AMKNIJ  ",                  1070, 952, 540, 45, ANULUJ

wynik_form = ExecForm OnCommandMain

if wynik_form < 1 then
    conLibra.Close()
    conLibraUpdate.Close()
    Error ""
endif

// =====================================================================
// EKRAN 2 - PODSUMOWANIE (po Importuj zaznaczone / Importuj wszystkie)
// =====================================================================
hCnt1 = using "Zaimportowano w tej operacji:   %d  pozycji",          gImportedThisRun
hCnt2 = using "Bledy w tej operacji:           %d  pozycji",          gErrorsThisRun
hCnt3 = using "AKTUALNY STAN KOLEJKI:  NOWY %d   IMPORTOWANE %d   BLAD %d   ANUL %d",   gCntNowy, gCntImp, gCntBlad, gCntAnul
hCnt4 = "Otworz Symfonia > bufor i zatwierdz. Numery dokumentow widoczne w gridzie (kolumna Dok. Symfonii)."

if gErrorsThisRun > 0 then
    hBanner = "                Z A K O N C Z O N O   Z   B L E D A M I   -   sprawdz kolumne BladKomunikat"
else
    hBanner = "                Z A K O N C Z O N O   P O M Y S L N I E   -   dokumenty w buforze Symfonii"
endif

Form "Podsumowanie importu paszy", 1300, 660
    ground 220, 252, 231
    Text "============================================================================================================================", 15,   8, 1270, 14
    Text hBanner,                                                                                                                     15,  26, 1270, 24
    Text "============================================================================================================================", 15,  54, 1270, 14

    Text hCnt1, 15, 100, 1270, 24
    Text hCnt2, 15, 130, 1270, 24
    Text "----------------------------------------------------------------------------------------------------------------------------", 15, 165, 1270, 14
    Text hCnt3, 15, 185, 1270, 24
    Text "----------------------------------------------------------------------------------------------------------------------------", 15, 220, 1270, 14
    Text hCnt4, 15, 240, 1270, 24

    // DIAGNOSTYKA - pokaz ostatni sukces i ostatni blad bezposrednio w UI
    Text "----------------------------------------------------------------------------------------------------------------------------", 15, 285, 1270, 14
    Text "OSTATNI POPRAWNIE UTWORZONY WIERSZ:",                                                                                       15, 305, 1270, 22
    Text gOstatniDokOk,                                                                                                                15, 330, 1270, 22
    Text "----------------------------------------------------------------------------------------------------------------------------", 15, 360, 1270, 14
    Text "OSTATNI BLAD (jezeli byl) - cala informacja diagnostyczna:",                                                                15, 380, 1270, 22
    Text gOstatniBlad,                                                                                                                15, 405, 1270, 22
    Text gOstatniBlad,                                                                                                                15, 425, 1270, 22
    Text gOstatniBlad,                                                                                                                15, 445, 1270, 22
    Text "----------------------------------------------------------------------------------------------------------------------------", 15, 475, 1270, 14
    Text "Jezeli wszystkie sa BLAD - sprawdz PaszaImportQueue.BladKomunikat (kolumna 4000 znakow).",                                  15, 495, 1270, 18

    Button "  &ZAMKNIJ  ", 550, 580, 200, 60, OK
ExecForm

conLibra.Close()
conLibraUpdate.Close()
