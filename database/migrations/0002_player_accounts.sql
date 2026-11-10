-- 0002 — contas web dos jogadores, sessões e tokens de uso único (PortalDB).
-- A conta do jogo continua no accountdb. Aqui fica só o que o jogo não guarda:
-- e-mail, confirmação, sessões do site e tokens de recuperação.
-- Tokens e sessões são guardados como SHA-256; o valor original nunca vai ao banco.

CREATE TABLE dbo.PlayerAccount (
    Id                 int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PlayerAccount PRIMARY KEY,
    GameLogin          varchar(32)    NOT NULL,
    Email              nvarchar(254)  NULL,
    EmailNormalized    nvarchar(254)  NULL,
    EmailConfirmedUtc  datetime2(0)   NULL,
    CreatedUtc         datetime2(0)   NOT NULL CONSTRAINT DF_PlayerAccount_Created DEFAULT (SYSUTCDATETIME()),
    CreatedIp          varchar(45)    NULL,
    LastLoginUtc       datetime2(0)   NULL
);
-- Login do jogo é case-insensitive (collation CI do accountdb); o índice segue a mesma regra.
CREATE UNIQUE INDEX UX_PlayerAccount_GameLogin ON dbo.PlayerAccount (GameLogin);
CREATE INDEX IX_PlayerAccount_Email ON dbo.PlayerAccount (EmailNormalized) WHERE EmailNormalized IS NOT NULL;

CREATE TABLE dbo.PlayerSession (
    Id           bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_PlayerSession PRIMARY KEY,
    TokenHash    binary(32)     NOT NULL,
    GameLogin    varchar(32)    NOT NULL,
    CreatedUtc   datetime2(0)   NOT NULL CONSTRAINT DF_PlayerSession_Created DEFAULT (SYSUTCDATETIME()),
    ExpiresUtc   datetime2(0)   NOT NULL,
    RevokedUtc   datetime2(0)   NULL,
    IpAddress    varchar(45)    NULL,
    UserAgent    nvarchar(256)  NULL
);
CREATE UNIQUE INDEX UX_PlayerSession_Token ON dbo.PlayerSession (TokenHash);
CREATE INDEX IX_PlayerSession_Login ON dbo.PlayerSession (GameLogin, ExpiresUtc);

-- Purpose: 'email-confirm' | 'password-reset'
CREATE TABLE dbo.OneTimeToken (
    Id           bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_OneTimeToken PRIMARY KEY,
    Purpose      varchar(32)    NOT NULL,
    TokenHash    binary(32)     NOT NULL,
    GameLogin    varchar(32)    NOT NULL,
    CreatedUtc   datetime2(0)   NOT NULL CONSTRAINT DF_OneTimeToken_Created DEFAULT (SYSUTCDATETIME()),
    ExpiresUtc   datetime2(0)   NOT NULL,
    UsedUtc      datetime2(0)   NULL,
    CONSTRAINT CK_OneTimeToken_Purpose CHECK (Purpose IN ('email-confirm','password-reset'))
);
CREATE UNIQUE INDEX UX_OneTimeToken_Token ON dbo.OneTimeToken (TokenHash);
CREATE INDEX IX_OneTimeToken_Login ON dbo.OneTimeToken (GameLogin, Purpose, CreatedUtc DESC);
