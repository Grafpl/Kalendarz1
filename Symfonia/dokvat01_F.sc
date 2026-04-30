//"dokvat01_F.sc","Dokument VAT I (formuła)","\Dokumenty\Sprzedaż\Dokument VAT\",0,2.1.9,SYSTEM
///////////////////////////
// Dokument VAT I (formuła)
// dokvat01_F.sc
///////////////////////////

#define SHOWGTU 1
#define XSYSTEM
#define DEKRET
#define PLATWYD
#define TOTAL_VAT_OPTIONAL

#ifndef SHOWOO
#define SHOWOO 1
#endif

#ifdef SIMPLIFIED                // dla poprawienia przejrzystosci kodu
Int CompactForm = 1                // "if-defowanie" pojedynczych linii
#else
Int CompactForm = 0
#endif

int VAT = 1
int offSWW = 0
int pozSWWoff(2)
int szerSTVAT
#include "Nagłówek i stopka raportu I"
#include "Engine do dokumentów"
long id_dokuemntu
long id_kontrahenta


//sprawdzenie czy FWS na podstawie RW
dispatch xDokMag//=xFactory.NewObject("BDokumentMg")
if  iTyp==FWS && xDocument.fakturawewnetrzna.count() then
    xDokMag=xDocument.fakturaWewnetrzna.item(1).PodajDokument()
    if xDokMag.Charakter==76 then //RW
        bRW=1
    else
        bRW=0
    endif
endif
float akcyza
int nDialog, tblIn,nlp
int kr,bc,nl,nc,nr,bbc,nbl,nb,nbr,bb,sc,sc2,sc3,grub,ss,numr,l12,yramk,dyramk,dxramk,bmp, grayTrigger, yBoldRamk, dyBoldRamk
string sNip, sBuf,s
int tbl0,tbl1,tbl2, i,j,x,y,nMalaStopka
int KolTab1(1),StlTab1(1),kol(1),kolumnyNP(1),StyleNP(1),kolNP(1),nkolumna
int KolTabPR(2)= 250,250
int StlTabPR(4)
int nTblWidth1,nTblWidth2,nDiff
int wSKK=170
int StlTabH(1), nSzer, KolTab2(1),StlTab2(1)
int nTblMarg = 5
long lTemp
int nResult
Float currBrutto, currExRate, currNetto, currNiePodlega, currTotal

#ifndef strNazwaVatOO_DEFINED
#define strNazwaVatOO_DEFINED
String strNazwaVatOO = "--"        // nazwa stawki VAT odwrotnego obciazenia
#endif

Int iZamienNazweVatOO = 1        // pozycje z odwrotnym obciazeniem -> zamieniaj "NP " na "--"

                                // wylaczamy w innych raportach
#ifdef SIMPLIFIED
iZamienNazweVatOO = 0
#endif
#ifdef PNABYWCE
iZamienNazweVatOO = 0
#endif
#ifdef ZAMOWIENIE
iZamienNazweVatOO = 0
#endif
If PLNonly != -1 Then
iZamienNazweVatOO = 0
EndIf

Float  fPlnOO = 0                // suma kwot zlotowkowych pozycji z odwrotnym obciazeniem
Float  fWalOO = 0                // suma kwot walutowych pozycji z odwrotnym obciazeniem

Int    iNP                        // wiersz podsumowania stawki VAT NP

Float  fBruttoSUM                // kwota brutto w podsumowaniu stawki VAT

if GRAF then
    str.wydruk ( 0, -1, -1 )
    strona 50,90,50,100
    CheckFooterSize(xDocument.TypDK)
endif

#include "Dokument sprzedaży - ramki - definicje"
#include "Dokument VAT - ustawienia"

//okno parametrow formuly pod faktura
string nr_partii, nr_samochodu, data_prod, poj_pobrane, poj_zdane,pal_zdane_d,pal_zdane_h1,pal_wydane_euro, pal_wydane_plastikowe
string s_notatka,nr_partii1,nr_partii2,nr_partii3,nr_partii4,nr_partii5,nr_partii6,nr_partii7,nr_partii8,nr_partii9,nr_partii10,nr_partii11,nr_partii12
string data_przyd_kurczak, data_przyd_elementy, data_przyd_podroby

// === Helper: dodaje nDays dni do daty yyyy-MM-dd, zwraca yyyy-MM-dd ===
// Obsluguje przejscia miedzy miesiacami i latami oraz lata przestepne.
// Jezeli data wejsciowa jest niepoprawna, zwraca ja bez zmian.
string sub AddDaysToDate(string sDate, int nDays)
    int rok, miesiac, dzien, dni_w_miesiacu, leap, gotowe, q
    string sM, sD

    rok     = val(mid(sDate, 1, 4))
    miesiac = val(mid(sDate, 6, 2))
    dzien   = val(mid(sDate, 9, 2))

    if rok < 1900 then
        AddDaysToDate = sDate
        exit
    endif
    if miesiac < 1 || miesiac > 12 then
        AddDaysToDate = sDate
        exit
    endif
    if dzien < 1 || dzien > 31 then
        AddDaysToDate = sDate
        exit
    endif

    dzien = dzien + nDays

    gotowe = 0
    while gotowe == 0
        // wyznacz liczbe dni w biezacym miesiacu
        dni_w_miesiacu = 31
        if miesiac == 4 then
            dni_w_miesiacu = 30
        endif
        if miesiac == 6 then
            dni_w_miesiacu = 30
        endif
        if miesiac == 9 then
            dni_w_miesiacu = 30
        endif
        if miesiac == 11 then
            dni_w_miesiacu = 30
        endif
        if miesiac == 2 then
            // rok przestepny: podzielny przez 4, ale nie przez 100, chyba ze przez 400
            leap = 0
            q = rok / 4
            if q * 4 == rok then
                leap = 1
                q = rok / 100
                if q * 100 == rok then
                    leap = 0
                    q = rok / 400
                    if q * 400 == rok then
                        leap = 1
                    endif
                endif
            endif
            if leap == 1 then
                dni_w_miesiacu = 29
            else
                dni_w_miesiacu = 28
            endif
        endif

        if dzien <= dni_w_miesiacu then
            gotowe = 1
        else
            dzien = dzien - dni_w_miesiacu
            miesiac = miesiac + 1
            if miesiac > 12 then
                miesiac = 1
                rok = rok + 1
            endif
        endif
    wend

    if miesiac < 10 then
        sM = using "0%l", miesiac
    else
        sM = using "%l", miesiac
    endif
    if dzien < 10 then
        sD = using "0%l", dzien
    else
        sD = using "%l", dzien
    endif
    AddDaysToDate = using "%l-%s-%s", rok, sM, sD
endsub

String id_doks
//identyfikator dokumentu
id_doks=arg1

// === Sciezka do pliku INI z preferencjami formuly ===
// Primary:  \\192.168.0.170\Public\SymfoniaINI\parametry.ini
// Fallback: \\192.168.0.171\Public\SymfoniaINI\parametry.ini
string sIniPath
sIniPath = "\\\\192.168.0.170\\Public\\SymfoniaINI\\parametry.ini"

// Probe - jezeli serwer 170 nie odpowiada, przepnij na 171
string sProbe
PutIni ("_health", "ts", "1", sIniPath)
sProbe = GetIni("_health", "ts", sIniPath)
if sProbe != "1" then
    sIniPath = "\\\\192.168.0.171\\Public\\SymfoniaINI\\parametry.ini"
endif

nr_partii1    =GetIni(id_doks,"nr_partii1",sIniPath)
nr_partii2    =GetIni(id_doks,"nr_partii2",sIniPath)
nr_partii3    =GetIni(id_doks,"nr_partii3",sIniPath)
nr_partii4    =GetIni(id_doks,"nr_partii4",sIniPath)
nr_partii5    =GetIni(id_doks,"nr_partii5",sIniPath)
nr_partii6    =GetIni(id_doks,"nr_partii6",sIniPath)
nr_partii7    =GetIni(id_doks,"nr_partii7",sIniPath)
nr_partii8    =GetIni(id_doks,"nr_partii8",sIniPath)
nr_partii9    =GetIni(id_doks,"nr_partii9",sIniPath)
nr_partii10    =GetIni(id_doks,"nr_partii10",sIniPath)
nr_partii11    =GetIni(id_doks,"nr_partii11",sIniPath)
nr_partii12    =GetIni(id_doks,"nr_partii12",sIniPath)



//Wyciąganie danych z poprzednich wpisów. Jeśli brak to puste konkretne formułki
nr_samochodu            =GetIni(id_doks,"nr_samochodu",sIniPath)
data_prod                =GetIni(id_doks,"data_prod",sIniPath)
data_przyd_kurczak        =GetIni(id_doks,"data_przyd_kurczak",sIniPath)
data_przyd_elementy        =GetIni(id_doks,"data_przyd_elementy",sIniPath)
data_przyd_podroby        =GetIni(id_doks,"data_przyd_podroby",sIniPath)
poj_zdane               =GetIni(id_doks,"poj_zdane",sIniPath)
pal_zdane_d             =GetIni(id_doks,"pal_zdane_d",sIniPath)
pal_zdane_h1            =GetIni(id_doks,"pal_zdane_h1",sIniPath)
pal_wydane_euro            =GetIni(id_doks,"pal_wydane_euro",sIniPath)
pal_wydane_plastikowe    =GetIni(id_doks,"pal_wydane_plastikowe",sIniPath)

buf=data_przyd_kurczak
if find regular "^{????}{?}{??}{?}{??}$" then
    data_przyd_kurczak = buf
else
data_przyd_kurczak = ""
endif

buf=data_przyd_elementy
if find regular "^{????}{?}{??}{?}{??}$" then
    data_przyd_elementy = buf
else
data_przyd_elementy = ""
endif

buf=data_przyd_podroby
if find regular "^{????}{?}{??}{?}{??}$" then
    data_przyd_podroby = buf
else
data_przyd_podroby = ""
endif

// === Walidacja data_prod (yyyy-MM-dd); jezeli pusta/niepoprawna - dzisiaj ===
buf=data_prod
if find regular "^{????}{?}{??}{?}{??}$" then
    data_prod = buf
else
    data_prod = data()
endif

// === Auto-wyliczenie dat przydatnosci OD daty produkcji ===
// Wylicza TYLKO gdy w INI nie ma zapisanej wartosci (puste po walidacji wyzej).
// Jezeli uzytkownik wczesniej zapisal wlasna date - jest ona zachowana.
//   tuszka   = data_prod + 7 dni
//   elementy = data_prod + 6 dni
//   podroby  = data_prod + 4 dni
if data_przyd_kurczak == "" then
    data_przyd_kurczak = AddDaysToDate(data_prod, 7)
endif
if data_przyd_elementy == "" then
    data_przyd_elementy = AddDaysToDate(data_prod, 6)
endif
if data_przyd_podroby == "" then
    data_przyd_podroby = AddDaysToDate(data_prod, 4)
endif


//Tworzenie formułki|okna
FORM "Formuła pod fakturą - parametry", 740, 440
//    GROUP "", 8, 4,780, 395
    Ground 240,230,200
    //Lewa strona partii
    EDIT     "Numer partii 1:", nr_partii1, 100, 27, 204, 20
    EDIT     "Numer partii 2:", nr_partii2, 100, 57, 204, 20
    EDIT     "Numer partii 3:", nr_partii3, 100, 87, 204, 20
    EDIT     "Numer partii 4:", nr_partii4, 100, 117, 204, 20
    EDIT    "Numer partii 5:", nr_partii5, 100, 147, 204, 20
    EDIT     "Numer partii 6:", nr_partii6, 100, 177, 204, 20
    //Prawa strona partii
    EDIT     "Numer partii 7:",  nr_partii7, 400, 27, 204, 20
    EDIT     "Numer partii 8:",  nr_partii8, 400, 57, 204, 20
    EDIT     "Numer partii 9:",  nr_partii9, 400, 87, 204, 20
    EDIT     "Numer partii 10:", nr_partii10, 400, 117, 204, 20
    EDIT     "Numer partii 11:", nr_partii11, 400, 147, 204, 20
    EDIT    "Numer partii 12:", nr_partii12, 400, 177, 204, 20
    //Lewa strona pojemnikow
    EDIT     "Pojemniki wydane:", poj_zdane,                          142, 235, 72, 20
    EDIT     "Palety drewniane wydane:", pal_zdane_d,                 142, 262, 72, 20
    EDIT     "Palety H1 wydane:", pal_zdane_h1,                       142, 292, 72, 20
    EDIT     "Palety euro wydane:", pal_wydane_euro,                   142, 322, 72, 20
    EDIT     "Palety plastikowe wydane:", pal_wydane_plastikowe,        142, 352, 72, 20

    // data_prod, data_przyd_kurczak/elementy/podroby zostaly wyliczone PRZED FORM
    // (na podstawie data_prod + 7/6/4 dni) - patrz blok "Auto-wyliczenie dat przydatnosci"

    //Prawa strona dat i aut
    EDIT     "Numer samochodu:", nr_samochodu,                         400, 235, 204, 20
    DatEdit "Data produkcji:", data_prod,                             400, 265, 204, 20
    DatEdit "Data przydatności tuszki:", data_przyd_kurczak,           400, 292, 204, 20
    DatEdit "Data przydatności elementów:", data_przyd_elementy,       400, 322, 204, 20
    DatEdit "Data przydatności podrobów:", data_przyd_podroby,         400, 352, 204, 20
    //Buttony
    BUTTON     "&Anuluj",      75, 378, 150, 28, -1   //  75 - odległość od lewej ramki form // 236 - odległość od górnej krawędzi// 100 - szerokość przycisku  // 28 - wysokośćprzycisku // 2- funkcja przekazująca typ przycisku
    BUTTON     "&OK",         403, 378, 150, 28, 2


ExecFormPC()

//Sprawdzanie pustych miejsc lub powtarzających się
if poj_zdane             == "" then poj_zdane  = "0"
if pal_zdane_d             == "" then pal_zdane_d = "0"
if pal_zdane_h1            == "" then pal_zdane_h1 = "0"
if pal_wydane_euro         == "" then pal_wydane_euro = "0"
if pal_wydane_plastikowe== "" then pal_wydane_plastikowe = "0"
if nr_samochodu         == "" then nr_samochodu  = "Brak auta"              //Else nr_samochodu             == "Brak auta" endif
if nr_samochodu         == "Brak auta" then nr_samochodu  = "Brak auta"
if data_prod             == "" then data_prod  = "Brak daty produkcji"       //Else data_prod                 == "Brak daty produkcji" endif
if data_prod             == "Brak daty produkcji" then data_prod  = "Brak daty produkcji"
if data_przyd_kurczak     == "" then data_przyd_kurczak  = "Brak tuszki"         //Else data_przyd_kurczak     == "Brak tuszki" endif
if data_przyd_kurczak     == "Brak tuszki" then data_przyd_kurczak  = "Brak tuszki"
if data_przyd_elementy     == "" then data_przyd_elementy = "Brak elementów"    //Else data_przyd_elementy    == "Brak elementów" endif
if data_przyd_elementy     == "Brak elementów" then data_przyd_elementy = "Brak elementów"
if data_przyd_podroby    == "" then data_przyd_podroby = "Brak podrobów"     //Else data_przyd_podroby     == "Brak podrobów" endif
if data_przyd_podroby    == "Brak podrobów" then data_przyd_podroby = "Brak podrobów"

//Wpisywanie wartości do pliku Ini
PutIni (id_doks,"nr_partii1",nr_partii1,sIniPath)
PutIni (id_doks,"nr_partii2",nr_partii2,sIniPath)
PutIni (id_doks,"nr_partii3",nr_partii3,sIniPath)
PutIni (id_doks,"nr_partii4",nr_partii4,sIniPath)
PutIni (id_doks,"nr_partii5",nr_partii5,sIniPath)
PutIni (id_doks,"nr_partii6",nr_partii6,sIniPath)
PutIni (id_doks,"nr_partii7",nr_partii7,sIniPath)
PutIni (id_doks,"nr_partii8",nr_partii8,sIniPath)
PutIni (id_doks,"nr_partii9",nr_partii9,sIniPath)
PutIni (id_doks,"nr_partii10",nr_partii10,sIniPath)
PutIni (id_doks,"nr_partii11",nr_partii11,sIniPath)
PutIni (id_doks,"nr_partii12",nr_partii12,sIniPath)
PutIni (id_doks,"nr_samochodu",nr_samochodu,sIniPath)
PutIni (id_doks,"data_prod",data_prod,sIniPath)
PutIni (id_doks,"data_przyd_kurczak",data_przyd_kurczak,sIniPath)
PutIni (id_doks,"data_przyd_elementy",data_przyd_elementy,sIniPath)
PutIni (id_doks,"data_przyd_podroby",data_przyd_podroby,sIniPath)
PutIni (id_doks,"poj_zdane",poj_zdane,sIniPath)
PutIni (id_doks,"pal_zdane_d",pal_zdane_d,sIniPath)
PutIni (id_doks,"pal_zdane_h1",pal_zdane_h1,sIniPath)
PutIni (id_doks,"pal_wydane_euro",pal_wydane_euro,sIniPath)
PutIni (id_doks,"pal_wydane_plastikowe",pal_wydane_plastikowe,sIniPath)


//Tworzenie pętli, do zwiększania numeru partii
if nr_partii1!="" then 
    nr_partii+=nr_partii1
endif
if nr_partii2!="" then 
    nr_partii+=","+nr_partii2
endif
if nr_partii3!="" then 
    nr_partii+=","+nr_partii3
endif
if nr_partii4!="" then 
    nr_partii+=","+nr_partii4
endif
if nr_partii5!="" then 
    nr_partii+=","+nr_partii5
endif
if nr_partii6!="" then 
    nr_partii+=","+nr_partii6
endif
if nr_partii7!="" then 
    nr_partii+=","+nr_partii7
endif
if nr_partii8!="" then 
    nr_partii+=","+nr_partii8
endif
if nr_partii9!="" then 
    nr_partii+=","+nr_partii9
endif
if nr_partii10!="" then 
    nr_partii+=","+nr_partii10
endif
if nr_partii11!="" then 
    nr_partii+=","+nr_partii11
endif
if nr_partii12!="" then 
    nr_partii+=","+nr_partii12
endif
nr_partii+="."

//zapisanie parametrow formuly w notatce dokumentu
s_notatka += using "Numer partii: %s\n",nr_partii
s_notatka += using "Numer samochodu: %s\n",nr_samochodu
s_notatka += using "Data Produkcji: %s\n",data_prod
s_notatka += using "Data Przydatności Kurczaka: %s\n",data_przyd_kurczak
s_notatka += using "Data Przydatności Elementy: %s\n",data_przyd_elementy
s_notatka += using "Data Przydatności Podroby: %s\n",data_przyd_podroby
s_notatka += using "Pojemniki wydane %s\n",poj_zdane
s_notatka += using "Palety H1 wydane: %s\n",pal_zdane_h1
s_notatka += using "Palety Drewniane wydane: %s\n",pal_zdane_d
s_notatka += using "Palety Euro wydane: %s\n",pal_wydane_euro
s_notatka += using "Palety Plastikowe wydane: %s\n",pal_wydane_plastikowe


xDocument.ZapiszNotatke(s_notatka)
//
#include "Dokument sprzedaży - ramki"

#ifdef SIMPLIFIED
Int TaxCount = 0                              // Ilosc roznych stawek VAT w pozycjach dokumentu
For i = 1 To i > Size( StawkiVat )
    TaxCount += Sign( StawkiVAT( i ).netto )
Next i
#endif

int sub Add(int pos, int szer, int stl, int typ )
    int i,siz=size(KolTab1)
    if KolTab1(1) then  //jeśli nie pierwsze wywołanie
        siz+=1
        Grow KolTab1,1
        Grow StlTab1,1
        Grow kol,1
    endif
    if pos == -1 then
        pos=siz
    else
        if pos >= SWW then
            i = 1
            while kol(i)!=pos && i<siz
                i += 1
            wend
            if i==siz then Error using "Zapetlenie! %d",pos
            pos = i
        endif
        For i = siz To i<=pos+1 step -1
            KolTab1(i) = KolTab1(i-1)
            StlTab1(i) = StlTab1(i-1)
            kol(i) = kol(i-1)
        Next i
        pos+=1
    endif
    KolTab1(pos) = szer
    StlTab1(pos) = stl
    kol(pos) = typ
endsub

//-----------------------------------------
if !GRAF then goto wydruk_tekstowy
bc = Styl ( "nagłówek", 0, "bc" )
nl = Styl ( "tekst", -1, "nl" )
nc = Styl ( "tekst", 0, "nc" )
nr = Styl ( "tekst", 1, "nr" )
kr = Styl ( "kwota", 1, "kr" )
nb = CopyFont ( "tekst",1 )
grub = CopyFont ( "tekst", 1, 45 )
numr = CopyFont ( "tytuł", 1, 45 )
nbl = Styl ( nb, -1, "nbl" )
ss = CopyFont ( "tekst", -1, 25 )
sc = Styl ( ss, 0, "sc" )
sc2 = Styl ( CopyFont ( "tekst", -1, 30 ), 0, "sc2" )
sc3 = Styl ( CopyFont ( "tekst", -1, 32 ), 0, "sc3" )
bb = CopyFont ( "tytuł", 1, 50 )
bbc = Styl ( bb, 0, "bbc" )
StlTabPR(1)=StlTabPR(2)=kr
#ifdef SIMPLIFIED
Add( -1, 105, nc, 0 )
If TaxCount > 1 Then
    Add( -1, Str.Szer - 105 - 165, nl, 0 )
    Add( -1, 165, kr, STAWKAVAT )
Else
    Add( -1, Str.Szer - 105, nl, 0 )
EndIf
#else
Add ( -1, 50, nc, 0 )
Add ( -1, 500, nl, 0 )
if nSWW==2 then KolTab1(2) += 100
if nKod==1 then KolTab1(2) += 100
Add ( -1, 100, kr, ILOSC )
Add ( -1, 80, nl, JM )

if bCenaPod then Add ( -1, wSKK, kr, CENAPOD )
if bCenaPod && bRabatDlaWartosci Then Add ( -1, wSKK, kr, WARTPRZEDRAB)

if CZYNETTO then
    If !bRabatDlaWartosci || !bCenaPod Then
        Add ( -1, wSKK, kr, CENAN )
    Else
        CENAN = WARTN
    EndIf
    Add ( -1, wSKK, kr, WARTN )
    Add ( -1, 110-offSWW, nr, STAWKAVAT )
    if bWart then
        Add ( -1, wSKK, kr, WARTB )
    else
        CENAN = -1
    EndIf
else
    If !bRabatDlaWartosci || !bCenaPod  Then
        Add ( -1, wSKK, kr, CENAB )
    Else
        CENAB = WARTN
    EndIf
    if bWart then
        Add ( -1, wSKK, kr, WARTN )
    else
        CENAB = -1
    EndIf
    Add ( -1, 110-offSWW, nr, STAWKAVAT )
    Add ( -1, wSKK, kr, WARTB )
endif
if bWartVat then Add ( STAWKAVAT, wSKK, kr, KWOTAVAT )
if nSWW==1 then
    if !bWartVat then
        Add ( STAWKAVAT, wSKK+offSWW, nc, SWW )
    else
        if !bWart then
            if CZYNETTO then
                Add ( -1, wSKK+offSWW, nc, SWW )
            else
                Add ( CENAB, wSKK+offSWW, nc, SWW )
            endif
        else
            Add ( 2, 170+offSWW, nc, SWW )
        endif
    endif
endif
if nOO==1 then
    Add ( -1, 170, nc, OO )
endif
if !nKod then
    if !bWartVat && nSWW!=1 then
            //JM
        Add ( STAWKAVAT, wSKK, nl, KOD )
    else
        if !bWart && !(bWartVat && nSWW==1) then
            if CZYNETTO then
                    //JM
                Add ( -1, wSKK, nl, KOD )
            else
                    //JM
                Add ( CENAB, wSKK, nl, KOD )
            endif
        else
                //JM
            Add ( 2, 180, nl, KOD )
        endif
    endif
endif
if bDostawy then
    if !bWartVat && ((nSWW!=1 && nKod) || (nSWW==1 && nKod) || (nSWW!=1 && !nKod))then
        Add ( STAWKAVAT, 220, nl, DOSTAWA )
    else
        Add ( 2, 220, nl, DOSTAWA )
    endif
endif
if bCena then
    if !bWartVat && nSWW!=1 && nKOD && !bDostawy then
        if CZYNETTO then
            If !bRabatDlaWartosci || !bCenaPod  Then Add ( STAWKAVAT, wSKK, kr, CENAB1 )
        else
            If !bRabatDlaWartosci || !bCenaPod  Then Add ( STAWKAVAT, wSKK, kr, CENAN1 )
        endif
    else
        if !bWart && (nSWW!=1 || nKod || !bDostawy) then
            if CZYNETTO then
                If !bRabatDlaWartosci || !bCenaPod  Then Add ( -1, wSKK, kr, CENAB1 )
            else
                If !bRabatDlaWartosci || !bCenaPod  Then Add ( CENAB, wSKK, kr, CENAN1 )
            endif
        else
            if CZYNETTO then
                If !bRabatDlaWartosci || !bCenaPod  Then Add ( CENAN, wSKK, kr, CENAB1 )
            else
                If !bRabatDlaWartosci || !bCenaPod  Then Add ( CENAB, wSKK, kr, CENAN1 )
            endif
        endif
    endif
endif
if bRabaty then
    If bRabatDlaWartosci && bCenaPod Then Add ( WARTPRZEDRAB, 120, kr, RABATY)
    If bCenaPod && !bRabatDlaWartosci then Add ( CENAPOD, 120, kr, RABATY )
    If !bCenaPod then Add ( JM, 120, kr, RABATY )
endif
nkol=Size(kol)
if CZYNETTO then
    while kol(nNettoPos)!=WARTN
        nNettoPos += 1
    wend
    if nNettoPos == nkol-1 then
        Add(-1,wSKK,0,TEMPKOL)
        Add(-1,wSKK,0,TEMPKOL)
    else
        if nNettoPos==nkol-2 then Add(-1,wSKK,0,TEMPKOL)
    endif
endif
#endif

CENAN = CENAN1
CENAB = CENAB1

nkol=Size(kol)
grow StlTabH,nkol-1
for i=1 to i>nkol
    StlTabH (i) = bc
    nTblWidth1 += KolTab1 (i)
next i
nSzer=str.szer
if nTblWidth1 > nSzer then
    message sMarg : error ""
else
#ifndef SIMPLIFIED
    nDiff = ( nSzer - nTblWidth1 ) / nkol
    nTblWidth1 = 0
    for i = 1 to i > nkol
        nTblWidth1 += ( KolTab1 (i) += nDiff )
    next i
    if nTblWidth1 < nSzer then KolTab1(2) += nSzer-nTblWidth1
    nTblWidth1 = nSzer
#endif
endif


if nSWW==1 then
    for i=1 to i>size(kol)
        select case kol(i)
            case SWW 
                pozSWWoff(1)=i
            case STAWKAVAT
                pozSWWoff(2)=i
        endselect
    next i
    szerSTVAT=TextWidth("Stawka ",nr)+5
    szerSTVAT=KolTab1(pozSWWoff(2))-szerSTVAT
    if szerSTVAT>0 then
        KolTab1(pozSWWoff(2))-=szerSTVAT
        KolTab1(pozSWWoff(1))+=szerSTVAT
    endif
    szerSTVAT=TextWidth("15.33.11-00.60 ",nc)+5
    szerSTVAT-=KolTab1(pozSWWoff(1))
    if szerSTVAT>0 then
        KolTab1(pozSWWoff(1))+=szerSTVAT
        KOlTab1(2)-=szerSTVAT
    endif
endif
For i = 1 To i>=nNettoPos
    nNettoX += KolTab1(i)
Next i
grow KolTab2,3
grow StlTab2,3
dxramk = KolTab1( IIF( CompactForm, 1, nkol-4 ) )
#ifdef SIMPLIFIED
    For i = 1 To i > Size( Koltab2 )
        KolTab2( i ) = IIF( i == 2, 165, 225 )
    Next i
#else
if CZYNETTO then
    KolTab2(1) = KolTab1(nNettoPos)
    KolTab2(2) = KolTab1(nNettoPos+1)
    KolTab2(3) = KolTab1(nNettoPos+2)
    KolTab2(4) = KolTab1(nNettoPos+3)
    while kol(nkol)==TEMPKOL
        nTblWidth1-=KolTab1(nkol)
        shrink kol,1
        shrink KolTab1,1
        shrink StlTab1,1
        nkol-=1
    wend
else
    lastpos = nkol
    KolTab2(4) = KolTab1(lastpos)
    KolTab2(3) = KolTab1(lastpos-=1)
    if KolTab2(3)<wSKK then
        KolTab2(3) = wSKK
        KolTab2(2) = 110
        KolTab2(1) = wSKK
    else
        KolTab2(2) = KolTab1(lastpos-=1)
        if KolTab2(2)>wSKK then
            KolTab2(2) = 110
            KolTab2(1) = wSKK
        else
            KolTab2(1) = KolTab1(lastpos-=1)
            if KolTab2(1)<wSKK then KolTab2(1) = wSKK
        endif
    endif
endif
#endif
For i = 1 To i>4
    nTblWidth2 += KolTab2(i)
    StlTab2(i) = kr
Next i

if nBitmap==1 then
    if !nBmpCoord(1) then nBmpCoord(1)=str.szer
    if !nBmpCoord(2) then nBmpCoord(2)=250
    bmp = Bitmap nBmpCoord(1),nBmpCoord(2),sBitmap
    Bitmap #bmp, at nBmpCoord(3),nBmpCoord(4)
    print lf : print at #X,#Y+5;
endif
FRNewRow(0)
select case nBitmap
    case 0
        If bmpStamp || bMiejsceNapieczec Then FRAddCol( -33, 220, 1, bRamki, 0 )
        FRAddCol( IIF( bmpStamp || bMiejsceNapieczec, -34, -50 ), 220, 0, 0, 0 )
        FRAddColRow(130,2,bRamki,nGray)
        FRAddColRow(80,3,bRamki,0)
        FRAddCol( IIF( bmpStamp || bMiejsceNapieczec, -33, -50 ), 220, 0, 0, 0)
        if iTyp!=FWS && iTyp!=WKS then
            FRAddColRow(65,4,bRamki,0)
            FRAddColRow(-100,5,0,0)
            FRAddColRow(80,6,bRamki,0)
        else
            FRAddColRow(100,4,bRamki,0)
        endif
    case 1
        FRAddCol(-33,170,0,0,0)
        FRAddColRow(80,3,bRamki,0)
        if iTyp!=FWS && iTyp!=WKS then FRAddColRow(-100,6,bRamki,0)
        FRAddCol(-34,170,2,1,nGray)
        FRAddCol(-33,170,0,0,0)
        if iTyp!=FWS && iTyp!=WKS then
            FRAddColRow(-50,4,bRamki,0)
            FRAddColRow(-50,5,0,0)
        else
            FRAddColRow(100,4,bRamki,0)
        endif
    case 2

        lTemp = str.szer
        lTemp = lTemp*66/100
        nPom=lTemp
        if !nBmpCoord(1) then nBmpCoord(1)=nPom - nBmpCoord(3)
        if !nBmpCoord(2) then nBmpCoord(2)=450 - iif(nBmpCoord(4)>=450,0,nBmpCoord(4))
        if nBmpCoord(1)>nPom then nBmpCoord(1)= nPom
        if nBmpCoord(1)+nBmpCoord(3)>nPom then nBmpCoord(3)=0

        nPom = iif(nBmpCoord(2)+nBmpCoord(4)>450,nBmpCoord(2)+nBmpCoord(4),450)
        FRAddCol(nBmpCoord(1)+nBmpCoord(3),nPom,7,0,0)
        FRAddCol(-100,nPom,0,0,0)
        FRAddColRow(130,2,1,nGray)
        if iTyp!=FWS && iTyp!=WKS then
            FRAddColRow(65,4,bRamki,0)
            FRAddColRow(-100,5,0,0)
        else
            FRAddColRow(100,4,bRamki,0)
        endif
        FRAddColRow(80,3,bRamki,0)
        if iTyp!=FWS && iTyp!=WKS then FRAddColRow(80,6,bRamki,0)
endselect
FRDraw()

if CZY_WYSTAWCA  Then

        FRNewRow(-1)
        if  (iTyp == FWS && bRW) || iTyp == WKS then
            if bSprzedawca || !nBitmap then
                FRAddCol(-100,10,R_WYSTAWCA,bRamki,0)
            endif
        else
            if bSprzedawca || !nBitmap then
                FRAddCol(-50,10,R_WYSTAWCA,bRamki,0)                
                FRAddCol(-50,10,R_NABYWCA,bRamki,0)
            else
                FRAddCol(-100,10,R_NABYWCA,bRamki,0)
            endif
        endif
        FRDraw()

        if iTyp==FWS || iTyp== WKS then
            //FRAddCol(-50,10,9,bRamki,0)            
            //if bOdbiorca then FRAddCol(-50,10,11,bRamki,0)
        else
            if bOdbiorca then 
                FRNewRow(-1)
                    FRAddCol(-50,10,R_SPRZEDAWCA,bRamki,0)
                    FRAddCol(-50,10,R_ODBIORCA,bRamki,0)
                FRDraw()    

                FRNewRow(-1)                
                    bOdbiorcaWyst = 0
                    FRAddCol(-100,10,R_DANEPLATNOSCI_WYST,bRamki,0)
                FRDraw()
            else
                FRNewRow(-1)
                    FRAddCol(-50,10,R_SPRZEDAWCA,bRamki,0)
                    bOdbiorcaWyst = 1
                    FRAddCol(-50,10,R_DANEPLATNOSCI_WYST,bRamki,0)
                FRDraw()
            endif
        endif        

else
        FRNewRow(-1)
        if  (iTyp == FWS && bRW) || iTyp == WKS then
            if bSprzedawca || !nBitmap then
                FRAddCol(-100,10,8,bRamki,0)
            endif
        else
            if bSprzedawca || !nBitmap then
                FRAddCol(-50,10,8,bRamki,0)
                FRAddCol(-50,10,9,bRamki,0)
            else
                FRAddCol(-100,10,9,bRamki,0)
            endif
        endif
        FRDraw()


        FRNewRow(-1)
        if iTyp==FWS || iTyp== WKS then
            if bOdbiorca then FRAddCol(-100,10,11,bRamki,0)
        else
            FRAddCol(-100+50*bOdbiorca,10,10,bRamki,0)
            if bOdbiorca then FRAddCol(-100+50*bOdbiorca,10,11,bRamki,0)
        endif
        FRDraw()


endIf



if bOpis && sOpis then
    Ramka od 0,#B+10,str.szer,10,bRamki
        SetFont ( "tekst" )
        print " " + sOpis
    koniec
endif

if bAnulowano == 1 || bAnulowano == 3 then
    ramka od 0,#Y+10, str.szer,10
        SetFont( CopyFont( "tekst",1,100 ) )
        Align(0)
        print "ANULOWANO"
    koniec
endif

        
if CzyNP then
    grow kolNP,1
    grow KolTab1NP,10 : grow StlTab1NP,10
    KolTab1NP(1)=KolTab1(1)
    StlTab1NP(1)=StlTab1(1)
    KolTab1NP(2)=KolTab1(2)
    StlTab1NP(2)=StlTab1(2)
    for i=3 to i > nkol
        Select case kol(i)
            case STAWKAVAT,SWW,KWOTAVAT,CENAN,WARTN,CENAB,WARTB
                Select case kol(i)
                    case CENAN, WARTN
                        if CZYNETTO then
                            if Kol(i)==CENAN then
                                KolTab1NP(10)+=KolTab1(i)
                                StlTab1NP(10)=nr //StlTab1(i)
                            else
                                KolTab1NP(11)+=KolTab1(i)
                                StlTab1NP(11)=nr
                            endif
                        else
                            KolTab1NP(2)+=KolTab1(i)
                        endif
                    case CENAB, WARTB
                        if !CZYNETTO then
                            if kol(i)==CENAB then
                                KolTab1NP(10)+=KolTab1(i)
                                StlTab1NP(10)=nr //StlTab1(i)
                            else
                                KolTab1NP(11)+=KolTab1(i)
                                StlTab1NP(11)=nr
                            endif
                        else
                            KolTab1NP(2)+=KolTab1(i)
                        endif
                    case else
                        KolTab1NP(2)+=KolTab1(i)
                endselect
            case KOD
                KolTab1NP(5)=KolTab1(i)
                StlTab1NP(5)=nl
            case ILOSC
                KolTab1NP(2)-=20
                KolTab1NP(3)=KolTab1(i)+20
                StlTab1NP(3)=StlTab1(i)
            case JM
                KolTab1NP(4)=KolTab1(i)
                StlTab1NP(4)=StlTab1(i)
            case CENAPOD
                KolTab1NP(6)=KolTab1(i)
                StlTab1NP(6)=StlTab1(i)
            case RABATY
                KolTAb1NP(7)+=KolTab1(i)
                StlTab1NP(7)=StlTAb1(i)
            case WARTPRZEDRAB
                KolTab1NP(8)+=KolTAb1(i)
                StlTAb1NP(8)=StlTab1(i)
            case DOSTAWA
                KolTAb1NP(9)+=KolTab1(i)
                StlTab1NP(9)=StlTab1(i)
        endselect
    next i
    for i=1 to i > 11
        if KolTab1NP(i)!=0 then
            if KolumnyNP(size(KolumnyNP))!=0 then grow kolumnyNP,1 : grow StyleNP,1
            KolumnyNP(size(KolumnyNP))=KolTab1NP(i)
            StyleNP(size(StyleNP))=StlTab1NP(i)
        endif
    next i
    grow StlTabHNP, size(kolumnyNP)-1 //tabela stylow headera
    for i=1 to i > size(StlTabHNP)
        StlTabHNP(i)=StlTabH(1)
    next i
    for i=1 to i > size(KolumnyNP)-2
        nMalaStopka+=KolumnyNP(i)
    next i
endif

if xPoz.Count() then //bNoPoz then
    tbl0 = tabela 1,nTblMarg,KolTab1,StlTabH
    ramka od 0, #Y+10,nTblWidth1,10,0,nGray
        tabela #tbl0
            sPom = "Nazwa"
            if nSWW==2 then sPom += ", "+sSWW_ //",SWW/PKWiU"
            if nOO==2 then sPom += ", odwrotne obciążenie"
            if nKod==1 then
                sPom += ", kod"
                if nKodTyp==2 then sPom+=" obcy"
                if nKodTyp==3 then sPom+=" paskowy"
            endif
            kolumna 1 : y = TextHeight("X")/2 : print at #X,y; " Lp. "
            kolumna 2 : print at #X,y; PiszWyrazy(sPom,KolTab1(2)-10)
            for i = 3 to i > nkol
                select case kol(i)
                    case SWW
                        sPom = sSWW_ //"SWW/PKWiU"
                    case OO
                        sPom = sOO_
                    case KOD
                        sPom = "Kod"
                        if nKodTyp==2 then sPom+=" obcy"
                        if nKodTyp==3 then sPom+=" paskowy"
                    case ILOSC
                        sPom = "Ilość"
                    case JM
                        sPom = "Jm"
                    case CENAPOD
                        If bRabatDlaWartosci Then
                            If CZYNETTO Then
                                sPom = "Cena netto" + fcSuffix()
                            Else
                                sPom = "Cena brutto" + fcSuffix()
                            EndIf
                        Else
                            sPom = "Cennik" + fcSuffix()
                        EndIf
                    case CENAN
                        sPom = "Cena"+lf+"netto" + fcSuffix()
                    case CENAB
                        sPom = "Cena"+lf+"brutto" + fcSuffix()
                    case WARTN
                        sPom = "Wartość"+lf+"netto" + fcSuffix()
                    case WARTB
                        sPom = "Wartość"+lf+"brutto" + fcSuffix()
                    case STAWKAVAT
                        sPom = "Stawka"+lf+"VAT"
                    case KWOTAVAT
                        sPom = "Kwota"+lf+"VAT" + fcSuffixVAT()
                    case RABATY
                        sPom = "Rabat %"
                    case WARTPRZEDRAB
                        sPom = "Wartość przed rab."
                    case DOSTAWA
                        sPom = "Nazwa dostawy"
                endselect
                select case kol(i)
                    case SWW,KOD,ILOSC,JM,RABATY//,CENAPOD
                        y = TextHeight(sPom)/2
                    case else
                        y = 0
                endselect
                kolumna i : print at #X,y; sPom
            next i
        koniec
    koniec
    nTblMarg=4005
    tbl1 = tabela 1*!bVertLines,nTblMarg,KolTab1,StlTab1
    nlp=1
    For i = 1 To i > xPoz.Count()
        if !xPoz.item(i).dokompletu then
        if !xPoz.item(i).NiePodlega then
        ramka nTblWidth1,5,0,nOddGray*grayTrigger
        if bOddGray then grayTrigger = !grayTrigger
        tabela #tbl1,od 0,#Y
        kolumna 1, (using "%d",xPoz.item(i).lp)+" "            //nlp) + " "
//        nlp+=1
        buf=xPoz.item(i).Opis //Poz_opis.Get(using"%d",i)
        if nSWW==2 then buf+= getCommaPC(xPoz.item(i),xDocument.SendAggregated)
        if nOO==2 then 
            buf+= ", "
            if xPoz.item(i).odwrotneObc then buf+="odwrotne obciążenie"
        endif
        if nKod==1 then
            select case nKodTyp
                case 1
                    buf+= ", " + xPoz.item(i).kod//Pozycje(i).Kod
                case 2
                    nResult=xPoz.item(i).PodajKodObcy() : xErr(nResult)
                    if nResult then Message sStndError : error ""
                    buf+= ", " + xPoz.item(i).KodObcy//Pozycje(i).KodObcy
                case 3
                    buf+= ", " + KodPaskowy_pozycji(i)
            endselect
        endif
        kolumna 2,PiszWyrazy(buf,KolTab1(2)-10)
        For j = 3 To j > nkol
            select case kol(j)
                case KOD
                    select case nKodTyp
                        case 1
                            buf = xPoz.item(i).Kod//Pozycje(i).Kod
                        case 2
                            nResult=xPoz.item(i).PodajKodObcy() : xErr(nResult)
                            if nResult then Message sStndError : error ""
                            buf = xPoz.item(i).KodObcy//Pozycje(i).KodObcy
                        case 3
                            buf = KodPaskowy_pozycji(i)
                    endselect
                case SWW
                    buf = getPC(xPoz.item(i),xDocument.SendAggregated)
                case OO
                    if xPoz.item(i).odwrotneObc then
                        buf = "x"
                    else
                        buf = ""
                    endif 
                case JM
                    buf = iif(nJednMiary,xPoz.item(i).JednostkaMiaryWp,xPoz.item(i).JednostkaMiary)//Pozycje(i).JmWP,Pozycje(i).Jm)
                case CENAPOD
                    currExRate = IIF( PLNonly == 1, xDocument.Kurs,1 )
                    buf = formatuj(iif(nJednMiary,xPoz.item(i).CenaCennikowa*currExRate,xPoz.item(i).CenaCennikowa/Ceny(i).PrzelJmDod*currExRate),nCenaRound+1)
                case ILOSC
                    buf = using "%4.4f",iif(nJednMiary,xPoz.item(i).IloscWP,xPoz.item(i).Ilosc)
                    delete regular "(0#$)|(.0#$)"
                    s = buf : buf = s : Replace "." , ","
                case CENAB
                    currExRate = IIF( PLNonly == 0, xDocument.Kurs,1 )
                    buf = formatuj( Round(iif(nJednMiary,Ceny(i).CenaBruttoWP/currExRate,Ceny(i).CenaBrutto/currExRate),nCenaRound+1),nCenaRound+1 )
                case CENAN
                    currExRate = IIF( PLNonly == 0, xDocument.Kurs,1 )
                    buf = formatuj( Round(iif(nJednMiary,Ceny(i).CenaNettoWP/currExRate,Ceny(i).CenaNetto/currExRate),nCenaRound+1),nCenaRound+1 )
                case WARTB
                    buf = kwota ( IIF( PLNonly==0, xPoz.item(i).BruttoWal, xPoz.item(i).Brutto ) )
                case WARTN
                    buf = kwota ( IIF( PLNonly==0, xPoz.item(i).NettoWal, xPoz.item(i).Netto ) )
                case STAWKAVAT
                    if( iZamienNazweVatOO && xPoz.item( i ).odwrotneObc ) Then
                        buf = strNazwaVatOO
                        // dp odliczenia od podsumowan stawki NP
                        fPlnOO = fPlnOO + xPoz.item( i ).Brutto
                        fWalOO = fWalOO + xPoz.item( i ).BruttoWal
                    Else
                        buf = xPoz.item(i).StVat.nazwa
                    EndIf
                case KWOTAVAT
                    buf = kwota ( xPoz.item(i).VAT)//Pozycje(i).KwotaVat )
                case RABATY
                    buf = using "%2.2f", Round(xPoz.item(i).Rabat*100,nRabatRound)
                    find regular "{(/-)|():d##}.{:d##}"
                    if (regular 2) == "00" then buf = (regular 1)
                    if buf == "0" then buf=""
                    s = buf : buf = s : Replace "." , ","
                case WARTPRZEDRAB
                    buf = kwota ( (xPoz.item(i).CenaCennikowa*xPoz.item(i).Ilosc)/Ceny(i).PrzelJmDod)//Pozycje(i).CenaPodstawowa * Pozycje(i).Ilosc)
                case DOSTAWA
                    buf=""
                    for idw=1 to idw>nDostawy
                        if Dostawy(idw).lp == i then
                            //ObliczWys(Dostawy(idw).nazwa,KolTab1(j)-15)
                            buf+= Dostawy(idw).nazwa+" "//sBroken
                        endif
                    next idw
            endselect
            kolumna j, PiszWyrazy(buf,KolTab1(j)-10)
        next j
    koniec
        koniec
        if bVertLines then
            if i==1 then DrawLine at 0,#T,nTblWidth1,0
            y = str.pozycja(#B)-str.pozycja(#T)
            x=0
            nPom = 1
            do
                x+=KolTab1(nPom)
                nPom+=1
                DrawLine at x,#T,0,y
            loop until nPom>size(KolTab1)
            DrawLine at 0,#T,0,y
        endif
        endif
        endif
    Next i
endif
if bVertLines then DrawLine at 0,#B,nTblWidth1,0

nTblMarg =6005
#ifdef TOTAL_VAT_OPTIONAL
If PPlnVat Then
#endif
if bWart && bWartVat then
    tbl2 = tabela 5,nTblMarg,KolTab2,StlTab2
    yramk = str.pozycja(#Y) + 10
    fkol = 1
    tabela #tbl2, od str.szer-nTblWidth2, yramk
        kolumna fkol,   kwota ( IIF( PLNonly==0, xDocument.NettoWal, xDocument.Netto ) )
        kolumna fkol+1 : Align(0) : print "X"
        kolumna fkol+2, kwota ( xDocument.Vat)
        kolumna fkol+3, kwota ( IIF( PLNonly==0, xDocument.BruttoWal, xDocument.Brutto ) )
    koniec
    dyramk=str.pozycja(#B)-str.pozycja(#T)
    yramk = str.pozycja(#T)
    ramka od str.szer-nTblWidth2-dxramk,yramk,dxramk,dyramk,1,nGray
        SetFont ( "kwota" ) : align(0)
        print at 0,6;"RAZEM"
    koniec
    yBoldRamk  = yramk
    dyBoldRamk = dyramk
    yramk = -1
    tbl2 = tabela 1,nTblMarg,KolTab2,StlTab2
    For i = 1 To i>size(StawkiVat)
        iNP = ( StawkiVat(i).Nazwa == "NP" )
        fBruttoSUM = IIF( PLNonly == 0, StawkiVat( i ).bruttoWal - IIF( iNP, fWalOO, 0 ), StawkiVat( i ).brutto - IIF( iNP, fPlnOO, 0 ) )
          if  bZerVat || sign( fBruttoSUM, 2 ) then
              tabela #tbl2,od str.szer-nTblWidth2,#Y
                 kolumna fkol,kwota ( IIF( PLNonly==0, StawkiVat(i).nettoWal- IIF( iNP, fWalOO, 0 ) , StawkiVat(i).netto - IIF( iNP, fPlnOO, 0 ) ) )
                 kolumna fkol+1,StawkiVat(i).nazwa
                 kolumna fkol+2,kwota ( StawkiVat(i).vat )
                 kolumna fkol+3,kwota ( fBruttoSUM )
              koniec
            if yramk==-1 then
                dyramk=str.pozycja(#B)-str.pozycja(#T)
                yramk = str.pozycja(#T)
                Ramka od str.szer-nTblWidth2-dxramk,yramk,dxramk,dyramk,1,nGray
                    SetFont ( "kwota" ) : align(0)
                    print at 0,6;"W tym"
                koniec
            endif
          endif
    Next i
    If Sign( fPlnOO ) || Sign( fWalOO ) Then
        // podsumowanie kwot z odwrotnym obciazeniem
          Tabela #tbl2, Od str.szer-nTblWidth2, #Y
             Kolumna fkol,   Kwota( fPlnOO )
             Kolumna fkol+1, strNazwaVatOO
             Kolumna fkol+2, "0,00"
             Kolumna fkol+3, Kwota( fPlnOO )
          Koniec
    EndIf
    y = str.Pozycja(#B)
else
    tbl2 = tabela 1,nTblMarg,KolTab2,StlTab2
    tbl0 = tabela 1,nTblMarg,KolTab2,StlTabH
    if CZYNETTO then
        xtab = nNettoX
    else
        xtab = str.szer-nTblWidth2
    endif
#ifdef SIMPLIFIED
    xtab = Str.Szer
    For i = 1 To i > Size( KolTab2 )
        xtab = xtab - KolTab2( i )
    Next i
#endif
    Ramka od xtab,#Y+10,nTblWidth2,10,0,nGray
        tabela #tbl0
            kolumna 1,"Netto" + fcSuffix()
            kolumna 2,"%"
            kolumna 3,"VAT" + fcSuffixVAT()
            kolumna 4,"Brutto" + fcSuffix()
        koniec
    koniec
    For i = 1 To i>size(StawkiVat)
        iNP = ( StawkiVat(i).Nazwa == "NP" )
        fBruttoSUM = IIF( PLNonly == 0, StawkiVat( i ).BruttoWal - IIF( iNP, fWalOO, 0 ), StawkiVat( i ).Brutto - IIF( iNP, fPlnOO, 0 ) )
          if bZerVat || sign( fBruttoSUM, 2 ) then
            tabela #tbl2, od xtab,#B
                kolumna 1, kwota ( IIF( PLNonly==0, StawkiVat(i).NettoWal - IIF( iNP, fWalOO, 0 ), StawkiVat(i).Netto - IIF( iNP, fPlnOO, 0 ) ) )
                kolumna 2, StawkiVat(i).Nazwa
                kolumna 3, kwota ( StawkiVat(i).Vat )
                kolumna 4, kwota ( fBruttoSUM )
            koniec
        endif
    next i
    If Sign( fPlnOO ) || Sign( fWalOO ) Then
        // podsumowanie kwot z odwrotnym obciazeniem
        Tabela #tbl2, Od xtab,#B
            Kolumna 1, Kwota( fPlnOO )
            Kolumna 2, strNazwaVatOO
            Kolumna 3, "0,00"
            Kolumna 4, Kwota( fPlnOO )
        Koniec
    EndIf
    tbl2 = tabela 5,nTblMarg,KolTab2,StlTab2
    tabela #tbl2, od xtab,#B
        kolumna 1, kwota ( IIF( PLNonly==0, xDocument.NettoWal, xDocument.Netto ) )
        kolumna 2 :    Align(0) : print "Razem"
        kolumna 3, kwota ( xDocument.Vat )
        kolumna 4, kwota ( IIF( PLNonly==0, xDocument.BruttoWal, xDocument.Brutto ) )
    koniec
    y = str.Pozycja(#B)
endif
#ifdef TOTAL_VAT_OPTIONAL
Else
    y = Str.Pozycja( #B )
EndIf // PPlnVat
#endif

if xPoz.Count() && CzyNP then
    tbl0=tabela 0, nTblMarg,str.szer,nc
    tabela #tbl0, od #X,#B+20
        kolumna 1, "POZYCJE NIE PODLEGAJĄCE VAT"
    koniec
    nTblMarg=4005
    tbl0 = tabela 1,nTblMarg,kolumnyNP,StlTabHNP
    ramka od 0, #Y+10,nTblWidth1,10,0,nGray
        tabela #tbl0
            sPom = "Nazwa"
            if nSWW==2 then sPom += ", "+sSWW_//    ",SWW/PKWiU"
            if nOO==2 then sPom += ", odwrotne obciążenie"
            if nKod==1 then
                sPom += ", kod"
                if nKodTyp==2 then sPom+=" obcy"
                if nKodTyp==3 then sPom+=" paskowy"
            endif
            //TextHeight("X")/2
            kolumna 1 : y = 0 : print at #X,y; " Lp. "
            kolumna 2 : print at #X,y; sPom
            nkolumna=2
            for i = 3 to i > size(KolTab1NP)
                if KolTab1NP(i)!=0 then
                    nkolumna+=1
                    Select case i
                        case 5
                            sPom = "Kod"
                            if nKodTyp==2 then sPom+=" obcy"
                            if nKodTyp==3 then sPom+=" paskowy"
                        case 3
                            sPom="Ilość"
                        case 4
                            sPom="Jm"
                        case 6
                            sPom = "Cennik" + fcSuffix()
                        case 7
                            sPom="Rabat %"
                        case 8
                            sPom="Wartość przed rab."
                        case 9
                            sPom="Nazwa dostawy"
                        case 10
                            sPom="Cena"
                        case 11
                            sPom="Wartość"
                    endselect
                    select case KolTab1NP(i)
                        case 3,4,5,6,7,9
                            y = 0//TextHeight(sPom)/2
                        case else
                            y = 0
                    endselect
                    kolumna nkolumna : print at #X,y; sPom
            endif
        next i
    koniec
    koniec
    nTblMarg = 4005
    tbl1 = tabela 1*!bVertLines,nTblMarg,KolumnyNP,StyleNP
    nlp=1
    For i = 1 To i > xPoz.Count()//size( Pozycje )
        if !xPoz.item(i).dokompletu then
        if xPoz.item(i).NiePodlega then //Pozycje(i).stawka==-1 then
        ramka nTblWidth1,5,0,nOddGray*grayTrigger
        if bOddGray then grayTrigger = !grayTrigger
        tabela #tbl1,od 0,#Y
        kolumna 1, (using "%d",nlp) + " "
        nlp+=1
           buf= xPoz.item(i).Opis//Poz_opis.Get(using"%d",i)
        if nKod==1 then
            select case nKodTyp
                case 1
                    buf+= ", " + xPoz.item(i).Kod
                case 2
                    nResult=xPoz.item(i).PodajKodObcy() : xErr(nResult)
                    if nResult then Message sStndError : error ""
                    buf+= ", " + xPoz.item(i).KodObcy
                case 3
                    buf+= ", " + KodPaskowy_pozycji(i)
            endselect
        endif
        kolumna 2,PiszWyrazy(buf,KolumnyNP(2)-10) : buf=""
        nkolumna=2
        For j = 3 To j > size(KolTab1NP)
            if KolTab1NP(j)!=0 then
                nkolumna+=1
                select case j
                    case 5
                        select case nKodTyp
                        case 1
                            buf = xPoz.item(i).Kod
                        case 2
                            nResult=xPoz.item(i).PodajKodObcy() : xErr(nResult)
                            if nResult then Message sStndError : error ""
                            buf = xPoz.item(i).KodObcy
                        case 3
                            buf = KodPaskowy_pozycji(i)
                        endselect
                    case 3
                         buf = using "%4.4f",iif(nJednMiary,xPoz.item(i).IloscWP,xPoz.item(i).Ilosc)
                        delete regular "(0#$)|(.0#$)"
                        s = buf : buf = s : Replace "." , ","
                    case 4
                        buf = iif(nJednMiary,xPoz.item(i).JednostkaMiaryWP,xPoz.item(i).JednostkaMiary)
                    case 6
                        currExRate = IIF( PLNonly == 1, xDocument.Kurs,1 )
                        buf = formatuj ( Round(iif(nJednMiary,xPoz.item(i).CenaCennikowa*currExRate,xPoz.item(i).CenaCEnnikowa/Ceny(i).PrzelJmDod*currExRate),nCenaRound+1),nCenaRound+1 )
                    case 7
                        buf = using "%2.2f", Round(xPoz.item(i).rabat*100 , nRabatRound)
                        find regular "{(/-)|():d##}.{:d##}"
                        if (regular 2) == "00" then buf = (regular 1)
                        if buf == "0" then buf=""
                        s = buf : buf = s : Replace "." , ","
                    case 8
                        buf = kwota ( iif(nJednMiary,xPoz.item(i).CenaCennikowa * xPoz.item(i).IloscWP,xPoz.item(i).CenaCennikowa * xPoz.item(i).Ilosc))
                    case 9
                        buf=""
                        for idw=1 to idw>nDostawy
                            if Dostawy(idw).lp == i then
                                //ObliczWys(Dostawy(idw).nazwa,KolTab1NP(nKolumna)-15)
                                buf+= Dostawy(idw).nazwa+" "//sBroken
                            endif
                        next idw
                    case 10
                        buf = formatuj( Round(iif(nJednMiary,Ceny(i).CenaNettoWP,Ceny(i).CenaNetto),nCenaRound+1),nCenaRound+1 )
                    case 11
                        buf = kwota ( xPoz.item(i).NiePodlega )
                endselect
                kolumna nkolumna, PiszWyrazy(buf,KolumnyNP(nKolumna)-10)
            endif
        next j
    koniec
    koniec
    if bVertLines then
        if i==1 then DrawLine at 0,#T,nTblWidth1,0
        y = str.pozycja(#B)-str.pozycja(#T)
        x=0
        nPom = 1
        do
            x+=KolTab1(nPom)
            nPom+=1
            DrawLine at x,#T,0,y
        loop until nPom>size(KolTab1)
        DrawLine at 0,#T,0,y
    endif
    endif
    endif
Next i
tbl1=tabela 1*!bVertLines,nTblMarg,KolumnyNP(size(KolumnyNP)-1),nc,KolumnyNP(size(KolumnyNP)),nr
tabela #tbl1, od nMalaStopka,#Y
    kolumna 1, "RAZEM"
    kolumna 2, kwota(xDocument.NiePodlega)//tot_nettoNP)
koniec
y = str.Pozycja(#B)
endif
if bVertLines then DrawLine at 0,#B,nTblWidth1,0

print at 0,y+20;
FRNewRow(0)
if iTyp!=FWS && iTyp!=WKS then FRAddCol(700,90,18,bRamki,0)
FRAddCol(50,90,0,0,-1)
if iTyp!=FWS && iTyp!= WKS && iTyp != SKO && iTyp != SKW then FRAddCol(-100,90,13,0,-1)
FRDraw()
ramka od 0,#B+10,str.szer,90,bRamki
    SetStyl ( nbl )
    print at 30,20;"Słownie: "
    SetFont ( "tekst" )
    If PLNonly == -1 Then
        print KwotaNaTekst( (using "%.2f",fDoZaplaty),"")//tot_brutto), "" )
    Else
        // SKW / SKWK / SZW / SZWK --> Slownie zawsze w walucie
        Print KwotaNaTekst( ( Using "%.2f", fDoZaplatyWal ), sWaluta )
    EndIf
koniec

#ifdef SIMPLIFIED
If CompactReportNo == 4 && !PPlnVAT Then
    Print At 0, #Y + 10;
    FRNewRow( 0 )
    FRAddCol( 700, 90, 23, bRamki, 0 )
    FRDraw()
EndIf
#endif

IF (bWartoscAkcyzy) THEN
    akcyza = ObliczAkcyze()
    ramka od 0,#B+10,700,90,bRamki
        SetStyl ( nbl )
        print at 30,20;"Wartość akcyzy: "
        SetFont ( "tekst" )
        IF akcyza == -1 THEN
            print "nieznana"
        ELSE
            print Kwota(akcyza)
        ENDIF
    koniec
ENDIF


if bRabPods then
    tbl0 = tabela 1,nTblMarg,KolTabPR,StlTabH
    Ramka od str.szer-500,#Y+50,500,10,0,nGray
        tabela #tbl0
            if fRabat >= 0 then
                kolumna 1,"W sumie rabat"
            else
                kolumna 1,"W sumie narzut"
            endif
            if CZYNETTO then
                kolumna 2,"Netto" + fcSuffix()
            else
                kolumna 2,"Brutto" + fcSuffix()
            endif
        koniec
    koniec
    tbl0 = tabela 1,nTblMarg,KolTabPR,StlTabPR
    tabela #tbl0, od str.szer-500,#Y
        if fRabat >= 0 then
            kolumna 1, kwota( Round(fRabat , nRabatRound) ) + "%"
        else
            kolumna 1, kwota( -Round(fRabat , nRabatRound) ) + "%"
        endif
        currBrutto = IIF( PLNonly==0, xDocument.BruttoWal, xDocument.Brutto )
        currNetto = IIF( PLNonly==0, xDocument.NettoWal, xDocument.Netto )
        currNiePodlega = IIF( PLNonly==0, xDocument.NiePodlegaWal, xDocument.NiePodlega )
        currTotal = IIF( PLNonly==0, tot_wartbaza/xDocument.Kurs, tot_wartbaza )
        if CZYNETTO then
            if fRabat >= 0 then
                kolumna 2, kwota ( currTotal - ( currNetto + currNiePodlega ) )
            else
                kolumna 2, kwota ( ( currNetto + currNiePodlega ) -currTotal )
            endif
        else
            if fRabat >= 0 then
                kolumna 2, kwota ( currTotal - ( currBrutto + currNiePodlega ) )
            else
                kolumna 2, kwota ( ( currBrutto + currNiePodlega ) -currTotal )
            endif
        endif
    koniec
endif

print at 0,#B+50;
FRNewRow(0)
FRAddCol(-100,250,14,0,0)
FRDraw()
PlatWyd()
Notatka()
NotatkaRodz()
Dekretacje()

//tekst forumuly pod faktura
string formula_1, formula_2, formula_3, formula_4, formula_5


SetFont ( "tekst" )
print at 0,#Y+50;using "nr partii: %s\n", nr_partii

formula_2 += "Faktura stanowi handlowy dokument identyfikacyjny.\n"
formula_2 += "Weterynaryjny numer identyfikacyjny: PL 10213901 WE\n"
formula_2 += "Zakład zakwalifikowany do prowadzenia sprzedaży: Na RYNEK UE\n"
formula_2 += "Opis towaru: WG FAKTURY, Rodzaj opakowań: POJEMNIKI, Waga towaru netto: WG FAKTURY\n"
formula_2 += "Miejsce przetwarzania/składowania: Ubojnia Drobiu \"PIÓRKOWSCY\" Jerzy Piórkowski w spadku, Zakład Uboju m. Koziołki 40\n"
formula_2 += "Wyżej wymieniony towar jest zgodny co do jakości i spełnia warunki odpowiadające instrukcji przyjęcia towaru.\n"
formula_2 += "Dane dotyczące procesu technicznego, norm jakościowych, produkcyjnych stosowanych przez producenta: HACCP\n"
formula_2 += using "Numer samochodu: %s / ", nr_samochodu
formula_2 += using "Data produkcji: %s / ", data_prod
formula_2 += using "Data przydatności tuszki z kurczaka: %s \n", data_przyd_kurczak
formula_2 += using "Data przydatności elementów z kurczaka: %s / ", data_przyd_elementy
formula_2 += using "Data przydatności podrobów z kurczaka: %s \n", data_przyd_podroby
print at 0,#Y;lf + formula_2

formula_5 += using "Pojemniki E2 Wydane: %s szt / Zwrócone .................  | ", poj_zdane,
formula_5 += using "Palety H1 Wydane: %s szt / Zwrócone .................  \n\n", pal_zdane_h1
formula_5 += using "Palety Plastikowe Wydane: %s szt / Zwrócone ................. | ", pal_wydane_plastikowe
formula_5 += using "Palety Drewniane Wydane: %s szt / Zwrócone ................. \n\n", pal_zdane_d
formula_5 += using "Palety EURO Wydane: %s szt / Zwrócone ................\n", pal_wydane_euro
formula_5 += "                                        data .............................. , podpis .................................."
print at 0,#Y;lf + formula_5

////////////////////// rozpoczęcie liczenia sald towarów//////
String datap,datak
String data_aktualna

datak=data()
datap="1999-01-01"   // początkowa data sprawdzenia dokuemntów magazynowych

string zawartosc,nip_kontrahenta,koszty_wydzialowe_miesiaca
int id(3)
string strmsg,nazwaPliku,s1,data_dok,numer_dok
string sn,cenas,cenacennikowas,nazwa_pliku,sciezka,sciezka2,sTmp
string adres_edi
dispatch xTw =xFactory.NewObject("BTowar")
Dispatch d,conn, rs,rs2
conn = GetAdoConnection()
rs2 = "ADODB.Recordset"

Limit 32767
float paleta
int err,licznik
string zapytanie1,zapytanie2,kodobcy
int iErrRs, iErrRs2
string numer_zos
int ilosc_wz,ilosc_wz_podsumowanie
string adres_dostawy(1)

#define MAGAZYN_ID 65559   /// definicja podstawowego magazynu

String ilosc_odczytana,nazwaERP,IdERP
String kod_towaru

Dispatch conn2,conn3,rs3
int iErrRs1



ProFormaInfo()
Footer(1)
end

wydruk_tekstowy:
    #include "dokument VAT tryb tekstowy"
ProFormaInfoTxt()
FooterTxt(1)
