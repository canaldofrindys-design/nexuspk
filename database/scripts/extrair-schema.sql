/* =====================================================================
   Extração de SCHEMA (somente leitura) — Portal Priston Tale
   - Não lê dados de jogadores, não altera nada, não mostra senhas.
   - Rode no SSMS com um login que tenha acesso de leitura aos bancos.
   - Antes de rodar: menu Query > Results To > Results to File (Ctrl+Shift+F)
     e salve como schema.txt. Depois envie o arquivo.
   ===================================================================== */
SET NOCOUNT ON;

SELECT @@VERSION AS SqlServerVersion;

-- Bancos citados pelo site antigo. Os que não existirem são ignorados.
DECLARE @dbs TABLE (name sysname);
INSERT INTO @dbs VALUES
 ('AccountDB'),('ServerDB'),('UserDB'),('ClanDB'),('clanDB'),('CoinShopDB'),
 ('TimeShopDB'),('QuestDB'),('ItemLogDB'),('PristonDB'),('sodDB'),
 ('KLVN Studio'),('NexusPKAdmin'),('LogDB'),('GameLogDB');

SELECT d.name AS Banco, d.collation_name AS Collation, d.compatibility_level
FROM sys.databases d WHERE d.name IN (SELECT name FROM @dbs);

-- Lista de todos os bancos de usuário (para identificar algum que faltou)
SELECT name AS TodosOsBancos FROM sys.databases WHERE database_id > 4 ORDER BY name;

DECLARE @db sysname, @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
  SELECT d.name FROM sys.databases d WHERE d.name IN (SELECT name FROM @dbs) AND d.state = 0;
OPEN c; FETCH NEXT FROM c INTO @db;
WHILE @@FETCH_STATUS = 0
BEGIN
  SET @sql = N'USE ' + QUOTENAME(@db) + N';

  -- 1) Colunas (tabela por tabela)
  SELECT DB_NAME() AS Banco, s.name AS SchemaName, t.name AS Tabela, c.column_id AS Ordem,
         c.name AS Coluna, ty.name AS Tipo, c.max_length AS TamanhoBytes,
         c.precision, c.scale, c.is_nullable, c.is_identity,
         dc.definition AS ValorPadrao, c.collation_name
  FROM sys.tables t
  JOIN sys.schemas s ON s.schema_id = t.schema_id
  JOIN sys.columns c ON c.object_id = t.object_id
  JOIN sys.types ty ON ty.user_type_id = c.user_type_id
  LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id
  ORDER BY t.name, c.column_id;

  -- 2) Chaves primárias, únicas e índices
  SELECT DB_NAME() AS Banco, t.name AS Tabela, i.name AS Indice, i.type_desc,
         i.is_primary_key, i.is_unique,
         STUFF((SELECT '','' + col.name FROM sys.index_columns ic
                JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
                WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                ORDER BY ic.key_ordinal FOR XML PATH('''')),1,1,'''') AS Colunas
  FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
  WHERE i.type > 0 ORDER BY t.name, i.name;

  -- 3) Chaves estrangeiras
  SELECT DB_NAME() AS Banco, fk.name AS FK, tp.name AS Tabela, cp.name AS Coluna,
         tr.name AS TabelaReferenciada, cr.name AS ColunaReferenciada
  FROM sys.foreign_keys fk
  JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
  JOIN sys.tables tp ON tp.object_id = fkc.parent_object_id
  JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
  JOIN sys.tables tr ON tr.object_id = fkc.referenced_object_id
  JOIN sys.columns cr ON cr.object_id = fkc.referenced_object_id AND cr.column_id = fkc.referenced_column_id;

  -- 4) Quantidade aproximada de linhas por tabela (sem ler os dados)
  SELECT DB_NAME() AS Banco, t.name AS Tabela, SUM(p.rows) AS LinhasAprox
  FROM sys.tables t JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
  GROUP BY t.name ORDER BY t.name;

  -- 5) Triggers, procedures, views e functions (com o código)
  SELECT DB_NAME() AS Banco, o.type_desc AS Tipo, OBJECT_NAME(o.parent_object_id) AS TabelaDoTrigger,
         o.name AS Objeto, m.definition AS Codigo
  FROM sys.objects o JOIN sys.sql_modules m ON m.object_id = o.object_id
  WHERE o.is_ms_shipped = 0
  ORDER BY o.type_desc, o.name;';
  BEGIN TRY
    EXEC sys.sp_executesql @sql;
  END TRY
  BEGIN CATCH
    SELECT @db AS Banco, ERROR_MESSAGE() AS ErroDeAcesso;
  END CATCH;
  FETCH NEXT FROM c INTO @db;
END
CLOSE c; DEALLOCATE c;
