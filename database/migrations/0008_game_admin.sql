-- 0008 — painel do jogo: histórico de banimentos, ligação das funções às tabelas do jogo e cópias dos rankings zerados.
-- Nada aqui altera o banco do jogo.

CREATE TABLE dbo.GameBan (
    Id          bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_GameBan PRIMARY KEY,
    GameLogin   varchar(32)    NOT NULL,
    Reason      nvarchar(300)  NOT NULL,
    BannedBy    varchar(32)    NOT NULL,
    BannedUtc   datetime2(0)   NOT NULL,
    ExpiresUtc  datetime2(0)   NULL,
    LiftedUtc   datetime2(0)   NULL,
    LiftedBy    varchar(32)    NULL,
    LiftReason  nvarchar(300)  NULL
);
CREATE INDEX IX_GameBan_Login ON dbo.GameBan (GameLogin, BannedUtc DESC);
CREATE INDEX IX_GameBan_Expiring ON dbo.GameBan (ExpiresUtc) WHERE LiftedUtc IS NULL AND ExpiresUtc IS NOT NULL;

-- Função do painel (box-send, skin-shop...) -> tabela real do jogo. Colunas em JSON (lista de nomes).
CREATE TABLE dbo.GameSourceBinding (
    FunctionKey     varchar(32)    NOT NULL CONSTRAINT PK_GameSourceBinding PRIMARY KEY,
    DatabaseName    nvarchar(128)  NOT NULL,
    TableName       nvarchar(128)  NOT NULL,
    KeyColumns      nvarchar(2000) NOT NULL,
    EditableColumns nvarchar(4000) NOT NULL,
    SearchColumns   nvarchar(2000) NOT NULL,
    AccountColumn   nvarchar(128)  NULL,
    AllowInsert     bit            NOT NULL,
    AllowDelete     bit            NOT NULL,
    NewestFirst     bit            NOT NULL,
    UpdatedBy       varchar(32)    NOT NULL,
    UpdatedUtc      datetime2(0)   NOT NULL
);

-- Valores de antes de cada reset de ranking (JSON), para desfazer.
CREATE TABLE dbo.RankingResetBackup (
    Id          bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RankingResetBackup PRIMARY KEY,
    Kind        varchar(32)    NOT NULL,
    CreatedBy   varchar(32)    NOT NULL,
    CreatedUtc  datetime2(0)   NOT NULL,
    RowsCount   int            NOT NULL,
    Data        nvarchar(max)  NOT NULL,
    RestoredUtc datetime2(0)   NULL,
    RestoredBy  varchar(32)    NULL
);
CREATE INDEX IX_RankingResetBackup_Created ON dbo.RankingResetBackup (CreatedUtc DESC);

-- Atividade de uma conta no painel (ações da equipe e do jogador sobre ela).
CREATE INDEX IX_AuditLog_Target ON dbo.AuditLog (TargetType, TargetId, OccurredUtc DESC);
