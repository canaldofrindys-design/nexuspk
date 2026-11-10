-- Logins SQL do portal com o mínimo de permissão.
-- Rode no SQL Server da VPS como administrador (sqlcmd -S .\SQLEXPRESS -E -C -i permissoes-portal.sql).
-- Troque cada <SENHA-...> por uma senha forte gerada na própria VPS. Não envie essas senhas por chat
-- e guarde-as só nas variáveis de ambiente dos serviços (GameDatabase__ReadConnectionString etc.).
-- O script não altera nenhuma tabela do jogo: só cria logins, usuários e permissões.

USE master;
GO
IF SUSER_ID('portal_game_read') IS NULL  CREATE LOGIN portal_game_read  WITH PASSWORD = '<SENHA-LEITURA>',  CHECK_POLICY = ON;
IF SUSER_ID('portal_game_write') IS NULL CREATE LOGIN portal_game_write WITH PASSWORD = '<SENHA-ESCRITA>',  CHECK_POLICY = ON;
IF SUSER_ID('portal_app') IS NULL        CREATE LOGIN portal_app        WITH PASSWORD = '<SENHA-PORTAL>',   CHECK_POLICY = ON;
GO

-- 1) Leitura do jogo: só as tabelas que o portal usa até agora.
USE accountdb;
GO
IF USER_ID('portal_game_read') IS NULL  CREATE USER portal_game_read  FOR LOGIN portal_game_read;
IF USER_ID('portal_game_write') IS NULL CREATE USER portal_game_write FOR LOGIN portal_game_write;
GO
DECLARE @shards varchar(36) = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ', @i int = 1, @t sysname, @sql nvarchar(max);
WHILE @i <= 36
BEGIN
    SET @t = QUOTENAME(SUBSTRING(@shards, @i, 1) + 'GameUser');
    -- leitura: login no painel e verificação de login existente
    SET @sql = N'GRANT SELECT ON dbo.' + @t + N' TO portal_game_read;';
    EXEC (@sql);
    -- escrita: cadastro (INSERT) e troca de senha (UPDATE só da coluna Passwd)
    SET @sql = N'GRANT SELECT, INSERT ON dbo.' + @t + N' TO portal_game_write; GRANT UPDATE (Passwd) ON dbo.' + @t + N' TO portal_game_write;';
    EXEC (@sql);
    SET @i += 1;
END
GO

USE ServerDB;
GO
IF USER_ID('portal_game_read') IS NULL CREATE USER portal_game_read FOR LOGIN portal_game_read;
GRANT SELECT ON dbo.LevelTable TO portal_game_read;
GO

USE UserDB;
GO
IF USER_ID('portal_game_read') IS NULL CREATE USER portal_game_read FOR LOGIN portal_game_read;
GRANT SELECT ON dbo.GameMasters TO portal_game_read;
GO

-- 2) Banco do portal: leitura e escrita nas próprias tabelas, sem alterar estrutura.
--    As migrações rodam com um login administrativo só na hora da publicação.
USE PortalDB;
GO
IF USER_ID('portal_app') IS NULL CREATE USER portal_app FOR LOGIN portal_app;
ALTER ROLE db_datareader ADD MEMBER portal_app;
ALTER ROLE db_datawriter ADD MEMBER portal_app;
GO
