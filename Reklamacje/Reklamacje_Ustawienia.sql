-- ============================================
-- SKRYPT: Tabela ReklamacjeUstawienia
-- Baza danych: LibraNet (serwer 192.168.0.109)
-- Tabela tworzona automatycznie w kodzie C#
-- Ten skrypt jest tylko do dokumentacji
-- ============================================

USE [LibraNet]
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ReklamacjeUstawienia')
BEGIN
    CREATE TABLE [dbo].[ReklamacjeUstawienia] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Klucz] NVARCHAR(100) NOT NULL UNIQUE,
        [Wartosc] NVARCHAR(500) NULL,
        [DataModyfikacji] DATETIME DEFAULT GETDATE(),
        [ZmodyfikowalUser] NVARCHAR(50) NULL
    );

    -- Domyslna data od korekt = 6 miesiecy wstecz
    INSERT INTO [dbo].[ReklamacjeUstawienia] (Klucz, Wartosc)
    VALUES ('DataOdKorekt', CONVERT(NVARCHAR, DATEADD(MONTH, -6, GETDATE()), 23));

    PRINT 'Utworzono tabele ReklamacjeUstawienia z domyslna data'
END
GO
