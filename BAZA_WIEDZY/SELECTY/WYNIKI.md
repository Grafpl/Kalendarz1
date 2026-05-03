# WYNIKI SELECT-ów — wklejaj poniżej

**Sergiuszu** — uruchom kolejno pliki `01-20.sql` w SSMS i wklej wyniki tutaj. Możesz wklejać:
- Bezpośrednio (kopiuj z SSMS, wklej między znaczniki)
- Albo opisowo (np. *"sekcja A zwróciła 47 wierszy, top 5 to: ..."*)
- Albo w formie tabel markdown

**Wskazówki SSMS dla kopiowania wyników:**
- Prawy klik na wynik → `Copy with Headers` (lub Ctrl+Shift+C)
- Albo `Save Results As...` → CSV → wklej zawartość pliku tutaj

---

## 📁 PLIK 01 — Lista tabel (`01_lista_tabel.sql`)

### A) Top 100 tabel po liczbie wierszy

TableFullName	RowCount_	TotalMB	UsedMB
dbo.In0E	2108520	545.320312	545.218750
dbo.Out1A	2005205	546.562500	546.429687
dbo.Aktywnosc	185004	5.757812	5.656250
dbo.State0E	101668	35.789062	35.500000
dbo.listapartii	37795	9.320312	7.062500
dbo.PartiaDostawca	37750	7.507812	5.640625
dbo.EtykietyZbiorcze	36365	8.445312	8.289062
dbo.Haccp	22717	3.320312	3.242187
dbo.KodyPocztowe	21808	3.257812	2.875000
dbo.GeoCache	20784	5.445312	5.390625
dbo.OdbiorcyCRM	20399	13.539062	13.453125
dbo.TymczasowiOdbiorcy	20378	9.445312	8.234375
dbo.ZamowieniaMiesoSnapshot	19374	1.257812	1.218750
dbo.ImportCRM	18001	4.507812	3.718750
dbo.HistoriaZmianZamowien	16136	5.007812	4.945312
dbo.ZamowieniaMiesoTowar	12549	1.320312	1.226562
dbo.WagoCounter	8168	0.507812	0.343750
dbo.FarmerCalcChangeLog	6543	1.257812	1.250000
dbo.ZamowieniaMieso	5604	2.554687	2.296875
dbo.AuditLog_Dostawy	5422	2.570312	2.523437
dbo.HarmonogramDostaw	5376	3.429687	2.632812
dbo.Notatki	4602	0.695312	0.554687
dbo.DostawaFeedback	4226	0.757812	0.648437
dbo.HistoriaZmianCRM	3031	0.382812	0.250000
dbo.kontrahenci	2633	0.765625	0.726562
dbo.WstawieniaKurczakow	2453	0.945312	0.757812
dbo.NotatkiCRM	2358	0.320312	0.242187
dbo.Pozyskiwanie_Hodowcy	1874	0.695312	0.687500
dbo.DostawcyAdresy	1512	0.382812	0.296875
dbo.CallReminderLog	1409	0.195312	0.101562
dbo.OrdBody	1292	0.320312	0.187500
dbo.StanyMagazynowe	1204	0.132812	0.078125
dbo.CenaMinisterialna	1184	0.070312	0.046875
dbo.Pozyskiwanie_Aktywnosci	1180	0.320312	0.226562
dbo.CenaRolnicza	1169	0.070312	0.046875
dbo.CenaTuszki	1113	0.195312	0.101562
dbo.FarmerCalc	1099	1.757812	1.671875
dbo.DocOut0E	1041	0.382812	0.242187
dbo.RozliczeniaZatwierdzenia	928	0.195312	0.117187
dbo.ContactHistory	879	0.257812	0.117187
dbo.Dostawcy	870	0.695312	0.523437
dbo.Reklamacje	621	0.507812	0.445312
dbo.OdpadyRejestr	555	0.507812	0.468750
dbo.PdfHistory	496	0.257812	0.195312
dbo.ReklamacjeTowary	463	0.257812	0.210937
dbo.'Dane hodowców$'	415	1.320312	0.515625
dbo.OrdHeader	404	0.257812	0.125000
dbo.DocNumber	340	0.070312	0.070312
dbo.HeaderDocOut0E	338	0.187500	0.125000
dbo.ZamowienieWydanieRoznice	291	0.070312	0.039062
dbo.DokMagPozycjeBuf	268	0.070312	0.039062
dbo.ReklamacjeHistoria	186	0.070312	0.054687
dbo.GeoCacheKodyPocztowe	176	0.070312	0.031250
dbo.KartotekaHistoriaZmian	176	0.070312	0.054687
dbo.ChatMessages	165	0.070312	0.039062
dbo.MatrycaTransferLog	107	0.070312	0.015625
dbo.FirefliesTranskrypcje	102	11.640625	11.570312
dbo.CallReminderContacts	99	0.070312	0.015625
dbo.RezerwacjeKlasWagowych	96	0.070312	0.031250
dbo.AvilogHodowcyMapping	87	0.070312	0.031250
dbo.ReklamacjeZdjecia	70	39.328125	39.250000
dbo.KartotekaOdbiorcyDane	63	0.070312	0.046875
dbo.Audit_Dostawcy	59	0.070312	0.015625
dbo.ObceKontrakty	58	0.070312	0.054687
dbo.operators	56	0.070312	0.031250
dbo.TowarZdjecia	56	23.390625	23.296875
dbo.CallReminderConfig	55	0.070312	0.031250
dbo.SpotkaniaNotyfikacje	52	0.070312	0.039062
dbo.FarmerWgtLog	51	0.070312	0.015625
dbo.Oferty_Pozycje	51	0.070312	0.015625
dbo.QC_Zdjecia	50	0.070312	0.031250
dbo.intel_Articles	49	0.070312	0.070312
dbo.ChatTypingStatus	48	0.070312	0.015625
dbo.DokMagHeaderBuf	43	0.070312	0.015625
dbo.sendback	41	0.015625	0.015625
dbo.Article	36	0.070312	0.031250
dbo.SmsHistory	35	0.070312	0.046875
dbo.ArtPartitionD	32	1.773437	1.742187
dbo.ScalowanieTowarow	32	0.070312	0.015625
dbo.CallReminderPKDPriority	29	0.070312	0.015625
dbo.DostawcyCRItem	29	0.070312	0.015625
dbo.intel_Prices	29	0.070312	0.015625
dbo.KonfiguracjaProduktow	28	0.070312	0.015625
dbo.OdbiorcyKurczaka	28	0.070312	0.039062
dbo.NotatkiWidocznosc	27	0.070312	0.015625
dbo.QC_Temperatury	26	0.070312	0.015625
dbo.DostawcyChangeRequest	25	0.070312	0.015625
dbo.KartotekaOdbiorcyKontakty	25	0.070312	0.015625
dbo.CarTrailer	24	0.070312	0.015625
dbo.HarmonogramDostaw_AuditLog	24	0.070312	0.015625
dbo.Driver	23	0.070312	0.015625
dbo.WlascicieleOdbiorcow	23	0.070312	0.015625
dbo.NotatkiUczestnicy	22	0.070312	0.015625
dbo.IRZplusLog	20	0.140625	0.062500
dbo.Temperatury	20	0.070312	0.015625
dbo.SpotkaniaUczestnicy	18	0.070312	0.015625
dbo.DostawcyCR	17	0.070312	0.015625
dbo.KartotekaOdbiorcyNotatki	17	0.070312	0.015625
dbo.Province	17	0.070312	0.015625
dbo.KG_TypyNieobecnosci	16	0.070312	0.015625

### B) Liczba wszystkich tabel + kolumn

LiczbaTabel	LiczbaKolumnLacznie
293	3884

### C) Tabele najnowsze (modyfikowane)

TableFullName	create_date	modify_date
dbo.operators	2025-08-05 14:33:31.327	2026-05-03 19:26:53.577
dbo.ZamowieniaMieso	2025-09-08 15:21:09.780	2026-04-30 16:26:43.530
dbo.HarmonogramDostaw_AuditLog	2026-04-27 10:50:28.083	2026-04-27 10:50:28.090
dbo.HarmonogramDostaw	2024-06-26 20:06:54.050	2026-04-26 23:34:14.477
dbo.Reklamacje	2025-10-06 20:45:02.480	2026-04-26 21:08:57.633
dbo.ReklamacjeKomentarze	2026-04-12 15:57:35.177	2026-04-12 15:57:35.183
dbo.ReklamacjeZalaczniki	2026-04-11 20:51:55.927	2026-04-11 20:51:55.930
dbo.NotatkiMentions	2026-04-11 19:53:43.233	2026-04-11 19:53:43.240
dbo.Notatki	2025-05-31 00:56:50.987	2026-04-11 19:53:43.230
dbo.CenaTuszki	2024-06-05 21:16:58.647	2026-04-10 07:17:29.433
dbo.CenaRolnicza	2023-10-25 11:37:51.043	2026-04-10 07:17:29.423
dbo.CenaMinisterialna	2023-10-25 14:14:52.773	2026-04-10 07:17:29.410
dbo.ReklamacjeUstawienia	2026-04-10 06:44:13.490	2026-04-10 06:44:13.490
dbo.WebfleetVehicleMapping	2026-03-28 19:27:56.383	2026-03-28 19:27:56.383
dbo.UstawieniaZmianZamowien	2026-03-18 21:00:51.140	2026-03-18 21:38:31.053
dbo.UstawieniaZmianZamowien_Wylaczenia	2026-03-18 21:00:51.150	2026-03-18 21:00:51.157
dbo.FarmerCalc	2025-07-25 10:27:10.497	2026-03-06 21:21:41.800
dbo.TransportZmiany	2026-03-06 20:50:35.683	2026-03-06 20:50:35.697
dbo.PartiaAuditLog	2026-03-05 22:45:17.920	2026-03-05 22:45:17.923
dbo.QC_Normy	2026-03-05 22:45:17.813	2026-03-05 22:45:17.813
dbo.PartiaStatus	2026-03-05 22:45:17.767	2026-03-05 22:45:17.770
dbo.listapartii	2007-07-27 13:44:27.310	2026-03-05 22:45:17.740
dbo.VehicleServiceLog	2026-03-05 22:38:14.423	2026-03-05 22:38:14.427
dbo.DriverVehicleAssignment	2026-03-05 22:38:14.413	2026-03-05 22:38:14.423
dbo.CarTrailer	2023-06-19 11:41:15.563	2026-03-05 22:38:14.423
dbo.Driver	2023-06-19 11:41:15.623	2026-03-05 22:38:14.417
dbo.VehicleDetails	2026-03-05 22:38:14.410	2026-03-05 22:38:14.410
dbo.DriverDetails	2026-03-05 22:38:14.397	2026-03-05 22:38:14.397
dbo.Pozyskiwanie_DuplicateIgnore	2026-02-21 18:06:44.000	2026-02-21 18:06:44.000
dbo.ZamowieniaMiesoSnapshot	2025-12-09 20:58:00.560	2026-02-21 14:45:21.740

---

## 📁 PLIK 02 — Views + procedury (`02_views_procedury.sql`)

### A) Lista widoków

name	create_date	modify_date
OdbiorcyCRM_Rozszerzeni	2025-07-27 20:00:48.233	2025-07-27 20:00:48.233
PartieDzisiejsze	2025-09-01 08:14:21.793	2025-09-01 08:14:21.793
RaportQC	2025-09-01 11:35:32.917	2025-09-01 11:35:32.917
v_1a_ceny	2007-07-27 13:45:55.170	2015-05-15 12:25:05.643
v_AktualnaKonfiguracjaWydajnosci	2025-10-14 20:22:59.827	2025-10-14 20:22:59.827
v_AktywneProduktKonfiguracja	2025-10-14 20:22:59.840	2025-10-14 20:22:59.840
v_DOCOUTEX_SUM	2007-08-06 14:54:38.967	2015-05-15 12:25:05.667
V_HR_AlertyNieodczytane	2025-12-22 11:46:16.467	2025-12-22 11:46:16.467
V_HR_BilansPodsumowanie	2025-12-22 11:46:16.460	2025-12-22 11:46:16.460
V_HR_UrlopydoZatwierdzenia	2025-12-22 11:46:16.473	2025-12-22 11:46:16.473
v_in1a_articleid_p2	2007-07-27 13:45:55.123	2015-05-15 12:25:05.517
v_in1a_ceny_p2	2007-07-27 13:45:55.140	2015-05-15 12:25:05.517
v_in1a_ceny_ustalone	2007-07-27 13:45:55.153	2015-05-15 12:25:05.517
V_KG_AlertyNieprzeczytane	2025-12-22 12:59:13.793	2025-12-22 12:59:13.793
V_KG_NadgodzinyAktywne	2025-12-22 12:59:13.803	2025-12-22 12:59:13.803
V_KG_SpoznieniaMiesiac	2025-12-22 12:59:13.813	2025-12-22 12:59:13.813
v_out1a_ceny_p1	2007-07-27 13:45:55.153	2015-05-15 12:25:05.603
v_SumOrdBody	2007-07-27 13:45:55.170	2015-05-15 12:25:05.643
v_WstawieniaDoKontaktu	2025-08-10 12:19:46.320	2025-08-10 12:54:00.887
vTTripFill	2025-09-08 19:19:28.020	2025-09-08 19:19:28.020
vTTripLoadSummary	2025-09-08 19:19:28.020	2025-09-08 19:19:28.020
vTTripSpaceFill	2025-09-13 02:09:00.900	2025-09-13 02:09:00.900
vw_AktywnoscUzytkownikow	2025-12-03 21:25:02.297	2025-12-03 21:25:02.297
vw_AuditLog_Czytelny	2026-01-17 14:13:57.763	2026-01-17 14:13:57.763
vw_ClosedDocs	2007-08-06 14:38:25.750	2015-05-15 12:25:05.663
vw_DostawcyBezSymfonii	2026-01-19 20:13:55.043	2026-01-19 20:13:55.043
VW_FarmerCalcRecentChanges	2026-01-04 13:51:06.003	2026-01-04 13:51:06.003
vw_NadchodzaceSpotkania	2026-01-15 14:48:35.633	2026-01-15 14:48:35.633
vw_OdpadyDzienne	2026-01-10 16:05:56.477	2026-01-10 16:05:56.477
vw_OfertyLista	2025-11-26 20:25:14.593	2025-11-26 20:25:14.593
vw_OfertyStatystyki	2025-11-26 20:25:14.600	2025-11-26 20:25:14.600
vw_OfertyTopKlienci	2025-11-26 20:25:14.610	2025-11-26 20:25:14.610
vw_OperatorzyPelne	2025-11-25 22:58:32.093	2025-11-25 22:58:32.093
VW_PdfHistoryWithDetails	2026-01-04 13:51:06.013	2026-01-04 13:51:06.013
vw_PodsumowanieSaldOpakowan	2025-11-27 20:32:32.447	2025-11-27 20:32:32.447
vw_QC_Podsum	2025-09-03 14:33:32.050	2025-09-03 14:44:46.090
vw_QC_TempSummary	2025-09-03 14:32:26.483	2025-09-03 14:44:31.817
vw_QC_WadySkale	2025-09-03 14:33:12.797	2025-09-03 14:44:40.240
vw_ReklamacjePelneInfo	2026-03-07 11:59:19.187	2026-03-07 11:59:19.187
vw_SaldaOpakowaniKontrahentow	2025-11-27 20:31:30.537	2025-11-27 20:31:30.537
vw_SpecyfikacjeDoEksportu	2026-01-19 20:10:00.207	2026-01-19 20:10:00.207
vw_SpotkaniaKalendarz	2026-01-15 14:48:35.623	2026-01-15 14:48:35.623
vw_StatusHistoriiSald	2025-11-27 21:04:50.617	2025-11-27 21:04:50.617
vw_TransportTripWithOrders	2025-09-08 18:05:20.067	2025-09-08 18:05:20.067
vwReklamacjeZbiorczy	2025-10-06 20:57:19.450	2025-10-06 20:57:19.450
vZamowieniaTransport	2025-09-16 17:04:06.790	2025-09-16 17:04:06.790
Wady	2025-09-01 16:05:14.577	2025-09-01 16:05:14.577
WidokGRYGLAS	2025-07-27 20:43:41.313	2025-07-27 20:43:41.313

### B) Lista stored procedures

name	create_date	modify_date
AddContactHistory	2025-08-10 12:19:26.460	2025-08-10 13:06:57.643
AktualizujTemperatureOstatnia	2025-09-01 14:22:29.610	2025-09-01 15:32:55.797
AktualizujWadyOstatnie	2025-09-01 14:22:29.613	2025-09-01 14:41:30.640
DodajTelefonOperatora	2025-12-14 16:54:05.597	2025-12-14 16:54:05.597
DodajZdjecie	2025-09-01 16:54:40.003	2025-09-01 16:54:40.003
GenerujHistorieSald	2025-11-27 20:31:30.907	2025-11-27 20:31:30.907
GetCallReminderStats	2026-01-25 20:55:38.267	2026-01-25 20:55:38.267
GetOperatorById	2025-09-01 13:29:52.277	2025-09-01 13:29:52.277
GetRandomContactsForReminder	2026-02-12 18:12:43.113	2026-02-12 18:12:43.113
GetTowarZdjecie	2026-01-01 10:28:38.340	2026-01-01 10:28:38.340
PobierzPartie	2025-09-01 08:14:43.893	2025-09-03 14:16:45.680
PobierzSaldoKontrahenta	2025-11-27 20:31:31.510	2025-11-27 20:31:31.510
PobierzTemperaturyPartii	2025-09-01 14:13:29.687	2025-09-01 14:13:29.687
PobierzWadyPartii	2025-09-01 14:13:29.690	2025-09-01 14:13:29.690
PrzeliczPaletyPojemniki	2025-09-16 17:04:06.800	2025-09-16 17:04:06.800
SaveTowarZdjecie	2026-01-01 10:28:38.347	2026-01-01 10:28:38.347
sp_AuditLog_GetByLP	2026-01-17 14:13:57.783	2026-01-17 14:13:57.783
sp_AuditLog_GetByUser	2026-01-17 14:13:57.810	2026-01-17 14:13:57.810
sp_AuditLog_GetRecent	2026-01-17 14:13:57.803	2026-01-17 14:13:57.803
sp_AuditLog_Insert	2026-01-17 14:13:57.773	2026-01-17 14:13:57.773
sp_AuditLog_Statistics	2026-01-17 14:13:57.820	2026-01-17 14:13:57.820
sp_BatchUpdateZamowieniaStatus	2025-12-28 18:38:28.460	2025-12-28 18:38:28.460
sp_CreateBuf1A	2023-03-07 13:03:51.783	2023-03-08 12:54:26.923
sp_CzyscStarePotwierdzenia	2025-11-27 20:32:32.413	2025-11-27 20:32:32.413
sp_GenDocYearNumber	2023-03-07 10:24:09.270	2023-08-29 11:02:32.610
sp_GenerujDziennaHistorieSald	2025-11-27 21:04:50.593	2025-11-27 21:04:50.593
sp_GenerujNumerOferty	2025-11-26 20:25:14.620	2025-11-26 20:25:14.620
sp_GetDashboardKPIs	2025-12-28 18:38:28.470	2025-12-28 18:38:28.470
sp_GetHistoriaZmianZDiff	2025-12-28 18:32:55.010	2025-12-28 18:32:55.010
sp_GetOdbiorcyDlaTowar	2026-01-10 16:05:56.490	2026-01-10 16:05:56.490
sp_GetPodsumowanieTowarowNaDzien	2025-12-28 18:38:28.480	2025-12-28 18:38:28.480
sp_GetStatystykiAnulowanych	2025-12-28 18:38:28.450	2025-12-28 18:38:28.450
sp_GetZamowieniaNaDzien	2025-12-28 18:38:28.430	2025-12-28 18:38:28.430
sp_LogujZmianeZamowienia	2025-12-03 21:25:02.270	2025-12-03 21:25:02.270
sp_NewPartNumber	2007-07-27 13:44:58.437	2015-05-15 12:25:05.300
sp_OznaczNotyfikacjePrzeczytane	2026-01-15 14:48:35.663	2026-01-15 14:48:35.663
sp_PobierzHistorieZamowienia	2025-12-03 21:25:02.280	2025-12-03 21:25:02.280
sp_PobierzKonfiguracjeNaDzien	2025-10-14 20:22:59.850	2025-10-14 20:22:59.850
sp_PobierzNieprzeczytaneNotyfikacje	2026-01-15 14:48:35.657	2026-01-15 14:48:35.657
sp_PobierzOdbiorcow	2025-11-27 01:49:24.290	2025-11-27 01:49:24.290
sp_PobierzOstatniaAktywnosc	2025-11-27 20:32:32.423	2025-11-27 20:32:32.423
sp_PobierzOstatnieZmiany	2025-12-03 21:25:02.287	2025-12-03 21:25:02.287
sp_PobierzPlanTygodniowy	2025-10-13 22:39:29.447	2025-10-13 22:39:29.447
sp_PobierzRankingHandlowcow	2025-10-06 15:43:42.447	2025-10-06 15:43:42.447
sp_PobierzSaldaDoWykresu	2025-11-27 20:32:32.400	2025-11-27 20:32:32.400
sp_PobierzStatystykiOpakowan	2025-11-27 20:32:32.407	2025-11-27 20:32:32.407
sp_PobierzSzczegolyReklamacji	2026-03-07 11:59:32.633	2026-03-07 11:59:32.633
sp_PrzeliczCeny1a	2007-07-27 13:44:58.450	2015-05-15 12:25:05.450
sp_ResetEksportuSymfonia	2026-01-19 20:10:00.240	2026-01-19 20:10:00.240
sp_SaveOrderSnapshot	2025-12-09 21:17:00.867	2025-12-09 21:17:00.867
sp_SearchOdbiorcy	2025-12-28 18:32:55.047	2025-12-28 18:32:55.047
sp_SprawdzDostepDoNotatki	2025-10-20 19:56:39.827	2025-10-20 19:57:47.780
sp_StatystykiEksportu	2026-01-19 20:10:00.227	2026-01-19 20:10:00.227
sp_StatystykiReklamacji	2026-03-07 11:59:19.287	2026-03-07 11:59:19.287
SP_UpdateHaccp	2007-07-27 13:44:58.450	2015-05-15 12:25:05.483
SP_UpdateHaccpFromTo	2007-07-27 13:44:58.467	2015-05-15 12:25:05.487
sp_UtworzPrzypomnienia	2026-01-15 14:48:35.647	2026-01-15 14:48:35.647
sp_UzupelnijHistorieSald	2025-11-27 21:04:50.603	2025-11-27 21:04:50.603
sp_WyszukajPodobneRozbieznosci	2025-11-27 20:32:32.433	2025-11-27 20:32:32.433
sp_ZapiszOferte	2025-11-26 20:25:14.630	2025-11-26 20:25:14.630
sp_ZmienStatusOferty	2025-11-26 20:25:14.640	2025-11-26 20:25:14.640
sp_ZmienStatusReklamacji	2026-03-07 11:59:32.650	2026-03-07 11:59:32.650
UpsertPodsumPartii	2025-09-02 10:34:59.793	2025-09-02 10:34:59.793
UpsertTemperaturaMiejsce	2025-09-01 16:54:39.983	2025-09-01 16:54:39.983
UpsertWadyPodsumowanie	2025-09-01 16:54:40.000	2025-09-01 16:54:40.000
UpsertWadySkale	2025-09-01 16:54:39.990	2025-09-01 16:54:39.990
ZapiszTemperature	2025-09-01 16:26:20.210	2025-09-02 10:34:59.787
ZapiszWadyPartii	2025-09-01 16:05:09.790	2025-09-01 16:05:09.790
ZapiszWadyPartiiSkale	2025-09-02 10:34:59.790	2025-09-02 10:34:59.790
ZapiszWadySzczegoly	2025-09-01 16:26:20.217	2025-09-01 16:26:20.217

### C) Lista funkcji

name	type_desc	create_date
fn_CzyPotwierdzoneSaldo	SQL_SCALAR_FUNCTION	2025-11-27 20:31:31.533
fn_IsOrderModifiedSinceSnapshot	SQL_SCALAR_FUNCTION	2025-12-09 21:17:00.877
GetOperatorName	SQL_SCALAR_FUNCTION	2025-10-08 22:59:48.703

### D) Definicje top 5 widoków

name	definition_preview
vw_ReklamacjePelneInfo	
CREATE VIEW [dbo].[vw_ReklamacjePelneInfo]
AS
SELECT
    r.Id,
    r.DataZgloszenia,
    r.UserID,
    r.IdDokumentu,
    r.NumerDokumentu,
    r.IdKontrahenta,
    r.NazwaKontrahenta,
    r.Opis,
    r.SumaKg,
    r.SumaWartosc,
    r.Status,
    r.OsobaRozpatrujaca,
    r.Komentarz,
    r.Rozwiazanie,
    r.DataModyfikacji,
    r.DataZamkniecia,
    r.TypReklamacji,
    r.Priorytet,
    r.KosztReklamacji,
    -- Obliczenia
    DATEDIFF(DAY, r.DataZgloszenia, ISNULL(r.DataZamkniecia, GETDATE())) AS DniRozpatrywania,
    (SELECT COUNT(*) FROM ReklamacjeTowary rt WHERE rt.IdReklamacji = r.Id) AS LiczbaTowrow,
    (SELECT COUNT(*) FROM ReklamacjeZdjecia rz WHERE rz.IdReklamacji = r.Id) AS LiczbaZdjec,
    (SELECT COUNT(*) FROM ReklamacjePartie rp WHERE rp.IdReklamacji = r.Id) AS LiczbaPartii,
    -- Kolorowanie statusu
    CASE r.Status
        WHEN 'Nowa' THEN '#3498db'
        WHEN 'W trakcie' THEN '#f39c12'
        WHEN 'Zaakceptowana' THEN '#27ae60'
        WHEN 'Odrzucona' THEN '#e74c3c'
        WHEN 'Zamknięta' THEN '#95a5a6'
        ELSE '#000000'
    END AS KolorStatusu
FROM [dbo].[Reklamacje] r
vw_DostawcyBezSymfonii	
-- Utwórz poprawiony widok (Address zamiast Adres)
CREATE VIEW dbo.vw_DostawcyBezSymfonii AS
SELECT 
    d.ID,
    d.ShortName,
    d.Name,
    d.Nip,
    d.Address,
    d.City,
    d.IdSymf,           -- ID z Symfonii (jeśli używane)
    d.SymfoniaKod,      -- Kod kontrahenta z Symfonii
    (SELECT COUNT(*) FROM dbo.FarmerCalc fc 
     WHERE LTRIM(RTRIM(fc.CustomerGID)) = LTRIM(RTRIM(d.ID))
       AND (fc.Symfonia = 0 OR fc.Symfonia IS NULL)) AS DostawyDoEksportu
FROM dbo.Dostawcy d
WHERE d.Halt = 0
  AND (d.SymfoniaKod IS NULL OR d.SymfoniaKod = '')
  AND (d.IdSymf IS NULL OR d.IdSymf = 0)  -- Też nie ma IdSymf
  AND EXISTS (
      SELECT 1 FROM dbo.FarmerCalc fc 
      WHERE LTRIM(RTRIM(fc.CustomerGID)) = LTRIM(RTRIM(d.ID))
  );
vw_SpecyfikacjeDoEksportu	
CREATE VIEW dbo.vw_SpecyfikacjeDoEksportu AS
SELECT 
    fc.ID,
    fc.CalcDate,
    fc.CarLp,
    fc.CustomerGID,
    d.ShortName AS DostawcaNazwa,
    d.NIP AS DostawcaNIP,
    d.SymfoniaKod,
    fc.DeclI1 AS SztukiDek,
    fc.DeclI2 AS Padle,
    fc.DeclI3 AS CH,
    fc.DeclI4 AS NW,
    fc.DeclI5 AS ZM,
    fc.NettoFarmWeight,
    fc.NettoWeight,
    fc.PayWgt,
    -- Obliczona waga do rozliczenia
    CASE 
        WHEN fc.PayWgt > 0 THEN fc.PayWgt
        WHEN fc.NettoFarmWeight > 0 THEN fc.NettoFarmWeight
        ELSE fc.NettoWeight
    END AS WagaDoRozliczenia,
    fc.Price,
    fc.Loss,
    fc.IncDeadConf AS PIK,
    fc.Symfonia,
    fc.SymfoniaDocNr,
    fc.SymfoniaExportDate
FROM dbo.FarmerCalc fc
LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(fc.CustomerGID)) = LTRIM(RTRIM(d.ID))
WHERE fc.CustomerGID IS NOT NULL
  AND fc.CustomerGID <> '';
vw_AuditLog_Czytelny	
CREATE VIEW vw_AuditLog_Czytelny AS
SELECT
    AuditID,
    FORMAT(DataZmiany, 'yyyy-MM-dd HH:mm:ss') AS DataZmianyFormatowana,
    UserID,
    UserName,
    NazwaTabeli,
    RekordID AS LP,
    CASE TypOperacji
        WHEN 'INSERT' THEN 'Dodanie'
        WHEN 'UPDATE' THEN 'Zmiana'
        WHEN 'DELETE' THEN 'Usunięcie'
        ELSE TypOperacji
    END AS TypOperacjiPL,
    CASE ZrodloZmiany
        WHEN 'DoubleClick_Auta' THEN 'Dwuklik - Auta'
        WHEN 'DoubleClick_Sztuki' THEN 'Dwuklik - Sztuki'
        WHEN 'DoubleClick_Waga' THEN 'Dwuklik - Waga'
        WHEN 'DoubleClick_Uwagi' THEN 'Dwuklik - Uwagi'
        WHEN 'Checkbox_Potwierdzenie' THEN 'Checkbox potwierdzenia'
        WHEN 'Checkbox_Wstawienie' THEN 'Checkbox wstawienia'
        WHEN 'Button_DataUp' THEN 'Przycisk data +1'
        WHEN 'Button_DataDown' THEN 'Przycisk data -1'
        WHEN 'DragDrop' THEN 'Przeciągnij i upuść'
        WHEN 'Form_Zapisz' THEN 'Formularz zapisu'
        WHEN 'Form_DodajNotatke' THEN 'Dodaj notatkę'
        WHEN 'QuickNote' THEN 'Szybka notatka'
        WHEN 'Button_Duplikuj' THEN 'Duplikacja'
        WHEN 'Button_Usun' THEN 'Usunięcie'
        WHEN 'ContextMenu_Potwierdz' THEN 'Menu - Potwierdź'
        WHEN 'ContextMenu_Anuluj' THEN 'Menu - Anuluj'
        WHEN 'BulkConfirm' THEN 'Masowe potwierdzenie'
        WHEN 'BulkCancel' THEN 'Masowe anulowanie'
        ELSE ZrodloZmiany
    END AS ZrodloZmianyPL,
    CASE NazwaPola
        WHEN 'A
vw_NadchodzaceSpotkania	
CREATE VIEW vw_NadchodzaceSpotkania AS
SELECT
    s.SpotkaniID,
    s.Tytul,
    s.DataSpotkania,
    s.CzasTrwaniaMin,
    s.TypSpotkania,
    s.Status,
    s.OrganizatorID,
    s.OrganizatorNazwa,
    s.Lokalizacja,
    s.LinkSpotkania,
    s.Priorytet,
    u.OperatorID AS UczestnikID,
    u.OperatorNazwa AS UczestnikNazwa,
    u.StatusZaproszenia,
    u.CzyObowiazkowy,
    u.CzyPowiadomiony,
    DATEDIFF(MINUTE, GETDATE(), s.DataSpotkania) AS MinutyDoSpotkania
FROM Spotkania s
INNER JOIN SpotkaniaUczestnicy u ON s.SpotkaniID = u.SpotkaniID
WHERE s.Status = 'Zaplanowane'
  AND s.DataSpotkania > GETDATE()
  AND s.DataSpotkania < DATEADD(DAY, 7, GETDATE());


### E) Triggery

table_name	trigger_name	create_date	is_disabled
Dostawcy	tr_Dostawcy_Audit	2025-08-30 07:46:28.380	1
Dostawcy	tr_Dostawcy_Stamps	2025-08-30 07:46:28.373	1
PotwierdzeniaSaldaOpakowan	trg_PotwierdzeniaSalda_Audit	2025-11-27 20:32:32.453	0
Reklamacje	tr_Reklamacje_LogujZmiany	2025-10-07 23:50:44.270	0
TCarTrailer	TCarTrailer_SetUpdatedAt	2025-09-10 10:02:06.027	0
ZamowieniaMiesoTowar	TR_ZamowieniaMiesoTowar_UpdateModyfikacja	2025-12-09 21:17:00.850	0

---

## 📁 PLIK 03 — Wersja serwera (`03_wersja_serwera.sql`)

### A) `@@VERSION`

WersjaServera
Microsoft SQL Server 2022 (RTM) - 16.0.1000.6 (X64) 
	Oct  8 2022 05:58:25 
	Copyright (C) 2022 Microsoft Corporation
	Developer Edition (64-bit) on Windows 10 Pro 10.0 <X64> (Build 19045: )


### B) Properties

```
BazaDanych	ProductVersion	ProductLevel	Edition	DefaultCollation	DBCollation
LibraNet	16.0.1000.6	RTM	Developer Edition (64-bit)	Polish_CI_AS	Polish_CI_AS

```

### C) Test TRY_CONVERT

```
Nie działa


```

### D) Rozmiar bazy

```
LogicalName	type_desc	SizeMB
PiorkowscyLibraNet	ROWS	2776.312500
PiorkowscyLibraNet_log	LOG	239.687500

```

---

## 📁 PLIK 04 — `listapartii` (`04_listapartii.sql`)

### A) Struktura kolumn

```
ORDINAL_POSITION	COLUMN_NAME	DATA_TYPE	CHARACTER_MAXIMUM_LENGTH	NUMERIC_PRECISION	IS_NULLABLE	COLUMN_DEFAULT
1	GUID	varchar	36	NULL	NO	NULL
2	DIR_ID	varchar	2	NULL	NO	NULL
3	Partia	varchar	15	NULL	NO	NULL
4	GrupaTowarowa	numeric	NULL	1	YES	NULL
5	ArticleID	varchar	10	NULL	YES	NULL
6	CreateData	varchar	10	NULL	YES	NULL
7	CreateGodzina	varchar	8	NULL	YES	NULL
8	ModificationData	varchar	10	NULL	YES	NULL
9	ModificationGodzina	varchar	10	NULL	YES	NULL
10	CreateOperator	varchar	6	NULL	YES	NULL
11	CloseData	varchar	10	NULL	YES	NULL
12	CloseGodzina	varchar	8	NULL	YES	NULL
13	CloseOperator	varchar	6	NULL	YES	NULL
14	IsClose	smallint	NULL	5	YES	NULL
15	CalcMethod	varchar	1	NULL	YES	NULL
16	CalcData	varchar	10	NULL	YES	NULL
17	CalcGodzina	varchar	8	NULL	YES	NULL
18	StatusV2	varchar	30	NULL	YES	('IN_PRODUCTION')
19	HarmonogramLp	int	NULL	10	YES	NULL
```

### B) 10 najnowszych rekordów

```
GUID	DIR_ID	Partia	GrupaTowarowa	ArticleID	CreateData	CreateGodzina	ModificationData	ModificationGodzina	CreateOperator	CloseData	CloseGodzina	CloseOperator	IsClose	CalcMethod	CalcData	CalcGodzina	StatusV2	HarmonogramLp
E256FA1F-7013-4765-B327-46EBB4A58F0E	1A	26119015	0		2026-04-29	11:34:38			1				0	O			IN_PRODUCTION	NULL
ADA40B43-ED42-4F36-92A9-23E93D0513C9	1A	26119014	0		2026-04-29	11:34:30			1				0	O			IN_PRODUCTION	NULL
04CC0547-50B8-43CF-8DB5-184112E207CD	1A	26119013	0		2026-04-29	09:51:30			1				0	O			IN_PRODUCTION	NULL
3EFE1BF8-0B5F-40ED-A92E-9FC0F8A6AD7E	1A	26119012	0		2026-04-29	09:51:17			1				0	O			IN_PRODUCTION	NULL
ED82FA9F-1706-4D5B-BE29-AC7D7F958319	1A	26119011	0		2026-04-29	09:51:07			1				0	O			IN_PRODUCTION	NULL
DB001C49-BF67-42C0-A2E2-80B4712536A5	1A	26119010	0		2026-04-29	09:51:00			1				0	O			IN_PRODUCTION	NULL
B8B89DBD-175D-4745-AE22-4A1B120320EB	1A	26119009	0		2026-04-29	08:14:02			1				0	O			IN_PRODUCTION	NULL
0F1FABF7-87F7-46F5-8DB5-4C9E346A2904	1A	26119008	0		2026-04-29	08:13:42			1				0	O			IN_PRODUCTION	NULL
DEA76CD5-FCFE-4418-868F-6D15972A8462	1A	26119007	0		2026-04-29	07:30:40			1				0	O			IN_PRODUCTION	NULL
65EF6A9B-6B62-4AE1-8E5F-8AAAA82C9D6A	1A	26119006	0		2026-04-29	05:52:41			1				0	O			IN_PRODUCTION	NULL
```

### C) Rozkład statusów V2

```
[wklej wynik tutaj]


```

### D) Rozkład działów

```
[wklej wynik tutaj]


```

### E) Partie dziennie ostatnie 30 dni

```
[wklej wynik tutaj]


```

### F) Liczba partii per rok

```
[wklej wynik tutaj]


```

### G) Min/max CreateData

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 05 — `In0E` (rdzeń ważeń) (`05_in0e.sql`)

### A) Struktura kolumn

```
[wklej wynik tutaj]


```

### B) 10 najnowszych ważeń

```
[wklej wynik tutaj]


```

### C) Rozkład klas wagowych (Kurczak A)

```
[wklej wynik tutaj]


```

### D) Rozkład TermID (terminale)

```
[wklej wynik tutaj]


```

### E) Rozkład Direction

```
[wklej wynik tutaj]


```

### F) Aktywni operatorzy

```
[wklej wynik tutaj]


```

### G) Histogram godzinowy ważeń

```
[wklej wynik tutaj]


```

### H) Empiryczna tolerancja per towar

```
[wklej wynik tutaj]


```

### I) Ważenia bez P1

```
[wklej wynik tutaj]


```

### J) Czy P2 != P1 się zdarza

```
[wklej wynik tutaj]


```

### K) Min/max Data

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 06 — `Article` (`06_article.sql`)

### A) Struktura kolumn

```
[wklej wynik tutaj]


```

### B) Pełne wiersze dla ID='40' (Kurczak A)

```
[wklej wynik tutaj]


```

### C) Top 30 najczęściej ważonych towarów

```
[wklej wynik tutaj]


```

### D) Lista wszystkich towarów (max 100)

```
[wklej wynik tutaj]


```

### E) Liczba towarów

```
[wklej wynik tutaj]


```

### F) Szukanie kolumn tolerancji

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 07 — `PartiaDostawca` (`07_partiadostawca.sql`)

### A) Struktura kolumn

```
[wklej wynik tutaj]


```

### B) 10 najnowszych

```
[wklej wynik tutaj]


```

### C) Top 30 hodowców (90 dni)

```
[wklej wynik tutaj]


```

### D) Duplikaty (ten sam CustomerName, różne CustomerID)

```
[wklej wynik tutaj]


```

### E) Liczba unikalnych hodowców

```
[wklej wynik tutaj]


```

### F) Test dekodera partii

```
[wklej wynik tutaj]


```

### G) Sanity check daty z partii vs CreateData

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 08 — `HarmonogramDostaw` + `FarmerCalc` + `WstawieniaKurczakow` (`08_harmonogram_farmercalc.sql`)

### A) Struktura HarmonogramDostaw

```
[wklej wynik tutaj]


```

### B) Struktura FarmerCalc

```
[wklej wynik tutaj]


```

### C) Struktura WstawieniaKurczakow

```
[wklej wynik tutaj]


```

### D) 10 najnowszych pozycji harmonogramu

```
[wklej wynik tutaj]


```

### E) 10 najnowszych farmer calc

```
[wklej wynik tutaj]


```

### F) 10 najnowszych wstawień

```
[wklej wynik tutaj]


```

### G) Statystyki harmonogramu 30 dni

```
[wklej wynik tutaj]


```

### H) Liczba rekordów

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 09 — `ZamowieniaMieso` (`09_zamowieniamieso.sql`)

### A) Struktura ZamowieniaMieso

```
[wklej wynik tutaj]


```

### B) Struktura ZamowieniaMiesoTowar

```
[wklej wynik tutaj]


```

### C) Struktury innych tabel zamówień

```
[wklej wynik tutaj]


```

### D) 10 najnowszych zamówień

```
[wklej wynik tutaj]


```

### E) Rozkład statusów (90 dni)

```
[wklej wynik tutaj]


```

### F) Rozkład TransportStatus

```
[wklej wynik tutaj]


```

### G) Top 30 klientów

```
[wklej wynik tutaj]


```

### H) Anulacje per dzień (30 dni)

```
[wklej wynik tutaj]


```

### I) Liczba wierszy w tabelach zamówień

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 10 — Kartoteka Odbiorcy CRM (`10_kartoteka_odbiorcy.sql`)

### A) Struktury 6 tabel CRM

```
[wklej wynik tutaj]


```

### B) Liczba rekordów per tabela

```
[wklej wynik tutaj]


```

### C-1) Sample KartotekaOdbiorcyDane

```
[wklej wynik tutaj]


```

### C-2) Sample KartotekaOdbiorcyKontakty

```
[wklej wynik tutaj]


```

### C-3) Sample KartotekaOdbiorcyNotatki

```
[wklej wynik tutaj]


```

### C-4) Sample KartotekaPrzypomnienia

```
[wklej wynik tutaj]


```

### C-5) Sample KartotekaScoring

```
[wklej wynik tutaj]


```

### D) Struktury ContactHistory + SmsHistory + SmsChangeLog

```
[wklej wynik tutaj]


```

### E) Liczby ContactHistory/SmsHistory/SmsChangeLog

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 11 — Klucze obce + indeksy (`11_relacje_klucze.sql`)

### A) Wszystkie foreign keys

```
[wklej wynik tutaj]


```

### B) Indeksy dla kluczowych tabel

```
[wklej wynik tutaj]


```

### C) Liczba indeksów per tabela

```
[wklej wynik tutaj]


```

### D) Default constraints

```
[wklej wynik tutaj]


```

### E) Check constraints

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 12 — Triggery + procedury (`12_triggery.sql`)

### A) Wszystkie triggery

```
[wklej wynik tutaj]


```

### B) Procedury/widoki używające tabel kluczowych

```
[wklej wynik tutaj]


```

### C) Definicje wszystkich procedur (skrócone)

```
[wklej wynik tutaj]


```

### D) Definicje wszystkich widoków (skrócone)

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 13 — Quirki: typy Data/Godzina (`13_quirki_typy.sql`)

### A) Wszystkie kolumny Data/Godzina/Czas

```
[wklej wynik tutaj]


```

### B) Kolumny varchar zawierające 'Data' w nazwie

```
[wklej wynik tutaj]


```

### C) Kolumny IsClose / Status

```
[wklej wynik tutaj]


```

### D) Kolumny GUID

```
[wklej wynik tutaj]


```

### E) Kolumny CustomerID/CustomerName

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 14 — Rozszerzenia ZPSP (`14_extensions_zpsp.sql`)

### A) Struktury wszystkich rozszerzeń

```
[wklej wynik tutaj]


```

### B) Liczba wierszy w rozszerzeniach

```
[wklej wynik tutaj]


```

### C) PartiaStatus — 10 najnowszych

```
[wklej wynik tutaj]


```

### D) QC_Normy — wszystkie

```
[wklej wynik tutaj]


```

### E) Pozyskiwanie_Hodowcy — sample

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 15 — Dostawcy + DostawcyCR (`15_dostawcy_cr.sql`)

### A) Struktury

```
[wklej wynik tutaj]


```

### B) Liczba rekordów

```
[wklej wynik tutaj]


```

### C) Sample DostawcyCR (Proposed)

```
[wklej wynik tutaj]


```

### D) Sample DostawcyCRItem

```
[wklej wynik tutaj]


```

### E) Rozkład statusów

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 16 — Avilog + WstawieniaKurczakow (`16_avilog_wstawienia.sql`)

### A) Struktury

```
[wklej wynik tutaj]


```

### B) Liczba rekordów

```
[wklej wynik tutaj]


```

### C) Sample 10 mapowań Avilog

```
[wklej wynik tutaj]


```

### D) Sample 10 wstawień

```
[wklej wynik tutaj]


```

### E) v_WstawieniaDoKontaktu — sample

```
[wklej wynik tutaj]


```

### F) Definicja v_WstawieniaDoKontaktu

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 17 — Kursy + Ładunki + Kierowca + Pojazd (`17_kursy_ladunki.sql`)

### A) Struktury

```
[wklej wynik tutaj]


```

### B) Liczba rekordów

```
[wklej wynik tutaj]


```

### C) Sample 5 kursów

```
[wklej wynik tutaj]


```

### D) Sample 5 ładunków

```
[wklej wynik tutaj]


```

### E) Sample 5 kierowców

```
[wklej wynik tutaj]


```

### F) Sample 5 pojazdów

```
[wklej wynik tutaj]


```

### G) Lista baz na serwerze 109

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 18 — SMS + komunikacja (`18_sms_komunikacja.sql`)

### A) Struktury

```
[wklej wynik tutaj]


```

### B) Liczba rekordów

```
[wklej wynik tutaj]


```

### C) Sample SmsHistory

```
[wklej wynik tutaj]


```

### D) Sample ContactHistory

```
[wklej wynik tutaj]


```

### E) Sample CallReminderLog

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 19 — Dashboard + AppSettings + Cenniki (`19_dashboard_appsettings.sql`)

### A) Struktury

```
[wklej wynik tutaj]


```

### B) Liczba rekordów

```
[wklej wynik tutaj]


```

### C-1) AppSettings — 20 rekordów

```
[wklej wynik tutaj]


```

### C-2) DashboardWidoki — 10 rekordów

```
[wklej wynik tutaj]


```

### C-3) KonfiguracjaWydajnosc

```
[wklej wynik tutaj]


```

### C-4) KolejnoscTowarow

```
[wklej wynik tutaj]


```

### C-5) PriceType

```
[wklej wynik tutaj]


```

### D) Struktury cenników

```
[wklej wynik tutaj]


```

### E) Liczba rekordów cenników

```
[wklej wynik tutaj]


```

### F) 10 najnowszych cen tuszki

```
[wklej wynik tutaj]


```

---

## 📁 PLIK 20 — Haccp + Jakość (`20_haccp_jakosc.sql`)

### A) Struktury

```
[wklej wynik tutaj]


```

### B) Liczba rekordów

```
[wklej wynik tutaj]


```

### C) Sample 10 Haccp

```
[wklej wynik tutaj]


```

### D) QC_Normy — wszystkie

```
[wklej wynik tutaj]


```

### E) Sample 10 QC_Zdjecia

```
[wklej wynik tutaj]


```

### F) Sample 10 OdpadyRejestr

```
[wklej wynik tutaj]


```

### G-1) vw_QC_Podsum — sample

```
[wklej wynik tutaj]


```

### G-2) vw_QC_WadySkale — sample

```
[wklej wynik tutaj]


```

### H) Out1A — sample 10 + min/max

```
[wklej wynik tutaj]


```

---

# ✅ STATUS UKOŃCZENIA

**Plik 01:** ☐ Lista tabel
**Plik 02:** ☐ Views + procedury
**Plik 03:** ☐ Wersja serwera
**Plik 04:** ☐ listapartii
**Plik 05:** ☐ In0E
**Plik 06:** ☐ Article
**Plik 07:** ☐ PartiaDostawca
**Plik 08:** ☐ HarmonogramDostaw + FarmerCalc
**Plik 09:** ☐ ZamowieniaMieso
**Plik 10:** ☐ Kartoteka Odbiorcy
**Plik 11:** ☐ Klucze + indeksy
**Plik 12:** ☐ Triggery
**Plik 13:** ☐ Quirki
**Plik 14:** ☐ Rozszerzenia ZPSP
**Plik 15:** ☐ Dostawcy CR
**Plik 16:** ☐ Avilog
**Plik 17:** ☐ Kursy + ładunki
**Plik 18:** ☐ SMS
**Plik 19:** ☐ Dashboard + AppSettings
**Plik 20:** ☐ Haccp + jakość

> Zaznacz `☑` po wklejeniu wyników. Możesz robić to po kawałku — niekoniecznie wszystko naraz.

---

# 🔧 PROBLEMY / NOTATKI Sergiusza

(Tu wpisuj jeśli coś nie działa, jakaś tabela nie istnieje, jakieś kolumny brakują)

```
[notatki Sergiusza tutaj]


```

---

# 📝 BONUS: Inne bazy do sprawdzenia kiedy indziej

## TransportPL (192.168.0.109)

```sql
USE TransportPL;
GO
SELECT t.name, p.rows
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id
WHERE p.index_id IN (0,1)
ORDER BY p.rows DESC;
```

```
[wklej wynik tutaj]


```

## Handel (192.168.0.112)

```sql
USE Handel;
GO
SELECT s.name + '.' + t.name AS pelna_nazwa, p.rows
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id
WHERE p.index_id IN (0,1)
  AND s.name IN ('HM', 'SSCommon', 'dbo')
ORDER BY p.rows DESC;
```

```
[wklej wynik tutaj]


```

## UNISYSTEM (192.168.0.23\SQLEXPRESS)

```sql
USE UNISYSTEM;
GO
SELECT name FROM sys.tables ORDER BY name;
SELECT name FROM sys.views ORDER BY name;
```

```
[wklej wynik tutaj]


```
