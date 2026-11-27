-- Descobrir quais valores de ItemLogDB.dbo.IL.Flag o servidor grava num aging e num mix/craft.
-- Só LEITURA: não muda nada. Rode no SSMS, na VPS.
--
-- Como usar:
--   1) Rode a PARTE 1 e guarde o resultado (Flags que existem hoje).
--   2) No jogo, com um personagem de teste, faça UM aging e depois UM mix/craft. Anote a hora.
--   3) Troque 'NomeDoPersonagem' abaixo e rode a PARTE 2: as linhas novas mostram o Flag de cada ação.
--   4) Coloque os valores no C:\NexusPortal\config\portal.json, em "GameDatabase":
--        "ItemLogAgingFlags": [<flag do aging>], "ItemLogCraftFlags": [<flag do mix>]
--      e reinicie a API. Sem esses valores, as abas Aging e Craft de /historico ficam desligadas.
SET NOCOUNT ON;

PRINT '=== PARTE 1) Flags dos últimos 30 dias: quantas linhas e a última vez ===';
SELECT Flag, COUNT(*) AS Linhas, MAX(RegistDay) AS Ultima
FROM ItemLogDB.dbo.IL
WHERE RegistDay >= DATEADD(DAY, -30, GETDATE())
GROUP BY Flag ORDER BY Flag;

PRINT '=== PARTE 2) Últimas 30 linhas do personagem de teste (sem IP) ===';
DECLARE @personagem varchar(32) = 'NomeDoPersonagem';
SELECT TOP 30 RegistDay, Flag, CharName, TCharName, ItemCode, ItemCount
FROM ItemLogDB.dbo.IL
WHERE CharName = @personagem
ORDER BY RegistDay DESC;
