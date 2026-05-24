-- ============================================================
-- 06. SQL DDL — gotowe do skopiowania
-- ============================================================
-- Wszystkie tabele prefiksowane BS_ (Broiler Signals)
-- Target: LibraNet (gdzie listapartii, In0E, Out1A)
--
-- KOLEJNOSC WDROZENIA:
--   QW01 -> U01 (zmiana ReklamacjeModels enum) - osobny skrypt w Reklamacje/SQL/
--   QW02 -> NF02 (BS_Antybiotyk, BS_FarmTreatment, BS_ResidueTest)
--   QW03 -> NF01 (BS_FlockScoring, BS_HodowcaScorecard)
--   QW04 -> U02 (walidator w PartiaService.cs, brak nowych tabel)
--   QW05 -> NF09 minimum (BS_PackagingBatch, BS_TraceabilityScan + view)
--   ST*  -> reszta NFs
--
-- KAZDA TABELA: zostaw place dla index suggestion (pattern: IX_BS_*_KeyCol)
-- ============================================================

USE LibraNet;
GO

-- ============================================================
-- NF01 — Hodowca FPD/Hock Scorecard
-- ============================================================
CREATE TABLE BS_FlockScoring (
    Id              INT IDENTITY PRIMARY KEY,
    PartiaId        INT NOT NULL,
    SampleTs        DATETIME NOT NULL,
    SampleSize      INT NOT NULL,
    FPD_Score0      INT NOT NULL DEFAULT 0,
    FPD_Score1      INT NOT NULL DEFAULT 0,
    FPD_Score2      INT NOT NULL DEFAULT 0,
    HockBurn_Count  INT NOT NULL DEFAULT 0,
    Scratches_Count INT NOT NULL DEFAULT 0,
    OperatorId      INT NOT NULL,
    Foto_BlobId     UNIQUEIDENTIFIER NULL,
    Uwagi           NVARCHAR(500) NULL,
    CreatedAt       DATETIME NOT NULL DEFAULT GETDATE()
);
CREATE INDEX IX_BS_FlockScoring_Partia ON BS_FlockScoring(PartiaId, SampleTs);

CREATE TABLE BS_HodowcaScorecard (
    Id              INT IDENTITY PRIMARY KEY,
    HodowcaId       INT NOT NULL,
    PartiaId        INT NOT NULL,
    DataObliczenia  DATETIME NOT NULL DEFAULT GETDATE(),
    FPD_Index       DECIMAL(6,2),    -- (score1*0.5 + score2*2)/total*100
    HockBurn_Pct    DECIMAL(5,2),
    Scratches_Pct   DECIMAL(5,2),
    DOA_Pct         DECIMAL(5,3),
    CatchInjury_Pct DECIMAL(5,3),
    AntybioCleanFlag BIT NOT NULL DEFAULT 0,
    Reklamacje_Cnt  INT NOT NULL DEFAULT 0,
    Punktacja       INT NOT NULL,    -- 0-100 score syntetyczny
    Notyfikacja     NVARCHAR(200) NULL
);
CREATE INDEX IX_BS_HodowcaScorecard_Hodowca ON BS_HodowcaScorecard(HodowcaId, DataObliczenia DESC);
CREATE UNIQUE INDEX UX_BS_HodowcaScorecard_Partia ON BS_HodowcaScorecard(PartiaId);


-- ============================================================
-- NF02 — Antybiotyki + Withdrawal + Residue
-- ============================================================
CREATE TABLE BS_Antybiotyk (
    Id              INT IDENTITY PRIMARY KEY,
    Nazwa           NVARCHAR(100) NOT NULL,
    Substancja      NVARCHAR(100) NULL,
    KategoriaWHO    NVARCHAR(20),    -- '1ST','2ND','3RD'
    WithdrawalDays  INT NOT NULL,
    MRL_mg_kg       DECIMAL(8,4) NULL,
    EUMaxDosage     DECIMAL(8,3) NULL,
    Aktywny         BIT NOT NULL DEFAULT 1
);

CREATE TABLE BS_FarmTreatment (
    Id              INT IDENTITY PRIMARY KEY,
    HodowcaId       INT NOT NULL,
    PartiaId        INT NULL,
    AntybiotykId    INT NOT NULL,
    DataPodania     DATE NOT NULL,
    DataKonca       DATE NOT NULL,
    WithdrawalDays  INT NOT NULL,    -- snapshot z BS_Antybiotyk w momencie wpisu
    Dawka           NVARCHAR(50),
    Powod           NVARCHAR(200),
    VetSignature    NVARCHAR(200),
    Skan_BlobId     UNIQUEIDENTIFIER NULL,
    DataMozliwegoUboju AS DATEADD(DAY, WithdrawalDays, DataKonca) PERSISTED,
    CreatedAt       DATETIME NOT NULL DEFAULT GETDATE(),
    FOREIGN KEY (AntybiotykId) REFERENCES BS_Antybiotyk(Id)
);
CREATE INDEX IX_BS_FarmTreatment_Hodowca ON BS_FarmTreatment(HodowcaId, DataMozliwegoUboju);

CREATE TABLE BS_ResidueTest (
    Id              INT IDENTITY PRIMARY KEY,
    PartiaId        INT NOT NULL,
    Lab             NVARCHAR(100),
    DataPobrania    DATE NOT NULL,
    DataWyniku      DATE,
    Wynik           NVARCHAR(20),    -- 'OK','DETECTED','EXCEEDED'
    Substancja      NVARCHAR(100) NULL,
    Wartosc_mg_kg   DECIMAL(8,4) NULL,
    MRL_mg_kg       DECIMAL(8,4) NULL,
    DokumentBlobId  UNIQUEIDENTIFIER NULL
);
CREATE INDEX IX_BS_ResidueTest_Partia ON BS_ResidueTest(PartiaId);

-- Seed kategorii antybiotyków (typowe dla broilerów PL)
INSERT INTO BS_Antybiotyk (Nazwa, Substancja, KategoriaWHO, WithdrawalDays, MRL_mg_kg) VALUES
('Amoxicillin 50%',        'amoksycylina',          '1ST', 1,  0.05),
('Doxycycline 50%',        'doksycyklina',          '2ND', 7,  0.10),
('Enrofloxacin 10%',       'enrofloksacyna',        '3RD', 13, 0.05),
('Tilmicosin 25%',         'tilmikozyna',           '2ND', 12, 0.075),
('Tylosin tartrate 100',   'tylozyna',              '2ND', 0,  0.20),
('Sulfadimethoxine',       'sulfadimetoksyna',      '2ND', 12, 0.10),
('Florfenicol 23%',        'florfenikol',           '2ND', 5,  0.10);


-- ============================================================
-- NF03 — Transport CCP (climat + ramp inspection)
-- ============================================================
-- UWAGA: tabele w TransportPL, nie w LibraNet!
USE TransportPL;
GO

CREATE TABLE BS_TransportClimat (
    Id              INT IDENTITY PRIMARY KEY,
    KursId          INT NOT NULL,
    PomiarTs        DATETIME NOT NULL,
    Temperatura     DECIMAL(4,1),
    Wilgotnosc      DECIMAL(4,1),
    Pozycja         NVARCHAR(20),    -- 'FRONT_TOP','CENTER','REAR_BOTTOM','REAR_TOP','FRONT_BOTTOM'
    AmbientTemp     DECIMAL(4,1) NULL,
    AmbientHumidity DECIMAL(4,1) NULL,
    StatusFlag      NVARCHAR(20) NULL -- 'OK','HOTSPOT','COLD_RISK','EXTREME'
);
CREATE INDEX IX_BS_TransportClimat_Kurs ON BS_TransportClimat(KursId, PomiarTs);

CREATE TABLE BS_RampInspection (
    Id              INT IDENTITY PRIMARY KEY,
    KursId          INT NOT NULL,
    PartiaId        INT NOT NULL,    -- LibraNet listapartii.Lp
    TsArrival       DATETIME NOT NULL,
    TsSlaughterStart DATETIME NULL,
    WaitingMinutes  AS DATEDIFF(MINUTE, TsArrival, TsSlaughterStart) PERSISTED,
    TotalBirds      INT NOT NULL,
    DOA_Count       INT NOT NULL,
    DOA_Pct         AS CAST(DOA_Count AS DECIMAL(10,3))/NULLIF(TotalBirds,0)*100 PERSISTED,
    -- 9-point welfare index
    Fractures_Count INT,
    Trapped_Count   INT,
    Supine_Count    INT,
    Haematomas_Count INT,
    SplayedLegs_Count INT,
    Crowding_Score  TINYINT,         -- 0-3
    ThermalStress_Score TINYINT,     -- 0-3
    RejectionsAtRamp_Count INT,
    -- Calculated
    WelfareIndex    AS (
        100 -
        ISNULL(Fractures_Count,0) * 1.5 -
        ISNULL(Trapped_Count,0) * 1.0 -
        ISNULL(Supine_Count,0) * 0.5 -
        ISNULL(Haematomas_Count,0) * 0.8 -
        ISNULL(SplayedLegs_Count,0) * 0.5 -
        ISNULL(Crowding_Score,0) * 5.0 -
        ISNULL(ThermalStress_Score,0) * 5.0 -
        ISNULL(RejectionsAtRamp_Count,0) * 2.0
    ) PERSISTED,
    InspectorId     INT NOT NULL,
    FotoUrls        NVARCHAR(MAX) NULL  -- JSON
);
CREATE INDEX IX_BS_Ramp_Partia ON BS_RampInspection(PartiaId);
CREATE INDEX IX_BS_Ramp_Kurs ON BS_RampInspection(KursId);


-- ============================================================
-- NF04 — Stunning CCP
-- ============================================================
USE LibraNet;
GO

CREATE TABLE BS_StunningSession (
    Id              INT IDENTITY PRIMARY KEY,
    LiniaId         INT NOT NULL,
    StartTs         DATETIME NOT NULL,
    EndTs           DATETIME NULL,
    Metoda          NVARCHAR(20) NOT NULL,    -- 'WATER_BATH','CAS_CO2','CAS_ARGON','CAS_N2'
    PartiaId        INT NULL,
    OperatorId      INT NOT NULL
);
CREATE INDEX IX_BS_StunningSession_Start ON BS_StunningSession(StartTs);

CREATE TABLE BS_StunningParam (
    Id              BIGINT IDENTITY PRIMARY KEY,
    SessionId       INT NOT NULL,
    Ts              DATETIME NOT NULL,
    Voltage_V       DECIMAL(6,1) NULL,
    Frequency_Hz    INT NULL,
    Current_mA      INT NULL,
    DurationSec     DECIMAL(4,1) NULL,
    CO2_Pct_Step1   DECIMAL(4,1) NULL,
    CO2_Pct_Step2   DECIMAL(4,1) NULL,
    GasTemp_C       DECIMAL(4,1) NULL,
    EUCompliantFlag BIT,
    AlertMsg        NVARCHAR(200) NULL
);
CREATE INDEX IX_BS_StunningParam_Session ON BS_StunningParam(SessionId, Ts);

CREATE TABLE BS_StunningQuality (
    Id              INT IDENTITY PRIMARY KEY,
    SessionId       INT NOT NULL,
    Ts              DATETIME NOT NULL,
    SampleSize      INT NOT NULL,
    PurpleBirds_Cnt INT NOT NULL DEFAULT 0,
    RedWingTips_Cnt INT NOT NULL DEFAULT 0,
    PoorBleeding_Cnt INT NOT NULL DEFAULT 0,
    HaematomasShoulder_Cnt INT NOT NULL DEFAULT 0,
    VLM_Confidence  DECIMAL(4,3),
    Fotos_Json      NVARCHAR(MAX) NULL
);
CREATE INDEX IX_BS_StunningQuality_Session ON BS_StunningQuality(SessionId, Ts);


-- ============================================================
-- NF05 — Scalding + Plucking
-- ============================================================
CREATE TABLE BS_ScaldingLog (
    Id              BIGINT IDENTITY PRIMARY KEY,
    SesjaId         INT NOT NULL,    -- FK BS_StunningSession
    Ts              DATETIME NOT NULL,
    TankNr          INT NOT NULL,
    TempC           DECIMAL(4,2) NOT NULL,
    SetpointC       DECIMAL(4,2) NOT NULL,
    DeviationC      AS (TempC - SetpointC) PERSISTED,
    AlertFlag       BIT NOT NULL DEFAULT 0
);
CREATE INDEX IX_BS_Scalding_Sesja ON BS_ScaldingLog(SesjaId, Ts);

CREATE TABLE BS_PluckerMaintenance (
    Id              INT IDENTITY PRIMARY KEY,
    DataObslugi     DATE NOT NULL,
    StationNr       INT NOT NULL,
    FingersReplaced INT NOT NULL,
    TotalFingers    INT NOT NULL,
    OperatorId      INT NOT NULL,
    PoorPluckingObs NVARCHAR(200) NULL,
    CreatedAt       DATETIME NOT NULL DEFAULT GETDATE()
);
CREATE INDEX IX_BS_PluckerMaintenance_Data ON BS_PluckerMaintenance(DataObslugi);

CREATE TABLE BS_PluckingQuality (
    Id              INT IDENTITY PRIMARY KEY,
    SesjaId         INT NOT NULL,
    Ts              DATETIME NOT NULL,
    SampleSize      INT NOT NULL,
    SkinRuptures_Small INT NOT NULL DEFAULT 0,
    SkinRuptures_Large INT NOT NULL DEFAULT 0,
    FeathersRemaining_Cnt INT NOT NULL DEFAULT 0,
    FaecalContamination_Cnt INT NOT NULL DEFAULT 0,
    VLM_Confidence  DECIMAL(4,3)
);


-- ============================================================
-- NF06 — PM Defects
-- ============================================================
CREATE TABLE BS_PM_DefectDict (
    Id              INT IDENTITY PRIMARY KEY,
    Kod             NVARCHAR(20) UNIQUE NOT NULL,
    NazwaPL         NVARCHAR(100) NOT NULL,
    NazwaEN         NVARCHAR(100) NOT NULL,
    DomyslnaAkcja   NVARCHAR(20),    -- 'COMPLETE_REJECT','PARTIAL_TRIM','DOWNGRADE'
    BRCv9Section    NVARCHAR(20),
    Opis            NVARCHAR(MAX),
    Ikona           NVARCHAR(50)
);

INSERT INTO BS_PM_DefectDict (Kod, NazwaPL, NazwaEN, DomyslnaAkcja, BRCv9Section) VALUES
('ASCITES',  'Ascites (wodobrzusze)',          'Ascites',                 'COMPLETE_REJECT', '5.6'),
('POLYSER',  'Polyserositis',                  'Polyserositis',           'COMPLETE_REJECT', '5.6'),
('HEPAT',    'Zapalenie watroby',              'Hepatitis',               'COMPLETE_REJECT', '5.6'),
('CELLUL',   'Cellulitis',                     'Cellulitis',              'COMPLETE_REJECT', '5.6'),
('CACHEX',   'Cachexia (wycienczenie)',        'Cachexia',                'COMPLETE_REJECT', '5.6'),
('FRACT',    'Zlamania kosci (otwarte)',       'Open fractures',          'PARTIAL_TRIM',    '5.4'),
('HAEM_EXT', 'Hematomy rozlegle',              'Extensive haematomas',    'PARTIAL_TRIM',    '5.4'),
('BCO_FEM',  'BCO - femoral head necrosis',    'BCO femur',               'PARTIAL_TRIM',    '5.6'),
('BCO_TIB',  'BCO - tibial head necrosis',     'BCO tibia',               'PARTIAL_TRIM',    '5.6'),
('KINKY',    'Kinky back (VO)',                'Kinky back',              'COMPLETE_REJECT', '5.6'),
('BBS',      'Black bone syndrome',            'BBS',                     'DOWNGRADE',       '5.4'),
('TD',       'Tibial dyschondroplasia',        'TD',                      'PARTIAL_TRIM',    '5.6'),
('WB',       'Wooden breast',                  'Wooden breast',           'DOWNGRADE',       '5.4'),
('WS',       'White striping',                 'White striping',          'DOWNGRADE',       '5.4'),
('SPAG',     'Spaghetti meat',                 'Spaghetti meat',          'DOWNGRADE',       '5.4'),
('GMD',      'Green muscle disease',           'GMD/DPM',                 'PARTIAL_TRIM',    '5.6'),
('DMP',      'Dorsal myopathy',                'DMP',                     'PARTIAL_TRIM',    '5.6'),
('FAECAL',   'Zanieczyszczenie kalem',         'Faecal contamination',    'COMPLETE_REJECT', '5.6'),
('BILE',     'Zanieczyszczenie zolcia',        'Bile contamination',      'PARTIAL_TRIM',    '5.6'),
('LIVER',    'Wady watroby pozostale',         'Liver abnormalities',     'PARTIAL_TRIM',    '5.6'),
('OTHER',    'Inne',                           'Other',                   'PARTIAL_TRIM',    NULL);

CREATE TABLE BS_PM_Defect (
    Id              BIGINT IDENTITY PRIMARY KEY,
    SesjaId         INT NOT NULL,
    PartiaId        INT NOT NULL,
    Ts              DATETIME NOT NULL,
    InspectorId     INT NOT NULL,
    PlatformNr      TINYINT,
    DefectKod       NVARCHAR(20) NOT NULL,
    Akcja           NVARCHAR(20),
    ShackleCount    INT NULL,
    Foto_BlobId     UNIQUEIDENTIFIER NULL,
    Uwagi           NVARCHAR(200) NULL,
    FOREIGN KEY (DefectKod) REFERENCES BS_PM_DefectDict(Kod)
);
CREATE INDEX IX_BS_PM_Partia_Defect ON BS_PM_Defect(PartiaId, DefectKod);
CREATE INDEX IX_BS_PM_Inspector_Ts ON BS_PM_Defect(InspectorId, Ts);

CREATE TABLE BS_PM_DailySummary (
    Id              INT IDENTITY PRIMARY KEY,
    Data            DATE NOT NULL,
    PartiaId        INT NOT NULL,
    HodowcaId       INT NOT NULL,
    TotalBirds      INT NOT NULL,
    Rejected_Complete INT NOT NULL DEFAULT 0,
    Rejected_Partial INT NOT NULL DEFAULT 0,
    Rejection_Pct   AS (Rejected_Complete + Rejected_Partial * 0.3) * 100.0/NULLIF(TotalBirds,0) PERSISTED,
    Top1_Defect     NVARCHAR(20),
    Top1_Count      INT,
    Top2_Defect     NVARCHAR(20),
    Top2_Count      INT,
    Top3_Defect     NVARCHAR(20),
    Top3_Count      INT,
    Polyser_Cnt     INT NOT NULL DEFAULT 0,
    Ascites_Cnt     INT NOT NULL DEFAULT 0,
    Hepat_Cnt       INT NOT NULL DEFAULT 0,
    Cellul_Cnt      INT NOT NULL DEFAULT 0,
    WB_Cnt          INT NOT NULL DEFAULT 0,
    WS_Cnt          INT NOT NULL DEFAULT 0,
    BCO_Cnt         INT NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX UX_BS_PMSum_Partia ON BS_PM_DailySummary(PartiaId);
CREATE INDEX IX_BS_PMSum_HodowcaData ON BS_PM_DailySummary(HodowcaId, Data DESC);


-- ============================================================
-- NF07 — Chilling Curve
-- ============================================================
CREATE TABLE BS_ChillSession (
    Id              INT IDENTITY PRIMARY KEY,
    LiniaId         INT NOT NULL,
    StartTs         DATETIME NOT NULL,
    EndTs           DATETIME NULL,
    Metoda          NVARCHAR(20),    -- 'AIR','SPIN','SPRAY','COMBO'
    PartiaId        INT NULL
);

CREATE TABLE BS_ChillTempLog (
    Id              BIGINT IDENTITY PRIMARY KEY,
    SessionId       INT NOT NULL,
    Ts              DATETIME NOT NULL,
    AmbientTempC    DECIMAL(4,1),
    CoreTempC       DECIMAL(4,1) NULL,
    AirFlowMs       DECIMAL(4,2) NULL,
    Humidity        DECIMAL(4,1) NULL,
    Position        NVARCHAR(20)     -- 'IN','MID','OUT','PROBE_BREAST'
);
CREATE INDEX IX_BS_ChillTemp_Session ON BS_ChillTempLog(SessionId, Ts);

CREATE TABLE BS_ChillCompliance (
    Id              INT IDENTITY PRIMARY KEY,
    SessionId       INT NOT NULL,
    PartiaId        INT NULL,
    StartCoreTempC  DECIMAL(4,1),
    Time_to_4C_Min  INT,
    EUCompliant     AS CAST(CASE WHEN Time_to_4C_Min <= 360 THEN 1 ELSE 0 END AS BIT) PERSISTED,
    AvgCurveScore   DECIMAL(4,2),
    Notes           NVARCHAR(500)
);

CREATE TABLE BS_DripLoss (
    Id              INT IDENTITY PRIMARY KEY,
    PartiaId        INT NOT NULL,
    DataPomiaru     DATE NOT NULL,
    SampleType      NVARCHAR(50),    -- 'WHOLE_CARCASS','BREAST_FILLET','LEG_FILLET'
    PackagingType   NVARCHAR(50) NULL, -- 'AIR_PERMEABLE','VACUUM','MAP_CO2','MAP_N2'
    SampleWeight_g  DECIMAL(8,2),
    DripWeight_g    DECIMAL(8,2),
    DripPct         AS (DripWeight_g / NULLIF(SampleWeight_g,0) * 100) PERSISTED,
    AmbientTempC    DECIMAL(4,1)
);
CREATE INDEX IX_BS_DripLoss_Partia ON BS_DripLoss(PartiaId);


-- ============================================================
-- NF08 — Vision Grading A/B/C
-- ============================================================
CREATE TABLE BS_GradingScan (
    Id              BIGINT IDENTITY PRIMARY KEY,
    Ts              DATETIME NOT NULL,
    PartiaId        INT NOT NULL,
    ShackleCount    INT,
    Weight_g        INT,
    Klasa           CHAR(1) NOT NULL CHECK (Klasa IN ('A','B','C')),
    Defekty_Json    NVARCHAR(500) NULL,
    VLM_Confidence  DECIMAL(4,3),
    Foto_BlobId     UNIQUEIDENTIFIER NULL,
    KosztTrim_g     INT NULL
);
CREATE INDEX IX_BS_Grading_PartiaKlasa ON BS_GradingScan(PartiaId, Klasa);


-- ============================================================
-- NF09 — Packaging + MAP + Traceability
-- ============================================================
CREATE TABLE BS_PackagingBatch (
    Id              INT IDENTITY PRIMARY KEY,
    PartiaId        INT NOT NULL,
    DataPakowania   DATE NOT NULL,
    TypOpakowania   NVARCHAR(50),    -- 'MAP_CO2_70','MAP_N2_30','VACUUM','OVERWRAP'
    O2_Pct          DECIMAL(4,1) NULL,
    CO2_Pct         DECIMAL(4,1) NULL,
    N2_Pct          DECIMAL(4,1) NULL,
    ShelfLifeDays   INT NOT NULL,
    ExpiryDate      AS DATEADD(DAY, ShelfLifeDays, DataPakowania) PERSISTED,
    AntiTamperUid   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    KlientId        INT NULL,
    ArtykulId       INT NULL,        -- FK do KartotekaTowarow.Article
    LiczbaPaczek    INT
);
CREATE INDEX IX_BS_PackBatch_Partia ON BS_PackagingBatch(PartiaId);
CREATE INDEX IX_BS_PackBatch_Klient ON BS_PackagingBatch(KlientId, DataPakowania);

CREATE TABLE BS_TraceabilityScan (
    Id              BIGINT IDENTITY PRIMARY KEY,
    Ts              DATETIME NOT NULL,
    QrCode          NVARCHAR(100),
    ScanType        NVARCHAR(30),    -- 'PACK','SHIP','CUSTOMER','COMPLAINT'
    KlientId        INT NULL,
    PalletId        INT NULL,
    PackagingBatchId INT NULL,
    UserId          INT NOT NULL,
    Lokalizacja     NVARCHAR(100) NULL
);
CREATE INDEX IX_BS_TraceScan_QR ON BS_TraceabilityScan(QrCode);

GO

CREATE VIEW BS_TraceabilityFull AS
SELECT
    p.Lp AS PartiaId,
    p.DataPrzyjecia,
    pd.Hodowca,
    -- antybiotyki na hodowcy ostatnich 30 dni
    (SELECT MAX(ft.DataMozliwegoUboju) FROM BS_FarmTreatment ft
     WHERE ft.HodowcaId IN (SELECT Id FROM Pozyskiwanie_Hodowcy WHERE Nazwa = pd.Hodowca)
       AND ft.DataKonca > DATEADD(DAY, -30, p.DataPrzyjecia)) AS LastAntybioBlock,
    -- PM defekty
    pmd.Rejection_Pct,
    pmd.Top1_Defect, pmd.Top1_Count,
    -- chilling
    cc.Time_to_4C_Min,
    cc.EUCompliant AS ChillEUCompliant,
    -- packaging
    pb.TypOpakowania,
    pb.ExpiryDate,
    pb.AntiTamperUid,
    pb.KlientId AS LastKlientId
FROM listapartii p
LEFT JOIN PartiaDostawca pd ON pd.Partia = p.Lp
LEFT JOIN BS_PM_DailySummary pmd ON pmd.PartiaId = p.Lp
LEFT JOIN BS_ChillCompliance cc ON cc.PartiaId = p.Lp
LEFT JOIN BS_PackagingBatch pb ON pb.PartiaId = p.Lp;

GO


-- ============================================================
-- NF10 — Pathogen Sampling + Logistic Slaughter
-- ============================================================
CREATE TABLE BS_PathogenSample (
    Id              INT IDENTITY PRIMARY KEY,
    HodowcaId       INT NOT NULL,
    PartiaId        INT NULL,
    DataPobrania    DATE NOT NULL,
    TypProbki       NVARCHAR(30),    -- 'OVERSHOE','CECUM','NECK_SKIN','BOOT'
    Lokalizacja     NVARCHAR(50),
    Lab             NVARCHAR(100),
    DataWyniku      DATE NULL,
    Patogen         NVARCHAR(30),    -- 'SE','ST','S_HADAR','S_INFANTIS','CAMPY_JEJUNI','CAMPY_COLI'
    Wynik           NVARCHAR(20),    -- 'POSITIVE','NEGATIVE','PENDING'
    CFU_per_g       DECIMAL(10,2) NULL,
    DokumentBlobId  UNIQUEIDENTIFIER NULL
);
CREATE INDEX IX_BS_Path_Hodowca ON BS_PathogenSample(HodowcaId, DataWyniku DESC);
CREATE INDEX IX_BS_Path_Partia ON BS_PathogenSample(PartiaId);

CREATE TABLE BS_LogisticSlaughter (
    PartiaId        INT PRIMARY KEY,
    Powod           NVARCHAR(50),
    SlotDnia        TINYINT,         -- 1-99 (99 = ostatni)
    HeatTreatRequired BIT,
    DataDecyzji     DATETIME NOT NULL DEFAULT GETDATE(),
    DecydujacyId    INT NOT NULL
);


-- ============================================================
-- NF11 — Foreign Material + Tool Tracking
-- ============================================================
CREATE TABLE BS_ForeignMatAlarm (
    Id              INT IDENTITY PRIMARY KEY,
    Ts              DATETIME NOT NULL,
    Linia           NVARCHAR(50),
    Typ             NVARCHAR(20),    -- 'METAL','XRAY_BONE','XRAY_PLASTIC','XRAY_GLASS'
    Material        NVARCHAR(50),
    Foto_BlobId     UNIQUEIDENTIFIER NULL,
    PartiaId        INT NULL,
    AkcjaPodjeta    NVARCHAR(200),
    Status          NVARCHAR(20) NOT NULL DEFAULT 'OPEN',    -- 'OPEN','INVESTIGATING','CLOSED'
    OperatorId      INT NOT NULL,
    ZamknieteTs     DATETIME NULL
);

CREATE TABLE BS_MaintenanceTool (
    Id              INT IDENTITY PRIMARY KEY,
    ToolId          NVARCHAR(50) NOT NULL,
    Nazwa           NVARCHAR(100),
    TechnikId       INT NOT NULL,
    DataWydania     DATETIME NOT NULL,
    DataZwrotu      DATETIME NULL,
    Lokalizacja     NVARCHAR(100),
    KomentaryZwrotu NVARCHAR(200)
);


-- ============================================================
-- NF12 — Compliance Requirements + Status
-- ============================================================
CREATE TABLE BS_ComplianceRequirement (
    Id              INT IDENTITY PRIMARY KEY,
    Standard        NVARCHAR(20),    -- 'BRC_v9','IFS_v8','IRZplus','KSeF'
    Section         NVARCHAR(30),
    Title           NVARCHAR(500),
    Priority        NVARCHAR(20),    -- 'FUNDAMENTAL','STATEMENT_INTENT','GENERAL'
    Description     NVARCHAR(MAX),
    EvidenceSource  NVARCHAR(200)    -- nazwa tabeli BS_* lub view
);

CREATE TABLE BS_ComplianceStatus (
    Id              INT IDENTITY PRIMARY KEY,
    RequirementId   INT NOT NULL,
    Status          NVARCHAR(30),    -- 'CONFORMING','MINOR_NC','MAJOR_NC','NOT_APPLIC','PENDING'
    LastChecked     DATETIME NOT NULL DEFAULT GETDATE(),
    CheckedBy       INT NOT NULL,
    EvidenceUrls    NVARCHAR(MAX) NULL,
    GapNotes        NVARCHAR(MAX) NULL,
    PlanowanaData   DATE NULL,
    OdpowiedzialnyId INT NULL,
    FOREIGN KEY (RequirementId) REFERENCES BS_ComplianceRequirement(Id)
);
CREATE INDEX IX_BS_Compliance_Req ON BS_ComplianceStatus(RequirementId, LastChecked DESC);

-- Seed: kluczowe wymagania BRC v9 sekcji 4 (CCP electronic monitoring)
INSERT INTO BS_ComplianceRequirement (Standard, Section, Title, Priority, EvidenceSource) VALUES
('BRC_v9','3.9',  'Traceability (4h recall)',                         'FUNDAMENTAL', 'BS_TraceabilityFull'),
('BRC_v9','4.2',  'Site security & food defence',                     'GENERAL',     'CentrumNagranAI'),
('BRC_v9','4.6',  'Equipment maintenance',                            'GENERAL',     'BS_PluckerMaintenance'),
('BRC_v9','4.7',  'Maintenance tool tracking',                        'GENERAL',     'BS_MaintenanceTool'),
('BRC_v9','4.9',  'Foreign body detection',                           'FUNDAMENTAL', 'BS_ForeignMatAlarm'),
('BRC_v9','4.10', 'CCP electronic monitoring',                        'FUNDAMENTAL', 'BS_StunningParam,BS_ChillTempLog'),
('BRC_v9','4.11', 'Temperature control (transport+chill)',            'FUNDAMENTAL', 'BS_TransportClimat,BS_ChillTempLog'),
('BRC_v9','5.1',  'Product design',                                   'GENERAL',     'BS_FlockScoring'),
('BRC_v9','5.4',  'Product release (withdrawal compliance)',          'FUNDAMENTAL', 'BS_FarmTreatment,BS_GradingScan'),
('BRC_v9','5.6',  'Pathogen control (Salmonella)',                    'FUNDAMENTAL', 'BS_PathogenSample,BS_LogisticSlaughter'),
('BRC_v9','6.1',  'Control of operations',                            'STATEMENT_INTENT', NULL),
('BRC_v9','6.3',  'Calibration log',                                  'GENERAL',     NULL),
('BRC_v9','3.11', 'Complaint handling',                               'STATEMENT_INTENT', 'Reklamacje');


-- ============================================================
-- HELPER VIEWS — najczęstsze raporty
-- ============================================================

-- Top hodowcy ostatnich 90 dni wg PM rejection
GO
CREATE VIEW BS_Top_PoorHodowcy AS
SELECT TOP 20
    HodowcaId,
    COUNT(DISTINCT PartiaId) AS Partii,
    SUM(TotalBirds) AS Birds,
    SUM(Rejected_Complete) AS RejectedComplete,
    SUM(Rejected_Partial) AS RejectedPartial,
    AVG(Rejection_Pct) AS AvgRejectionPct,
    SUM(Polyser_Cnt) AS PolyserSum,
    SUM(Ascites_Cnt) AS AscitesSum
FROM BS_PM_DailySummary
WHERE Data >= DATEADD(DAY, -90, GETDATE())
GROUP BY HodowcaId
ORDER BY AvgRejectionPct DESC;
GO

-- BRC v9 compliance dashboard
CREATE VIEW BS_BRC_Dashboard AS
SELECT
    r.Standard, r.Section, r.Title, r.Priority,
    s.Status,
    s.LastChecked,
    DATEDIFF(DAY, s.LastChecked, GETDATE()) AS DaysSinceLastCheck,
    s.GapNotes,
    r.EvidenceSource
FROM BS_ComplianceRequirement r
LEFT JOIN BS_ComplianceStatus s ON s.RequirementId = r.Id
    AND s.LastChecked = (SELECT MAX(LastChecked) FROM BS_ComplianceStatus WHERE RequirementId = r.Id);
GO
