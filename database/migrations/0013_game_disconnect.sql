-- 0013 — "Desconectar personagem travado" no painel do jogador (PortalDB).
-- O banco do jogo não tem como derrubar uma conta: quem segura o personagem é a memória do servidor do jogo.
-- O portal grava aqui o pedido; o servidor do jogo lê, desconecta a conta e devolve a situação
-- (contrato completo em docs/DESCONECTAR.md). Nada no banco do jogo muda.

-- Status: 'pending' (portal gravou), 'done' (servidor desconectou), 'not-online' (servidor não achou a conta),
-- 'failed' (servidor achou e não conseguiu). Pedido 'pending' sem resposta em 2 minutos o portal mostra como expirado.
CREATE TABLE dbo.GameDisconnectRequest (
    Id            bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_GameDisconnectRequest PRIMARY KEY,
    GameLogin     varchar(32)    NOT NULL,
    RequestedUtc  datetime2(0)   NOT NULL CONSTRAINT DF_GameDisconnectRequest_Requested DEFAULT (SYSUTCDATETIME()),
    RequestedIp   varchar(45)    NULL,
    Status        varchar(16)    NOT NULL CONSTRAINT DF_GameDisconnectRequest_Status DEFAULT ('pending'),
    ProcessedUtc  datetime2(0)   NULL,
    ServerNote    nvarchar(200)  NULL,
    CONSTRAINT CK_GameDisconnectRequest_Status CHECK (Status IN ('pending','done','not-online','failed'))
);
-- Leitura do servidor do jogo: só os pendentes, em ordem. Sem índice filtrado, para não exigir opções SET da conexão do servidor.
CREATE INDEX IX_GameDisconnectRequest_Pending ON dbo.GameDisconnectRequest (Status, Id) INCLUDE (GameLogin, RequestedUtc);
CREATE INDEX IX_GameDisconnectRequest_Login ON dbo.GameDisconnectRequest (GameLogin, RequestedUtc DESC);

-- Sinal de vida de cada função que o servidor do jogo atende. O painel só liga o botão com LastSeenUtc recente.
CREATE TABLE dbo.GameServerRuntime (
    Feature          varchar(32)   NOT NULL CONSTRAINT PK_GameServerRuntime PRIMARY KEY,
    ProtocolVersion  int           NOT NULL CONSTRAINT DF_GameServerRuntime_Protocol DEFAULT (0),
    LastSeenUtc      datetime2(0)  NULL
);
INSERT INTO dbo.GameServerRuntime (Feature, ProtocolVersion, LastSeenUtc) VALUES ('disconnect', 0, NULL);
