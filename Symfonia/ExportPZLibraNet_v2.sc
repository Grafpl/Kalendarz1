//"ExportPZLibraNet_v2.sc","Eksport PZ + FV + RWU z LibraNet","\Procedury\Raporty z menu kartotek\Magazyn",0,5.4.0,SYSTEM
// Wersja 5.4.0 — KRYTYCZNE: waga liczona SWIEZO z pol surowych (CROSS APPLY), NIE z PayWgt.
//                PayWgt bywal zapisany niespojnie przez WPF (ubytek na 1 wierszu, na 2 nie).
//                Teraz DoZapl = round(netto - padleKg - konfKg - ubytekKg - opas - klB) liczone w SQL.
//                Wzor 1:1 z PDF/karta Wyliczenia. Niezalezne od PayWgt.
// Wersja 5.3.0 — UPDATE HM.DK po imporcie ustawia teraz rodzaj + schemat='ZK':
//                ImportZK dla FVR daje schemat="ZKRR", a firmowy standard to "ZK" (367 vs 10 w historii).
//                Po naszym UPDATE FVR i FVZ maja zawsze schemat='ZK'.
// Wersja 5.2.0 — Rodzaj dokumentu zakupu USTAWIANY przez UPDATE HM.DK.rodzaj po imporcie
//                (setField("rodzaj") jest ignorowane przez ImportZK, wiec UPDATE bezposrednio).
//                FVR -> 107271 "Faktury RR" / FVZ -> 774162 "Kurczak VAT"
// Wersja 5.1.0 — proba setField("rodzaj") (ignorowane przez ImportZK).
// Wersja 5.0.0 — KLUCZOWE: pelne odzwierciedlenie PDF specyfikacji.
//                Waga = FarmerCalc.PayWgt (Do zapl. z PDF, juz po potraceniach: padle, konf., ubytek, opas., klB)
//                Cena = Price + Addition (cena bazowa + dodatek per kg)
//                Iloczyn = identyczna Wartosc jak w PDF.
//                Skipujemy wiersze z PayWgt = 0 (niezatwierdzone).
// Wersja 4.0.0 — przywrocono PZ + RWU (pelny lancuch):
//                Per dostawca: PZ -> FV (1:1).
//                Sumarycznie per dzien: RWU dla kazdego kodu towaru (-7 / -8).
//                Kolejnosc: PZ -> FV -> akumulacja kg -> (po petli) RWU.
//                Jesli PZ failuje -> skip dostawcy. Jesli FV failuje -> PZ zostaje, RWU pomija kg.
// Wersja 3.2.0 — polishing (FV-only), Typ z historii.
// Wersja 3.1.0 — uproszczony EKRAN 1.
// Wersja 3.0.0 — TYLKO FAKTURY ZAKUPU (FVR/FVZ).

noOutput()

#define OK 2
#define ANULUJ -1
#define TRYB_FVR 2
#define TRYB_FVZ 3

String mySrv
String myDb
String myUsr
String myPwd
String gConnStr
String gWarunekSymfonia
String datap

int dniTerminu
int wynik_form

String gRaportPZ
String gRaportFV
String gRaportRWU
String gRaportBledow

long gLastIdPZ
String gLastNrPZ
long gLastIdFV
String gLastNrFV
long gLastIdRWU7
String gLastNrRWU7
long gLastIdRWU8
String gLastNrRWU8

int gLicznikOK
int gLicznikSkip
int gLicznikErr
int gLicznikPZ
int gLicznikRWU

float gSumaKgFv
// Akumulatory RWU - sumaryczne kg per kod towaru (akumulowane w PZ, RWU na koncu petli)
float gSumaKg7
float gSumaKg8

// Globalne wyniki PobierzObieOstatnie (dla unikniecia 2 round-tripow per dostawca)
String gLastFVR
String gLastFVZ

// Globalne zmienne EKRANu 2
int gLiczbaWierszy
String tytul2
String licznikTxt

// =====================================================================
// WYRAZENIE "Do zaplaty" liczone SWIEZO z pol surowych (NIE z PayWgt!).
// MUSI dac IDENTYCZNY wynik jak PDF specyfikacji (kod C#: Math.Round(...) + .ToString("N0")).
//   netto    = COALESCE(NULLIF(NettoFarmWeight,0), NettoWeight)
//   srWaga   = netto / (LumQnt + DeclI2)
//   padleKg  = PiK ? 0 : ROUND_BANKOWO(DeclI2 × srWaga)             <- Math.Round w PDF = do parzystej
//   konfKg   = PiK ? 0 : ROUND_BANKOWO((DeclI3+DeclI4+DeclI5) × srWaga)
//   ubytekKg = ROUND_BANKOWO(netto × Loss)
//   opasKg   = ROUND_BANKOWO(Opasienie),  klBKg = ROUND_BANKOWO(KlasaB)
//   DoZapl   = ROUND(netto - padleKg - konfKg - ubytekKg - opasKg - klBKg, 0)  <- jak .ToString("N0") = w gore od .5
//
// KLUCZOWE ROZNICE vs poprzednia wersja (naprawiaja rozjazd 1 wiersza na 16):
//   1) Wszystko w DECIMAL (CAST) — eliminuje szum float przy dzieleniu srWaga.
//   2) Skladniki (pad/kon/uby/opas/klB) zaokraglane BANKOWO (half-to-even) — bo PDF uzywa Math.Round,
//      ktore domyslnie zaokragla do liczby parzystej. SQL ROUND() zaokragla w gore od .5 -> stad rozjazd
//      gdy iloczyn wpadnie dokladnie na X.5.
//   3) Finalne DoZapl zwyklym ROUND(,0) — bo PDF wyswietla doZaplaty przez .ToString("N0") (w gore od .5).
// PayWgt bywal zapisany niespojnie przez WPF, dlatego liczymy sami. gCrossApplyDoZapl -> FROM, gSelDoZapl -> kolumna.
String gCrossApplyDoZapl
String gSelDoZapl

gCrossApplyDoZapl = " CROSS APPLY (SELECT netto = CAST(COALESCE(NULLIF(fc.NettoFarmWeight,0), fc.NettoWeight) AS DECIMAL(28,8))) v1"
gCrossApplyDoZapl = gCrossApplyDoZapl + " CROSS APPLY (SELECT cnt = (ISNULL(fc.LumQnt,0)+ISNULL(fc.DeclI2,0))) vc"
gCrossApplyDoZapl = gCrossApplyDoZapl + " CROSS APPLY (SELECT srw = CASE WHEN vc.cnt > 0 THEN v1.netto / CAST(vc.cnt AS DECIMAL(28,8)) ELSE CAST(0 AS DECIMAL(28,8)) END) v2"
gCrossApplyDoZapl = gCrossApplyDoZapl + " CROSS APPLY (SELECT padR = CASE WHEN ISNULL(fc.IncDeadConf,0)=1 THEN CAST(0 AS DECIMAL(28,8)) ELSE CAST(ISNULL(fc.DeclI2,0) AS DECIMAL(28,8))*v2.srw END,"
gCrossApplyDoZapl = gCrossApplyDoZapl + " konR = CASE WHEN ISNULL(fc.IncDeadConf,0)=1 THEN CAST(0 AS DECIMAL(28,8)) ELSE CAST(ISNULL(fc.DeclI3,0)+ISNULL(fc.DeclI4,0)+ISNULL(fc.DeclI5,0) AS DECIMAL(28,8))*v2.srw END,"
gCrossApplyDoZapl = gCrossApplyDoZapl + " ubyR = v1.netto*CAST(ISNULL(fc.Loss,0) AS DECIMAL(28,10)),"
gCrossApplyDoZapl = gCrossApplyDoZapl + " opaR = CAST(ISNULL(fc.Opasienie,0) AS DECIMAL(28,8)),"
gCrossApplyDoZapl = gCrossApplyDoZapl + " klbR = CAST(ISNULL(fc.KlasaB,0) AS DECIMAL(28,8))) vr"
gCrossApplyDoZapl = gCrossApplyDoZapl + " CROSS APPLY (SELECT pad = (CASE WHEN vr.padR-FLOOR(vr.padR)<0.5 THEN FLOOR(vr.padR) WHEN vr.padR-FLOOR(vr.padR)>0.5 THEN FLOOR(vr.padR)+1 ELSE (CASE WHEN CAST(FLOOR(vr.padR) AS BIGINT)%2=0 THEN FLOOR(vr.padR) ELSE FLOOR(vr.padR)+1 END) END),"
gCrossApplyDoZapl = gCrossApplyDoZapl + " kon = (CASE WHEN vr.konR-FLOOR(vr.konR)<0.5 THEN FLOOR(vr.konR) WHEN vr.konR-FLOOR(vr.konR)>0.5 THEN FLOOR(vr.konR)+1 ELSE (CASE WHEN CAST(FLOOR(vr.konR) AS BIGINT)%2=0 THEN FLOOR(vr.konR) ELSE FLOOR(vr.konR)+1 END) END),"
gCrossApplyDoZapl = gCrossApplyDoZapl + " uby = (CASE WHEN vr.ubyR-FLOOR(vr.ubyR)<0.5 THEN FLOOR(vr.ubyR) WHEN vr.ubyR-FLOOR(vr.ubyR)>0.5 THEN FLOOR(vr.ubyR)+1 ELSE (CASE WHEN CAST(FLOOR(vr.ubyR) AS BIGINT)%2=0 THEN FLOOR(vr.ubyR) ELSE FLOOR(vr.ubyR)+1 END) END),"
gCrossApplyDoZapl = gCrossApplyDoZapl + " opa = (CASE WHEN vr.opaR-FLOOR(vr.opaR)<0.5 THEN FLOOR(vr.opaR) WHEN vr.opaR-FLOOR(vr.opaR)>0.5 THEN FLOOR(vr.opaR)+1 ELSE (CASE WHEN CAST(FLOOR(vr.opaR) AS BIGINT)%2=0 THEN FLOOR(vr.opaR) ELSE FLOOR(vr.opaR)+1 END) END),"
gCrossApplyDoZapl = gCrossApplyDoZapl + " klb = (CASE WHEN vr.klbR-FLOOR(vr.klbR)<0.5 THEN FLOOR(vr.klbR) WHEN vr.klbR-FLOOR(vr.klbR)>0.5 THEN FLOOR(vr.klbR)+1 ELSE (CASE WHEN CAST(FLOOR(vr.klbR) AS BIGINT)%2=0 THEN FLOOR(vr.klbR) ELSE FLOOR(vr.klbR)+1 END) END)) v3"
gCrossApplyDoZapl = gCrossApplyDoZapl + " CROSS APPLY (SELECT DoZapl = CAST(ROUND(v1.netto - v3.pad - v3.kon - v3.uby - v3.opa - v3.klb, 0) AS DECIMAL(18,0))) calc"

gSelDoZapl = "calc.DoZapl"

dispatch grid

mySrv = "192.168.0.109"
myDb  = "LibraNet"
myUsr = "pronova"
myPwd = "pronova"
gConnStr = "Provider=SQLOLEDB;Data Source=" + mySrv + ";Initial Catalog=" + myDb + ";User ID=" + myUsr + ";Password=" + myPwd

dniTerminu = 35
datap = data()

gLicznikOK = 0
gLicznikSkip = 0
gLicznikErr = 0

// =====================================================================
// EKRAN 1 — tylko wybor daty + 2 guziki
// =====================================================================
form "ZPSP  >>>  Symfonia    Eksport FV zakupu  v3", 560, 320
    ground 248, 250, 252

    Text "===========================================================", 15,  8, 530, 14
    Text "  E K S P O R T   P Z   +   F V   +   R W U",                  15, 22, 530, 24
    Text "          Przyjecia / Faktury / Rozchody ubojowe",              15, 48, 530, 18
    Text "          do bufora Symfonii .112                            ", 15, 66, 530, 18
    Text "===========================================================", 15, 86, 530, 14

    Group "  >>  W Y B I E R Z   D A T E   D O S T A W   <<  ", 15, 115, 530, 85
    Datedit "Data dostaw:", datap,    35, 145, 320, 32
    Text "(rozliczenia z tego dnia trafia do gridu na nastepnym ekranie)", 35, 178, 510, 18

    Button "  >>>  &POKAZ SPECYFIKACJE  >>>  ",  20, 230, 350, 60, OK
    Button "  &ANULUJ  ",                       390, 230, 155, 60, ANULUJ

wynik_form = ExecForm

if wynik_form < 1 then
    Error ""
endif

// Tryb domyslny: pokaz wszystkie rozliczenia z dnia
gWarunekSymfonia = ""

// =====================================================================
// FUNKCJE POMOCNICZE
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

// UPDATE bezposrednio na HM.DK po imporcie — setField na rodzaj/schemat jest ignorowane przez ImportZK.
// Ustawia:
//   - rodzaj (ID z HM.PurchaseDocumentKinds: FVR=107271, FVZ=774162)
//   - schemat (ksiegowanie) = 'ZK' zawsze (zgodnie ze standardem firmy — ImportZK dla FVR daje "ZKRR")
int sub UstawRodzajDk(long pIdDok, String pIdRodz)
    Dispatch con
    String sql
    String idStr
    idStr = using "%d", pIdDok
    con = getAdoConnection()
    sql = "UPDATE HM.DK SET rodzaj = " + pIdRodz + ", schemat = 'ZK' WHERE id = " + idStr
    con.execute(sql)
    UstawRodzajDk = 1
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

String sub PobierzNazweTowaru(String pKodTowaru)
    Dispatch con
    Dispatch rs
    String sql
    String wynik
    con = getAdoConnection()
    rs  = "ADODB.Recordset"
    sql = "SELECT ISNULL(nazwa,'') AS N FROM HM.TW WHERE kod = '" + pKodTowaru + "'"
    rs.open(sql, con)
    if rs.EOF then
        wynik = pKodTowaru
    else
        wynik = rs.fields("N").value
    endif
    rs.close()
    PobierzNazweTowaru = wynik
endsub

// Shortcut kontrahenta w Symfonii (krotki kod do daneKh.khKod) — wzorzec z WZ_v3.sc
String sub PobierzShortcutSymf(String pIdSymf)
    Dispatch con
    Dispatch rs
    String sql
    String wynik
    if pIdSymf == "0" then
        wynik = ""
    else
        con = getAdoConnection()
        rs  = "ADODB.Recordset"
        sql = "SELECT ISNULL(Shortcut,'') AS S FROM SSCommon.STContractors WHERE Id = " + pIdSymf
        rs.open(sql, con)
        if rs.EOF then
            wynik = ""
        else
            wynik = rs.fields("S").value
        endif
        rs.close()
    endif
    PobierzShortcutSymf = wynik
endsub

String sub PobierzNipSymf(String pIdSymf)
    Dispatch con
    Dispatch rs
    String sql
    String wynik
    if pIdSymf == "0" then
        wynik = ""
    else
        con = getAdoConnection()
        rs  = "ADODB.Recordset"
        sql = "SELECT ISNULL(NIP,'') AS NIP FROM SSCommon.STContractors WHERE Id = " + pIdSymf
        rs.open(sql, con)
        if rs.EOF then
            wynik = ""
        else
            wynik = rs.fields("NIP").value
        endif
        rs.close()
    endif
    PobierzNipSymf = wynik
endsub

int sub PobierzTypFakturyDostawcy(String pCustomerGID)
    Dispatch con
    Dispatch rs
    String sql
    int wynik
    String sNum
    con = createObject("ADODB.Connection")
    con.connectionString = gConnStr
    con.open()
    rs  = "ADODB.Recordset"
    sql = "SELECT CAST(ISNULL(CAST(IsVatowiec AS INT), -1) AS VARCHAR(5)) AS V FROM dbo.Dostawcy WHERE ID = '" + pCustomerGID + "'"
    rs.open(sql, con)
    if rs.EOF then
        wynik = -1
    else
        sNum = rs.fields("V").value
        wynik = val(sNum)
    endif
    rs.close()
    con.close()
    PobierzTypFakturyDostawcy = wynik
endsub

// Pobiera obie ostatnie FV (FVR + FVZ) w jednym zapytaniu. Wynik w globalnych gLastFVR / gLastFVZ.
int sub PobierzObieOstatnie(String pIdSymf)
    Dispatch con
    Dispatch rs
    String sql
    String typ
    String dataStr
    String kodStr
    String linia

    gLastFVR = "—"
    gLastFVZ = "—"

    if pIdSymf == "0" then
        PobierzObieOstatnie = 0
    else
        con = getAdoConnection()
        rs  = "ADODB.Recordset"
        sql = "SELECT typ_dk, CONVERT(VARCHAR(10), data, 120) AS d, ISNULL(kod,'') AS k FROM ("
        sql = sql + "  SELECT typ_dk, data, kod, "
        sql = sql + "    ROW_NUMBER() OVER (PARTITION BY typ_dk ORDER BY data DESC, id DESC) AS rn "
        sql = sql + "  FROM HM.DK "
        sql = sql + "  WHERE khid = " + pIdSymf + " "
        sql = sql + "    AND typ_dk IN ('FVR', 'FVZ') "
        sql = sql + "    AND ISNULL(anulowany,0) = 0 AND aktywny = 1 "
        sql = sql + "    AND data >= DATEADD(MONTH, -12, GETDATE()) "
        sql = sql + ") x WHERE rn = 1"
        rs.open(sql, con)
        While !rs.EOF
            typ     = rs.fields("typ_dk").value
            dataStr = rs.fields("d").value
            kodStr  = rs.fields("k").value
            linia   = dataStr + "  " + kodStr
            if typ == "FVR" then
                gLastFVR = linia
            endif
            if typ == "FVZ" then
                gLastFVZ = linia
            endif
            rs.moveNext()
        Wend
        rs.close()
        PobierzObieOstatnie = 1
    endif
endsub

// Zapis ID i numeru FV do LibraNet.FarmerCalc.
// pFiltrFC uzywa aliasu "fc" — robimy UPDATE poprzez podzapytanie.
int sub ZapiszFakture(String pFiltrFC, String pData, long pIdFV, String pNrFV)
    Dispatch con
    String sql
    String sIdFV
    sIdFV = using "%d", pIdFV
    con = createObject("ADODB.Connection")
    con.connectionString = gConnStr
    con.open()
    sql = "UPDATE dbo.FarmerCalc SET "
    sql = sql + "Symfonia = 1, "
    sql = sql + "SymfoniaIdFV = " + sIdFV + ", "
    sql = sql + "SymfoniaNrFV = '" + pNrFV + "', "
    sql = sql + "SymfoniaExportDate = GETDATE() "
    sql = sql + "WHERE ID IN (SELECT fc.ID FROM dbo.FarmerCalc fc WHERE " + pFiltrFC + " AND fc.CalcDate = '" + pData + "')"
    con.execute(sql)
    con.close()
    ZapiszFakture = 1
endsub

// =====================================================================
// IMPORT PZ — Przyjecie Zewnetrzne (HM.MG via importMg)
// Bufor=1, magazyn docelowy "M. PROD", per dostawca.
// Pozycje czytane z FarmerCalc + akumulacja kg do globalnych kubelkow (gSumaKg7/gSumaKg8).
// =====================================================================
int sub ImportujPZDostawcy(String pFiltrFC, String pShortcut, String pNazwa, String pData, String pKodTowaru)
    IORec dokPz
    Dispatch con
    Dispatch rs
    String sql
    String sWaga
    String sCena
    int nPoz
    float dWaga
    long idMg

    dokPz.setField("typDk", "PZ")
    dokPz.setField("seria", "sPZ")
    dokPz.setField("dataWystawienia",     pData)
    dokPz.setField("dataOperacji",        pData)
    dokPz.setField("dataDostawy",         pData)
    dokPz.setField("dataDokumentuObcego", pData)
    dokPz.setField("dataZakupu",          pData)
    dokPz.setField("dzial", "M. PROD")
    dokPz.setField("bufor", "1")
    dokPz.setField("opis", using "LibraNet %s", pNazwa)

    dokPz.beginSection("daneKh")
        dokPz.setField("khKod", pShortcut)
    dokPz.endSection()

    con = createObject("ADODB.Connection")
    con.connectionString = gConnStr
    con.open()
    rs  = "ADODB.Recordset"
    // Waga = DoZapl liczona SWIEZO z pol surowych (nie PayWgt — bywal niespojny).
    // Cena jednostkowa = Price + Addition. Iloczyn = identyczna Wartosc jak w PDF.
    sql = "SELECT ISNULL(CAST(" + gSelDoZapl + " AS VARCHAR(50)),'0') AS Waga, "
    sql = sql + "ISNULL(CAST((fc.Price + ISNULL(fc.Addition,0)) AS VARCHAR(50)),'0') AS Cena "
    sql = sql + "FROM dbo.FarmerCalc fc "
    sql = sql + gCrossApplyDoZapl + " "
    sql = sql + "WHERE " + pFiltrFC + " AND fc.CalcDate = '" + pData + "' "
    sql = sql + "AND " + gSelDoZapl + " > 0"
    rs.open(sql, con)
    nPoz = 0
    While !rs.EOF
        sWaga = rs.fields("Waga").value
        sCena = rs.fields("Cena").value
        dWaga = val(sWaga)
        if dWaga > 0 then
            dokPz.beginSection("Pozycja dokumentu")
                dokPz.setField("kod", pKodTowaru)
                dokPz.setField("ilosc", sWaga)
                dokPz.setField("cena", sCena)
                dokPz.setField("jednostka", "kg")
            dokPz.endSection()
            // Akumulacja do globalnych kubelkow RWU - per kod towaru
            if pKodTowaru == "Kurczak żywy -7" then
                gSumaKg7 = gSumaKg7 + dWaga
            else
                gSumaKg8 = gSumaKg8 + dWaga
            endif
            nPoz = nPoz + 1
        endif
        rs.moveNext()
    Wend
    rs.close()
    con.close()

    if nPoz == 0 then
        ImportujPZDostawcy = 0
    else
        idMg = dokPz.importMg()
        if idMg > 0 then
            gLastIdPZ = idMg
            gLastNrPZ = PobierzNumerMG(idMg)
            ImportujPZDostawcy = nPoz
        else
            ImportujPZDostawcy = 0
        endif
    endif
endsub

// =====================================================================
// IMPORT RWU — Rozchod ubojowy (HM.MG via importMg), 1 dokument z 1-2 pozycjami.
// gSumaKg7 -> pozycja "Kurczak żywy -7"
// gSumaKg8 -> pozycja "Kurczak żywy - 8"
// Wywolywane RAZ na koniec batch eksportu.
// =====================================================================
long sub ImportujRWUlaczone(String pData, float pKg7, float pKg8)
    IORec dokRwu
    long idMg
    String sKg7
    String sKg8
    int nPoz

    sKg7 = using "%.2f", pKg7
    sKg8 = using "%.2f", pKg8

    dokRwu.setField("typDk", "RWU")
    dokRwu.setField("seria", "sRWU")
    dokRwu.setField("dataWystawienia", pData)
    dokRwu.setField("dataOperacji", pData)
    dokRwu.setField("dzial", "M. PROD")
    dokRwu.setField("bufor", "1")
    dokRwu.setField("opis", using "Rozchod ubojowy %s", pData)

    nPoz = 0
    if pKg7 > 0 then
        dokRwu.beginSection("Pozycja dokumentu")
            dokRwu.setField("kod", "Kurczak żywy -7")
            dokRwu.setField("ilosc", sKg7)
            dokRwu.setField("cena", "0")
            dokRwu.setField("jednostka", "kg")
        dokRwu.endSection()
        nPoz = nPoz + 1
    endif
    if pKg8 > 0 then
        dokRwu.beginSection("Pozycja dokumentu")
            dokRwu.setField("kod", "Kurczak żywy - 8")
            dokRwu.setField("ilosc", sKg8)
            dokRwu.setField("cena", "0")
            dokRwu.setField("jednostka", "kg")
        dokRwu.endSection()
        nPoz = nPoz + 1
    endif

    if nPoz == 0 then
        ImportujRWUlaczone = 0
    else
        idMg = dokRwu.importMg()
        ImportujRWUlaczone = idMg
    endif
endsub

// =====================================================================
// IMPORT FAKTURY — bufor=1, jeden dokument per dostawca
// =====================================================================
int sub ImportujFakture(String pFiltrFC, String pShortcut, String pNazwa, String pData, String pTypDk, String pSeria, String pKodTowaru)
    IORec dokFv
    Date termin
    long dokId
    Dispatch con
    Dispatch rs
    String sql
    String sWaga
    String sCena
    String sRodzaj
    int nPoz
    int rcRodz
    float dWaga

    termin.today()
    termin.addDays(dniTerminu)
    gSumaKgFv = 0

    // Rodzaj dokumentu zakupu (HM.DK.rodzaj, ID z HM.PurchaseDocumentKinds):
    //   FVR -> 107271 = "Faktury RR"     (rolnik VAT RR)
    //   FVZ -> 774162 = "Kurczak VAT"    (vatowiec)
    if pTypDk == "FVR" then
        sRodzaj = "107271"
    else
        sRodzaj = "774162"
    endif

    dokFv.setField("typDk", pTypDk)
    dokFv.setField("seria", pSeria)
    dokFv.setField("dataWystawienia",     pData)
    dokFv.setField("dataOperacji",        pData)
    dokFv.setField("dataDokumentuObcego", pData)
    dokFv.setField("dataDostawy",         pData)
    dokFv.setField("dataZakupu",          pData)
    dokFv.setField("termin",              termin.toStr())
    dokFv.setField("bufor", "1")
    dokFv.setField("opis", using "LibraNet %s", pNazwa)

    dokFv.beginSection("daneKh")
        dokFv.setField("khKod", pShortcut)
    dokFv.endSection()

    con = createObject("ADODB.Connection")
    con.connectionString = gConnStr
    con.open()
    rs  = "ADODB.Recordset"
    // Waga = DoZapl liczona SWIEZO z pol surowych (nie PayWgt). Cena = Price + Addition.
    sql = "SELECT ISNULL(CAST(" + gSelDoZapl + " AS VARCHAR(50)),'0') AS Waga, "
    sql = sql + "ISNULL(CAST((fc.Price + ISNULL(fc.Addition,0)) AS VARCHAR(50)),'0') AS Cena "
    sql = sql + "FROM dbo.FarmerCalc fc "
    sql = sql + gCrossApplyDoZapl + " "
    sql = sql + "WHERE " + pFiltrFC + " AND fc.CalcDate = '" + pData + "' "
    sql = sql + "AND " + gSelDoZapl + " > 0"
    rs.open(sql, con)
    nPoz = 0
    While !rs.EOF
        sWaga = rs.fields("Waga").value
        sCena = rs.fields("Cena").value
        dWaga = val(sWaga)
        if dWaga > 0 then
            dokFv.beginSection("Pozycja dokumentu")
                dokFv.setField("kod", pKodTowaru)
                dokFv.setField("ilosc", sWaga)
                dokFv.setField("cena", sCena)
                dokFv.setField("jednostka", "kg")
            dokFv.endSection()
            gSumaKgFv = gSumaKgFv + dWaga
            nPoz = nPoz + 1
        endif
        rs.moveNext()
    Wend
    rs.close()
    con.close()

    if nPoz == 0 then
        ImportujFakture = 0
    else
        dokId = ImportZK(dokFv)
        if dokId > 0 then
            gLastIdFV = dokId
            gLastNrFV = PobierzNumerDok(dokId)
            // Po imporcie ustawiamy rodzaj dokumentu bezposrednio na HM.DK.rodzaj
            // (setField("rodzaj") jest ignorowane przez ImportZK)
            rcRodz = UstawRodzajDk(dokId, sRodzaj)
            ImportujFakture = nPoz
        else
            ImportujFakture = 0
        endif
    endif
endsub

// =====================================================================
// DIALOG WYBORU TYPU FAKTURY (gdy IsVatowiec nieustawiony i brak historii)
// =====================================================================
int sub ZapytajOTypFaktury(String pDostawca)
    int w
    String tyt
    tyt = using "?  Brak IsVatowiec  ?  %s", pDostawca
    form tyt, 600, 310
        ground 248, 250, 252
        Text "==================================================================", 10,  8, 580, 14
        Text "       W Y B I E R Z   T Y P   F A K T U R Y",                       10, 22, 580, 22
        Text "==================================================================", 10, 46, 580, 14

        Text "Dostawca nie ma ustawionego pola IsVatowiec w LibraNet.",            20,  72, 560, 20
        Text "Wybierz typ faktury recznie:",                                       20,  92, 560, 20
        Text pDostawca,                                                            20, 118, 560, 24

        Button "  >>  &FVR  -  Rolnik ryczaltowy  ",  30, 165, 260, 65, TRYB_FVR
        Button "  >>  FV&Z  -  Vatowiec  ",          310, 165, 260, 65, TRYB_FVZ

        Text "  *  Wskazowka: ustaw Dostawcy.IsVatowiec w LibraNet aby unikac pytania", 20, 250, 560, 22
    w = ExecForm
    if w == TRYB_FVZ then
        ZapytajOTypFaktury = 1
    else
        ZapytajOTypFaktury = 0
    endif
endsub

// =====================================================================
// LADOWANIE DANYCH DO GRIDU
// =====================================================================
int sub ZaladujGridDostawcow()
    Dispatch con
    Dispatch rs
    String sql
    String sCustomerGID
    String sIdSymf
    String sDostawca
    String sNetto
    String sCena
    String sTypTxt
    String sTypNum
    String sMapowanie
    String sIsPosrednik
    String sIdPosrednik
    int n
    int typ

    grid.RowCount = 0
    con = createObject("ADODB.Connection")
    con.connectionString = gConnStr
    con.open()
    rs  = "ADODB.Recordset"

    // Grupujemy per entity:
    //   - jesli fc.IdPosrednik IS NOT NULL  -> "P:idPosrednika" (jedna pozycja per Posrednik)
    //   - inaczej                            -> "D:CustomerGID"  (jedna pozycja per Dostawca)
    // Posrednik nadpisuje: nazwa, IdSymf, NIP, Typ=FVZ (zawsze).
    // Grid pokazuje DOKLADNIE te same wartosci co PDF specyfikacji:
    //   Netto = SUM(PayWgt) = "Do zapl." z PDF (po potraceniach: padle, konf., ubytek, opas., klB)
    //   Cena  = wazona srednia (Price+Addition) wazona po PayWgt — daje zgodna z PDF kalkulacje
    //           (gdy roznice cen wystepuja per wiersz; przy jednolitej cenie po prostu rowna Price+Addition)
    sql = "SELECT "
    sql = sql + "  CAST(CASE WHEN fc.IdPosrednik IS NOT NULL THEN 1 ELSE 0 END AS VARCHAR(1)) AS IsPosrednik, "
    sql = sql + "  ISNULL(CAST(fc.IdPosrednik AS VARCHAR(20)),'') AS IdPosrednik, "
    sql = sql + "  LTRIM(RTRIM(fc.CustomerGID)) AS CustomerGID, "
    sql = sql + "  COALESCE(p.Name1, d.ShortName, '?') AS Nazwa, "
    sql = sql + "  ISNULL(CAST(COALESCE(p.SymfoniaId, d.IdSymf) AS VARCHAR(20)),'0') AS IdSymf, "
    sql = sql + "  CASE WHEN fc.IdPosrednik IS NOT NULL THEN '1' "
    sql = sql + "       ELSE CAST(ISNULL(CAST(d.IsVatowiec AS INT), -1) AS VARCHAR(5)) END AS Typ, "
    sql = sql + "  ISNULL(CAST(CAST(SUM(" + gSelDoZapl + ") AS DECIMAL(18,2)) AS VARCHAR(30)),'0') AS Netto, "
    sql = sql + "  ISNULL(CAST(CAST(CASE WHEN SUM(" + gSelDoZapl + ") > 0 "
    sql = sql + "       THEN SUM(" + gSelDoZapl + " * (fc.Price + ISNULL(fc.Addition,0))) / SUM(" + gSelDoZapl + ") "
    sql = sql + "       ELSE 0 END AS DECIMAL(18,4)) AS VARCHAR(30)),'0') AS Cena "
    sql = sql + "FROM dbo.FarmerCalc fc "
    sql = sql + "LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(fc.CustomerGID)) = LTRIM(RTRIM(d.ID)) "
    sql = sql + "LEFT JOIN dbo.Posrednicy p ON fc.IdPosrednik = p.Id AND p.Aktywny = 1 "
    sql = sql + gCrossApplyDoZapl + " "
    sql = sql + "WHERE fc.CalcDate = '" + datap + "' "
    sql = sql + "AND fc.CustomerGID IS NOT NULL "
    sql = sql + "AND " + gSelDoZapl + " > 0 "
    sql = sql + "GROUP BY "
    sql = sql + "  CASE WHEN fc.IdPosrednik IS NOT NULL THEN 1 ELSE 0 END, "
    sql = sql + "  fc.IdPosrednik, fc.CustomerGID, p.Name1, d.ShortName, "
    sql = sql + "  p.SymfoniaId, d.IdSymf, d.IsVatowiec "
    sql = sql + "ORDER BY Nazwa"
    rs.open(sql, con)

    n = 0
    While !rs.EOF
        sIsPosrednik = rs.fields("IsPosrednik").value
        sIdPosrednik = rs.fields("IdPosrednik").value
        sCustomerGID = rs.fields("CustomerGID").value
        sDostawca    = rs.fields("Nazwa").value
        sIdSymf      = rs.fields("IdSymf").value
        sTypNum      = rs.fields("Typ").value
        typ          = val(sTypNum)
        sNetto       = rs.fields("Netto").value
        sCena        = rs.fields("Cena").value

        // Pobierz historie FVR + FVZ jednym zapytaniem
        PobierzObieOstatnie(sIdSymf)

        // Posrednik -> ZAWSZE FVZ. Inaczej deduce z IsVatowiec / historii.
        if sIsPosrednik == "1" then
            sTypTxt = "FVZ"
        else
            if typ == 1 then
                sTypTxt = "FVZ"
            else
                if typ == 0 then
                    sTypTxt = "FVR"
                else
                    if gLastFVZ != "—" then
                        sTypTxt = "FVZ?"
                    else
                        if gLastFVR != "—" then
                            sTypTxt = "FVR?"
                        else
                            sTypTxt = "?"
                        endif
                    endif
                endif
            endif
        endif

        // Prefix nazwy: POSR dla posrednikow zeby latwo bylo wizualnie odroznic
        if sIsPosrednik == "1" then
            sDostawca = "[POSR] " + sDostawca
        endif

        // Status mapowania na Symfonie
        if sIdSymf == "0" then
            sMapowanie = "!! BRAK MAPOWANIA"
        else
            sMapowanie = "OK  (IdSymf=" + sIdSymf + ")"
        endif

        n = n + 1
        grid.InsertRow(grid.RowCount)
        grid.Rows(grid.RowCount-1).Value(0) = using "%d", n
        grid.Rows(grid.RowCount-1).Value(1) = sDostawca
        grid.Rows(grid.RowCount-1).Value(2) = sNetto
        grid.Rows(grid.RowCount-1).Value(3) = sCena
        grid.Rows(grid.RowCount-1).Value(4) = sTypTxt
        grid.Rows(grid.RowCount-1).Value(5) = gLastFVR
        grid.Rows(grid.RowCount-1).Value(6) = gLastFVZ
        grid.Rows(grid.RowCount-1).Value(7) = sMapowanie
        grid.Rows(grid.RowCount-1).Value(8) = sCustomerGID
        grid.Rows(grid.RowCount-1).Value(9) = sIdSymf
        grid.Rows(grid.RowCount-1).Value(10) = sIdPosrednik

        rs.moveNext()
    Wend
    rs.close()
    con.close()
    ZaladujGridDostawcow = n
endsub

// =====================================================================
// EKSPORT - PER DOSTAWCA + PER BATCH
// =====================================================================
// EksportujDostawce — uwzglednia POSREDNIKA:
//   pIdPosrednik="" -> entity = Dostawca, filtr FarmerCalc: CustomerGID + IdPosrednik IS NULL
//   pIdPosrednik!="" -> entity = Posrednik, filtr FarmerCalc: IdPosrednik = X, zawsze FVZ
int sub EksportujDostawce(String pCustomerGID, String pIdSymf, String pIdPosrednik, String pNazwa, String pTypHint)
    String sTypDk
    String sSeria
    String sKodTowaru
    String sNipSymf
    String sShortcut
    String sFiltrFC
    String linia
    int typFakt
    int nPoz
    int nPozPZ
    int isPosrednik

    if pIdPosrednik == "" then
        isPosrednik = 0
    else
        isPosrednik = 1
    endif

    // Filtr FarmerCalc (wspolny dla PZ + FV + ZapiszFakture)
    if isPosrednik == 1 then
        sFiltrFC = "fc.IdPosrednik = " + pIdPosrednik
    else
        sFiltrFC = "fc.CustomerGID = '" + pCustomerGID + "' AND fc.IdPosrednik IS NULL"
    endif

    if pIdSymf == "0" then
        gLicznikSkip = gLicznikSkip + 1
        linia = "Skip " + pNazwa + " - brak IdSymf"
        if gRaportBledow == "" then
            gRaportBledow = linia
        else
            gRaportBledow = gRaportBledow + " | " + linia
        endif
        EksportujDostawce = 0
    else
        sShortcut = PobierzShortcutSymf(pIdSymf)
        if sShortcut == "" then
            gLicznikSkip = gLicznikSkip + 1
            linia = "Skip " + pNazwa + " - brak Shortcut w Symfonii"
            if gRaportBledow == "" then
                gRaportBledow = linia
            else
                gRaportBledow = gRaportBledow + " | " + linia
            endif
            EksportujDostawce = 0
        else
            // Posrednik = ZAWSZE FVZ. Dostawca = jak dotad.
            if isPosrednik == 1 then
                typFakt = 1
            else
                typFakt = PobierzTypFakturyDostawcy(pCustomerGID)
                if typFakt == -1 then
                    if pTypHint == "FVZ" then
                        typFakt = 1
                    else
                        if pTypHint == "FVZ?" then
                            typFakt = 1
                        else
                            if pTypHint == "FVR" then
                                typFakt = 0
                            else
                                if pTypHint == "FVR?" then
                                    typFakt = 0
                                else
                                    typFakt = ZapytajOTypFaktury(pNazwa)
                                endif
                            endif
                        endif
                    endif
                endif
            endif

            if typFakt == 1 then
                sTypDk = "FVZ"
                sSeria = "sFVZ"
                sKodTowaru = "Kurczak żywy - 8"
            else
                sTypDk = "FVR"
                sSeria = "sFVR"
                sKodTowaru = "Kurczak żywy -7"
            endif

            // KROK 1: PZ (kontrahent po khKod = Shortcut z STContractors, wzorzec WZ_v3.sc)
            nPozPZ = ImportujPZDostawcy(sFiltrFC, sShortcut, pNazwa, datap, sKodTowaru)
            if nPozPZ == 0 then
                gLicznikErr = gLicznikErr + 1
                linia = "ERR " + pNazwa + " - PZ nieutworzone (skip FV)"
                if gRaportBledow == "" then
                    gRaportBledow = linia
                else
                    gRaportBledow = gRaportBledow + " | " + linia
                endif
                EksportujDostawce = 0
            else
                gLicznikPZ = gLicznikPZ + 1
                linia = gLastNrPZ + " (" + pNazwa + ")"
                if gRaportPZ == "" then
                    gRaportPZ = linia
                else
                    gRaportPZ = gRaportPZ + " | " + linia
                endif

                // KROK 2: FAKTURA (kontrahent po khKod = Shortcut)
                nPoz = ImportujFakture(sFiltrFC, sShortcut, pNazwa, datap, sTypDk, sSeria, sKodTowaru)
                if nPoz > 0 then
                    ZapiszFakture(sFiltrFC, datap, gLastIdFV, gLastNrFV)
                    gLicznikOK = gLicznikOK + 1
                    linia = gLastNrFV + " " + sTypDk + " (" + pNazwa + ")"
                    if gRaportFV == "" then
                        gRaportFV = linia
                    else
                        gRaportFV = gRaportFV + " | " + linia
                    endif
                    EksportujDostawce = 1
                else
                    gLicznikErr = gLicznikErr + 1
                    linia = "ERR " + pNazwa + " - FV nieutworzona (PZ ok: " + gLastNrPZ + ")"
                    if gRaportBledow == "" then
                        gRaportBledow = linia
                    else
                        gRaportBledow = gRaportBledow + " | " + linia
                    endif
                    EksportujDostawce = 0
                endif
            endif
        endif
    endif
endsub

int sub PokazPodsumowanie()
    String tytul
    String linia1
    String txtPZ
    String txtFV
    String txtRWU
    String txtErr
    int dummy

    if gLicznikErr > 0 then
        tytul = using "Wynik - %d BLEDY (PZ %d / FV %d / RWU %d)", gLicznikErr, gLicznikPZ, gLicznikOK, gLicznikRWU
    else
        tytul = using "Wynik - PZ %d / FV %d / RWU %d", gLicznikPZ, gLicznikOK, gLicznikRWU
    endif

    linia1 = using "PZ: %d   |   FV: %d   |   RWU: %d   |   Pominieto: %d   |   Bledow: %d", gLicznikPZ, gLicznikOK, gLicznikRWU, gLicznikSkip, gLicznikErr

    if gRaportPZ == "" then
        txtPZ = "(brak)"
    else
        txtPZ = gRaportPZ
    endif
    if gRaportFV == "" then
        txtFV = "(brak)"
    else
        txtFV = gRaportFV
    endif
    if gRaportRWU == "" then
        txtRWU = "(brak)"
    else
        txtRWU = gRaportRWU
    endif
    if gRaportBledow == "" then
        txtErr = "(brak bledow)"
    else
        txtErr = gRaportBledow
    endif

    form tytul, 960, 700
        ground 248, 250, 252

        Text "================================================================================", 10,  8, 940, 14
        Text "  P O D S U M O W A N I E   E K S P O R T U   ( P Z  +  F V  +  R W U )",          10, 22, 940, 22
        Text "================================================================================", 10, 46, 940, 14
        Text linia1,                                                                              10, 66, 940, 22

        Group "  >>  P Z   (Przyjecie Zewnetrzne)  ",   10, 100, 935, 130
        Text txtPZ,                                      25, 125, 910, 95

        Group "  >>  F V   (Faktury zakupu)  ",         10, 235, 935, 175
        Text txtFV,                                      25, 260, 910, 140

        Group "  >>  R W U   (Rozchod ubojowy)  ",      10, 415, 935, 75
        Text txtRWU,                                     25, 438, 910, 45

        Group "  >>  P O M I N I E T E   /   B L E D Y  ", 10, 495, 935, 105
        Text txtErr,                                       25, 520, 910, 75

        Text "  Wszystko jest w BUFORZE Symfonii  -  zatwierdz recznie (FV: menu Zakup, PZ/RWU: menu Magazyn).", 20, 610, 760, 22
        Button "  X   &ZAMKNIJ  ",                                                                                770, 605, 170, 45, OK
    dummy = ExecForm
    PokazPodsumowanie = 1
endsub

// Reset wszystkich globalnych liczników/raportów przed batch eksportem
int sub ResetBatchGlobals()
    gLicznikOK = 0
    gLicznikSkip = 0
    gLicznikErr = 0
    gLicznikPZ = 0
    gLicznikRWU = 0
    gRaportPZ = ""
    gRaportFV = ""
    gRaportRWU = ""
    gRaportBledow = ""
    gSumaKg7 = 0
    gSumaKg8 = 0
    ResetBatchGlobals = 0
endsub

// Po zakonczeniu petli per-dostawca tworzymy JEDEN RWU z 1-2 pozycjami (-7 / -8).
int sub UtworzRWUDlaBatch()
    long idRWU
    String sKg7
    String sKg8
    String linia
    String nrRWU
    float lacznieKg

    lacznieKg = gSumaKg7 + gSumaKg8
    if lacznieKg == 0 then
        UtworzRWUDlaBatch = 0
    else
        idRWU = ImportujRWUlaczone(datap, gSumaKg7, gSumaKg8)
        if idRWU > 0 then
            nrRWU = PobierzNumerMG(idRWU)
            gLastIdRWU7 = idRWU
            gLastNrRWU7 = nrRWU
            gLastIdRWU8 = idRWU
            gLastNrRWU8 = nrRWU
            gLicznikRWU = 1
            sKg7 = using "%.2f", gSumaKg7
            sKg8 = using "%.2f", gSumaKg8
            linia = nrRWU + "   (-7: " + sKg7 + " kg,  -8: " + sKg8 + " kg)"
            gRaportRWU = linia
        else
            sKg7 = using "%.2f", gSumaKg7
            sKg8 = using "%.2f", gSumaKg8
            linia = "ERR RWU nieutworzony (-7: " + sKg7 + ", -8: " + sKg8 + ")"
            if gRaportBledow == "" then
                gRaportBledow = linia
            else
                gRaportBledow = gRaportBledow + " | " + linia
            endif
        endif
        UtworzRWUDlaBatch = 0
    endif
endsub

int sub EksportujZaznaczone()
    int i
    int zaznaczone
    int dummy
    String sCustomerGID
    String sIdSymf
    String sIdPosrednik
    String sDostawca
    String sTyp
    int rc

    zaznaczone = 0
    i = 0
    While i < grid.RowCount
        if grid.Rows(i).Tagged == -1 then
            zaznaczone = zaznaczone + 1
        endif
        i = i + 1
    Wend

    if zaznaczone == 0 then
        Message "Nie zaznaczono dostawcow! Klik w LEWA krawedz wiersza aby zaznaczyc."
        EksportujZaznaczone = 0
    else
        dummy = ResetBatchGlobals()

        i = 0
        While i < grid.RowCount
            if grid.Rows(i).Tagged == -1 then
                sDostawca    = grid.Rows(i).Value(1)
                sTyp         = grid.Rows(i).Value(4)
                sCustomerGID = grid.Rows(i).Value(8)
                sIdSymf      = grid.Rows(i).Value(9)
                sIdPosrednik = grid.Rows(i).Value(10)
                rc = EksportujDostawce(sCustomerGID, sIdSymf, sIdPosrednik, sDostawca, sTyp)
            endif
            i = i + 1
        Wend

        // Po pętli: RWU sumaryczne per kod towaru
        dummy = UtworzRWUDlaBatch()

        dummy = PokazPodsumowanie()
        dummy = ZaladujGridDostawcow()
        EksportujZaznaczone = 0
    endif
endsub

int sub EksportujWszystkie()
    int i
    int dummy
    String sCustomerGID
    String sIdSymf
    String sIdPosrednik
    String sDostawca
    String sTyp
    int rc

    if grid.RowCount == 0 then
        Message "Lista jest pusta - nic do eksportu."
        EksportujWszystkie = 0
    else
        dummy = ResetBatchGlobals()

        i = 0
        While i < grid.RowCount
            sDostawca    = grid.Rows(i).Value(1)
            sTyp         = grid.Rows(i).Value(4)
            sCustomerGID = grid.Rows(i).Value(8)
            sIdSymf      = grid.Rows(i).Value(9)
            sIdPosrednik = grid.Rows(i).Value(10)
            rc = EksportujDostawce(sCustomerGID, sIdSymf, sIdPosrednik, sDostawca, sTyp)
            i = i + 1
        Wend

        // Po pętli: RWU sumaryczne per kod towaru
        dummy = UtworzRWUDlaBatch()

        dummy = PokazPodsumowanie()
        dummy = ZaladujGridDostawcow()
        EksportujWszystkie = 0
    endif
endsub

// =====================================================================
// HANDLER — inicjalizacja gridu (id=0, msg=0). Tylko tu mozna ruszac gridem.
// =====================================================================
int sub OnCommand(int id, int msg)
    int n
    if id == 0 then
        if msg == 0 then
            grid.rowHeader = 3
            grid.ColumnCount = 11
            grid.Locked = 1

            grid.Columns(0).name = "Lp"
            grid.Columns(1).name = "Dostawca / Posrednik"
            grid.Columns(2).name = "Kg dzis (=DoZapl)"
            grid.Columns(3).name = "Cena (z dod.)"
            grid.Columns(4).name = "Typ"
            grid.Columns(5).name = "Ostatnia FVR (12m)"
            grid.Columns(6).name = "Ostatnia FVZ (12m)"
            grid.Columns(7).name = "Mapowanie Symfonia"
            grid.Columns(8).name = ""
            grid.Columns(9).name = ""
            grid.Columns(10).name = ""

            grid.Columns(0).width = 35
            grid.Columns(1).width = 260
            grid.Columns(2).width = 85
            grid.Columns(3).width = 80
            grid.Columns(4).width = 60
            grid.Columns(5).width = 180
            grid.Columns(6).width = 180
            grid.Columns(7).width = 175
            grid.Columns(8).width = 0
            grid.Columns(9).width = 0
            grid.Columns(10).width = 0

            n = ZaladujGridDostawcow()
        endif
    endif
    OnCommand = 0
endsub

// =====================================================================
// EKRAN 2 — Form z gridem + akcje
// =====================================================================
tytul2 = "ZPSP  >>>  Symfonia    Dostawcy z dnia " + datap

Form tytul2, 1220, 790
    ground 248, 250, 252

    // === BANNER ===
    Text "============================================================================================", 10,  8, 1200, 14
    Text "  D O S T A W C Y   D O   Z A F A K T U R O W A N I A         data: " + datap,                  10, 22, 1200, 22
    Text "============================================================================================", 10, 46, 1200, 14

    // === LEGENDA ===
    Group "  L E G E N D A  ", 10, 70, 1200, 100
    Text "  *  Klik w LEWA KRAWEDZ wiersza = zaznaczenie  (mozesz zaznaczyc dowolnie wiele).",                  25,  90, 1180, 18
    Text "  *  [POSR] = Posrednik -> PZ + FV zawsze na POSR (FVZ).  Bez [POSR] = Dostawca (hodowca).",          25, 110, 1180, 18
    Text "  *  Kg = DoZapl. z PDF (po potraceniach: padle, konf., ubytek, opas., klB). Cena = baza + dodatek.", 25, 130, 1180, 18
    Text "  *  Wiersze NIEZATWIERDZONE (PayWgt=0) sa pomijane. Zatwierdz specyfikacje przed eksportem.",        25, 150, 1180, 18

    // === GRID ===
    Control "grid", grid, 10, 185, 1200, 505

    // === AKCJE — pasek dolny ===
    Group "  A K C J E  ", 10, 700, 1200, 75
    Button "  >>>  &EKSPORTUJ ZAZNACZONE  ",    25, 720, 380, 50, EksportujZaznaczone()
    Button "  Eksportuj  &WSZYSTKIE  ",        415, 720, 380, 50, EksportujWszystkie()
    Button "  X   &ZAMKNIJ  ",                 970, 720, 230, 50, ANULUJ

ExecForm OnCommand

Error ""
