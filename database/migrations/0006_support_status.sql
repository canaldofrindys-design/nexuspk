-- 0006 — central de suporte (chamados) e monitoramento do servidor.
-- Tudo aqui fica no banco do portal. O banco do jogo não é alterado.

-- Chamados abertos pelos jogadores. O dono é o login do jogo (mesma chave de dbo.PlayerAccount).
CREATE TABLE dbo.SupportTicket (
    Id               int IDENTITY(1000,1) NOT NULL CONSTRAINT PK_SupportTicket PRIMARY KEY,
    GameLogin        varchar(32)    NOT NULL,
    Category         varchar(16)    NOT NULL,
    Subject          nvarchar(120)  NOT NULL,
    Status           varchar(16)    NOT NULL CONSTRAINT DF_SupportTicket_Status DEFAULT ('aberto'),
    Priority         varchar(8)     NOT NULL CONSTRAINT DF_SupportTicket_Priority DEFAULT ('normal'),
    AssignedStaffId  int            NULL CONSTRAINT FK_SupportTicket_Staff REFERENCES dbo.StaffUser (Id),
    CreatedUtc       datetime2(0)   NOT NULL CONSTRAINT DF_SupportTicket_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedUtc       datetime2(0)   NOT NULL CONSTRAINT DF_SupportTicket_Updated DEFAULT (SYSUTCDATETIME()),
    -- Quem escreveu por último (jogador ou equipe), para a fila mostrar quem está esperando.
    LastAuthor       varchar(8)     NOT NULL CONSTRAINT DF_SupportTicket_LastAuthor DEFAULT ('jogador'),
    -- 1 quando a equipe respondeu e o jogador ainda não abriu o chamado.
    PlayerUnread     bit            NOT NULL CONSTRAINT DF_SupportTicket_Unread DEFAULT (0),
    ClosedUtc        datetime2(0)   NULL,
    ClosedBy         varchar(40)    NULL,
    CONSTRAINT CK_SupportTicket_Category CHECK (Category IN ('conta','personagem','bug','denuncia','evento','loja','outro')),
    CONSTRAINT CK_SupportTicket_Status CHECK (Status IN ('aberto','em_atendimento','aguardando','resolvido','fechado')),
    CONSTRAINT CK_SupportTicket_Priority CHECK (Priority IN ('baixa','normal','alta')),
    CONSTRAINT CK_SupportTicket_LastAuthor CHECK (LastAuthor IN ('jogador','equipe'))
);
CREATE INDEX IX_SupportTicket_Login ON dbo.SupportTicket (GameLogin, UpdatedUtc DESC);
CREATE INDEX IX_SupportTicket_Queue ON dbo.SupportTicket (Status, UpdatedUtc);

-- Mensagens do chamado. Internal = nota da equipe, nunca mostrada ao jogador.
CREATE TABLE dbo.SupportMessage (
    Id          bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SupportMessage PRIMARY KEY,
    TicketId    int            NOT NULL CONSTRAINT FK_SupportMessage_Ticket REFERENCES dbo.SupportTicket (Id),
    AuthorType  varchar(8)     NOT NULL,
    StaffId     int            NULL CONSTRAINT FK_SupportMessage_Staff REFERENCES dbo.StaffUser (Id),
    Body        nvarchar(4000) NOT NULL,
    Internal    bit            NOT NULL CONSTRAINT DF_SupportMessage_Internal DEFAULT (0),
    CreatedUtc  datetime2(0)   NOT NULL CONSTRAINT DF_SupportMessage_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_SupportMessage_Author CHECK (AuthorType IN ('jogador','equipe')),
    CONSTRAINT CK_SupportMessage_StaffInternal CHECK (Internal = 0 OR AuthorType = 'equipe')
);
CREATE INDEX IX_SupportMessage_Ticket ON dbo.SupportMessage (TicketId, Id);

-- Imagens anexadas. O arquivo fica no banco (até 3 MB cada), já sem metadados (EXIF/texto).
-- O Id é aleatório e só é servido a quem pode ver a mensagem.
CREATE TABLE dbo.SupportAttachment (
    Id           uniqueidentifier NOT NULL CONSTRAINT PK_SupportAttachment PRIMARY KEY NONCLUSTERED,
    TicketId     int            NOT NULL CONSTRAINT FK_SupportAttachment_Ticket REFERENCES dbo.SupportTicket (Id),
    MessageId    bigint         NOT NULL CONSTRAINT FK_SupportAttachment_Message REFERENCES dbo.SupportMessage (Id),
    FileName     nvarchar(100)  NOT NULL,
    ContentType  varchar(16)    NOT NULL,
    SizeBytes    int            NOT NULL,
    Data         varbinary(max) NOT NULL,
    CreatedUtc   datetime2(0)   NOT NULL CONSTRAINT DF_SupportAttachment_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_SupportAttachment_Type CHECK (ContentType IN ('image/png','image/jpeg','image/bmp')),
    CONSTRAINT CK_SupportAttachment_Size CHECK (SizeBytes > 0 AND SizeBytes <= 3145728)
);
CREATE CLUSTERED INDEX CX_SupportAttachment_Message ON dbo.SupportAttachment (MessageId);

-- Resultado de cada teste de porta feito pelo Worker. É a base do histórico de disponibilidade:
-- só existe a partir da implantação, e a página de status diz desde quando mede.
CREATE TABLE dbo.ServerProbe (
    Id          bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ServerProbe PRIMARY KEY,
    TargetKey   varchar(32)    NOT NULL,
    CheckedUtc  datetime2(0)   NOT NULL,
    Online      bit            NOT NULL,
    LatencyMs   int            NULL
);
CREATE INDEX IX_ServerProbe_Target ON dbo.ServerProbe (TargetKey, CheckedUtc) INCLUDE (Online);

-- Avisos e manutenções publicados pela equipe na página de status.
CREATE TABLE dbo.ServerNotice (
    Id          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ServerNotice PRIMARY KEY,
    Kind        varchar(16)    NOT NULL,
    Title       nvarchar(160)  NOT NULL,
    Body        nvarchar(2000) NOT NULL,
    StartsUtc   datetime2(0)   NOT NULL,
    EndsUtc     datetime2(0)   NULL,
    Status      varchar(16)    NOT NULL CONSTRAINT DF_ServerNotice_Status DEFAULT ('draft'),
    CreatedUtc  datetime2(0)   NOT NULL CONSTRAINT DF_ServerNotice_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedUtc  datetime2(0)   NOT NULL CONSTRAINT DF_ServerNotice_Updated DEFAULT (SYSUTCDATETIME()),
    CreatedBy   varchar(32)    NULL,
    UpdatedBy   varchar(32)    NULL,
    CONSTRAINT CK_ServerNotice_Kind CHECK (Kind IN ('aviso','manutencao')),
    CONSTRAINT CK_ServerNotice_Status CHECK (Status IN ('draft','published','cancelled')),
    CONSTRAINT CK_ServerNotice_Window CHECK (EndsUtc IS NULL OR EndsUtc > StartsUtc)
);
CREATE INDEX IX_ServerNotice_Window ON dbo.ServerNotice (Status, StartsUtc);
