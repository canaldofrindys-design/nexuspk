-- Login do PAINEL DA EQUIPE no banco do jogo (portal_game_admin): ler e gravar dados (db_datareader + db_datawriter)
-- nos bancos do jogo que existirem nesta lista. Não pode criar, apagar nem alterar tabelas.
-- Usado só pela API, nas telas do painel: contas, banimentos, coins, VIP, rankings e editores (lojas, drops, itens).
-- O jeito fácil é rodar deploy\Enable-GameAdmin.ps1, que gera a senha na VPS e grava a connection string.
-- À mão: troque <SENHA-ADMIN> por uma senha forte gerada na VPS e rode como administrador do SQL Server.
-- Rodar de novo é seguro (idempotente).

USE master;
GO
IF SUSER_ID('portal_game_admin') IS NULL CREATE LOGIN portal_game_admin WITH PASSWORD = '<SENHA-ADMIN>', CHECK_POLICY = ON;
GO

DECLARE @dbs TABLE (name sysname);
INSERT INTO @dbs (name) VALUES
    ('accountdb'), ('ServerDB'), ('UserDB'), ('ClanDB'), ('CoinShopDB'), ('TimeShopDB'),
    ('ItemDB'), ('ItemLogDB'), ('GameLogDB'), ('PristonDB'), ('EventDB'), ('QuestDB'), ('SodDb'),
    ('NexusPKAdmin'), ('NexusMarket'), ('BattleEvents'), ('LogadoPremiado');

DECLARE @db sysname, @sql nvarchar(max), @exec nvarchar(400);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
    SELECT d.name FROM sys.databases d JOIN @dbs x ON x.name = d.name WHERE d.state = 0;
OPEN c;
FETCH NEXT FROM c INTO @db;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'IF USER_ID(''portal_game_admin'') IS NULL CREATE USER portal_game_admin FOR LOGIN portal_game_admin;
EXEC sp_addrolemember ''db_datareader'', ''portal_game_admin'';
EXEC sp_addrolemember ''db_datawriter'', ''portal_game_admin'';';
    SET @exec = QUOTENAME(@db) + N'.sys.sp_executesql';
    EXEC @exec @sql;
    PRINT 'portal_game_admin liberado em ' + @db;
    FETCH NEXT FROM c INTO @db;
END
CLOSE c;
DEALLOCATE c;
GO
