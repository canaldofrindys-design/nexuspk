-- 0004 — notícias, eventos e lembretes de evento (PortalDB).
-- O conteúdo é cadastrado pela equipe no painel administrativo (Fase 10).
-- Um evento cadastrado aqui é só divulgação: nada é executado dentro do jogo.

CREATE TABLE dbo.News (
    Id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_News PRIMARY KEY,
    Slug          varchar(120)   NOT NULL,
    Category      varchar(16)    NOT NULL,
    Title         nvarchar(160)  NOT NULL,
    Summary       nvarchar(400)  NOT NULL,
    Body          nvarchar(max)  NOT NULL,
    ImageUrl      nvarchar(400)  NULL,
    AuthorName    nvarchar(64)   NULL,
    Status        varchar(16)    NOT NULL CONSTRAINT DF_News_Status DEFAULT ('draft'),
    Pinned        bit            NOT NULL CONSTRAINT DF_News_Pinned DEFAULT (0),
    PublishedUtc  datetime2(0)   NULL,
    CreatedUtc    datetime2(0)   NOT NULL CONSTRAINT DF_News_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedUtc    datetime2(0)   NOT NULL CONSTRAINT DF_News_Updated DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_News_Category CHECK (Category IN ('noticia','atualizacao','manutencao','evento')),
    CONSTRAINT CK_News_Status CHECK (Status IN ('draft','published','archived')),
    CONSTRAINT CK_News_Published CHECK (Status <> 'published' OR PublishedUtc IS NOT NULL)
);
CREATE UNIQUE INDEX UX_News_Slug ON dbo.News (Slug);
CREATE INDEX IX_News_Published ON dbo.News (Status, PublishedUtc DESC) INCLUDE (Category, Pinned);

CREATE TABLE dbo.GameEvent (
    Id             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_GameEvent PRIMARY KEY,
    Slug           varchar(120)   NOT NULL,
    Category       varchar(16)    NOT NULL,
    Name           nvarchar(120)  NOT NULL,
    Summary        nvarchar(400)  NOT NULL,
    Description    nvarchar(max)  NOT NULL,
    Rules          nvarchar(max)  NULL,
    Prizes         nvarchar(max)  NULL,
    Location       nvarchar(120)  NULL,
    ImageUrl       nvarchar(400)  NULL,
    StartsUtc      datetime2(0)   NOT NULL,
    EndsUtc        datetime2(0)   NULL,
    Status         varchar(16)    NOT NULL CONSTRAINT DF_GameEvent_Status DEFAULT ('draft'),
    -- Como o evento acontece no jogo. 'manual' = conduzido pela equipe; nenhum outro modo existe hoje.
    ExecutionMode  varchar(16)    NOT NULL CONSTRAINT DF_GameEvent_Execution DEFAULT ('manual'),
    CreatedUtc     datetime2(0)   NOT NULL CONSTRAINT DF_GameEvent_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedUtc     datetime2(0)   NOT NULL CONSTRAINT DF_GameEvent_Updated DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_GameEvent_Category CHECK (Category IN ('exp','drop','boss','pvp','invasao','especial','comemorativo','gm')),
    CONSTRAINT CK_GameEvent_Status CHECK (Status IN ('draft','published','cancelled')),
    CONSTRAINT CK_GameEvent_Execution CHECK (ExecutionMode IN ('manual')),
    CONSTRAINT CK_GameEvent_Period CHECK (EndsUtc IS NULL OR EndsUtc > StartsUtc)
);
CREATE UNIQUE INDEX UX_GameEvent_Slug ON dbo.GameEvent (Slug);
CREATE INDEX IX_GameEvent_Starts ON dbo.GameEvent (Status, StartsUtc) INCLUDE (EndsUtc, Category);

-- Jogador logado pede aviso por e-mail antes do início.
CREATE TABLE dbo.GameEventReminder (
    EventId      int            NOT NULL CONSTRAINT FK_GameEventReminder_Event REFERENCES dbo.GameEvent (Id) ON DELETE CASCADE,
    GameLogin    varchar(32)    NOT NULL,
    CreatedUtc   datetime2(0)   NOT NULL CONSTRAINT DF_GameEventReminder_Created DEFAULT (SYSUTCDATETIME()),
    SentUtc      datetime2(0)   NULL,
    CONSTRAINT PK_GameEventReminder PRIMARY KEY (EventId, GameLogin)
);
CREATE INDEX IX_GameEventReminder_Pending ON dbo.GameEventReminder (SentUtc, EventId);
