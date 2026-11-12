-- 0007 — conteúdo institucional do site, banners da home e imagens enviadas pelo painel.
-- Tudo no banco do portal. O banco do jogo não é alterado.

-- Textos do site editados pela equipe. Cada chave guarda um JSON validado pela API:
--   'server'    nome, slogan, texto "O Servidor", taxas e links da comunidade;
--   'downloads' texto de abertura, requisitos e a lista de arquivos do jogo.
CREATE TABLE dbo.SiteSetting (
    [Key]        varchar(32)    NOT NULL CONSTRAINT PK_SiteSetting PRIMARY KEY,
    Value        nvarchar(max)  NOT NULL,
    UpdatedUtc   datetime2(0)   NOT NULL CONSTRAINT DF_SiteSetting_Updated DEFAULT (SYSUTCDATETIME()),
    UpdatedBy    varchar(32)    NULL,
    CONSTRAINT CK_SiteSetting_Key CHECK ([Key] IN ('server','downloads'))
);

-- Banners e destaques da página inicial.
CREATE TABLE dbo.SiteBanner (
    Id           int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SiteBanner PRIMARY KEY,
    Title        nvarchar(120)  NOT NULL,
    Subtitle     nvarchar(300)  NULL,
    ImageUrl     nvarchar(400)  NULL,
    LinkUrl      nvarchar(400)  NULL,
    LinkLabel    nvarchar(40)   NULL,
    SortOrder    int            NOT NULL CONSTRAINT DF_SiteBanner_Sort DEFAULT (0),
    Active       bit            NOT NULL CONSTRAINT DF_SiteBanner_Active DEFAULT (1),
    StartsUtc    datetime2(0)   NULL,
    EndsUtc      datetime2(0)   NULL,
    CreatedUtc   datetime2(0)   NOT NULL CONSTRAINT DF_SiteBanner_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedUtc   datetime2(0)   NOT NULL CONSTRAINT DF_SiteBanner_Updated DEFAULT (SYSUTCDATETIME()),
    CreatedBy    varchar(32)    NULL,
    UpdatedBy    varchar(32)    NULL,
    CONSTRAINT CK_SiteBanner_Period CHECK (EndsUtc IS NULL OR StartsUtc IS NULL OR EndsUtc > StartsUtc)
);
CREATE INDEX IX_SiteBanner_Active ON dbo.SiteBanner (Active, SortOrder);

-- Imagens enviadas pela equipe (notícias, eventos, banners). Já chegam sem metadados.
CREATE TABLE dbo.MediaFile (
    Id           uniqueidentifier NOT NULL CONSTRAINT PK_MediaFile PRIMARY KEY,
    FileName     nvarchar(100)  NOT NULL,
    ContentType  varchar(32)    NOT NULL,
    SizeBytes    int            NOT NULL,
    Data         varbinary(max) NOT NULL,
    CreatedUtc   datetime2(0)   NOT NULL CONSTRAINT DF_MediaFile_Created DEFAULT (SYSUTCDATETIME()),
    CreatedBy    varchar(32)    NULL,
    CONSTRAINT CK_MediaFile_Type CHECK (ContentType IN ('image/png','image/jpeg')),
    CONSTRAINT CK_MediaFile_Size CHECK (SizeBytes > 0 AND SizeBytes <= 3145728)
);
