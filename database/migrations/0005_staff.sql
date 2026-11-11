-- 0005 — equipe do portal (painel administrativo), separada das contas de jogador.
-- A senha é guardada como PBKDF2-SHA256 com sal; nunca em texto.
-- O primeiro superadministrador é criado pelo DbMigrator (--criar-superadmin), com a senha digitada no próprio servidor.

CREATE TABLE dbo.StaffUser (
    Id                 int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StaffUser PRIMARY KEY,
    Username           varchar(32)    NOT NULL,
    DisplayName        nvarchar(64)   NOT NULL,
    PasswordHash       varchar(200)   NOT NULL,
    Role               varchar(16)    NOT NULL,
    Active             bit            NOT NULL CONSTRAINT DF_StaffUser_Active DEFAULT (1),
    MustChangePassword bit            NOT NULL CONSTRAINT DF_StaffUser_MustChange DEFAULT (1),
    FailedLogins       int            NOT NULL CONSTRAINT DF_StaffUser_Failed DEFAULT (0),
    LockedUntilUtc     datetime2(0)   NULL,
    CreatedUtc         datetime2(0)   NOT NULL CONSTRAINT DF_StaffUser_Created DEFAULT (SYSUTCDATETIME()),
    CreatedBy          varchar(32)    NULL,
    LastLoginUtc       datetime2(0)   NULL,
    PasswordChangedUtc datetime2(0)   NULL,
    CONSTRAINT CK_StaffUser_Role CHECK (Role IN ('superadmin','admin','moderador','suporte'))
);
CREATE UNIQUE INDEX UX_StaffUser_Username ON dbo.StaffUser (Username);

CREATE TABLE dbo.StaffSession (
    Id           bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_StaffSession PRIMARY KEY,
    TokenHash    binary(32)     NOT NULL,
    StaffId      int            NOT NULL CONSTRAINT FK_StaffSession_Staff REFERENCES dbo.StaffUser (Id),
    CreatedUtc   datetime2(0)   NOT NULL CONSTRAINT DF_StaffSession_Created DEFAULT (SYSUTCDATETIME()),
    ExpiresUtc   datetime2(0)   NOT NULL,
    RevokedUtc   datetime2(0)   NULL,
    IpAddress    varchar(45)    NULL,
    UserAgent    nvarchar(256)  NULL
);
CREATE UNIQUE INDEX UX_StaffSession_Token ON dbo.StaffSession (TokenHash);
CREATE INDEX IX_StaffSession_Staff ON dbo.StaffSession (StaffId, ExpiresUtc);

-- Quem criou e quem alterou por último cada conteúdo.
ALTER TABLE dbo.News ADD CreatedBy varchar(32) NULL, UpdatedBy varchar(32) NULL;
ALTER TABLE dbo.GameEvent ADD CreatedBy varchar(32) NULL, UpdatedBy varchar(32) NULL;
