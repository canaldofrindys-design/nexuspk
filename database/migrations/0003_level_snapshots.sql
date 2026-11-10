-- 0003 — fotos diárias de nível (base dos rankings diário, semanal e mensal).
-- O jogo não guarda histórico de nível nem de EXP. O histórico do portal começa na primeira foto.

CREATE TABLE dbo.LevelSnapshotRun (
    SnapshotDate  date          NOT NULL CONSTRAINT PK_LevelSnapshotRun PRIMARY KEY, -- data local (Brasília)
    TakenUtc      datetime2(0)  NOT NULL,
    CharacterCount int          NOT NULL
);

CREATE TABLE dbo.LevelSnapshot (
    SnapshotDate   date         NOT NULL,
    CharacterName  varchar(32)  NOT NULL,
    ClassCode      int          NOT NULL,
    Level          int          NOT NULL,
    CONSTRAINT PK_LevelSnapshot PRIMARY KEY (SnapshotDate, CharacterName),
    CONSTRAINT FK_LevelSnapshot_Run FOREIGN KEY (SnapshotDate) REFERENCES dbo.LevelSnapshotRun (SnapshotDate) ON DELETE CASCADE
);
