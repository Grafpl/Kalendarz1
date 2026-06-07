DECLARE @Data DATE = '2026-06-03';
SELECT hd.Lp, hd.Dostawca, hd.Auta AS Plan_Aut, hd.SztukiDek AS Plan_Szt,
       hd.Utworzone, hd.Otrzymane,
       hd.PotwWaga, hd.PotwSztuki, hd.PotwCena, hd.KtoWaga, hd.KtoSztuki,
       CONVERT(varchar(16), hd.DataUtw, 120) AS Utw,
       CONVERT(varchar(16), hd.DataMod, 120) AS DataModyfik,
       (SELECT COUNT(*) FROM dbo.FarmerCalc fc WHERE fc.LpDostawy = hd.Lp AND fc.CalcDate = @Data AND ISNULL(fc.Deleted,0)=0) AS AutaReal
FROM dbo.HarmonogramDostaw hd
WHERE CAST(hd.DataOdbioru AS DATE) = @Data
ORDER BY hd.Dostawca, hd.Lp;
