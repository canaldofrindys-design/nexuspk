-- Permissão para o SERVIDOR DO JOGO atender "Desconectar personagem travado" (docs/DESCONECTAR.md).
-- O servidor só lê os pedidos e grava a resposta; não cria, não apaga e não vê nenhuma outra tabela do PortalDB.
-- Troque <LOGIN-DO-SERVIDOR> pelo login SQL que o servidor do jogo já usa (o mesmo do distribuidor de donate).
-- Se o servidor conecta com 'sa' ou outro sysadmin, não precisa rodar nada: o acesso já existe.
-- Rodar de novo é seguro (idempotente).

USE PortalDB;
GO
IF DATABASE_PRINCIPAL_ID('game_server_disconnect') IS NULL CREATE ROLE game_server_disconnect;
GO
GRANT SELECT ON dbo.GameDisconnectRequest TO game_server_disconnect;
GRANT UPDATE (Status, ProcessedUtc, ServerNote) ON dbo.GameDisconnectRequest TO game_server_disconnect;
GRANT SELECT ON dbo.GameServerRuntime TO game_server_disconnect;
GRANT UPDATE (ProtocolVersion, LastSeenUtc) ON dbo.GameServerRuntime TO game_server_disconnect;
GO
IF USER_ID('<LOGIN-DO-SERVIDOR>') IS NULL CREATE USER [<LOGIN-DO-SERVIDOR>] FOR LOGIN [<LOGIN-DO-SERVIDOR>];
ALTER ROLE game_server_disconnect ADD MEMBER [<LOGIN-DO-SERVIDOR>];
GO
