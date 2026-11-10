-- 0001 — estrutura inicial do banco do portal (PortalDB).
-- Aplica somente no banco do portal. Nunca no banco do jogo.

CREATE TABLE dbo.AppSetting (
    [Key]        varchar(100)   NOT NULL CONSTRAINT PK_AppSetting PRIMARY KEY,
    [Value]      nvarchar(max)  NULL,
    UpdatedAtUtc datetime2(0)   NOT NULL CONSTRAINT DF_AppSetting_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedBy    nvarchar(64)   NULL
);

-- Auditoria de operações sensíveis. Nunca grava senha, token ou cookie.
CREATE TABLE dbo.AuditLog (
    Id           bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
    OccurredUtc  datetime2(3)   NOT NULL CONSTRAINT DF_AuditLog_Occurred DEFAULT (SYSUTCDATETIME()),
    ActorType    varchar(16)    NOT NULL, -- staff | player | system
    ActorId      nvarchar(64)   NULL,
    Action       varchar(100)   NOT NULL,
    TargetType   varchar(50)    NULL,
    TargetId     nvarchar(100)  NULL,
    IpAddress    varchar(45)    NULL,
    BeforeJson   nvarchar(max)  NULL,
    AfterJson    nvarchar(max)  NULL,
    CONSTRAINT CK_AuditLog_ActorType CHECK (ActorType IN ('staff','player','system'))
);
CREATE INDEX IX_AuditLog_Occurred ON dbo.AuditLog (OccurredUtc DESC);
CREATE INDEX IX_AuditLog_Actor ON dbo.AuditLog (ActorType, ActorId, OccurredUtc DESC);

INSERT INTO dbo.AppSetting ([Key], [Value], UpdatedBy)
VALUES ('portal.timezone', N'America/Sao_Paulo', N'migration');
