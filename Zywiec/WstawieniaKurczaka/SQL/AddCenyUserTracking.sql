-- Dodanie kolumn KtoDodal i KiedyDodal do tabel cen
-- Uruchomic raz w SSMS na LibraNet (192.168.0.109)

USE LibraNet;
GO

-- CenaMinisterialna
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CenaMinisterialna') AND name = 'KtoDodal')
BEGIN
    ALTER TABLE dbo.CenaMinisterialna ADD KtoDodal INT NULL;
    ALTER TABLE dbo.CenaMinisterialna ADD KiedyDodal DATETIME NULL;
END
GO

-- CenaRolnicza
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CenaRolnicza') AND name = 'KtoDodal')
BEGIN
    ALTER TABLE dbo.CenaRolnicza ADD KtoDodal INT NULL;
    ALTER TABLE dbo.CenaRolnicza ADD KiedyDodal DATETIME NULL;
END
GO

-- CenaTuszki
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CenaTuszki') AND name = 'KtoDodal')
BEGIN
    ALTER TABLE dbo.CenaTuszki ADD KtoDodal INT NULL;
    ALTER TABLE dbo.CenaTuszki ADD KiedyDodal DATETIME NULL;
END
GO
